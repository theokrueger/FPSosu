// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Utils;
using osu.Game.Extensions;
using osu.Game.Rulesets.FPSosu.Configuration;
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
        /// Gameplay inherits osu! through a separate ruleset instance, so the identity that instance reports must be
        /// FPSosu's. Otherwise scores, beatmaps and cached star ratings would be filed under standard osu!.
        /// </summary>
        [Test]
        public void TestGameplayKeepsTheRegisteredRulesetIdentity()
        {
            var registered = new FPSosuRuleset().RulesetInfo;

            AddAssert("picks up the registered ruleset", () => Player.DrawableRuleset != null && registered.ShortName == FPSosuRuleset.SHORT_NAME);
            AddAssert("same short name", () => drawableRuleset.Ruleset.RulesetInfo.ShortName == registered.ShortName);
            AddAssert("same name", () => drawableRuleset.Ruleset.RulesetInfo.Name == registered.Name);
            AddAssert("same online id", () => drawableRuleset.Ruleset.RulesetInfo.OnlineID == registered.OnlineID);
            AddAssert("not a legacy ruleset", () => !drawableRuleset.Ruleset.RulesetInfo.IsLegacyRuleset());
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

        /// <summary>
        /// A graphics tablet reports absolute positions, so tablet input must point the camera at the pen rather than
        /// accumulate movement. The cursor must also be left where the pen is: overriding it is what breaks a tablet,
        /// because the next absolute report undoes the override.
        /// </summary>
        [Test]
        public void TestTabletAbsolutePositionDrivesCamera()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            Vector2 penPosition = default;

            AddStep("select tablet input", () => setInputSource(FPSosuInputSource.Tablet));

            AddStep("place pen right of the crosshair", () => InputManager.MoveMouseTo(penPosition = crosshairScreenPosition + new Vector2(240, 0)));
            AddUntilStep("camera turned right", () => camera.Yaw > 0.01f);

            AddStep("place pen left of the crosshair", () => InputManager.MoveMouseTo(penPosition = crosshairScreenPosition - new Vector2(240, 0)));
            AddUntilStep("camera turned left", () => camera.Yaw < -0.01f);

            AddStep("place pen above the crosshair", () => InputManager.MoveMouseTo(penPosition = crosshairScreenPosition - new Vector2(0, 120)));
            AddUntilStep("camera pitched up", () => camera.Pitch > 0.01f);
            AddWaitStep("let it settle", 5);

            // If the cursor were being pulled back, the camera would be recomputed from the crosshair each frame and
            // would have collapsed back to the centre by now.
            AddAssert("camera held at the pen", () => camera.Pitch > 0.01f);
            AddAssert("pen position not overridden", () => Vector2.Distance(InputManager.CurrentState.Mouse.Position, penPosition) < 1f);

            AddStep("restore mouse input", () => setInputSource(FPSosuInputSource.Mouse));
        }

        /// <summary>
        /// Changing the source must not change what the Sensitivity slider means: the same movement has to turn the
        /// camera the same distance whether it came from the mouse or from the pen's absolute position.
        /// </summary>
        [Test]
        public void TestTabletSensitivityMatchesMouse()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            var offset = new Vector2(240, 120);
            float mouseYaw = 0;
            float mousePitch = 0;

            AddStep("move the mouse", () => moveMouseBy(offset));
            AddUntilStep("camera turned", () => camera.Yaw != 0);
            AddStep("record mouse angles", () =>
            {
                mouseYaw = camera.Yaw;
                mousePitch = camera.Pitch;
            });

            AddStep("select tablet input and place the pen", () =>
            {
                setInputSource(FPSosuInputSource.Tablet);
                InputManager.MoveMouseTo(crosshairScreenPosition + offset);
            });

            AddUntilStep("pen turns the camera exactly as far", () =>
                Precision.AlmostEquals(camera.Yaw, mouseYaw, 1e-3f) && Precision.AlmostEquals(camera.Pitch, mousePitch, 1e-3f));

            AddStep("restore mouse input", () => setInputSource(FPSosuInputSource.Mouse));
        }

        /// <summary>
        /// Standard tablet style is geometric: the pen points at a position on the beatmap and the camera turns so
        /// that position is under the crosshair, exactly as a tablet behaves in standard osu!.
        /// </summary>
        [Test]
        public void TestStandardTabletPointsAtTheBeatmap()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            var beatmapPoint = FPSosuProjector.PLAYFIELD_CENTRE + new Vector2(120, -80);
            Vector2 expected = default;
            Vector2 penPosition = default;

            AddStep("select standard tablet input", () => setInputSource(FPSosuInputSource.TabletStandard));
            AddStep("point the pen at a beatmap position", () =>
            {
                expected = playfield.Projector.PlayfieldToCameraAngles(beatmapPoint);
                InputManager.MoveMouseTo(penPosition = playfield.ToScreenSpace(beatmapPoint));
            });

            AddUntilStep("camera aimed at the pen's beatmap point", () =>
                Precision.AlmostEquals(camera.Yaw, expected.X, 1e-3f) && Precision.AlmostEquals(camera.Pitch, expected.Y, 1e-3f));

            AddAssert("pen position not overridden", () => Vector2.Distance(InputManager.CurrentState.Mouse.Position, penPosition) < 1f);

            AddStep("restore mouse input", () => setInputSource(FPSosuInputSource.Mouse));
        }

        /// <summary>
        /// Standard tablet style is defined by the playfield rather than by a movement scale, so the Sensitivity
        /// slider must not change where the pen points.
        /// </summary>
        [Test]
        public void TestStandardTabletIgnoresSensitivity()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            float yawBefore = 0;
            float pitchBefore = 0;

            AddStep("select standard tablet input", () => setInputSource(FPSosuInputSource.TabletStandard));
            AddStep("point the pen at a beatmap position", () => InputManager.MoveMouseTo(playfield.ToScreenSpace(FPSosuProjector.PLAYFIELD_CENTRE + new Vector2(120, -80))));
            AddUntilStep("camera aimed", () => camera.Yaw != 0);
            AddStep("record angles", () =>
            {
                yawBefore = camera.Yaw;
                pitchBefore = camera.Pitch;
            });

            AddStep("raise sensitivity", () => setSensitivity(5));
            AddWaitStep("let it settle", 5);
            AddAssert("mapping unchanged", () => Precision.AlmostEquals(camera.Yaw, yawBefore, 1e-4f) && Precision.AlmostEquals(camera.Pitch, pitchBefore, 1e-4f));

            AddStep("restore sensitivity and mouse input", () =>
            {
                setSensitivity(1);
                setInputSource(FPSosuInputSource.Mouse);
            });
        }

        /// <summary>
        /// Switching sources must not turn the camera by the distance between where the old and new devices happen
        /// to be. The accumulated mouse position is forgotten instead, so the new source starts from where it is.
        /// </summary>
        [Test]
        public void TestSwitchingInputSourceDoesNotJump()
        {
            AddUntilStep("wait for objects", () => aliveObjects().Any());

            AddStep("select tablet input", () => setInputSource(FPSosuInputSource.Tablet));
            AddStep("place pen far from the crosshair", () => InputManager.MoveMouseTo(crosshairScreenPosition + new Vector2(600, 0)));
            AddUntilStep("camera turned right", () => camera.Yaw > 0.01f);

            float yawBefore = 0;

            AddStep("record angles", () => yawBefore = camera.Yaw);
            AddStep("restore mouse input", () => setInputSource(FPSosuInputSource.Mouse));
            AddWaitStep("let updates run", 5);
            AddAssert("camera did not jump", () => Precision.AlmostEquals(camera.Yaw, yawBefore, 1e-4f));
        }

        /// <summary>
        /// The screen-space position of the point the crosshair marks, which is also where the input manager pins
        /// its cursor.
        /// </summary>
        private Vector2 crosshairScreenPosition => playfield.ToScreenSpace(FPSosuProjector.PLAYFIELD_CENTRE);

        private FPSosuConfigManager fpsConfig => drawableRuleset.Dependencies.Get<FPSosuConfigManager>();

        private void setInputSource(FPSosuInputSource source)
            => fpsConfig.GetBindable<FPSosuInputSource>(FPSosuRulesetSetting.InputSource).Value = source;

        private void setSensitivity(float value)
            => fpsConfig.GetBindable<float>(FPSosuRulesetSetting.Sensitivity).Value = value;

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
