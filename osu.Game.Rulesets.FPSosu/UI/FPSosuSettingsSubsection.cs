// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
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
        private readonly Bindable<FPSosuSensitivityGame> sourceGame = new();
        private readonly Bindable<string> sourceSensitivity = new("1");
        private readonly Bindable<int?> sourceDpi = new(800);

        private FPSosuConfigManager fpsConfig = null!;
        private Bindable<float> sensitivity = null!;
        private OsuSpriteText conversionResult = null!;

        public FPSosuSettingsSubsection(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            fpsConfig = ((FPSosuRulesetConfigManager)Config).Fps;
            sensitivity = fpsConfig.GetBindable<float>(FPSosuRulesetSetting.Sensitivity);

            Children =
            [
                enumDropdown<FPSosuProjectionMode>(FPSosuRulesetSetting.ProjectionMode, "Projection",
                    "How the beatmap is placed in the 3D world. A dome keeps every flick the same angular distance; a flat plane makes the edges of the beatmap harder to reach."),
                floatSlider(FPSosuRulesetSetting.FieldOfView, "Field of view",
                    "Zoom. Lower values magnify the beatmap; higher values fit more of the world on screen."),
                floatSlider(FPSosuRulesetSetting.PlayfieldSpan, "Playfield span",
                    "How wide the beatmap is in the world, in degrees. Larger values require turning further to cross the beatmap.", 5),
                enumDropdown<FPSosuInputSource>(FPSosuRulesetSetting.InputSource, "Input source",
                    "Which device turns the camera. Mouse reads relative movement. Tablet (mouse sensitivity) points the camera from the pen's absolute position, turning as far per unit of movement as the mouse. Standard tablet style makes the pen point at a beatmap position like a tablet does in standard osu!, so its range follows the playfield span and Sensitivity does not apply. Both tablet modes require osu!'s tablet input to be enabled."),
                floatSlider(FPSosuRulesetSetting.Sensitivity, "Sensitivity",
                    "Multiplier applied to mouse movement when turning the camera.", 0.05f),
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
                floatSlider(FPSosuRulesetSetting.CrosshairOvershoot, "Crosshair overshoot",
                    "How far past the edge of the playfield the crosshair can move, in degrees. Set to zero to keep it locked to the beatmap.", 5),
                checkbox(FPSosuRulesetSetting.InvertPitch, "Invert vertical look"),
                checkbox(FPSosuRulesetSetting.ShowCrosshair, "Show crosshair"),
                floatSlider(FPSosuRulesetSetting.CrosshairGap, "Crosshair gap",
                    "The empty space between the centre and the crosshair lines.", 0.1f),
                floatSlider(FPSosuRulesetSetting.CrosshairLineLength, "Crosshair line length",
                    "How long each crosshair line is.", 0.1f),
                floatSlider(FPSosuRulesetSetting.CrosshairThickness, "Crosshair line thickness",
                    "How thick the crosshair lines and centre dot are.", 0.1f),
                floatSlider(FPSosuRulesetSetting.CrosshairOpacity, "Crosshair opacity",
                    "How transparent the crosshair is. Lower values let the playfield show through.", 0.01f),
                checkbox(FPSosuRulesetSetting.CrosshairCenterDot, "Crosshair centre dot",
                    "Draws a dot at the exact point hits are registered."),
                checkbox(FPSosuRulesetSetting.CrosshairOutline, "Crosshair outline",
                    "Draws a contrasting outline around the crosshair so it stays visible against bright objects."),
                enumDropdown<FPSosuCrosshairColour>(FPSosuRulesetSetting.CrosshairColour, "Crosshair colour"),
            ];

            sourceGame.BindValueChanged(_ => updateConversionPreview());
            sourceSensitivity.BindValueChanged(_ => updateConversionPreview());
            sourceDpi.BindValueChanged(_ => updateConversionPreview());
            updateConversionPreview();
        }

        private SettingsSlider<float> floatSlider(FPSosuRulesetSetting setting, string label, string tooltip = "", float step = 1)
            => new SettingsSlider<float>
            {
                LabelText = label,
                TooltipText = tooltip,
                Current = fpsConfig.GetBindable<float>(setting),
                KeyboardStep = step,
            };

        private SettingsCheckbox checkbox(FPSosuRulesetSetting setting, string label, string tooltip = "")
            => new SettingsCheckbox
            {
                LabelText = label,
                TooltipText = tooltip,
                Current = fpsConfig.GetBindable<bool>(setting),
            };

        private SettingsEnumDropdown<T> enumDropdown<T>(FPSosuRulesetSetting setting, string label, string tooltip = "")
            where T : struct, Enum
            => new SettingsEnumDropdown<T>
            {
                LabelText = label,
                TooltipText = tooltip,
                Current = fpsConfig.GetBindable<T>(setting),
            };

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
