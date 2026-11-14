// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    /// camera projection, and the result is drawn as line strips. This gives the player a stable frame of reference
    /// for where the beatmap is while they turn, instead of objects floating in empty space.
    ///
    /// Samples that fall behind the camera, or far enough outside the view that their projection runs towards
    /// infinity, are dropped. Each contiguous run of visible samples is drawn as its own path, so a gap never turns
    /// into a line streaking across the screen.
    /// </remarks>
    public partial class FPSosuPlayfieldBoundary : CompositeDrawable
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

        /// <summary>
        /// Four edges can each be split into at most two visible runs by the camera, so this covers any orientation.
        /// </summary>
        private const int max_paths = 8;

        private readonly Path[] paths = new Path[max_paths];
        private readonly List<Vector2> run = new();
        private int pathCount;

        public FPSosuPlayfieldBoundary()
        {
            RelativeSizeAxes = Axes.Both;

            // Sit behind the hit objects; it is a reference guide, not gameplay.
            Depth = float.MaxValue;

            for (int i = 0; i < max_paths; i++)
            {
                AddInternal(paths[i] = new Path
                {
                    PathRadius = 1f,
                    Colour = new Color4(1f, 1f, 1f, 0.4f),
                    Alpha = 0,
                });
            }
        }

        /// <summary>
        /// Rebuilds the boundary for the given projection and camera orientation.
        /// </summary>
        public void Refresh(FPSosuProjector projector, float yaw, float pitch)
        {
            pathCount = 0;
            run.Clear();

            var size = OsuPlayfield.BASE_SIZE;

            addEdge(projector, yaw, pitch, Vector2.Zero, new Vector2(size.X, 0));
            addEdge(projector, yaw, pitch, new Vector2(size.X, 0), size);
            addEdge(projector, yaw, pitch, size, new Vector2(0, size.Y));
            addEdge(projector, yaw, pitch, new Vector2(0, size.Y), Vector2.Zero);

            flush();

            for (int i = pathCount; i < max_paths; i++)
                paths[i].Alpha = 0;
        }

        private void addEdge(FPSosuProjector projector, float yaw, float pitch, Vector2 start, Vector2 end)
        {
            var size = OsuPlayfield.BASE_SIZE;

            for (int i = 0; i <= samples_per_edge; i++)
            {
                var playfieldPoint = Vector2.Lerp(start, end, (float)i / samples_per_edge);

                if (!projector.WorldToPlayfield(projector.PlayfieldToWorld(playfieldPoint), yaw, pitch, out var projected, out _)
                    || projected.X < -offscreen_margin || projected.X > size.X + offscreen_margin
                    || projected.Y < -offscreen_margin || projected.Y > size.Y + offscreen_margin)
                {
                    flush();
                    continue;
                }

                run.Add(projected);
            }
        }

        /// <summary>
        /// Turns the current run of visible samples into a path, if it has enough points to draw a line.
        /// </summary>
        private void flush()
        {
            if (run.Count < 2 || pathCount >= max_paths)
            {
                run.Clear();
                return;
            }

            var path = paths[pathCount++];

            path.Vertices = run;
            path.Alpha = 1;

            // Path draws its vertices relative to the top-left of their bounding box, so the position has to be that
            // same top-left for the vertices to land at their true playfield coordinates. The box is grown from the
            // origin rather than from the first vertex (Path.vertexBounds starts its minimum at zero), so the minimum
            // here starts at zero too: a run which lies entirely in positive coordinates has a box top-left of zero,
            // and offsetting it would displace the run by its own minimum.
            float minX = 0;
            float minY = 0;

            foreach (var sample in run)
            {
                minX = Math.Min(minX, sample.X - path.PathRadius);
                minY = Math.Min(minY, sample.Y - path.PathRadius);
            }

            path.Position = new Vector2(minX, minY);
            run.Clear();
        }
    }
}
