// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
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
        /// While a spinner is active it stays centred in the view, because it is spun in place rather than aimed at.
        /// </summary>
        [Test]
        public void TestSpinnerStaysCentred()
        {
            DrawableSpinner? spinner = null;

            AddUntilStep("wait for spinner", () => (spinner = findSpinner()) != null);
            AddUntilStep("wait for spinnable", () => spinner?.RotationTracker.IsSpinnableTime == true);

            AddStep("turn camera", () => moveMouseBy(new Vector2(300, 0)));
            AddWaitStep("let it settle", 5);

            AddAssert("spinner held at playfield centre", () => Precision.AlmostEquals(spinner?.Position ?? Vector2.Zero, FPSosuProjector.PLAYFIELD_CENTRE, 1f));
        }

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
