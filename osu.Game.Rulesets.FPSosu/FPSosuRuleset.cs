// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.FPSosu
{
    public partial class FPSosuRuleset : OsuRuleset, ILegacyRuleset
    {
        int ILegacyRuleset.LegacyID => -1;
        public override string Description => "FPSosu";

        public override string ShortName => "fpsosuruleset";

        public override string PlayingVerb => "Shooting targets";

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
