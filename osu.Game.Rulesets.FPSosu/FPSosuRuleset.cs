// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
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

        public override Drawable CreateIcon() => new Icon(ShortName[0]);

        public partial class Icon : CompositeDrawable
        {
            public Icon(char c)
            {
                InternalChildren = new Drawable[]
                {
                    new Circle
                    {
                        Size = new Vector2(20),
                        Colour = Color4.White,
                    },
                    new SpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Text = c.ToString(),
                        Font = OsuFont.Default.With(size: 18)
                    }
                };
            }
        }
    }
}
