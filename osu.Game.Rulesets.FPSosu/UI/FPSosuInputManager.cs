// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// An <see cref="OsuInputManager"/> which turns mouse movement into camera rotation and keeps the cursor pinned
    /// to the centre of the playfield.
    /// </summary>
    /// <remarks>
    /// Buttons and key bindings are inherited unchanged, so clicking still hits whatever the crosshair covers. Only
    /// positional input is reinterpreted:
    /// <list type="number">
    /// <item><description>
    /// Each frame, the movement of the parent input manager's cursor since the previous frame is converted into
    /// camera rotation. Measuring frame-to-frame deltas (rather than recentering the cursor and reading its offset)
    /// applies every movement exactly once regardless of frame timing.
    /// </description></item>
    /// <item><description>
    /// This manager's own cursor is pinned to the crosshair, which is where hits are registered. The parent cursor is
    /// pulled back to the crosshair only if it drifts beyond a margin, so it never runs into the window edge and
    /// stops reporting movement.
    /// </description></item>
    /// </list>
    /// While paused (or during a replay) camera control is inactive, leaving the cursor free for menus.
    /// </remarks>
    public partial class FPSosuInputManager : OsuInputManager
    {
        /// <summary>
        /// Radians of camera rotation per playfield unit of mouse movement at a sensitivity of 1.
        /// </summary>
        /// <remarks>
        /// Sized so that, by default, dragging across the playfield turns the camera by roughly the angle the
        /// playfield spans, which keeps the feel close to standard osu! before the player tunes sensitivity.
        /// </remarks>
        private const float radians_per_unit = 0.004f;

        [Resolved]
        private FPSosuCamera camera { get; set; } = null!;

        private readonly BindableFloat sensitivity = new BindableFloat(1);
        private readonly BindableBool invertPitch = new BindableBool();

        /// <summary>
        /// Whether mouse movement should rotate the camera and pin the cursor to the crosshair.
        /// </summary>
        /// <remarks>
        /// Only the input manager hosting the playfield enables this. The one hosting the resume overlay leaves it
        /// off so overlay cursors stay free, and it is also cleared during replay playback, where the camera is aimed
        /// from replay frames instead.
        /// </remarks>
        public bool AllowCameraControl { get; set; }

        /// <summary>
        /// Provides the screen-space point the cursor is held at, which is the centre of the playfield where the
        /// crosshair is drawn. Supplied by the drawable ruleset once the playfield exists.
        /// </summary>
        public Func<Vector2>? LockPosition { get; set; }

        /// <summary>
        /// The parent input manager, whose cursor position is the one the OS actually tracks.
        /// </summary>
        private InputManager? parentInputManager;

        /// <summary>
        /// The parent input manager's cursor position on the previous frame. Used to turn the frame-to-frame mouse
        /// movement into camera rotation. Cleared whenever camera control is inactive so the movement made over the
        /// pause menu is not applied as a jump on resume.
        /// </summary>
        private Vector2? lastParentPosition;

        public FPSosuInputManager(RulesetInfo ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load(FPSosuConfigManager? config)
        {
            config?.BindWith(FPSosuRulesetSetting.Sensitivity, sensitivity);
            config?.BindWith(FPSosuRulesetSetting.InvertPitch, invertPitch);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            parentInputManager = GetContainingInputManager();
        }

        protected override bool Handle(UIEvent e)
        {
            // Movement is consumed in Update() by comparing against the parent's cursor position, because the
            // high-frequency mouse move events the framework synthesises each frame carry no usable delta
            // (their "last position" is set equal to their current position). Only swallow movement during active
            // gameplay, so the cursor stays free to navigate the pause menu.
            if (e is MouseMoveEvent && AllowCameraControl && UseParentInput)
                return true;

            return base.Handle(e);
        }

        /// <summary>
        /// Converts a screen-space mouse movement into camera rotation.
        /// </summary>
        private void rotateBy(Vector2 screenSpaceDelta)
        {
            if (screenSpaceDelta == Vector2.Zero)
                return;

            // Express the movement in playfield units so sensitivity is independent of window size and resolution.
            // Only the vector is being converted, so the origin is subtracted back out.
            Vector2 local = ToLocalSpace(ToScreenSpace(Vector2.Zero) + screenSpaceDelta);
            Vector2 delta = local * playfieldUnitsPerLocalUnit;

            float scale = radians_per_unit * sensitivity.Value;

            // Screen Y grows downwards, so moving the mouse up (negative Y) must raise the pitch.
            float pitchDelta = -delta.Y * scale;

            if (invertPitch.Value)
                pitchDelta = -pitchDelta;

            camera.Rotate(delta.X * scale, pitchDelta);
        }

        private float playfieldUnitsPerLocalUnit => DrawSize.X > 0 ? OsuPlayfield.BASE_SIZE.X / DrawSize.X : 1;

        protected override void Update()
        {
            base.Update();

            var parent = parentInputManager;

            // UseParentInput is only true during active gameplay: the drawable ruleset clears it while paused and a
            // replay handler clears it during replays. Bailing out here leaves the cursor free for the pause menu, and
            // forgetting the last position means the movement made over the menu is not applied on resume.
            if (parent == null || !AllowCameraControl || !UseParentInput)
            {
                lastParentPosition = null;
                return;
            }

            if (parent.CurrentState.Mouse.IsPositionValid)
            {
                Vector2 position = parent.CurrentState.Mouse.Position;

                // Rotate by however far the cursor moved since the last frame, rather than recentering it, so the
                // movement is applied exactly once regardless of frame timing.
                if (lastParentPosition is Vector2 last)
                    rotateBy(position - last);

                lastParentPosition = position;
            }

            // Hold this manager's own cursor at the crosshair, where hits are registered.
            Vector2 target = LockPosition?.Invoke() ?? ToScreenSpace(DrawSize / 2);

            if (CurrentState.Mouse.Position != target)
                new MousePositionAbsoluteInput { Position = target }.Apply(CurrentState, this);

            // Keep the parent's cursor from running into the edge of the window, where it would stop reporting
            // movement. Pull it back to the crosshair only once it drifts beyond a margin, so the frame-to-frame
            // deltas above are left intact during normal play.
            if (parent.CurrentState.Mouse.IsPositionValid)
            {
                float margin = Math.Min(parent.DrawSize.X, parent.DrawSize.Y) * 0.4f;

                if (Vector2.Distance(parent.CurrentState.Mouse.Position, target) > margin)
                {
                    new MousePositionAbsoluteInput { Position = target }.Apply(parent.CurrentState, parent);
                    lastParentPosition = target;
                }
            }
        }
    }
}
