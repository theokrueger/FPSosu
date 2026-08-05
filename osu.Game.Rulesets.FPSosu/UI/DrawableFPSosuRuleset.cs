// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.FPSosu.Objects;
using osu.Game.Rulesets.FPSosu.Objects.Drawables;
using osu.Game.Rulesets.FPSosu.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.FPSosu.UI
{
    [Cached]
    public partial class DrawableFPSosuRuleset : DrawableRuleset<FPSosuHitObject>
    {
        public DrawableFPSosuRuleset(FPSosuRuleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override Playfield CreatePlayfield() => new FPSosuPlayfield();

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new FPSosuFramedReplayInputHandler(replay);

        public override DrawableHitObject<FPSosuHitObject> CreateDrawableRepresentation(FPSosuHitObject h) => new DrawableFPSosuHitObject(h);

        protected override PassThroughInputManager CreateInputManager() => new FPSosuInputManager(Ruleset?.RulesetInfo);
    }
}
