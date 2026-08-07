// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
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

        /// <summary>
        /// The osu! judgement popup layer, inherited from <see cref="OsuPlayfield"/>. Popups are placed there once at
        /// judgement time, so they are re-projected every frame to stay pinned to their note's board position.
        /// </summary>
        public JudgementContainer<DrawableOsuJudgement>? JudgementLayer { get; private set; }

        private float lastBoundaryYaw = float.NaN;
        private float lastBoundaryPitch = float.NaN;
        private bool boundaryProjectionDirty = true;

        /// <summary>
        /// The camera orientation on the previous frame, used to turn the frame's camera movement into spinner
        /// rotation.
        /// </summary>
        private float lastSpinnerYaw;
        private float lastSpinnerPitch;

        /// <summary>
        /// Degrees of spinner rotation earned per radian of camera turn. With the default overshoot the camera can
        /// sweep roughly 190 degrees edge to edge, and this value makes one such sweep spin the disc about once, so
        /// sweeping the camera back and forth completes spinners at a natural pace.
        /// </summary>
        private const float spinner_rotation_per_camera_radian = 100f;

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

        protected override void LoadComplete()
        {
            base.LoadComplete();

            JudgementLayer = InternalChildren.OfType<JudgementContainer<DrawableOsuJudgement>>().FirstOrDefault();
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

            updateJudgements(yaw, pitch);
            updateSpinners(yaw, pitch);
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

            // Spinners are spun in place at the centre of the view rather than aimed at, so keep them centred while
            // the world turns around them. Their rotation is driven by camera movement in updateSpinners.
            if (osuObject is DrawableSpinner)
            {
                osuObject.Alpha = 1;
                osuObject.Position = FPSosuProjector.PLAYFIELD_CENTRE;
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
            osuObject.Scale = new Vector2(scale);
        }

        /// <summary>
        /// Re-projects the judgement popups so each one stays where its note was on the board, moving and scaling
        /// with the world as the camera turns, exactly like the notes do.
        /// </summary>
        private void updateJudgements(float yaw, float pitch)
        {
            if (JudgementLayer == null)
                return;

            foreach (var judgement in JudgementLayer)
            {
                var hitObject = judgement.JudgedHitObject;

                if (hitObject == null)
                    continue;

                // Sliders judge at their tail, matching where osu! places the popup; everything else at its position.
                Vector2 playfieldPosition = (hitObject as OsuHitObject)?.StackedEndPosition
                                            ?? (hitObject as IHasPosition)?.Position
                                            ?? FPSosuProjector.PLAYFIELD_CENTRE;

                var world = projector.PlayfieldToWorld(playfieldPosition);

                if (!projector.WorldToPlayfield(world, yaw, pitch, out Vector2 position, out float scale))
                {
                    judgement.Alpha = 0;
                    continue;
                }

                judgement.Alpha = 1;
                judgement.Position = position;
                judgement.Scale = new Vector2(scale * ((hitObject as OsuHitObject)?.Scale ?? 1));
            }
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
        /// Turns this frame's camera movement into rotation for any live spinner.
        /// </summary>
        private void updateSpinners(float yaw, float pitch)
        {
            float yawDelta = yaw - lastSpinnerYaw;
            float pitchDelta = pitch - lastSpinnerPitch;
            lastSpinnerYaw = yaw;
            lastSpinnerPitch = pitch;

            // Spinning is driven by how much the camera turned this frame, in any direction.
            float rotationDelta = MathF.Sqrt(yawDelta * yawDelta + pitchDelta * pitchDelta) * spinner_rotation_per_camera_radian;

            if (rotationDelta <= 0)
                return;

            foreach (var entry in HitObjectContainer.AliveEntries)
            {
                if (entry.Value is DrawableSpinner spinner)
                    spinSpinner(spinner, rotationDelta*3);
            }
        }

        /// <summary>
        /// Spins a spinner from camera movement.
        /// </summary>
        /// <remarks>
        /// The cursor is pinned to the crosshair, so the standard "move the cursor in circles" action is not
        /// possible. Rotating the camera takes its place. This mirrors how the Spun Out mod drives spinners, except
        /// the rotation comes from camera movement instead of a fixed speed, and no button needs to be held.
        /// </remarks>
        private void spinSpinner(DrawableSpinner spinner, float rotationDelta)
        {
            // Stop the spinner listening for (pinned) cursor input and take over its tracking.
            spinner.HandleUserInput = false;

            var tracker = spinner.RotationTracker;
            tracker.Tracking = tracker.IsSpinnableTime && !spinner.Result.HasResult;

            if (tracker.Tracking)
                tracker.AddRotation(rotationDelta);
        }

        /// <summary>
        /// The cursor is locked to the centre of the screen, so the standard osu! cursor is replaced by a crosshair.
        /// </summary>
        protected override GameplayCursorContainer CreateCursor() => new FPSosuCrosshairContainer();
    }
}
