# Upstream: derived custom rulesets are silently dropped from the ruleset store

A custom ruleset which derives from an official one (`OsuRuleset`, `TaikoRuleset`, `CatchRuleset`,
`ManiaRuleset`) is silently not registered at all - it never appears in the ruleset selector, and **nothing is
written to the log** - on any install which has already registered one other custom ruleset.

This bit FPSosu; see [the report](https://github.com/theokrueger/FPSosu/issues/1). It applies to every derived
custom ruleset, not just this one.

## Root cause

`Ruleset.RulesetInfo` gives a ruleset an online ID of `-1` unless it is a legacy ruleset
(`osu.Game/Rulesets/Ruleset.cs`):

```csharp
OnlineID = (this as ILegacyRuleset)?.LegacyID ?? -1,
```

Official rulesets report 0-3. Every ordinary custom ruleset reports `-1`. So does any ruleset which derives from an
official one: it inherits `ILegacyRuleset` (it cannot not implement it), and to keep away from the official ID of 0
it has to override `LegacyID` - and there is no ID available to it except a negative one.

`RealmRulesetStore.prepareDetachedRulesets` then registers legacy rulesets first and keys their rows on
`RulesetInfo.OnlineID` rather than on their instantiation info:

```csharp
// add all legacy rulesets first to ensure they have exclusive choice of primary key.
foreach (var r in instances.Where(r => r is ILegacyRuleset))
{
    if (realm.All<RulesetInfo>().FirstOrDefault(rr => rr.OnlineID == r.RulesetInfo.OnlineID) == null)
        realm.Add(new RulesetInfo(r.RulesetInfo.ShortName, r.RulesetInfo.Name, r.RulesetInfo.InstantiationInfo, r.RulesetInfo.OnlineID));
}
```

For a derived custom ruleset that `FirstOrDefault` finds the row of whichever unrelated custom ruleset registered
first - they all share online ID `-1` - so no row is written for it. The consistency pass which follows only
iterates rows which exist, so the ruleset is never instantiated, never validated, never made available, and never
reported as broken. `RulesetInfo.ShortName` is the primary key, so a ruleset cannot recover from this by changing
its online ID later either: the legacy pass would then try to insert a *second* row with the same short name, and
`RealmDuplicatePrimaryKeyValueException` is thrown out of the store's constructor (which takes ruleset loading down
with it).

The two behaviours the legacy-first pass exists for - official rulesets getting first choice of ID, and a ruleset
not being re-added once it has a row - both only make sense for real legacy IDs (0-3). The store already has the
predicate for that (`osu.Game/Extensions/ModelExtensions.cs`):

```csharp
public static bool IsLegacyRuleset(this IRulesetInfo ruleset) => ruleset.OnlineID >= 0 && ruleset.OnlineID <= ILegacyRuleset.MAX_LEGACY_RULESET_ID;
```

## Reproduction

In the game:

1. Start from a data directory with no custom rulesets registered (move `client.realm` aside).
2. Put **any other** custom ruleset into `rulesets/` and launch osu! once, so it gets a row.
3. Add the derived ruleset's DLL and launch again.

The derived ruleset does not appear, and the log says nothing about it - other broken rulesets in the same folder
still report errors normally.

As a test (`osu.Game.Tests/Database/RulesetStoreTests.cs`), seed a row for an unrelated custom ruleset and expect
the derived one to come back available:

```csharp
[Test]
public void TestDerivedRulesetWithNegativeLegacyIdIsAvailable()
{
    RunTestWithRealm((realm, storage) =>
    {
        // some unrelated custom ruleset was registered first, which is what every custom ruleset's row looks like
        realm.Write(r => r.Add(new RulesetInfo("other-custom", "Other custom", "Other.Custom.Ruleset, Other", -1)));

        using var rulesets = new RealmRulesetStore(realm, storage);

        ClassicAssert.IsNotNull(rulesets.GetRuleset("derived-ruleset"));
    });
}
```

with a test ruleset of the shape `class DerivedTestRuleset : OsuRuleset { int ILegacyRuleset.LegacyID => -1; }`.

## Proposed fix

Route by real legacy IDs rather than by the interface, so official rulesets keep their exclusive first choice and
derived rulesets are registered by instantiation info like every other custom ruleset:

```diff
-                foreach (var r in instances.Where(r => r is ILegacyRuleset))
+                foreach (var r in instances.Where(r => r.RulesetInfo.IsLegacyRuleset()))
                     if (realm.All<RulesetInfo>().FirstOrDefault(rr => rr.OnlineID == r.RulesetInfo.OnlineID) == null)
                         realm.Add(new RulesetInfo(r.RulesetInfo.ShortName, r.RulesetInfo.Name, r.RulesetInfo.InstantiationInfo, r.RulesetInfo.OnlineID));
```

(`using osu.Game.Extensions;`) A more conservative equivalent keeping the interface check is
`r is ILegacyRuleset && r.RulesetInfo.OnlineID >= 0`.

This fixes both fresh installs and installs which are already affected, and it leaves existing rows - and therefore
saved scores, settings and cached difficulty - untouched.

## Workaround used here

`FPSosuRuleset` registers as a plain `Ruleset` (not an `ILegacyRuleset`) and forwards osu!'s behaviour to an
internal `OsuRuleset` which is never registered itself - see the remarks on `FPSosuRuleset` and the
[registration section](../AGENTS.md#ruleset-registration). That is a workaround with a real cost: every member
`OsuRuleset` overrides has to be forwarded manually, and the ruleset has to keep up with osu! adding new ones.
