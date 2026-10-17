// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Input.Handlers.Mouse;
using osu.Framework.Input.StateChanges;
using osu.Framework.Platform;
using osu.Game.Rulesets.FPSosu.Configuration;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Osu;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.UI
{
    /// <summary>
    /// An <see cref="OsuInputManager"/> which turns mouse movement into camera rotation and keeps the cursor pinned
    /// to the centre of the playfield.
    /// </summary>
    /// <remarks>
    /// Buttons and key bindings are inherited unchanged, so clicking still hits whatever the crosshair covers. Only
    /// positional input is reinterpreted, and which device supplies it is chosen by
    /// <see cref="FPSosuRulesetSetting.InputSource"/>:
    /// <list type="bullet">
    /// <item><description>
    /// <b>Mouse</b> (the default) is relative: each frame the movement of the parent input manager's cursor since
    /// the previous frame is converted into camera rotation. Measuring frame-to-frame deltas (rather than
    /// recentering the cursor and reading its offset) applies every movement exactly once regardless of frame
    /// timing. The parent cursor is pulled back to the crosshair only if it drifts beyond a margin, so it never runs
    /// into the window edge and stops reporting movement.
    /// </description></item>
    /// <item><description>
    /// <b>Tablet</b> is absolute: the pen's position on the tablet points the camera, with the pen at the crosshair
    /// looking straight ahead. This is what a graphics tablet actually reports, and the reason the relative path
    /// cannot serve it: pulling the parent cursor back to the crosshair is immediately undone by the next absolute
    /// pen report, so the resulting deltas oscillate. Absolute input therefore never overrides the parent cursor.
    /// </description></item>
    /// </list>
    /// Both devices share one scale, so a given movement turns the camera just as far either way and the Sensitivity
    /// slider keeps its meaning when the source changes. This manager's own cursor is always pinned to the crosshair,
    /// which is where hits are registered.
    /// While paused (or during a replay) camera control is inactive, leaving the cursor free for menus.
    /// </remarks>
    public partial class FPSosuInputManager : OsuInputManager
    {

        [Resolved]
        private FPSosuCamera camera { get; set; } = null!;

        private readonly BindableFloat sensitivity = new BindableFloat(1);
        private readonly BindableBool invertPitch = new BindableBool();
        private readonly Bindable<FPSosuInputSource> inputSource = new Bindable<FPSosuInputSource>(FPSosuInputSource.Mouse);

        [Resolved]
        private GameHost host { get; set; } = null!;

        /// <summary>
        /// osu!'s cursor sensitivity and high-precision toggle, read live from the active mouse handler so their
        /// effect can be normalised out of the camera rotation. Null when the host has no mouse handler (tests).
        /// </summary>
        private BindableNumber<double>? cursorSensitivity;
        private Bindable<bool>? highPrecisionMouse;

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
            config?.BindWith(FPSosuRulesetSetting.InputSource, inputSource);

            // Changing source mid-play would otherwise turn the stale cursor position into one enormous delta. The
            // position is forgotten instead, so the new source starts from wherever it is now.
            inputSource.BindValueChanged(_ => lastParentPosition = null);

            var mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();

            if (mouseHandler != null)
            {
                // Read the handler's own bindables rather than bound copies: they live as long as the host, so a
                // copy would only add a subscription which this manager is never in a position to dispose.
                cursorSensitivity = mouseHandler.Sensitivity;
                highPrecisionMouse = mouseHandler.UseRelativeMode;
            }
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

            Vector2 local = screenDeltaToLocal(screenSpaceDelta);
            float scale = radiansPerLocalUnit();

            // Screen Y grows downwards, so moving the mouse up (negative Y) must raise the pitch.
            float pitchDelta = -local.Y * scale;

            if (invertPitch.Value)
                pitchDelta = -pitchDelta;

            camera.Rotate(local.X * scale, pitchDelta);
        }

        /// <summary>
        /// Points the camera from the absolute position of a tablet pen.
        /// </summary>
        /// <param name="screenSpacePosition">The pen's position, in screen space.</param>
        /// <param name="crosshair">The screen-space position the crosshair (and therefore the camera's rest
        /// orientation) is held at.</param>
        private void applyAbsoluteLook(Vector2 screenSpacePosition, Vector2 crosshair)
        {
            // Sharing the relative path's scale is what makes a pen movement turn the camera exactly as far as the
            // same mouse movement, so Sensitivity keeps its meaning across sources. Screen Y grows downwards, so a
            // pen above the crosshair must raise the pitch.
            Vector2 local = screenDeltaToLocal(screenSpacePosition - crosshair);
            float scale = radiansPerLocalUnit();

            float pitch = -local.Y * scale;

            if (invertPitch.Value)
                pitch = -pitch;

            camera.LookAt(local.X * scale, pitch);
        }

        /// <summary>
        /// Expresses a screen-space movement in this manager's local units.
        /// </summary>
        /// <remarks>
        /// One mouse count moves one local unit, so the turn rate is a physical constant independent of the window
        /// size - the property the sensitivity converter relies on. osu!'s own cursor sensitivity is divided back
        /// out of the scale itself so it never stacks with the FPSosu sensitivity.
        /// </remarks>
        private Vector2 screenDeltaToLocal(Vector2 screenSpaceDelta)
            => ToLocalSpace(ToScreenSpace(Vector2.Zero) + screenSpaceDelta);

        /// <summary>
        /// Radians of camera rotation per local unit of movement, shared by both input sources.
        /// </summary>
        private float radiansPerLocalUnit()
            => FPSosuSensitivityConverter.EffectiveRadiansPerPixel(cursorSensitivity?.Value ?? 1, highPrecisionMouse?.Value ?? false) * sensitivity.Value;

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

            // The screen-space point the crosshair is held at, which is also the rest orientation of the camera and
            // therefore the origin absolute input is measured from.
            Vector2 target = LockPosition?.Invoke() ?? ToScreenSpace(DrawSize / 2);

            if (parent.CurrentState.Mouse.IsPositionValid)
            {
                if (inputSource.Value == FPSosuInputSource.Tablet)
                {
                    // The pen's position is absolute, so it points the camera directly. The parent cursor is left
                    // alone: a tablet re-asserts its absolute position every report, so overriding it here would be
                    // undone immediately and the mismatch would show up as the next frame's delta.
                    applyAbsoluteLook(parent.CurrentState.Mouse.Position, target);
                }
                else
                {
                    Vector2 position = parent.CurrentState.Mouse.Position;

                    // Rotate by however far the cursor moved since the last frame, rather than recentering it, so the
                    // movement is applied exactly once regardless of frame timing.
                    if (lastParentPosition is Vector2 last)
                        rotateBy(position - last);

                    lastParentPosition = position;
                }
            }

            // Hold this manager's own cursor at the crosshair, where hits are registered.
            if (CurrentState.Mouse.Position != target)
                new MousePositionAbsoluteInput { Position = target }.Apply(CurrentState, this);

            // Keep the parent's cursor from running into the edge of the window, where it would stop reporting
            // movement. Pull it back to the crosshair only once it drifts beyond a margin, so the frame-to-frame
            // deltas above are left intact during normal play. This must not run for a tablet, whose absolute
            // position would simply be re-reported.
            if (inputSource.Value == FPSosuInputSource.Mouse && parent.CurrentState.Mouse.IsPositionValid)
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
