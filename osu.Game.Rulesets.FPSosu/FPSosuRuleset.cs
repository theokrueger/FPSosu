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
    /// A ruleset which turns standard osu! into a 3D first-person aim trainer.
    /// </summary>
    /// <remarks>
    /// The flat osu! playfield is embedded into a 3D world and projected back onto the screen through a camera that
    /// the mouse rotates, while the crosshair stays fixed at the centre of the screen. Everything else, including
    /// hit objects, scoring, mods and difficulty calculation, is inherited from <see cref="OsuRuleset"/>.
    /// </remarks>
    public partial class FPSosuRuleset : OsuRuleset, ILegacyRuleset
    {
        /// <summary>
        /// Overridden so scores and beatmaps from this ruleset never collide with standard osu! (which is ID 0).
        /// </summary>
        int ILegacyRuleset.LegacyID => -1;

        public override string Description => "FPSosu";

        public override string ShortName => "fpsosuruleset";

        public override string PlayingVerb => "Shooting targets";

        public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            => new DrawableFPSosuRuleset(this, beatmap, mods);

        public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new FPSosuRulesetConfigManager(settings, RulesetInfo);

        public override RulesetSettingsSubsection CreateSettings() => new FPSosuSettingsSubsection(this);

        /// <summary>
        /// The osu! mods which do not work under the 3D projection: they either error out or rewrite object positions
        /// in ways the projection overwrites, so they are hidden from the mod list.
        /// </summary>
        private static readonly Type[] unsupported_mods =
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

                    yield return kept.Length == multi.Mods.Length ? multi : kept.Length == 1 ? kept[0] : new MultiMod(kept);
                }
                else if (!unsupported_mods.Contains(mod.GetType()))
                {
                    yield return mod;
                }
            }
        }

        public override Drawable CreateIcon() => new Icon();

        /// <summary>
        /// A sniper-scope crosshair: a ring with four ticks and a centre dot.
        /// </summary>
        public partial class Icon : CompositeDrawable
        {
            public Icon()
            {
                Size = new Vector2(20);

                InternalChildren = new Drawable[]
                {
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
                };
            }
        }
    }
}
