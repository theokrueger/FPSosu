// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Game.Beatmaps;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.FPSosu.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// The FPSosu drawable ruleset.
    /// </summary>
    /// <remarks>
    /// This extends <see cref="DrawableOsuRuleset"/> rather than <see cref="DrawableRuleset{T}"/> directly, because
    /// several inherited osu! mods cast to that exact type (Classic, Relax and Autopilot among them). Extending it
    /// keeps the entire standard mod set usable here.
    /// </remarks>
    public partial class DrawableFPSosuRuleset : DrawableOsuRuleset
    {
        /// <summary>
        /// The camera driven by mouse movement, and read by the playfield when projecting hit objects.
        /// </summary>
        [Cached]
        public FPSosuCamera Camera { get; } = new FPSosuCamera();

        public new FPSosuPlayfield Playfield => (FPSosuPlayfield)base.Playfield;

        public new FPSosuInputManager KeyBindingInputManager => (FPSosuInputManager)base.KeyBindingInputManager;

        public DrawableFPSosuRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            // The FPS settings live alongside the inherited osu! ones. Cache them so the playfield, input manager
            // and crosshair can all resolve the same instance.
            if (Config is FPSosuRulesetConfigManager fpsConfig)
                dependencies.Cache(fpsConfig.Fps);

            return dependencies;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The playfield exists by now, so the input manager can be told where to pin the cursor.
            KeyBindingInputManager.LockPosition = crosshairScreenSpacePosition;
        }

        protected override Playfield CreatePlayfield() => new FPSosuPlayfield();

        protected override PassThroughInputManager CreateInputManager() => new FPSosuInputManager(Ruleset.RulesetInfo);

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay)
        {
            // Camera control belongs to the replay while one is loaded.
            KeyBindingInputManager.AllowCameraControl = false;

            return new FPSosuFramedReplayInputHandler(replay, Camera, () => Playfield.Projector, crosshairScreenSpacePosition);
        }

        /// <summary>
        /// The standard osu! playfield is nudged downwards to line up with historical storyboards. Here the centre of
        /// the playfield is where the crosshair lives, so it must sit at the true centre of the view instead.
        /// </summary>
        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new OsuPlayfieldAdjustmentContainer { AlignWithStoryboard = false };

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new FPSosuReplayRecorder(score, Camera, () => Playfield.Projector);

        /// <summary>
        /// The screen-space position the cursor is held at, which is the centre of the playfield.
        /// </summary>
        private Vector2 crosshairScreenSpacePosition() => Playfield.ToScreenSpace(FPSosuProjector.PLAYFIELD_CENTRE);

        /// <summary>
        /// The standard osu! resume overlay asks the player to click a specific position to resume, which cannot be
        /// done when the cursor is locked to the centre of the screen. A simple timed countdown is used instead.
        /// </summary>
        protected override ResumeOverlay CreateResumeOverlay() => new DelayedResumeOverlay { Scale = new Vector2(0.65f) };
    }
}
