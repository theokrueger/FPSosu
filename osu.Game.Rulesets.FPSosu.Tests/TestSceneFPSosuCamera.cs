// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.FPSosu.Projection;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    [TestFixture]
    public class TestSceneFPSosuCamera
    {
        [Test]
        public void TestStartsLookingAtPlayfieldCentre()
        {
            var camera = new FPSosuCamera();

            Assert.That(camera.Yaw, Is.Zero);
            Assert.That(camera.Pitch, Is.Zero);
        }

        [Test]
        public void TestRotationAccumulates()
        {
            var camera = new FPSosuCamera { AngularLimit = new Vector2(1f) };

            camera.Rotate(0.2f, 0.1f);
            camera.Rotate(0.2f, 0.1f);

            Assert.That(camera.Yaw, Is.EqualTo(0.4f).Within(1e-5f));
            Assert.That(camera.Pitch, Is.EqualTo(0.2f).Within(1e-5f));
        }

        /// <summary>
        /// Rotation must be clamped so the player can never turn far enough to lose the beatmap behind them.
        /// </summary>
        [Test]
        public void TestRotationClampsToAngularLimit()
        {
            var camera = new FPSosuCamera { AngularLimit = new Vector2(0.5f, 0.25f) };

            camera.Rotate(10f, 10f);

            Assert.That(camera.Yaw, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(camera.Pitch, Is.EqualTo(0.25f).Within(1e-5f));

            camera.Rotate(-100f, -100f);

            Assert.That(camera.Yaw, Is.EqualTo(-0.5f).Within(1e-5f));
            Assert.That(camera.Pitch, Is.EqualTo(-0.25f).Within(1e-5f));
        }

        /// <summary>
        /// Replay playback sets absolute orientation, which must also respect the limit.
        /// </summary>
        [Test]
        public void TestLookAtIsClamped()
        {
            var camera = new FPSosuCamera { AngularLimit = new Vector2(0.5f, 0.25f) };

            camera.LookAt(new Vector2(2f, -2f));

            Assert.That(camera.Yaw, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(camera.Pitch, Is.EqualTo(-0.25f).Within(1e-5f));
        }

        [Test]
        public void TestResetReturnsToCentre()
        {
            var camera = new FPSosuCamera { AngularLimit = new Vector2(1f) };

            camera.Rotate(0.4f, 0.3f);
            camera.Reset();

            Assert.That(camera.Yaw, Is.Zero);
            Assert.That(camera.Pitch, Is.Zero);
        }

        /// <summary>
        /// Tightening the limit must not leave the camera outside it; the next rotation has to bring it back in range.
        /// </summary>
        [Test]
        public void TestRotationAfterLimitTightensIsClamped()
        {
            var camera = new FPSosuCamera { AngularLimit = new Vector2(1f) };

            camera.Rotate(0.9f, 0.9f);
            camera.AngularLimit = new Vector2(0.2f);
            camera.Rotate(0, 0);

            Assert.That(camera.Yaw, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(camera.Pitch, Is.EqualTo(0.2f).Within(1e-5f));
        }
    }
}
