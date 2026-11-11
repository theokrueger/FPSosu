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
    /// The flat osu! playfield is embedded into a 3D world and projected back through a camera the mouse rotates,
    /// while the crosshair stays fixed at the centre of the screen. Hit objects, scoring, mods and difficulty are
    /// inherited from osu!.
    ///
    /// <para>
    /// This type is what the game registers, and it deliberately does <b>not</b> derive from
    /// <see cref="Osu.OsuRuleset"/>. <see cref="RealmRulesetStore"/> registers <see cref="ILegacyRuleset"/>s first and
    /// keys their rows on <see cref="RulesetInfo.OnlineID"/>; every non-legacy ruleset reports <c>-1</c>, so a
    /// derived legacy ruleset loses its row to whichever custom ruleset registered first and then silently vanishes.
    /// Not being legacy makes the store key the row on this type instead.
    /// </para>
    ///
    /// <para>
    /// All osu! behaviour lives in <see cref="FPSosuOsuRuleset"/>, an internal ruleset which is never registered, and
    /// the members below forward to it. <see cref="DrawableFPSosuRuleset"/> still extends osu!'s drawable ruleset,
    /// which the inherited osu! mods require. When osu! is updated, <c>TestFPSosuRulesetSurface</c> fails if a new
    /// <see cref="Osu.OsuRuleset"/> override is not forwarded here.
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
        private readonly FPSosuOsuRuleset osuEngine = new();

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
