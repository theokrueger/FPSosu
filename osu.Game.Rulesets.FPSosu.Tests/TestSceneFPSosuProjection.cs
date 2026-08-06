// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.FPSosu.Projection;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    [TestFixture]
    public class TestSceneFPSosuProjection
    {
        private const float tolerance = 0.05f;

        private static readonly FPSosuProjectionMode[] modes = { FPSosuProjectionMode.Dome, FPSosuProjectionMode.Plane };

        private static readonly Vector2[] sample_positions =
        {
            FPSosuProjector.PLAYFIELD_CENTRE,
            Vector2.Zero,
            new Vector2(512, 0),
            new Vector2(0, 384),
            new Vector2(512, 384),
            new Vector2(100, 300),
            new Vector2(383, 47),
        };

        /// <summary>
        /// With the camera at rest the centre of the playfield must project to the crosshair at natural size,
        /// so a centred beatmap reads exactly like standard osu!.
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestCentreIsUnchangedAtRest(FPSosuProjectionMode mode)
        {
            var projector = new FPSosuProjector(mode, 90, 100);

            Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(FPSosuProjector.PLAYFIELD_CENTRE), 0, 0, out var position, out float scale), Is.True);

            Assert.That(position.X, Is.EqualTo(FPSosuProjector.PLAYFIELD_CENTRE.X).Within(tolerance));
            Assert.That(position.Y, Is.EqualTo(FPSosuProjector.PLAYFIELD_CENTRE.Y).Within(tolerance));
            Assert.That(scale, Is.EqualTo(1).Within(0.001f));
        }

        /// <summary>
        /// Aiming the camera at an object must bring that object under the crosshair. This is the invariant the
        /// whole ruleset rests on: it is what makes an object hittable, and what lets autoplay and replays aim.
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestAimingCentresTarget(FPSosuProjectionMode mode)
        {
            var projector = new FPSosuProjector(mode, 90, 100);

            foreach (var target in sample_positions)
            {
                var angles = projector.PlayfieldToCameraAngles(target);

                Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(target), angles.X, angles.Y, out var position, out _), Is.True,
                    $"{target} should be visible when aimed at in {mode} mode");

                Assert.That(position.X, Is.EqualTo(FPSosuProjector.PLAYFIELD_CENTRE.X).Within(tolerance), $"yaw for {target} in {mode} mode");
                Assert.That(position.Y, Is.EqualTo(FPSosuProjector.PLAYFIELD_CENTRE.Y).Within(tolerance), $"pitch for {target} in {mode} mode");
            }
        }

        /// <summary>
        /// Recording a replay depends on recovering the aimed playfield position from camera angles alone,
        /// because the raw cursor is pinned to the centre of the screen and carries no aiming information.
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestCameraAnglesRoundTrip(FPSosuProjectionMode mode)
        {
            foreach (float span in new[] { 40f, 100f, 160f })
            {
                var projector = new FPSosuProjector(mode, 90, span);

                foreach (var target in sample_positions)
                {
                    var angles = projector.PlayfieldToCameraAngles(target);
                    var recovered = projector.CameraAnglesToPlayfield(angles.X, angles.Y);

                    Assert.That(recovered.X, Is.EqualTo(target.X).Within(tolerance), $"x for {target}, span {span}, {mode} mode");
                    Assert.That(recovered.Y, Is.EqualTo(target.Y).Within(tolerance), $"y for {target}, span {span}, {mode} mode");
                }
            }
        }

        /// <summary>
        /// The playfield span is the aim-training workload: it must be exactly the angle between the left and right
        /// edges of the beatmap, and must not be affected by the field of view (which is only zoom).
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestSpanControlsTurnAngleIndependentlyOfFieldOfView(FPSosuProjectionMode mode)
        {
            foreach (float span in new[] { 40f, 100f, 160f })
            {
                foreach (float fov in new[] { 60f, 90f, 120f })
                {
                    var projector = new FPSosuProjector(mode, fov, span);

                    float left = projector.PlayfieldToCameraAngles(new Vector2(0, FPSosuProjector.PLAYFIELD_CENTRE.Y)).X;
                    float right = projector.PlayfieldToCameraAngles(new Vector2(512, FPSosuProjector.PLAYFIELD_CENTRE.Y)).X;

                    Assert.That(float.RadiansToDegrees(right - left), Is.EqualTo(span).Within(0.01f), $"span {span} at fov {fov} in {mode} mode");
                }
            }
        }

        /// <summary>
        /// A lower field of view means more zoom, so an off-centre object must appear further from the crosshair.
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestLowerFieldOfViewMagnifies(FPSosuProjectionMode mode)
        {
            var target = new Vector2(512, FPSosuProjector.PLAYFIELD_CENTRE.Y);

            float offsetAt60 = offsetFromCentre(new FPSosuProjector(mode, 60, 100), target);
            float offsetAt120 = offsetFromCentre(new FPSosuProjector(mode, 120, 100), target);

            Assert.That(offsetAt60, Is.GreaterThan(offsetAt120));
        }

        private static float offsetFromCentre(FPSosuProjector projector, Vector2 target)
        {
            Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(target), 0, 0, out var position, out _), Is.True);
            return Math.Abs(position.X - FPSosuProjector.PLAYFIELD_CENTRE.X);
        }

        /// <summary>
        /// The camera must not be able to rotate past the beatmap, otherwise objects could end up behind the player
        /// and become impossible to hit.
        /// </summary>
        [TestCaseSource(nameof(modes))]
        public void TestAngularExtentCoversWholePlayfield(FPSosuProjectionMode mode)
        {
            var projector = new FPSosuProjector(mode, 90, 100);
            var extent = projector.AngularExtent;

            foreach (var target in sample_positions)
            {
                var angles = projector.PlayfieldToCameraAngles(target);

                Assert.That(Math.Abs(angles.X), Is.LessThanOrEqualTo(extent.X + 1e-4f), $"yaw for {target} in {mode} mode");
                Assert.That(Math.Abs(angles.Y), Is.LessThanOrEqualTo(extent.Y + 1e-4f), $"pitch for {target} in {mode} mode");
            }
        }

        /// <summary>
        /// Turning away from an object must push it towards the edge of the screen, and eventually behind the camera.
        /// This is what makes the scene feel like a 3D world rather than a panning 2D image.
        /// </summary>
        [Test]
        public void TestTurningAwayMovesObjectOffScreenThenBehind()
        {
            var projector = new FPSosuProjector(FPSosuProjectionMode.Dome, 90, 100);
            var world = projector.PlayfieldToWorld(FPSosuProjector.PLAYFIELD_CENTRE);

            Assert.That(projector.WorldToPlayfield(world, 0, 0, out var atRest, out _), Is.True);
            Assert.That(projector.WorldToPlayfield(world, 0.5f, 0, out var turned, out _), Is.True);

            // Turning right moves a centred object to the left of the screen.
            Assert.That(turned.X, Is.LessThan(atRest.X));

            // Turning a full quarter turn away puts it out of view entirely.
            Assert.That(projector.WorldToPlayfield(world, MathF.PI / 2, 0, out _, out _), Is.False);
        }

        /// <summary>
        /// Objects further from the camera must appear smaller. On the dome every object is equidistant, so
        /// perspective scaling is driven purely by how far off-axis the object is.
        /// </summary>
        [Test]
        public void TestOffAxisObjectsScaleUpOnDome()
        {
            var projector = new FPSosuProjector(FPSosuProjectionMode.Dome, 90, 100);

            Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(FPSosuProjector.PLAYFIELD_CENTRE), 0, 0, out _, out float centreScale), Is.True);
            Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(new Vector2(512, 384)), 0, 0, out _, out float cornerScale), Is.True);

            // The dome curves away from the projection plane, so its corners sit closer to the plane and are magnified.
            Assert.That(cornerScale, Is.GreaterThan(centreScale));
        }

        /// <summary>
        /// A flat plane keeps every object at the same depth along the view axis, so scale must stay uniform.
        /// </summary>
        [Test]
        public void TestPlaneKeepsUniformScaleAtRest()
        {
            var projector = new FPSosuProjector(FPSosuProjectionMode.Plane, 90, 100);

            foreach (var target in sample_positions)
            {
                Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(target), 0, 0, out _, out float scale), Is.True);
                Assert.That(scale, Is.EqualTo(1).Within(0.001f), $"scale for {target}");
            }
        }

        /// <summary>
        /// On a flat plane, setting the span equal to the field of view means the beatmap exactly fills the view,
        /// which must reproduce the standard osu! layout pixel for pixel while the camera is at rest.
        /// </summary>
        [Test]
        public void TestPlaneReproducesPlayfieldWhenSpanMatchesFieldOfView()
        {
            var projector = new FPSosuProjector(FPSosuProjectionMode.Plane, 90, 90);

            foreach (var target in sample_positions)
            {
                Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(target), 0, 0, out var position, out _), Is.True);

                Assert.That(position.X, Is.EqualTo(target.X).Within(tolerance), $"x for {target}");
                Assert.That(position.Y, Is.EqualTo(target.Y).Within(tolerance), $"y for {target}");
            }
        }

        /// <summary>
        /// A span wider than the field of view spreads the beatmap beyond the edges of the view, which is what
        /// forces the player to turn to reach objects instead of seeing everything at once.
        /// </summary>
        [Test]
        public void TestWiderSpanPushesObjectsOutsideTheView()
        {
            var projector = new FPSosuProjector(FPSosuProjectionMode.Plane, 90, 140);
            var edge = new Vector2(512, FPSosuProjector.PLAYFIELD_CENTRE.Y);

            Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(edge), 0, 0, out var position, out _), Is.True);

            // Beyond the right edge of the 512-wide view.
            Assert.That(position.X, Is.GreaterThan(512));
        }
    }
}
