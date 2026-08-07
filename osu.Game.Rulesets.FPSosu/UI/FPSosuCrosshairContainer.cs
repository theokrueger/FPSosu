// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
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
    /// Its gap, line length, thickness, centre dot, opacity, outline and colour can be tuned in the ruleset settings.
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
            private const float outline_width = 1;

            private readonly Container pieces = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                AutoSizeAxes = Axes.Both,
            };

            private readonly List<Box> fillPieces = new List<Box>();
            private readonly List<Box> outlinePieces = new List<Box>();

            private readonly BindableBool crosshairOutline = new BindableBool(true);
            private readonly Bindable<FPSosuCrosshairColour> crosshairColour = new Bindable<FPSosuCrosshairColour>(FPSosuCrosshairColour.White);
            private readonly BindableFloat crosshairGap = new BindableFloat(3);
            private readonly BindableFloat crosshairLineLength = new BindableFloat(10);
            private readonly BindableFloat crosshairThickness = new BindableFloat(2);
            private readonly BindableBool crosshairCenterDot = new BindableBool(true);
            private readonly BindableFloat crosshairOpacity = new BindableFloat(1);

            [BackgroundDependencyLoader]
            private void load(FPSosuConfigManager? config)
            {
                config?.BindWith(FPSosuRulesetSetting.CrosshairOutline, crosshairOutline);
                config?.BindWith(FPSosuRulesetSetting.CrosshairColour, crosshairColour);
                config?.BindWith(FPSosuRulesetSetting.CrosshairGap, crosshairGap);
                config?.BindWith(FPSosuRulesetSetting.CrosshairLineLength, crosshairLineLength);
                config?.BindWith(FPSosuRulesetSetting.CrosshairThickness, crosshairThickness);
                config?.BindWith(FPSosuRulesetSetting.CrosshairCenterDot, crosshairCenterDot);
                config?.BindWith(FPSosuRulesetSetting.CrosshairOpacity, crosshairOpacity);

                AutoSizeAxes = Axes.Both;
                AddInternal(pieces);
                build();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                crosshairOutline.BindValueChanged(outline => updateOutline(outline.NewValue), true);
                crosshairColour.BindValueChanged(_ => updateColour(), true);
                crosshairGap.BindValueChanged(_ => rebuild());
                crosshairLineLength.BindValueChanged(_ => rebuild());
                crosshairThickness.BindValueChanged(_ => rebuild());
                crosshairCenterDot.BindValueChanged(_ => rebuild());
                crosshairOpacity.BindValueChanged(opacity => pieces.Alpha = opacity.NewValue, true);
            }

            private void build()
            {
                float gap = crosshairGap.Value;
                float armLength = crosshairLineLength.Value;
                float thickness = crosshairThickness.Value;

                var arms = new[]
                {
                    (offset: new Vector2(0, -(gap + armLength / 2)), size: new Vector2(thickness, armLength)),
                    (offset: new Vector2(0, gap + armLength / 2), size: new Vector2(thickness, armLength)),
                    (offset: new Vector2(-(gap + armLength / 2), 0), size: new Vector2(armLength, thickness)),
                    (offset: new Vector2(gap + armLength / 2, 0), size: new Vector2(armLength, thickness)),
                };

                // Draw every outline first so all of them sit behind every filled piece.
                foreach (var arm in arms)
                    addPiece(arm.size + new Vector2(outline_width * 2), arm.offset, outlinePieces, Color4.Black);

                if (crosshairCenterDot.Value)
                    addPiece(new Vector2(thickness + outline_width * 2), Vector2.Zero, outlinePieces, Color4.Black);

                foreach (var arm in arms)
                    addPiece(arm.size, arm.offset, fillPieces, Color4.White);

                if (crosshairCenterDot.Value)
                    addPiece(new Vector2(thickness), Vector2.Zero, fillPieces, Color4.White);
            }

            private void rebuild()
            {
                pieces.Clear();
                fillPieces.Clear();
                outlinePieces.Clear();
                build();
                updateOutline(crosshairOutline.Value);
                updateColour();
            }

            private void addPiece(Vector2 size, Vector2 offset, List<Box> tracking, Color4 colour)
            {
                var piece = new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = size,
                    Position = offset,
                    Colour = colour,
                };

                pieces.Add(piece);
                tracking.Add(piece);
            }

            private void updateOutline(bool visible)
            {
                foreach (var piece in outlinePieces)
                    piece.Alpha = visible ? 1 : 0;
            }

            private void updateColour()
            {
                Color4 colour = toColour(crosshairColour.Value);

                foreach (var piece in fillPieces)
                    piece.Colour = colour;
            }

            private static Color4 toColour(FPSosuCrosshairColour colour)
            {
                switch (colour)
                {
                    case FPSosuCrosshairColour.Red:
                        return Color4.Red;

                    case FPSosuCrosshairColour.Green:
                        return Color4.Green;

                    case FPSosuCrosshairColour.Blue:
                        return Color4.Blue;

                    default:
                        return Color4.White;
                }
            }
        }
    }
}
