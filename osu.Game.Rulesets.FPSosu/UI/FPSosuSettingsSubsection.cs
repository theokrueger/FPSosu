// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// Exposes the projection and camera settings in the game's settings overlay.
    /// </summary>
    public partial class FPSosuSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "FPSosu";

        public FPSosuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = ((FPSosuRulesetConfigManager)Config).Fps;

            Children = new Drawable[]
            {
                new SettingsEnumDropdown<FPSosuProjectionMode>
                {
                    LabelText = "Projection",
                    TooltipText = "How the beatmap is placed in the 3D world. A dome keeps every flick the same angular distance; a flat plane makes the edges of the beatmap harder to reach.",
                    Current = config.GetBindable<FPSosuProjectionMode>(FPSosuRulesetSetting.ProjectionMode),
                },
                new SettingsSlider<float>
                {
                    LabelText = "Field of view",
                    TooltipText = "Zoom. Lower values magnify the beatmap; higher values fit more of the world on screen.",
                    Current = config.GetBindable<float>(FPSosuRulesetSetting.FieldOfView),
                    KeyboardStep = 1,
                },
                new SettingsSlider<float>
                {
                    LabelText = "Playfield span",
                    TooltipText = "How wide the beatmap is in the world, in degrees. Larger values require turning further to cross the beatmap.",
                    Current = config.GetBindable<float>(FPSosuRulesetSetting.PlayfieldSpan),
                    KeyboardStep = 5,
                },
                new SettingsSlider<float>
                {
                    LabelText = "Sensitivity",
                    TooltipText = "Multiplier applied to mouse movement when turning the camera.",
                    Current = config.GetBindable<float>(FPSosuRulesetSetting.Sensitivity),
                    KeyboardStep = 0.05f,
                },
                new SettingsCheckbox
                {
                    LabelText = "Invert vertical look",
                    Current = config.GetBindable<bool>(FPSosuRulesetSetting.InvertPitch),
                },
                new SettingsCheckbox
                {
                    LabelText = "Show crosshair",
                    Current = config.GetBindable<bool>(FPSosuRulesetSetting.ShowCrosshair),
                },
            };
        }
    }
}
