// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Game.Database;

namespace osu.Game.Rulesets.FPSosu.Tests
{
    /// <summary>
    /// Guards the way the ruleset is registered with the game.
    /// </summary>
    /// <remarks>
    /// <see cref="RealmRulesetStore"/> registers "legacy" rulesets (anything implementing <see cref="ILegacyRuleset"/>)
    /// before anything else, keying their database rows on <see cref="RulesetInfo.OnlineID"/> rather than on their
    /// instantiation info. Every ruleset which is not itself a legacy ruleset reports <c>OnlineID == -1</c>
    /// (see the fallback in <see cref="Ruleset.RulesetInfo"/>), so a derived ruleset reporting <c>-1</c> itself loses
    /// its row to whichever custom ruleset was registered first - and then silently disappears from the game, with
    /// nothing written to the log.
    ///
    /// These tests build a real ruleset store against a throwaway ruleset database, which is the smallest
    /// reproduction of that: the ruleset assembly is already loaded, so the store finds it exactly as it would find
    /// it in the rulesets folder.
    /// </remarks>
    [TestFixture]
    public class TestFPSosuRulesetRegistration
    {
        /// <summary>
        /// The ruleset's short name, which doubles as the primary key of its row in the ruleset database.
        /// </summary>
        private const string fpsosu_short_name = "fpsosuruleset";

        private const string other_ruleset_short_name = "fpsosu-tests-other-ruleset";

        private string storageDirectory = null!;

        private RealmAccess realm = null!;

        [SetUp]
        public void SetUp()
        {
            storageDirectory = Path.Combine(Path.GetTempPath(), $"fpsosu-ruleset-registration-{Guid.NewGuid():N}");

            Directory.CreateDirectory(storageDirectory);

            // a store with no storage of its own, so only the already-loaded ruleset assembly is considered.
            realm = new RealmAccess(new NativeStorage(storageDirectory), "client.realm");
        }

        [TearDown]
        public void TearDown()
        {
            realm.Dispose();

            try
            {
                Directory.Delete(storageDirectory, true);
            }
            catch (IOException)
            {
                // The realm backing file can still be held open briefly on some platforms; the directory is in the
                // system temp folder either way.
            }
        }

        /// <summary>
        /// The bug this ruleset was restructured to avoid: a ruleset claiming the online ID every custom ruleset
        /// shares never gets a row of its own, so it is invisible in the game with no error anywhere.
        /// </summary>
        [Test]
        public void TestRulesetIsRegisteredAlongsideAnotherCustomRuleset()
        {
            Assert.That(new FPSosuRuleset().ShortName, Is.EqualTo(fpsosu_short_name), "the short name in this test must match the ruleset's");

            // Simulate an install which already has a different custom ruleset registered. Non-legacy custom rulesets
            // all share online ID -1, which is what the store keys its "already registered" check on.
            realm.Write(r => r.Add(new RulesetInfo(other_ruleset_short_name, "Another custom ruleset", "No.Such.Type, NoSuchAssembly", -1)));

            using var store = new RealmRulesetStore(realm, null);

            Assert.That(store.GetRuleset(other_ruleset_short_name), Is.Null, "the seeded ruleset is not expected to be playable");

            Assert.That(store.GetRuleset(fpsosu_short_name), Is.Not.Null,
                "FPSosu must be registered even when another custom ruleset is already installed");
        }

        /// <summary>
        /// An install which registered FPSosu before this restructuring must keep working: its row holds the saved
        /// scores, beatmaps and settings, so the store has to adopt that row rather than write a new one (a second
        /// row with the same short name would throw, because the short name is the primary key).
        /// </summary>
        [Test]
        public void TestExistingRowIsReusedRatherThanDuplicated()
        {
            var ruleset = new FPSosuRuleset();

            realm.Write(r => r.Add(new RulesetInfo(fpsosu_short_name, ruleset.Description, ruleset.RulesetInfo.InstantiationInfo, ruleset.RulesetInfo.OnlineID)));

            using var store = new RealmRulesetStore(realm, null);

            Assert.That(store.GetRuleset(fpsosu_short_name), Is.Not.Null, "the existing row must be adopted");

            // read inside the realm: managed objects (and realm queries) cannot be read once it is closed.
            var rows = realm.Run(r =>
            {
                var found = new List<(string ShortName, string InstantiationInfo)>();

                foreach (var info in r.All<RulesetInfo>())
                    found.Add((info.ShortName, info.InstantiationInfo));

                return found;
            });

            var fpsosuRows = rows.Where(row => row.ShortName == fpsosu_short_name).ToList();

            Assert.That(fpsosuRows, Has.Count.EqualTo(1),
                $"the store must not write a second row for the same short name (rows: {string.Join(" | ", rows.Select(row => row.ShortName))})");

            Assert.That(Type.GetType(fpsosuRows[0].InstantiationInfo), Is.EqualTo(typeof(FPSosuRuleset)),
                $"existing rows must keep resolving to the registered ruleset, or saved scores and beatmaps are orphaned (instantiation info: {fpsosuRows[0].InstantiationInfo})");
        }
    }
}
