// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Projection
{
    /// <summary>
    /// Converts between the flat osu! playfield and the 3D world seen through a first-person camera.
    /// </summary>
    /// <remarks>
    /// This type is deliberately free of framework and drawable dependencies so the projection can be reasoned
    /// about, and tested, in isolation. All angles are in radians unless stated otherwise.
    ///
    /// The world uses the camera's resting basis: +X right, +Y up, +Z forward. Output is in osu! playfield
    /// coordinates (the same 512x384 space hit objects live in), so a projected position can be assigned directly
    /// to a drawable inside the playfield.
    ///
    /// Two settings shape the projection, and they are deliberately independent:
    /// <list type="bullet">
    /// <item><description>
    /// Field of view acts as zoom. It sets the focal length, so it controls how large everything appears
    /// without changing where objects sit in the world.
    /// </description></item>
    /// <item><description>
    /// Playfield span is the angular width of the beatmap in the world. It controls how far the player must
    /// physically turn to get from one side of the beatmap to the other, which is the aim-training workload.
    /// </description></item>
    /// </list>
    /// </remarks>
    public readonly struct FPSosuProjector
    {
        /// <summary>
        /// The centre of the osu! playfield. The camera looks at this point when it has no rotation.
        /// </summary>
        public static readonly Vector2 PLAYFIELD_CENTRE = OsuPlayfield.BASE_SIZE / 2;

        /// <summary>
        /// Radius of the dome, in playfield units.
        /// </summary>
        /// <remarks>
        /// For <see cref="FPSosuProjectionMode.Dome"/> every object is equidistant from the camera, so this value
        /// cancels out of both the projected position and the projected scale. It only needs to be large enough to
        /// stay clear of the near plane.
        /// </remarks>
        private const float dome_radius = 512f;

        /// <summary>
        /// The focal length at the default field of view of 90 degrees. As tan(45) is 1 this equals the playfield
        /// half-width. Object scale is normalised against it so that moving away from the default field of view
        /// magnifies or shrinks objects, while the default view keeps them at their natural osu! size.
        /// </summary>
        private static readonly float default_focal_length = PLAYFIELD_CENTRE.X;

        /// <summary>
        /// Points closer to the camera than this along the view axis are treated as not visible.
        /// A small positive epsilon keeps the perspective divide well conditioned.
        /// </summary>
        private const float near_plane = 0.5f;

        private readonly FPSosuProjectionMode mode;

        /// <summary>
        /// Playfield units from the camera to the projection plane. Derived from the field of view.
        /// </summary>
        private readonly float focalLength;

        /// <summary>
        /// Half the angular width of the playfield, in radians.
        /// </summary>
        private readonly float halfSpan;

        /// <summary>
        /// For <see cref="FPSosuProjectionMode.Dome"/>, radians of arc per playfield unit.
        /// For <see cref="FPSosuProjectionMode.Plane"/>, this is unused.
        /// </summary>
        private readonly float radiansPerUnit;

        /// <summary>
        /// Distance from the camera to the world geometry along the view axis, in playfield units.
        /// </summary>
        private readonly float distance;

        /// <summary>
        /// Creates a projector.
        /// </summary>
        /// <param name="mode">How the playfield is embedded into the world.</param>
        /// <param name="fieldOfView">Horizontal field of view in degrees, acting as zoom. Clamped to a usable range.</param>
        /// <param name="playfieldSpan">
        /// The angle in degrees subtended by the full width of the playfield, controlling how far the player must
        /// turn to cross the beatmap. Clamped to stay below a half turn.
        /// </param>
        public FPSosuProjector(FPSosuProjectionMode mode, float fieldOfView, float playfieldSpan)
        {
            this.mode = mode;

            float clampedFov = Math.Clamp(fieldOfView, 10f, 170f);
            float clampedSpan = Math.Clamp(playfieldSpan, 10f, 170f);

            focalLength = PLAYFIELD_CENTRE.X / MathF.Tan(float.DegreesToRadians(clampedFov) / 2);
            halfSpan = float.DegreesToRadians(clampedSpan) / 2;

            if (mode == FPSosuProjectionMode.Dome)
            {
                radiansPerUnit = halfSpan / PLAYFIELD_CENTRE.X;
                distance = dome_radius;
            }
            else
            {
                radiansPerUnit = 0;

                // Place the plane at the distance which makes its width subtend exactly the requested span.
                distance = PLAYFIELD_CENTRE.X / MathF.Tan(halfSpan);
            }
        }

        /// <summary>
        /// The maximum rotation away from centre needed to bring any point of the playfield under the crosshair,
        /// as (yaw, pitch) in radians.
        /// </summary>
        /// <remarks>
        /// Clamping camera rotation to this keeps the whole beatmap reachable and stops the player from spinning
        /// away and losing it entirely.
        /// </remarks>
        public Vector2 AngularExtent =>
            mode == FPSosuProjectionMode.Dome
                ? new Vector2(halfSpan, PLAYFIELD_CENTRE.Y * radiansPerUnit)
                : new Vector2(halfSpan, MathF.Atan2(PLAYFIELD_CENTRE.Y, distance));

        /// <summary>
        /// Maps a position on the flat osu! playfield into the 3D world.
        /// </summary>
        /// <param name="playfieldPosition">A position in osu! playfield coordinates.</param>
        /// <returns>The world-space point, relative to the camera origin.</returns>
        public Vector3 PlayfieldToWorld(Vector2 playfieldPosition)
        {
            // Offset from the playfield centre. Y is negated because playfield Y grows downwards, world Y grows up.
            float offsetX = playfieldPosition.X - PLAYFIELD_CENTRE.X;
            float offsetY = PLAYFIELD_CENTRE.Y - playfieldPosition.Y;

            if (mode == FPSosuProjectionMode.Dome)
            {
                // Treat the playfield offset as arc length on the surface of a sphere, so equal playfield
                // distances always cost equal angular movement wherever they are on the beatmap.
                float yaw = offsetX * radiansPerUnit;
                float pitch = offsetY * radiansPerUnit;

                float cosPitch = MathF.Cos(pitch);

                return new Vector3(
                    MathF.Sin(yaw) * cosPitch * dome_radius,
                    MathF.Sin(pitch) * dome_radius,
                    MathF.Cos(yaw) * cosPitch * dome_radius);
            }

            // The playfield stays a flat rectangle floating at a fixed distance ahead.
            return new Vector3(offsetX, offsetY, distance);
        }

        /// <summary>
        /// Projects a world-space point through the camera back onto the playfield.
        /// </summary>
        /// <param name="world">A world-space point relative to the camera origin.</param>
        /// <param name="yaw">Camera yaw in radians. Positive turns right.</param>
        /// <param name="pitch">Camera pitch in radians. Positive looks up.</param>
        /// <param name="playfieldPosition">The resulting position in osu! playfield coordinates.</param>
        /// <param name="scale">
        /// Perspective scale for an object at this point. This is 1 for an object at the centre of the resting view
        /// at the default field of view, and grows or shrinks with zoom so objects read like world geometry.
        /// </param>
        /// <returns><c>true</c> if the point is in front of the camera, and therefore visible.</returns>
        public bool WorldToPlayfield(Vector3 world, float yaw, float pitch, out Vector2 playfieldPosition, out float scale)
        {
            // Rotate the point into view space by applying the inverse camera rotation: undo yaw about Y, then pitch about X.
            float cosYaw = MathF.Cos(yaw);
            float sinYaw = MathF.Sin(yaw);

            float viewX = world.X * cosYaw - world.Z * sinYaw;
            float forward = world.X * sinYaw + world.Z * cosYaw;

            float cosPitch = MathF.Cos(pitch);
            float sinPitch = MathF.Sin(pitch);

            float viewY = world.Y * cosPitch - forward * sinPitch;
            float viewZ = world.Y * sinPitch + forward * cosPitch;

            if (viewZ < near_plane)
            {
                playfieldPosition = PLAYFIELD_CENTRE;
                scale = 0;
                return false;
            }

            float perspective = focalLength / viewZ;

            playfieldPosition = new Vector2(
                PLAYFIELD_CENTRE.X + viewX * perspective,
                PLAYFIELD_CENTRE.Y - viewY * perspective);

            // Objects further along the view axis shrink, and the whole scene magnifies with the focal length so
            // that zooming (changing the field of view) changes object size. Normalising by the default focal length
            // keeps a centred object at its natural osu! size at the default field of view.
            scale = distance * focalLength / (viewZ * default_focal_length);
            return true;
        }

        /// <summary>
        /// Returns the camera rotation which places the given playfield position directly under the crosshair.
        /// </summary>
        /// <param name="playfieldPosition">A position in osu! playfield coordinates.</param>
        /// <returns>The required (yaw, pitch) in radians.</returns>
        /// <remarks>
        /// This is the exact inverse of <see cref="WorldToPlayfield"/> at the crosshair. It is how aiming is
        /// expressed in this ruleset: autoplay and replays rotate the camera instead of moving a cursor.
        /// </remarks>
        public Vector2 PlayfieldToCameraAngles(Vector2 playfieldPosition)
        {
            var world = PlayfieldToWorld(playfieldPosition);

            // Yaw is measured in the horizontal plane; pitch against the remaining horizontal distance.
            return new Vector2(
                MathF.Atan2(world.X, world.Z),
                MathF.Atan2(world.Y, MathF.Sqrt(world.X * world.X + world.Z * world.Z)));
        }

        /// <summary>
        /// Returns the playfield position the camera is aiming at, i.e. the point under the crosshair.
        /// </summary>
        /// <param name="yaw">Camera yaw in radians.</param>
        /// <param name="pitch">Camera pitch in radians.</param>
        /// <remarks>
        /// This is the exact inverse of <see cref="PlayfieldToCameraAngles"/>. Replay recording needs it because
        /// the raw cursor is always pinned to the centre of the screen: the meaningful aim point is wherever the
        /// camera is currently pointing, which is what gets written into replay frames.
        /// </remarks>
        public Vector2 CameraAnglesToPlayfield(float yaw, float pitch)
        {
            float offsetX;
            float offsetY;

            if (mode == FPSosuProjectionMode.Dome)
            {
                // Angles map linearly back to arc length along the dome surface.
                offsetX = yaw / radiansPerUnit;
                offsetY = pitch / radiansPerUnit;
            }
            else
            {
                // Intersect the view ray with the plane sitting at a fixed distance ahead.
                float cosYaw = MathF.Cos(yaw);

                if (MathF.Abs(cosYaw) < 1e-6f)
                    return PLAYFIELD_CENTRE;

                offsetX = distance * MathF.Tan(yaw);
                offsetY = distance / cosYaw * MathF.Tan(pitch);
            }

            return new Vector2(PLAYFIELD_CENTRE.X + offsetX, PLAYFIELD_CENTRE.Y - offsetY);
        }
    }
}
