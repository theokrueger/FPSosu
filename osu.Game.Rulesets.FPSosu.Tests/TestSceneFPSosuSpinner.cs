// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Verifies that spinners are spun by rotating the camera, since the cursor is pinned to the crosshair and the
    /// standard "move the cursor in circles" action is not possible.
    /// </summary>
    [TestFixture]
    public partial class TestSceneFPSosuSpinner : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FPSosuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            HitObjects = new List<HitObject>
            {
                new Spinner
                {
                    StartTime = 500,
                    Duration = 10000,
                    Position = OsuPlayfield.BASE_SIZE / 2,
                },
            },
        };

        private DrawableFPSosuRuleset drawableRuleset => (DrawableFPSosuRuleset)Player.DrawableRuleset;

        private FPSosuPlayfield playfield => drawableRuleset.Playfield;

        /// <summary>
        /// Rotating the camera during a spinner must accumulate rotation on the spinner, so it can be completed.
        /// </summary>
        [Test]
        public void TestSpinnerSpunByCamera()
        {
            DrawableSpinner? spinner = null;
            int direction = 1;

            AddUntilStep("wait for spinner", () => (spinner = findSpinner()) != null);
            AddUntilStep("wait for spinnable", () => spinner?.RotationTracker.IsSpinnableTime == true);

            AddRepeatStep("sweep camera back and forth", () =>
            {
                direction = -direction;
                moveMouseBy(new Vector2(300 * direction, 0));
            }, 30);

            AddUntilStep("spinner accumulated rotation", () => (spinner?.Result?.TotalRotation ?? 0) > 0);
        }

        /// <summary>
        /// The spinner sits at the centre of the playfield, so it must move with the world as the camera turns
        /// rather than staying pinned to the screen like a flat overlay.
        /// </summary>
        [Test]
        public void TestSpinnerMovesWithPlayfield()
        {
            DrawableSpinner? spinner = null;

            AddUntilStep("wait for spinner", () => (spinner = findSpinner()) != null);
            AddUntilStep("wait for spinnable", () => spinner?.RotationTracker.IsSpinnableTime == true);

            AddAssert("spinner sits at the projected playfield centre", () =>
                Precision.AlmostEquals(spinner!.Position, projectedPositionOf(spinner.HitObject.StackedPosition), 1f));

            AddStep("turn camera", () => moveMouseBy(new Vector2(300, 0)));
            AddWaitStep("let it settle", 5);

            AddAssert("spinner moved with the world", () =>
                Precision.AlmostEquals(spinner!.Position, projectedPositionOf(spinner.HitObject.StackedPosition), 1f));
            AddAssert("spinner left the centre of the view", () => Vector2.Distance(spinner!.Position, FPSosuProjector.PLAYFIELD_CENTRE) > 1f);
        }

        /// <summary>
        /// The spinner is a large object, so it must be sized from the playfield (field of view and span) rather than
        /// keeping its natural playfield-filling size. Its size must also not change as the camera turns it away from
        /// the centre, otherwise the same rotation reads as faster off to the side.
        /// </summary>
        [Test]
        public void TestSpinnerScalesWithProjection()
        {
            DrawableSpinner? spinner = null;

            AddUntilStep("wait for spinner", () => (spinner = findSpinner()) != null);
            AddUntilStep("wait for spinnable", () => spinner?.RotationTracker.IsSpinnableTime == true);

            AddAssert("spinner sized from the playfield", () =>
                Precision.AlmostEquals(spinner!.Scale.X, playfield.Projector.CentreScale, 1e-3f));

            AddStep("turn the camera", () => moveMouseBy(new Vector2(300, 0)));
            AddWaitStep("let it settle", 5);
            AddAssert("size unchanged by turning", () =>
                Precision.AlmostEquals(spinner!.Scale.X, playfield.Projector.CentreScale, 1e-3f));

            AddStep("shrink the playfield span", () => setPlayfieldSpan(30));
            AddWaitStep("let the projection rebuild", 5);

            AddAssert("spinner still sized from the playfield", () =>
                Precision.AlmostEquals(spinner!.Scale.X, playfield.Projector.CentreScale, 1e-3f));
            AddAssert("spinner shrank with the span", () => spinner!.Scale.X < 0.5f);

            AddStep("restore the playfield span", () => setPlayfieldSpan(FPSosuProjector.DEFAULT_PLAYFIELD_SPAN));
        }

        /// <summary>
        /// Turning the camera to spin a spinner pushes it away from the centre of the view, and turning far enough
        /// puts it behind the camera. It must stay drawn and on the playfield throughout, because a spinner that
        /// vanishes mid-spin cannot be completed.
        /// </summary>
        [Test]
        public void TestSpinnerStaysVisibleWhileSpinning()
        {
            DrawableSpinner? spinner = null;
            int direction = 1;

            AddUntilStep("wait for spinner", () => (spinner = findSpinner()) != null);
            AddUntilStep("wait for spinnable", () => spinner?.RotationTracker.IsSpinnableTime == true);

            AddRepeatStep("sweep the camera to its limits", () =>
            {
                direction = -direction;
                moveMouseBy(new Vector2(4000 * direction, 0));
            }, 6);

            AddAssert("spinner not culled", () => spinner!.Alpha > 0.99f && spinner.AlwaysPresent);
            AddAssert("spinner held on the playfield", () =>
                spinner!.Position.X >= 0 && spinner.Position.X <= OsuPlayfield.BASE_SIZE.X
                && spinner.Position.Y >= 0 && spinner.Position.Y <= OsuPlayfield.BASE_SIZE.Y);
            AddAssert("spinner still accumulated rotation", () => spinner!.Result.TotalRotation > 360);
        }

        private FPSosuCamera camera => drawableRuleset.Camera;

        private Vector2 projectedPositionOf(Vector2 playfieldPosition)
        {
            Assert.That(playfield.Projector.WorldToPlayfield(playfield.Projector.PlayfieldToWorld(playfieldPosition), camera.Yaw, camera.Pitch, out var position, out _), Is.True);
            return position;
        }

        private FPSosuConfigManager fpsConfig => drawableRuleset.Dependencies.Get<FPSosuConfigManager>();

        private void setPlayfieldSpan(float value)
            => fpsConfig.GetBindable<float>(FPSosuRulesetSetting.PlayfieldSpan).Value = value;

        private DrawableSpinner? findSpinner()
        {
            foreach (var entry in playfield.HitObjectContainer.AliveEntries)
            {
                if (entry.Value is DrawableSpinner spinner)
                    return spinner;
            }

            return null;
        }

        private void moveMouseBy(Vector2 delta)
            => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + delta);
    }
}
