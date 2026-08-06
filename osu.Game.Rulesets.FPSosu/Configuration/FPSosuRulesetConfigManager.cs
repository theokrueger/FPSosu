// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Osu.Configuration;

namespace osu.Game.Rulesets.FPSosu.Configuration
{
    /// <summary>
    /// The configuration returned by <see cref="FPSosuRuleset.CreateConfig"/>.
    /// </summary>
    /// <remarks>
    /// This derives from <see cref="OsuRulesetConfigManager"/> for two reasons: inherited osu! components resolve
    /// that exact type from dependencies (the cursor trail, hit animations, the replay analysis overlay), and
    /// <see cref="Osu.UI.DrawableOsuRuleset"/> casts its config to it. Deriving keeps every standard osu! setting
    /// working under this ruleset's name.
    ///
    /// A <see cref="RulesetConfigManager{TLookup}"/> can only bind a single settings enum, so the FPS-specific settings
    /// live in a nested <see cref="FPSosuConfigManager"/>. Both are owned by this instance, which the game caches
    /// once per ruleset, so the settings UI and active gameplay share the same bindables and update live.
    /// </remarks>
    public class FPSosuRulesetConfigManager : OsuRulesetConfigManager
    {
        /// <summary>
        /// The FPS-specific settings controlling projection and camera behaviour.
        /// </summary>
        public FPSosuConfigManager Fps { get; }

        public FPSosuRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
            Fps = new FPSosuConfigManager(settings, ruleset, variant);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (isDisposing)
                Fps.Dispose();
        }
    }
}
