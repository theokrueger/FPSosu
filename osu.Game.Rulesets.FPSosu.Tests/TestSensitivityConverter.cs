// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.FPSosu.Configuration;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    [TestFixture]
    public class TestSensitivityConverter
    {
        [Test]
        public void TestConversionPreservesCmPer360()
        {
            foreach (var game in new[]
                     {
                         FPSosuSensitivityGame.CounterStrike2,
                         FPSosuSensitivityGame.Valorant,
                         FPSosuSensitivityGame.RainbowSixSiege,
                         FPSosuSensitivityGame.ApexLegends,
                         FPSosuSensitivityGame.Overwatch2,
                         FPSosuSensitivityGame.CallOfDuty,
                     })
            {
                foreach (float sourceSensitivity in new[] { 0.3f, 1.0f, 2.5f })
                {
                    foreach (int dpi in new[] { 400, 800, 1600 })
                    {
                        float converted = game.ToFPSosuSensitivity(sourceSensitivity);

                        Assert.That(converted, Is.GreaterThan(0), $"{game} conversion must stay positive");
                        Assert.That(FPSosuSensitivityConverter.GetFPSosuCmPer360(converted, dpi),
                                    Is.EqualTo(game.GetCmPer360(sourceSensitivity, dpi)).Within(1e-3),
                                    $"{game} @ {sourceSensitivity} sens, {dpi} DPI: conversion must preserve cm/360");
                    }
                }
            }
        }

        [Test]
        public void TestKnownConversions()
        {
            // Yaw ratios against FPSosu's yaw of 0.0005 rad/px * 180/pi deg/count.
            Assert.That(FPSosuSensitivityGame.CounterStrike2.ToFPSosuSensitivity(1.0f), Is.EqualTo(0.768).Within(0.01));
            Assert.That(FPSosuSensitivityGame.Valorant.ToFPSosuSensitivity(0.5f), Is.EqualTo(1.222).Within(0.01));
            Assert.That(FPSosuSensitivityGame.RainbowSixSiege.ToFPSosuSensitivity(10.0f), Is.EqualTo(2.0).Within(0.01));

            // A CS2 player at sens 1 on 800 DPI turns 360 degrees over ~52 cm.
            Assert.That(FPSosuSensitivityGame.CounterStrike2.GetCmPer360(1.0f, 800), Is.EqualTo(51.9).Within(0.2));
        }
    }
}
