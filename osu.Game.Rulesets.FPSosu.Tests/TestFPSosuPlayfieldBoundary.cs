// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics.Lines;
using osu.Framework.Testing;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.FPSosu.UI;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    [TestFixture]
    public class TestFPSosuPlayfieldBoundary
    {
        /// <summary>
        /// Every drawn vertex must land on the playfield coordinate its sample was projected to.
        /// </summary>
        /// <remarks>
        /// A <see cref="Path"/> draws its vertices relative to the top-left of its bounding box, which always includes
        /// the origin, so the path has to be positioned by that same top-left. Getting this wrong shifts each run of
        /// the border whenever the camera turns far enough to split it, which is invisible at rest.
        /// </remarks>
        [TestCase(FPSosuProjectionMode.Dome)]
        [TestCase(FPSosuProjectionMode.Plane)]
        public void TestVerticesLandOnTheirProjectedPositions(FPSosuProjectionMode mode)
        {
            var boundary = new FPSosuPlayfieldBoundary();
            var projector = new FPSosuProjector(mode, FPSosuProjector.DEFAULT_FIELD_OF_VIEW, FPSosuProjector.DEFAULT_PLAYFIELD_SPAN);

            foreach (float yaw in new[] { 0f, 0.05f, 0.2f, 0.5f, 1f, -0.4f })
            {
                foreach (float pitch in new[] { 0f, 0.15f, -0.3f })
                {
                    boundary.Refresh(projector, yaw, pitch);

                    int vertices = 0;

                    foreach (var path in boundary.ChildrenOfType<Path>())
                    {
                        foreach (var vertex in path.Vertices)
                        {
                            Vector2 drawn = path.Position + path.PositionInBoundingBox(vertex);

                            Assert.That(Vector2.Distance(drawn, vertex), Is.LessThan(0.01f),
                                $"vertex {vertex} was drawn at {drawn} ({mode}, yaw {yaw}, pitch {pitch})");

                            vertices++;
                        }
                    }

                    Assert.That(vertices, Is.GreaterThan(0), "the border must draw something at this orientation");
                }
            }
        }
    }
}
