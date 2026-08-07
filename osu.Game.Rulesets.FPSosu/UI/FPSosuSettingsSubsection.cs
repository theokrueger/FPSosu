// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
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
        private readonly Bindable<FPSosuSensitivityGame> sourceGame = new Bindable<FPSosuSensitivityGame>();
        private readonly Bindable<string> sourceSensitivity = new Bindable<string>("1");
        private readonly Bindable<int?> sourceDpi = new Bindable<int?>(800);

        private Bindable<float> sensitivity = null!;
        private OsuSpriteText conversionResult = null!;

        public FPSosuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = ((FPSosuRulesetConfigManager)Config).Fps;
            sensitivity = config.GetBindable<float>(FPSosuRulesetSetting.Sensitivity);

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
                new SettingsEnumDropdown<FPSosuSensitivityGame>
                {
                    LabelText = "Convert from",
                    TooltipText = "The game an entered sensitivity comes from. The converter matches its turn rate exactly.",
                    Current = sourceGame,
                },
                new SettingsTextBox
                {
                    LabelText = "Source sensitivity",
                    TooltipText = "Your sensitivity in the selected game, e.g. 1.0 for CS2 or 0.5 for Valorant.",
                    Current = sourceSensitivity,
                },
                new SettingsNumberBox
                {
                    LabelText = "Mouse DPI",
                    TooltipText = "Only used to report the equivalent cm/360; the converted sensitivity itself does not depend on DPI.",
                    Current = sourceDpi,
                },
                new SettingsButton
                {
                    Text = "Apply converted sensitivity",
                    Action = applyConvertedSensitivity,
                },
                conversionResult = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 14),
                    Colour = OsuColour.Gray(0.8f),
                },
                new SettingsSlider<float>
                {
                    LabelText = "Crosshair overshoot",
                    TooltipText = "How far past the edge of the playfield the crosshair can move, in degrees. Set to zero to keep it locked to the beatmap.",
                    Current = config.GetBindable<float>(FPSosuRulesetSetting.CrosshairOvershoot),
                    KeyboardStep = 5,
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
                new SettingsSlider<float>
                {
                    LabelText = "Crosshair size",
                    TooltipText = "How large the crosshair is drawn.",
                    Current = config.GetBindable<float>(FPSosuRulesetSetting.CrosshairSize),
                    KeyboardStep = 0.1f,
                },
                new SettingsCheckbox
                {
                    LabelText = "Crosshair outline",
                    TooltipText = "Draws a contrasting outline around the crosshair so it stays visible against bright objects.",
                    Current = config.GetBindable<bool>(FPSosuRulesetSetting.CrosshairOutline),
                },
                new SettingsEnumDropdown<FPSosuCrosshairColour>
                {
                    LabelText = "Crosshair colour",
                    Current = config.GetBindable<FPSosuCrosshairColour>(FPSosuRulesetSetting.CrosshairColour),
                },
            };
            sourceGame.BindValueChanged(_ => updateConversionPreview());
            sourceSensitivity.BindValueChanged(_ => updateConversionPreview());
            sourceDpi.BindValueChanged(_ => updateConversionPreview());
            updateConversionPreview();
        }

        private void applyConvertedSensitivity()
        {
            if (!tryReadConverterInputs(out var game, out float sourceSens, out _))
                return;

            sensitivity.Value = game.ToFPSosuSensitivity(sourceSens);
            updateConversionPreview();
        }

        private bool tryReadConverterInputs(out FPSosuSensitivityGame game, out float sourceSens, out float dpi)
        {
            game = sourceGame.Value;
            dpi = sourceDpi.Value is int d && d > 0 ? d : 800;

            return float.TryParse(sourceSensitivity.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out sourceSens) && sourceSens > 0;
        }

        private void updateConversionPreview()
        {
            if (conversionResult == null)
                return;

            if (!tryReadConverterInputs(out var game, out float sourceSens, out float dpi))
            {
                conversionResult.Text = "Enter a sensitivity above to preview the conversion.";
                return;
            }

            float converted = game.ToFPSosuSensitivity(sourceSens);

            conversionResult.Text = $"{game.GetCmPer360(sourceSens, dpi):0.#} cm/360°  ->  FPSosu sensitivity {converted:0.###}";
        }
    }
}
