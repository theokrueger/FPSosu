// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;

namespace osu.Game.Rulesets.FPSosu.Configuration
{
    /// <summary>
    /// Persists the FPS-specific settings which control how the 2D playfield is projected into the 3D world
    /// and how mouse movement drives the camera.
    /// </summary>
    public class FPSosuConfigManager : RulesetConfigManager<FPSosuRulesetSetting>
    {
        public FPSosuConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(FPSosuRulesetSetting.ProjectionMode, FPSosuProjectionMode.Dome);
            SetDefault(FPSosuRulesetSetting.FieldOfView, 90f, 30f, 150f, 1f);
            SetDefault(FPSosuRulesetSetting.PlayfieldSpan, 100f, 20f, 170f, 5f);
            SetDefault(FPSosuRulesetSetting.Sensitivity, 1f, 0.1f, 5f, 0.05f);
            SetDefault(FPSosuRulesetSetting.CrosshairOvershoot, 45f, 0f, 120f, 5f);
            SetDefault(FPSosuRulesetSetting.InvertPitch, false);
            SetDefault(FPSosuRulesetSetting.ShowCrosshair, true);
            SetDefault(FPSosuRulesetSetting.CrosshairSize, 1f, 0.5f, 3f, 0.1f);
            SetDefault(FPSosuRulesetSetting.CrosshairOutline, true);
            SetDefault(FPSosuRulesetSetting.CrosshairColour, FPSosuCrosshairColour.White);
        }
    }

    public enum FPSosuRulesetSetting
    {
        /// <summary>
        /// How the flat playfield is embedded into the 3D world.
        /// </summary>
        ProjectionMode,

        /// <summary>
        /// Horizontal field of view, in degrees. Acts as zoom.
        /// </summary>
        FieldOfView,

        /// <summary>
        /// The angle in degrees subtended by the full width of the playfield, controlling how far the player
        /// must turn to cross the beatmap.
        /// </summary>
        PlayfieldSpan,

        /// <summary>
        /// Multiplier applied to mouse movement when rotating the camera.
        /// </summary>
        Sensitivity,

        /// <summary>
        /// Whether moving the mouse down should look up, as in inverted flight controls.
        /// </summary>
        InvertPitch,

        /// <summary>
        /// How far in degrees the crosshair may move past the edge of the playfield, letting the player look
        /// outside the beatmap. Zero keeps the crosshair locked to the playfield.
        /// </summary>
        CrosshairOvershoot,

        /// <summary>
        /// Whether the centred crosshair is drawn.
        /// </summary>
        ShowCrosshair,

        /// <summary>
        /// Scale multiplier for the crosshair.
        /// </summary>
        CrosshairSize,

        /// <summary>
        /// Whether a contrasting outline is drawn around the crosshair for visibility.
        /// </summary>
        CrosshairOutline,

        /// <summary>
        /// The colour of the crosshair.
        /// </summary>
        CrosshairColour,
    }

    /// <summary>
    /// The preset colours the crosshair can be drawn in.
    /// </summary>
    public enum FPSosuCrosshairColour
    {
        White,
        Red,
        Green,
        Blue,
    }
}
