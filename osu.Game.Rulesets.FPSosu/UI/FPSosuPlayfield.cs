// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// An <see cref="OsuPlayfield"/> whose hit objects are placed by projecting the flat beatmap through a
    /// first-person camera.
    /// </summary>
    /// <remarks>
    /// Hit objects keep their real osu! playfield positions; only their drawable position and scale are rewritten
    /// each frame. Everything downstream of the drawable therefore keeps working untouched: hit detection follows
    /// the moved hit receptors, slider tracking and spinner rotation read the (centred) cursor against the moved
    /// slider body, and judgements, scoring and star rating are inherited wholesale from osu!.
    /// </remarks>
    public partial class FPSosuPlayfield : OsuPlayfield
    {
        /// <summary>
        /// The camera shared with <see cref="FPSosuInputManager"/>, which drives its rotation.
        /// </summary>
        [Resolved]
        private FPSosuCamera camera { get; set; } = null!;

        private readonly Bindable<FPSosuProjectionMode> projectionMode = new Bindable<FPSosuProjectionMode>(FPSosuProjectionMode.Dome);
        private readonly BindableFloat fieldOfView = new BindableFloat(90);
        private readonly BindableFloat playfieldSpan = new BindableFloat(100);
        private readonly BindableFloat crosshairOvershoot = new BindableFloat(45);

        /// <summary>
        /// The current projection. Rebuilt only when a setting changes, so the per-frame path stays allocation free.
        /// </summary>
        private FPSosuProjector projector;

        private FPSosuPlayfieldBoundary boundary = null!;

        private float lastBoundaryYaw = float.NaN;
        private float lastBoundaryPitch = float.NaN;
        private bool boundaryProjectionDirty = true;

        [BackgroundDependencyLoader]
        private void load(FPSosuConfigManager? config)
        {
            config?.BindWith(FPSosuRulesetSetting.ProjectionMode, projectionMode);
            config?.BindWith(FPSosuRulesetSetting.FieldOfView, fieldOfView);
            config?.BindWith(FPSosuRulesetSetting.PlayfieldSpan, playfieldSpan);
            config?.BindWith(FPSosuRulesetSetting.CrosshairOvershoot, crosshairOvershoot);

            projectionMode.BindValueChanged(_ => updateProjector());
            fieldOfView.BindValueChanged(_ => updateProjector());
            playfieldSpan.BindValueChanged(_ => updateProjector(), true);
            crosshairOvershoot.BindValueChanged(_ => updateAngularLimit());

            // The flat playfield border and follow points are drawn in unprojected playfield space and would not
            // line up with the objects, so they are replaced by a boundary that follows the projection.
            FollowPoints.Hide();
            AddInternal(boundary = new FPSosuPlayfieldBoundary());
        }

        private void updateProjector()
        {
            projector = new FPSosuProjector(projectionMode.Value, fieldOfView.Value, playfieldSpan.Value);

            // Keep every object reachable under the new projection, plus any configured overshoot past the edge.
            updateAngularLimit();

            boundaryProjectionDirty = true;
        }

        private void updateAngularLimit()
        {
            float overshoot = float.DegreesToRadians(crosshairOvershoot.Value);
            camera.AngularLimit = projector.AngularExtent + new Vector2(overshoot);
        }

        /// <summary>
        /// The projection currently in use.
        /// </summary>
        public FPSosuProjector Projector => projector;

        protected override void Update()
        {
            base.Update();

            float yaw = camera.Yaw;
            float pitch = camera.Pitch;

            if (boundaryProjectionDirty || yaw != lastBoundaryYaw || pitch != lastBoundaryPitch)
            {
                boundary.Refresh(projector, yaw, pitch);

                lastBoundaryYaw = yaw;
                lastBoundaryPitch = pitch;
                boundaryProjectionDirty = false;
            }

            foreach (var entry in HitObjectContainer.AliveEntries)
                project(entry.Value, yaw, pitch);
        }

        private void project(DrawableHitObject drawable, float yaw, float pitch)
        {
            // Only top-level objects appear in AliveEntries; nested pieces (slider heads, ticks, repeats) are
            // positioned by their parent slider and so are carried along by the parent's transform.
            if (drawable is not DrawableOsuHitObject osuObject)
                return;

            // Sliders are extended objects, so a single position and uniform scale would leave their far end in the
            // wrong place under perspective. They are projected by their head and tail instead.
            if (osuObject is DrawableSlider slider)
            {
                projectSlider(slider, yaw, pitch);
                return;
            }

            var world = projector.PlayfieldToWorld(osuObject.HitObject.StackedPosition);

            if (!projector.WorldToPlayfield(world, yaw, pitch, out Vector2 position, out float scale))
            {
                // Behind the camera. Hide it rather than draw a mirrored ghost in front of the player.
                osuObject.Alpha = 0;
                return;
            }

            osuObject.Alpha = 1;
            osuObject.Position = position;

            // Spinners are full-playfield objects centred on the screen and are spun rather than aimed at, so
            // rescaling them with perspective would only shrink the area the player has to work with.
            if (osuObject is not DrawableSpinner)
                osuObject.Scale = new Vector2(scale);
        }

        /// <summary>
        /// Projects a slider by anchoring its head and tail at their true projected positions, so the body follows
        /// the perspective instead of swimming as the camera turns.
        /// </summary>
        private void projectSlider(DrawableSlider slider, float yaw, float pitch)
        {
            var hitObject = slider.HitObject;

            Vector2 headPlayfield = hitObject.StackedPosition;
            Vector2 tailPlayfield = headPlayfield + hitObject.Path.PositionAt(1);

            if (!projector.WorldToPlayfield(projector.PlayfieldToWorld(headPlayfield), yaw, pitch, out Vector2 headScreen, out float headScale))
            {
                // Head is behind the camera; hide the whole slider rather than draw a mirrored ghost.
                slider.Alpha = 0;
                return;
            }

            slider.Alpha = 1;

            if (projector.WorldToPlayfield(projector.PlayfieldToWorld(tailPlayfield), yaw, pitch, out Vector2 tailScreen, out _))
            {
                Vector2 localSpan = tailPlayfield - headPlayfield;
                Vector2 screenSpan = tailScreen - headScreen;

                // The transform that carries the head to its projection and the tail to its projection. Guarded for
                // degenerate spans (a nearly zero-length slider, or one seen exactly end-on).
                if (localSpan.LengthSquared > 1e-6f && screenSpan.LengthSquared > 1e-6f)
                {
                    slider.Position = headScreen;
                    slider.Scale = new Vector2(screenSpan.Length / localSpan.Length);
                    slider.Rotation = float.RadiansToDegrees(
                        MathF.Atan2(screenSpan.Y, screenSpan.X) - MathF.Atan2(localSpan.Y, localSpan.X));
                    return;
                }
            }

            // Tail unavailable (behind the camera or degenerate). Fall back to point projection at the head.
            slider.Position = headScreen;
            slider.Scale = new Vector2(headScale);
            slider.Rotation = 0;
        }

        /// <summary>
        /// The cursor is locked to the centre of the screen, so the standard osu! cursor is replaced by a crosshair.
        /// </summary>
        protected override GameplayCursorContainer CreateCursor() => new FPSosuCrosshairContainer();
    }
}
