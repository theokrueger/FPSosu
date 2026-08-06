// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Projection
{
    /// <summary>
    /// Holds the first-person camera orientation shared between the input manager (which writes it)
    /// and the playfield (which reads it to project hit objects).
    /// </summary>
    public class FPSosuCamera
    {
        /// <summary>
        /// Camera yaw in radians. Positive turns right.
        /// </summary>
        public float Yaw { get; private set; }

        /// <summary>
        /// Camera pitch in radians. Positive looks up.
        /// </summary>
        public float Pitch { get; private set; }

        /// <summary>
        /// The maximum rotation away from centre the camera may reach, as (yaw, pitch) in radians.
        /// </summary>
        /// <remarks>
        /// Clamping to the angular extent of the playfield guarantees every hit object stays reachable:
        /// the player can never spin around and lose the beatmap behind them.
        /// </remarks>
        public Vector2 AngularLimit { get; set; } = new Vector2(MathF.PI / 2);

        /// <summary>
        /// Rotates the camera by an angular delta, in radians, clamping to <see cref="AngularLimit"/>.
        /// </summary>
        public void Rotate(float yawDelta, float pitchDelta) => LookAt(Yaw + yawDelta, Pitch + pitchDelta);

        /// <summary>
        /// Points the camera at an absolute orientation, in radians, clamping to <see cref="AngularLimit"/>.
        /// </summary>
        public void LookAt(float yaw, float pitch)
        {
            Yaw = Math.Clamp(yaw, -AngularLimit.X, AngularLimit.X);
            Pitch = Math.Clamp(pitch, -AngularLimit.Y, AngularLimit.Y);
        }

        /// <summary>
        /// Points the camera at an absolute orientation given as a (yaw, pitch) vector in radians.
        /// </summary>
        public void LookAt(Vector2 angles) => LookAt(angles.X, angles.Y);

        /// <summary>
        /// Returns the camera to its resting orientation, looking at the centre of the playfield.
        /// </summary>
        public void Reset() => LookAt(0, 0);
    }
}
