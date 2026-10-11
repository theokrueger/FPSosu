// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Replays.Types;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Screens.Ranking.Statistics;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.FPSosu
{
    /// <summary>
    /// A ruleset which turns standard osu! into a 3D first-person aim trainer.
    /// </summary>
    /// <remarks>
    /// The flat osu! playfield is embedded into a 3D world and projected back onto the screen through a camera that
    /// the mouse rotates, while the crosshair stays fixed at the centre of the screen. Everything else, including
    /// hit objects, scoring, mods and difficulty calculation, is inherited from osu!.
    ///
    /// <para>
    /// This type is what the game registers, and it exists only because of how <see cref="RealmRulesetStore"/>
    /// registers rulesets. Anything implementing <see cref="ILegacyRuleset"/> is registered before everything else,
    /// and its database row is looked up by <see cref="RulesetInfo.OnlineID"/> rather than by its instantiation info -
    /// a row is only written when no row with that online ID exists yet. Every ruleset which is not itself a legacy
    /// ruleset reports an online ID of <c>-1</c> (see the fallback in <see cref="Ruleset.RulesetInfo"/>), and so does
    /// a derived ruleset which overrides <see cref="ILegacyRuleset.LegacyID"/> to keep away from osu!'s ID of 0.
    /// A derived ruleset therefore loses its row to whichever other custom ruleset was registered first, and then
    /// silently disappears from the game with nothing written to the log.
    /// </para>
    ///
    /// <para>
    /// So this type deliberately does <b>not</b> derive from <see cref="Osu.OsuRuleset"/>: not being an
    /// <see cref="ILegacyRuleset"/> is what makes the store key its row on this type instead, which works alongside
    /// any number of other custom rulesets. All of osu!'s behaviour lives in <see cref="FPSosuOsuRuleset"/>, an
    /// internal ruleset which inherits <see cref="Osu.OsuRuleset"/> in full and is never registered itself, and the
    /// members below simply forward to it. <see cref="DrawableFPSosuRuleset"/> still extends osu!'s drawable ruleset,
    /// which is what the inherited osu! mods (Classic, Relax and Autopilot among them) require.
    /// </para>
    ///
    /// <para>
    /// When osu! is updated, check that this type forwards any newly added override of
    /// <see cref="Osu.OsuRuleset"/> - <c>TestFPSosuRulesetSurface</c> fails if one is missed.
    /// </para>
    /// </remarks>
    public partial class FPSosuRuleset : Ruleset
    {
        public const string SHORT_NAME = "fpsosuruleset";

        public const string DESCRIPTION = "FPSosu";

        public const string PLAYING_VERB = "Shooting targets";

        /// <summary>
        /// The osu! ruleset all gameplay behaviour is inherited from.
        /// </summary>
        private readonly FPSosuOsuRuleset osuEngine = new FPSosuOsuRuleset();

        public override string ShortName => SHORT_NAME;

        public override string Description => DESCRIPTION;

        public override string PlayingVerb => PLAYING_VERB;

        /// <summary>
        /// Reports the osu! API version this build was compiled against, which is what inheriting
        /// <see cref="Osu.OsuRuleset"/> reported before. osu! refuses to load rulesets built against an older API.
        /// </summary>
        public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableFPSosuRuleset(osuEngine, beatmap, mods);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => osuEngine.CreateConfig(settings);

        public override RulesetSettingsSubsection CreateSettings() => new FPSosuSettingsSubsection(this);

        #region Forwarded to osu!

        public override ScoreProcessor CreateScoreProcessor() => osuEngine.CreateScoreProcessor();

        public override HealthProcessor CreateHealthProcessor(double drainStartTime) => osuEngine.CreateHealthProcessor(drainStartTime);

        public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => osuEngine.CreateBeatmapConverter(beatmap);

        public override IBeatmapProcessor CreateBeatmapProcessor(IBeatmap beatmap) => osuEngine.CreateBeatmapProcessor(beatmap);

        public override IEnumerable<KeyBinding> GetDefaultKeyBindings(int variant = 0) => osuEngine.GetDefaultKeyBindings(variant);

        public override IEnumerable<Mod> ConvertFromLegacyMods(LegacyMods mods) => osuEngine.ConvertFromLegacyMods(mods);

        public override LegacyMods ConvertToLegacyMods(Mod[] mods) => osuEngine.ConvertToLegacyMods(mods);

        public override IEnumerable<Mod> GetModsFor(ModType type) => osuEngine.GetModsFor(type);

        public override ScoreMultiplierCalculator CreateScoreMultiplierCalculator(ScoreMultiplierContext context) => osuEngine.CreateScoreMultiplierCalculator(context);

        public override Drawable CreateIcon() => osuEngine.CreateIcon();

        public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => osuEngine.CreateDifficultyCalculator(beatmap);

        public override PerformanceCalculator CreatePerformanceCalculator() => osuEngine.CreatePerformanceCalculator();

        public override HitObjectComposer CreateHitObjectComposer() => osuEngine.CreateHitObjectComposer();

        public override IBeatmapVerifier CreateBeatmapVerifier() => osuEngine.CreateBeatmapVerifier();

        public override ISkin? CreateSkinTransformer(ISkin skin, IBeatmap beatmap) => osuEngine.CreateSkinTransformer(skin, beatmap);

        public override IConvertibleReplayFrame CreateConvertibleReplayFrame() => osuEngine.CreateConvertibleReplayFrame();

        public override IEnumerable<HitResult> GetValidHitResults() => osuEngine.GetValidHitResults();

        public override LocalisableString GetDisplayNameForHitResult(HitResult result) => osuEngine.GetDisplayNameForHitResult(result);

        public override StatisticItem[] CreateStatisticsForScore(ScoreInfo score, IBeatmap playableBeatmap) => osuEngine.CreateStatisticsForScore(score, playableBeatmap);

        public override IEnumerable<Drawable> CreateEditorSetupSections() => osuEngine.CreateEditorSetupSections();

        public override BeatmapDifficulty GetAdjustedDisplayDifficulty(IBeatmapInfo difficulty, IReadOnlyCollection<Mod> mods) => osuEngine.GetAdjustedDisplayDifficulty(difficulty, mods);

        public override IEnumerable<RulesetBeatmapAttribute> GetBeatmapAttributesForDisplay(IBeatmapInfo beatmapInfo, IReadOnlyCollection<Mod> mods) => osuEngine.GetBeatmapAttributesForDisplay(beatmapInfo, mods);

        public override bool EditorShowScrollSpeed => osuEngine.EditorShowScrollSpeed;

        #endregion
    }
}
