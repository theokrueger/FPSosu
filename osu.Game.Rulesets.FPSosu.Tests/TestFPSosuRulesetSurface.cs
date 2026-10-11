// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Guards the shape of <see cref="FPSosuRuleset"/> itself: how the game registers it, and that it keeps
    /// forwarding osu!'s behaviour instead of quietly falling back to the defaults of <see cref="Ruleset"/>.
    /// </summary>
    [TestFixture]
    public class TestFPSosuRulesetSurface
    {
        /// <summary>
        /// The ruleset must not implement <see cref="ILegacyRuleset"/>, whatever else it inherits.
        /// </summary>
        /// <remarks>
        /// <see cref="RealmRulesetStore"/> registers legacy rulesets first and keys their database rows on
        /// <see cref="RulesetInfo.OnlineID"/> instead of on their instantiation info. Non-legacy custom rulesets all
        /// report an online ID of -1, so a legacy ruleset claiming -1 loses its row to any other custom ruleset which
        /// was registered first, and then silently disappears from the game with nothing written to the log.
        /// </remarks>
        [Test]
        public void TestRegisteredRulesetIsNotALegacyRuleset()
        {
            Assert.That(typeof(FPSosuRuleset).GetInterfaces(), Does.Not.Contain(typeof(ILegacyRuleset)),
                "the registered ruleset must not be an ILegacyRuleset, or the ruleset store can silently skip it");
        }

        /// <summary>
        /// Every member <see cref="OsuRuleset"/> overrides must also be overridden here, because gameplay inherits
        /// osu! through <see cref="FPSosuOsuRuleset"/> while everything the game asks the *registered* ruleset keeps
        /// answering osu!'s answer rather than <see cref="Ruleset"/>'s much emptier default.
        /// </summary>
        /// <remarks>
        /// This is what makes osu! updates safe: if a future version adds a new override to <see cref="OsuRuleset"/>,
        /// this test fails until the new member is forwarded (or deliberately handled) here.
        /// </remarks>
        [Test]
        public void TestEveryOsuRulesetOverrideIsDeclaredOnTheRegisteredRuleset()
        {
            var forwarded = rulesetOverrides(typeof(FPSosuRuleset)).ToHashSet();
            var missing = rulesetOverrides(typeof(OsuRuleset)).Except(forwarded).OrderBy(s => s).ToList();

            Assert.That(missing, Is.Empty,
                "FPSosuRuleset must forward these osu! members (see the remarks on FPSosuRuleset): " + string.Join(", ", missing));
        }

        /// <summary>
        /// The signatures of every member of <paramref name="type"/> which overrides a member of <see cref="Ruleset"/>.
        /// </summary>
        private static IEnumerable<string> rulesetOverrides(Type type)
            => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                   .Where(m => m.DeclaringType == type)
                   .Where(m => m.GetBaseDefinition().DeclaringType == typeof(Ruleset))
                   .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name))})");
    }
}
