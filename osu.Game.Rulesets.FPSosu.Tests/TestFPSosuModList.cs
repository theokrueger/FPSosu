// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.FPSosu;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// The 3D projection is incompatible with a set of osu! mods: some crash it, others rewrite object positions in
    /// ways the projection overwrites. They must not be offered in the mod list.
    /// </summary>
    [TestFixture]
    public class TestFPSosuModList
    {
        private static readonly Type[] removed_mods =
        {
            typeof(OsuModAutoplay),
            typeof(OsuModCinema),
            typeof(OsuModBubbles),
            typeof(OsuModBloom),
            typeof(OsuModBarrelRoll),
            typeof(OsuModDeflate),
            typeof(OsuModGrow),
            typeof(OsuModSpinIn),
            typeof(OsuModTransform),
            typeof(OsuModWiggle),
            typeof(OsuModDepth),
            typeof(OsuModRepel),
            typeof(OsuModMagnetised),
            typeof(OsuModNoScope),
        };

        [Test]
        public void TestIncompatibleModsAreHidden()
        {
            foreach (var mod in offeredMods())
            {
                var types = mod is MultiMod multi ? multi.Mods.Select(m => m.GetType()) : new[] { mod.GetType() };

                foreach (var type in types)
                    Assert.That(removed_mods, Does.Not.Contain(type), $"{type.Name} must not be offered");
            }
        }

        [Test]
        public void TestCompatibleModsRemainOffered()
        {
            var offered = offeredMods().Select(mod => mod.GetType()).ToHashSet();

            Assert.That(offered, Does.Contain(typeof(OsuModHardRock)));
            Assert.That(offered, Does.Contain(typeof(OsuModEasy)));
            Assert.That(offered, Does.Contain(typeof(OsuModRelax)));
            Assert.That(offered, Does.Contain(typeof(OsuModSpunOut)));
            Assert.That(offered, Does.Contain(typeof(OsuModDifficultyAdjust)));
        }

        private IEnumerable<Mod> offeredMods()
        {
            var ruleset = new FPSosuRuleset();

            return Enum.GetValues<ModType>().SelectMany(type => ruleset.GetModsFor(type));
        }
    }
}
