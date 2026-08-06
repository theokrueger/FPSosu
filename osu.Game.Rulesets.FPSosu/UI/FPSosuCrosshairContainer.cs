// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// The gameplay cursor for FPSosu: a crosshair fixed at the centre of the playfield.
    /// </summary>
    /// <remarks>
    /// The base <see cref="Framework.Graphics.Cursor.CursorContainer"/> moves its cursor to follow mouse movement.
    /// Here the cursor is anchored to the centre instead, because aiming is expressed by rotating the camera. The
    /// crosshair still marks exactly where hits are registered, since the input manager holds the real cursor there.
    /// </remarks>
    public partial class FPSosuCrosshairContainer : GameplayCursorContainer
    {
        private readonly BindableBool showCrosshair = new BindableBool(true);

        protected override Drawable CreateCursor() => new Crosshair();

        [BackgroundDependencyLoader]
        private void load(FPSosuConfigManager? config)
        {
            config?.BindWith(FPSosuRulesetSetting.ShowCrosshair, showCrosshair);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ActiveCursor.Anchor = Anchor.Centre;
            ActiveCursor.Origin = Anchor.Centre;

            showCrosshair.BindValueChanged(visible => ActiveCursor.Alpha = visible.NewValue ? 1 : 0, true);
        }

        protected override void Update()
        {
            base.Update();

            // Hold the crosshair at the centre regardless of any positional input that reaches us.
            ActiveCursor.Position = Vector2.Zero;
        }

        private partial class Crosshair : CompositeDrawable
        {
            private const float arm_length = 10;
            private const float thickness = 2;
            private const float gap = 3;

            public Crosshair()
            {
                AutoSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    arm(Anchor.Centre, new Vector2(thickness, arm_length), new Vector2(0, -(gap + arm_length / 2))),
                    arm(Anchor.Centre, new Vector2(thickness, arm_length), new Vector2(0, gap + arm_length / 2)),
                    arm(Anchor.Centre, new Vector2(arm_length, thickness), new Vector2(-(gap + arm_length / 2), 0)),
                    arm(Anchor.Centre, new Vector2(arm_length, thickness), new Vector2(gap + arm_length / 2, 0)),
                    new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(thickness),
                        Colour = Color4.White,
                    },
                };
            }

            private static Drawable arm(Anchor anchor, Vector2 size, Vector2 offset) => new Box
            {
                Anchor = anchor,
                Origin = Anchor.Centre,
                Size = size,
                Position = offset,
                Colour = Color4.White,
            };
        }
    }
}
