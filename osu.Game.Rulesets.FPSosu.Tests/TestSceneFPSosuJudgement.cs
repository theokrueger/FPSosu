// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Verifies that hit judgement popups stay pinned to where their note was on the board, moving with the world as
    /// the camera turns, instead of remaining fixed on the screen.
    /// </summary>
    /// <remarks>
    /// The single centre circle is left to miss on purpose: the miss popup is a judgement like any other, and a
    /// manual (non-replay) player is used so the mouse keeps rotating the camera afterwards.
    /// </remarks>
    [TestFixture]
    public partial class TestSceneFPSosuJudgement : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FPSosuRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            HitObjects = new List<HitObject>
            {
                new HitCircle
                {
                    StartTime = 500,
                    Position = OsuPlayfield.BASE_SIZE / 2,
                },
            },
        };

        private FPSosuPlayfield playfield => (FPSosuPlayfield)Player.DrawableRuleset.Playfield;

        [Test]
        public void TestJudgementMovesWithCamera()
        {
            DrawableOsuJudgement? judgement = null;
            Vector2 positionBefore = default;

            AddUntilStep("wait for judgement", () => (judgement = findJudgement()) != null);
            AddStep("record position", () => positionBefore = judgement!.Position);

            AddStep("turn camera", () => InputManager.MoveMouseTo(InputManager.CurrentState.Mouse.Position + new Vector2(300, 0)));

            AddUntilStep("judgement moved with the world", () => !Precision.AlmostEquals(judgement!.Position, positionBefore, 0.1f));
        }

        private DrawableOsuJudgement? findJudgement()
            => playfield.JudgementLayer?.Children.FirstOrDefault(j => j.IsPresent);
    }
}
