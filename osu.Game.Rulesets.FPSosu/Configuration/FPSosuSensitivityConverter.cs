// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Rulesets.FPSosu.Configuration
{
    /// <summary>
    /// Converts mouse sensitivities from other FPS titles into the equivalent FPSosu sensitivity.
    /// </summary>
    /// <remarks>
    /// Every game in <see cref="FPSosuSensitivityGame"/> defines its turn rate as a fixed yaw - the degrees of camera
    /// rotation per mouse count at a sensitivity of 1. FPSosu does the same: one mouse count moves one screen pixel
    /// (osu!'s own cursor sensitivity is normalised out by the input manager), so <see cref="RADIANS_PER_PIXEL"/> is
    /// FPSosu's yaw.
    /// Matching two sensitivities then only needs the ratio of the yaws; the mouse DPI cancels because the same mouse
    /// is used on both sides. DPI is only needed to report the physical cm/360° a matched sensitivity corresponds to.
    /// </remarks>
    public static class FPSosuSensitivityConverter
    {
        /// <summary>
        /// Radians of camera rotation per pixel of mouse movement at a sensitivity of 1. This also defines FPSosu's
        /// yaw, so changing it changes what every converted sensitivity means - keep it stable.
        /// </summary>
        public const float RADIANS_PER_PIXEL = 0.0005f;

        /// <summary>
        /// FPSosu's yaw: degrees of camera rotation per mouse count at a sensitivity of 1.
        /// </summary>
        public static float FpsosuYawDegrees => RADIANS_PER_PIXEL * 180 / MathF.PI;

        /// <summary>
        /// The radians of camera rotation per pixel of mouse movement at a sensitivity of 1, after normalising out
        /// osu!'s cursor sensitivity.
        /// </summary>
        /// <remarks>
        /// With high-precision (relative) mouse mode enabled the framework pre-multiplies raw mouse counts by osu!'s
        /// cursor sensitivity before they reach the ruleset, so the factor is divided back out to keep the turn rate
        /// constant per count. Without it the OS pointer scaling is in effect and cannot be normalised away.
        /// </remarks>
        public static float EffectiveRadiansPerPixel(double cursorSensitivity, bool highPrecisionMouse)
            => highPrecisionMouse && cursorSensitivity > 0 ? RADIANS_PER_PIXEL / (float)cursorSensitivity : RADIANS_PER_PIXEL;

        /// <summary>
        /// The yaw of the given game, in degrees of camera rotation per mouse count at a sensitivity of 1.
        /// </summary>
        public static float GetYawDegrees(this FPSosuSensitivityGame game) => game switch
        {
            FPSosuSensitivityGame.CounterStrike2 => 0.022f,
            FPSosuSensitivityGame.Valorant => 0.07f,
            FPSosuSensitivityGame.RainbowSixSiege => 0.00572958f,
            FPSosuSensitivityGame.ApexLegends => 0.022f,
            FPSosuSensitivityGame.Overwatch2 => 0.0066f,
            FPSosuSensitivityGame.CallOfDuty => 0.0066f,
            _ => throw new ArgumentOutOfRangeException(nameof(game), game, null),
        };

        /// <summary>
        /// The FPSosu sensitivity which turns the camera at the same rate as <paramref name="sourceSensitivity"/>
        /// in <paramref name="game"/>.
        /// </summary>
        public static float ToFPSosuSensitivity(this FPSosuSensitivityGame game, float sourceSensitivity)
            => sourceSensitivity * game.GetYawDegrees() / FpsosuYawDegrees;

        /// <summary>
        /// The physical distance in centimetres the mouse must travel for a full 360° turn at the given sensitivity
        /// and DPI.
        /// </summary>
        public static float GetCmPer360(this FPSosuSensitivityGame game, float sensitivity, float dpi)
            => 914.4f / (dpi * sensitivity * game.GetYawDegrees());

        /// <summary>
        /// The physical distance in centimetres the mouse must travel for a full 360° turn in FPSosu at the given
        /// sensitivity and DPI.
        /// </summary>
        public static float GetFPSosuCmPer360(float sensitivity, float dpi)
            => 914.4f / (dpi * sensitivity * FpsosuYawDegrees);
    }

    /// <summary>
    /// The FPS titles the sensitivity converter can convert from.
    /// </summary>
    public enum FPSosuSensitivityGame
    {
        [Description("Counter-Strike 2 / CS:GO")]
        CounterStrike2,

        [Description("Valorant")]
        Valorant,

        [Description("Rainbow Six Siege")]
        RainbowSixSiege,

        [Description("Apex Legends")]
        ApexLegends,

        [Description("Overwatch 2")]
        Overwatch2,

        [Description("Call of Duty (MW / Warzone)")]
        CallOfDuty,
    }
}
