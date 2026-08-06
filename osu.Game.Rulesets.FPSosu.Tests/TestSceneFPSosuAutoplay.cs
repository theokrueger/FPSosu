// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Verifies that gameplay still scores through the 3D projection.
    /// </summary>
    /// <remarks>
    /// This is the end-to-end proof that the transformation preserved playability. Autoplay aims by rotating the
    /// camera, so if projection, camera clamping, the cursor lock or hit detection were wrong, objects would sit
    /// away from the crosshair and every one of them would be missed.
    /// </remarks>
    [TestFixture]
    public partial class TestSceneFPSosuAutoplay : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new FPSosuRuleset();

        protected override bool Autoplay => true;

        [Test]
        public void TestAutoplayScoresWithoutMissing()
        {
            AddAssert("fps ruleset in use", () => Player.DrawableRuleset, Is.TypeOf<DrawableFPSosuRuleset>);

            AddUntilStep("objects were hit", () => Player.ScoreProcessor.Combo.Value > 5);

            AddAssert("nothing was missed", () => !Player.ScoreProcessor.Statistics.TryGetValue(HitResult.Miss, out int misses) || misses == 0);
        }

        /// <summary>
        /// The camera must be doing the aiming. If the beatmap were still being played flat with a moving cursor,
        /// the camera would never leave its resting orientation.
        /// </summary>
        [Test]
        public void TestAutoplayAimsByRotatingCamera()
        {
            AddUntilStep("objects were hit", () => Player.ScoreProcessor.Combo.Value > 5);

            AddAssert("camera rotated away from centre", () =>
            {
                var camera = ((DrawableFPSosuRuleset)Player.DrawableRuleset).Camera;
                return camera.Yaw != 0 || camera.Pitch != 0;
            });
        }
    }
}
