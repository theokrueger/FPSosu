// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Rulesets.FPSosu.Projection
{
    /// <summary>
    /// Describes how the flat osu! playfield is embedded into the 3D world before being
    /// projected back onto the screen.
    /// </summary>
    public enum FPSosuProjectionMode
    {
        /// <summary>
        /// The playfield is wrapped onto the inside of a sphere centred on the camera.
        /// Playfield X maps to yaw and playfield Y maps to pitch, so a horizontal flick always
        /// costs the same angular distance regardless of where on the playfield it happens.
        /// </summary>
        [Description("Dome")]
        Dome,

        /// <summary>
        /// The playfield stays a flat rectangle floating in front of the camera.
        /// Objects near the edges require progressively larger angular movement to reach,
        /// which mirrors how a flat monitor target behaves in a traditional FPS aim trainer.
        /// </summary>
        [Description("Flat plane")]
        Plane,
    }
}
