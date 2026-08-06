// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Lines;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Osu.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// Draws the edge of the osu! playfield as it sits in the 3D world.
    /// </summary>
    /// <remarks>
    /// The flat playfield rectangle is sampled along its four edges, every sample is pushed through the current
    /// camera projection, and the result is drawn as a line strip. This gives the player a stable frame of reference
    /// for where the beatmap is while they turn, instead of objects floating in empty space.
    ///
    /// Samples that fall behind the camera, or far enough outside the view that their projection runs towards
    /// infinity, are dropped so the line never streaks across the screen.
    /// </remarks>
    public partial class FPSosuPlayfieldBoundary : Path
    {
        /// <summary>
        /// How many segments each edge of the playfield is split into. More samples follow the dome's curvature
        /// more closely.
        /// </summary>
        private const int samples_per_edge = 32;

        /// <summary>
        /// How far outside the playfield a projected sample may land before it is discarded. This clips the samples
        /// nearest the camera, whose projection otherwise tends towards infinity.
        /// </summary>
        private const float offscreen_margin = 1000f;

        private readonly List<Vector2> samples = new List<Vector2>();

        public FPSosuPlayfieldBoundary()
        {
            PathRadius = 1f;
            Colour = new Color4(1f, 1f, 1f, 0.4f);

            // Sit behind the hit objects; it is a reference guide, not gameplay.
            Depth = float.MaxValue;
        }

        /// <summary>
        /// Rebuilds the boundary for the given projection and camera orientation.
        /// </summary>
        public void Refresh(FPSosuProjector projector, float yaw, float pitch)
        {
            var size = OsuPlayfield.BASE_SIZE;

            samples.Clear();
            addEdge(projector, yaw, pitch, Vector2.Zero, new Vector2(size.X, 0));
            addEdge(projector, yaw, pitch, new Vector2(size.X, 0), size);
            addEdge(projector, yaw, pitch, size, new Vector2(0, size.Y));
            addEdge(projector, yaw, pitch, new Vector2(0, size.Y), Vector2.Zero);

            Vertices = samples;

            // Path draws its vertices relative to the top-left of their bounding box, so shift it back so the
            // vertices land at their true playfield coordinates.
            float minX = 0;
            float minY = 0;

            foreach (var sample in samples)
            {
                minX = Math.Min(minX, sample.X - PathRadius);
                minY = Math.Min(minY, sample.Y - PathRadius);
            }

            Position = new Vector2(minX, minY);
        }

        private void addEdge(FPSosuProjector projector, float yaw, float pitch, Vector2 start, Vector2 end)
        {
            var size = OsuPlayfield.BASE_SIZE;

            for (int i = 0; i <= samples_per_edge; i++)
            {
                var playfieldPoint = Vector2.Lerp(start, end, (float)i / samples_per_edge);

                if (!projector.WorldToPlayfield(projector.PlayfieldToWorld(playfieldPoint), yaw, pitch, out var projected, out _))
                    continue;

                if (projected.X < -offscreen_margin || projected.X > size.X + offscreen_margin
                                                     || projected.Y < -offscreen_margin || projected.Y > size.Y + offscreen_margin)
                    continue;

                samples.Add(projected);
            }
        }
    }
}
