// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Drives real gameplay through the FPSosu ruleset, exercising the 3D transformation end to end.
    /// </summary>
    [TestFixture]
    public partial class TestSceneFPSosuPlayer : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FPSosuRuleset();

        private DrawableFPSosuRuleset drawableRuleset => (DrawableFPSosuRuleset)Player.DrawableRuleset;

        private FPSosuPlayfield playfield => drawableRuleset.Playfield;

        private FPSosuCamera camera => drawableRuleset.Camera;

        /// <summary>
        /// The ruleset must actually run using the FPS components, rather than silently falling back to
        /// standard osu! (which is what happened before the ruleset was wired up).
        /// </summary>
        [Test]
        public void TestGameplayUsesFpsComponents()
        {
            AddAssert("fps drawable ruleset in use", () => Player.DrawableRuleset, Is.TypeOf<DrawableFPSosuRuleset>);
            AddAssert("crosshair cursor in use", () => playfield.Cursor, Is.TypeOf<FPSosuCrosshairContainer>);
            AddAssert("fps input manager in use", () => drawableRuleset.KeyBindingInputManager, Is.TypeOf<FPSosuInputManager>);
        }

        /// <summary>
        /// The core interaction: moving the mouse must rotate the camera.
        /// </summary>
        [Test]
        public void TestMouseMovementRotatesCamera()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            AddStep("move mouse right", () => moveMouseBy(new Vector2(80, 0)));
            AddUntilStep("camera turned right", () => camera.Yaw > 0.01f);

            AddStep("move mouse left past centre", () => moveMouseBy(new Vector2(-200, 0)));
            AddUntilStep("camera turned left", () => camera.Yaw < -0.01f);

            AddStep("move mouse up", () => moveMouseBy(new Vector2(0, -80)));
            AddUntilStep("camera pitched up", () => camera.Pitch > 0.01f);

            AddStep("move mouse down past centre", () => moveMouseBy(new Vector2(0, 200)));
            AddUntilStep("camera pitched down", () => camera.Pitch < -0.01f);
        }

        /// <summary>
        /// However far the mouse travels, the cursor must end up back at the centre of the playfield, because that
        /// is where the crosshair is drawn and where hits are registered.
        /// </summary>
        [Test]
        public void TestCursorStaysCentred()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            for (int i = 0; i < 3; i++)
            {
                int index = i;

                AddStep($"move mouse ({index})", () => moveMouseBy(new Vector2(index % 2 == 0 ? 150 : -150, 90)));
                AddUntilStep("cursor recentred", () => cursorOffsetFromCrosshair() < 1f);
            }
        }

        /// <summary>
        /// Rotating the camera must move hit objects across the screen, which is what makes the beatmap behave like
        /// a 3D scene rather than a static 2D image.
        /// </summary>
        [Test]
        public void TestCameraRotationMovesHitObjects()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            AddAssert("turning moves objects on screen", () =>
            {
                var projector = playfield.Projector;
                var world = projector.PlayfieldToWorld(aliveObjects().First().HitObject.StackedPosition);

                Assert.That(projector.WorldToPlayfield(world, 0, 0, out var atRest, out _), Is.True);
                Assert.That(projector.WorldToPlayfield(world, 0.3f, 0, out var turnedRight, out _), Is.True);

                // Turning right sweeps the scene to the left.
                return turnedRight.X < atRest.X - 1f;
            });
        }

        /// <summary>
        /// Aiming at an object must place it under the crosshair. This is the invariant that makes objects hittable
        /// at all, since the cursor never leaves the centre of the screen.
        /// </summary>
        [Test]
        public void TestAimedObjectLandsUnderCrosshair()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            AddAssert("aiming centres object", () =>
            {
                var projector = playfield.Projector;

                foreach (var drawable in aliveObjects())
                {
                    var target = drawable.HitObject.StackedPosition;
                    var angles = projector.PlayfieldToCameraAngles(target);

                    Assert.That(projector.WorldToPlayfield(projector.PlayfieldToWorld(target), angles.X, angles.Y, out var projected, out _), Is.True);
                    Assert.That(Vector2.Distance(projected, FPSosuProjector.PLAYFIELD_CENTRE), Is.LessThan(0.5f));
                }

                return true;
            });
        }

        /// <summary>
        /// The camera is clamped so the player cannot spin forever, but the clamp reaches past the edge of the
        /// playfield by the configured overshoot, so the crosshair can move outside the beatmap while the whole
        /// beatmap stays reachable.
        /// </summary>
        [Test]
        public void TestCameraCanOvershootPlayfield()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            AddStep("slam mouse far right and down", () => moveMouseBy(new Vector2(20000, 20000)));

            AddUntilStep("camera turned past the playfield extent", () =>
            {
                var extent = playfield.Projector.AngularExtent;
                return camera.Yaw > extent.X + 1e-3f;
            });

            AddAssert("camera clamped to its angular limit", () =>
                camera.Yaw <= camera.AngularLimit.X + 1e-4f && camera.Pitch >= -camera.AngularLimit.Y - 1e-4f);

            AddAssert("angular limit still covers the beatmap", () =>
            {
                var extent = playfield.Projector.AngularExtent;
                return camera.AngularLimit.X >= extent.X - 1e-4f && camera.AngularLimit.Y >= extent.Y - 1e-4f;
            });
        }

        /// <summary>
        /// When the ruleset is not taking input, as when paused, moving the mouse must not rotate the camera or drag
        /// the cursor back to the crosshair. That is what leaves the cursor free to navigate the pause menu.
        /// </summary>
        [Test]
        public void TestMouseIgnoredWhenPassThroughDisabled()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            float yawBefore = 0;
            float pitchBefore = 0;

            AddStep("disable pass-through as pause does", () =>
            {
                yawBefore = camera.Yaw;
                pitchBefore = camera.Pitch;
                drawableRuleset.KeyBindingInputManager.UseParentInput = false;
            });

            AddStep("move the mouse", () => moveMouseBy(new Vector2(800, 800)));
            AddWaitStep("let updates run", 10);
            AddAssert("camera did not rotate", () => camera.Yaw == yawBefore && camera.Pitch == pitchBefore);

            AddStep("restore pass-through", () => drawableRuleset.KeyBindingInputManager.UseParentInput = true);
            AddWaitStep("let it resync", 5);
        }

        private IEnumerable<DrawableOsuHitObject> aliveObjects()
            => playfield.HitObjectContainer.AliveObjects.OfType<DrawableOsuHitObject>();

        /// <summary>
        /// How far the gameplay cursor sits from the crosshair, in playfield units.
        /// </summary>
        /// <remarks>
        /// This reads the ruleset's own input manager rather than the test scene's, because that is the cursor
        /// gameplay actually uses for hit detection, and the one the ruleset pins to the crosshair.
        /// </remarks>
        private float cursorOffsetFromCrosshair()
            => Vector2.Distance(
                playfield.ToLocalSpace(drawableRuleset.KeyBindingInputManager.CurrentState.Mouse.Position),
                FPSosuProjector.PLAYFIELD_CENTRE);

        private void moveMouseBy(Vector2 delta)
            => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + delta);
    }
}
