// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.FPSosu
{
    /// <summary>
    /// The osu!-derived half of the ruleset: everything the game needs while a beatmap is being played.
    /// </summary>
    /// <remarks>
    /// This is deliberately <b>not</b> the registered type - see <see cref="FPSosuRuleset"/>. It exists so gameplay
    /// inherits every osu! behaviour (hit objects, scoring, health, mods, difficulty, skinning, replays, judgement)
    /// instead of reimplementing them, and reports FPSosu's identity so scores and beatmaps are not filed under osu!.
    ///
    /// <para>
    /// It must stay non-public: the ruleset store registers the first <i>public</i> <see cref="Ruleset"/> subclass in
    /// the assembly, and registering this one would restore the legacy-row bug.
    /// </para>
    /// </remarks>
    internal partial class FPSosuOsuRuleset : OsuRuleset, ILegacyRuleset
    {
        public override string ShortName => FPSosuRuleset.SHORT_NAME;

        public override string Description => FPSosuRuleset.DESCRIPTION;

        public override string PlayingVerb => FPSosuRuleset.PLAYING_VERB;

        /// <summary>
        /// Reported so that this ruleset's <see cref="RulesetInfo.OnlineID"/> matches the row the registered
        /// <see cref="FPSosuRuleset"/> created for it. Inheriting osu!'s ID of 0 would silently attribute scores,
        /// beatmaps and replays of this ruleset to standard osu!.
        /// </summary>
        int ILegacyRuleset.LegacyID => -1;

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableFPSosuRuleset(this, beatmap, mods);

        /// <summary>
        /// The osu! mods which do not work under the 3D projection: they error out, rewrite object positions in ways
        /// the projection overwrites, or drive the cursor themselves (which is pinned to the crosshair here).
        /// </summary>
        private static readonly HashSet<Type> unsupported_mods =
        [
            typeof(OsuModAutoplay),
            typeof(OsuModAutopilot),
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
        ];

        public override IEnumerable<Mod> GetModsFor(ModType type)
        {
            foreach (var mod in base.GetModsFor(type))
            {
                if (mod is MultiMod multi)
                {
                    // Some incompatible mods are only offered wrapped in a multi-mod; drop those entirely and keep
                    // the pairing only when it still contains compatible mods.
                    var kept = multi.Mods.Where(m => !unsupported_mods.Contains(m.GetType())).ToArray();

                    if (kept.Length == 0)
                        continue;

                    if (kept.Length == multi.Mods.Length)
                        yield return multi;
                    else if (kept.Length == 1)
                        yield return kept[0];
                    else
                        yield return new MultiMod(kept);
                }
                else if (!unsupported_mods.Contains(mod.GetType()))
                {
                    yield return mod;
                }
            }
        }

        public override Drawable CreateIcon() => new Icon();

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new FPSosuRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() => new FPSosuSettingsSubsection(this);

        /// <summary>
        /// A sniper-scope crosshair: a ring with four ticks and a centre dot.
        /// </summary>
        public partial class Icon : CompositeDrawable
        {
            public Icon()
            {
                Size = new Vector2(20);

                InternalChildren =
                [
                    new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.Transparent,
                        BorderColour = Color4.White,
                        BorderThickness = 2,
                    },
                    new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(2, 6), Position = new Vector2(0, -7), Colour = Color4.White },
                    new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(2, 6), Position = new Vector2(0, 7), Colour = Color4.White },
                    new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(6, 2), Position = new Vector2(-7, 0), Colour = Color4.White },
                    new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(6, 2), Position = new Vector2(7, 0), Colour = Color4.White },
                    new Box { Anchor = Anchor.Centre, Origin = Anchor.Centre, Size = new Vector2(2), Colour = Color4.White },
                ];
            }
        }
    }
}
