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
    /// Buttons and key bindings are inherited unchanged, so clicking hits whatever the crosshair covers. Only
    /// positional input is reinterpreted; <see cref="FPSosuRulesetSetting.InputSource"/> picks the device:
    /// <list type="bullet">
    /// <item><description>
    /// <b>Mouse</b> (default) is relative: each <see cref="MouseMoveEvent"/> rotates the camera by the movement it
    /// reports, so a platform which cannot warp the cursor never feeds a stale position back in.
    /// </description></item>
    /// <item><description>
    /// <b>Tablet (mouse sensitivity)</b> is absolute and shares the mouse's scale, so the pen points the camera and
    /// Sensitivity means the same thing either way.
    /// </description></item>
    /// <item><description>
    /// <b>Standard tablet style</b> maps the pen onto the beatmap, exactly as a tablet does in standard osu!. Its
    /// range follows the playfield span and Sensitivity does not apply.
    /// </description></item>
    /// </list>
    /// The manager's own cursor stays pinned to the crosshair, where hits are registered. While paused or during a
    /// replay, camera control is inactive so the cursor stays free for menus.
    /// </remarks>
    public partial class FPSosuInputManager : OsuInputManager
    {
        [Resolved]
        private FPSosuCamera camera { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private readonly BindableFloat sensitivity = new(1);
        private readonly BindableBool invertPitch = new();
        private readonly Bindable<FPSosuInputSource> inputSource = new(FPSosuInputSource.Mouse);

        /// <summary>
        /// osu!'s cursor sensitivity, read live from the active mouse handler so its effect can be normalised out of
        /// relative mouse movement. Null when the host has no mouse handler (tests).
        /// </summary>
        private BindableNumber<double>? cursorSensitivity;

        /// <summary>
        /// osu!'s relative (raw) mouse toggle, read live from the active mouse handler. Relative movement is
        /// pre-multiplied by <see cref="cursorSensitivity"/>, so it has to be divided back out.
        /// </summary>
        private Bindable<bool>? relativeMode;

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
        /// Converts a screen-space position into the camera angles which bring that point of the beatmap under the
        /// crosshair. Supplied by the drawable ruleset once the playfield exists, and used by
        /// <see cref="FPSosuInputSource.TabletStandard"/>.
        /// </summary>
        public Func<Vector2, Vector2>? ScreenToPlayfieldAngles { get; set; }

        /// <summary>
        /// The parent input manager, whose cursor position is the one the OS actually tracks.
        /// </summary>
        private InputManager? parentInputManager;

        /// <summary>
        /// The parent cursor position reported by the last mouse move event, in screen space. Null until the first
        /// active update, so movement made while paused is not applied on resume.
        /// </summary>
        private Vector2? lastEventPosition;

        /// <summary>
        /// The last move event applied. The framework periodically re-propagates the most recent event to
        /// high-frequency listeners; it must not be applied twice.
        /// </summary>
        private MouseMoveEvent? lastMoveEvent;

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

            // Changing source mid-play would otherwise turn the stale cursor position into one enormous delta.
            inputSource.BindValueChanged(_ => resetTracking());

            var mouseHandler = host.AvailableInputHandlers.OfType<MouseHandler>().FirstOrDefault();

            if (mouseHandler is not null)
            {
                cursorSensitivity = mouseHandler.Sensitivity;
                relativeMode = mouseHandler.UseRelativeMode;
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            parentInputManager = GetContainingInputManager();
        }

        protected override bool Handle(UIEvent e)
        {
            if (e is not MouseMoveEvent move || !AllowCameraControl || !UseParentInput)
                return base.Handle(e);

            if (ReferenceEquals(move, lastMoveEvent))
                return true;

            lastMoveEvent = move;

            if (inputSource.Value == FPSosuInputSource.Mouse)
                handleMouseMove(move);

            // Movement is consumed here; the cursor is pinned to the crosshair in Update.
            return true;
        }

        /// <summary>
        /// Rotates the camera by the movement reported by a mouse move event.
        /// </summary>
        private void handleMouseMove(MouseMoveEvent move)
        {
            Vector2 position = move.ScreenSpaceMousePosition;

            if (lastEventPosition is not Vector2 last || position == last)
                return;

            rotateBy(position - last);
            lastEventPosition = position;
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

            camera.Rotate(local.X * scale, pitchFor(-local.Y * scale));
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

            camera.LookAt(local.X * scale, pitchFor(-local.Y * scale));
        }

        /// <summary>
        /// Points the camera at the point of the beatmap under a tablet pen.
        /// </summary>
        /// <param name="screenSpacePosition">The pen's position, in screen space.</param>
        /// <remarks>
        /// Unlike <see cref="applyAbsoluteLook"/> this is geometric rather than a scaled movement: the pen points at
        /// a position on the beatmap and the camera turns so that position is under the crosshair, exactly as a
        /// tablet behaves in standard osu!. Sensitivity deliberately does not apply, because the playfield span
        /// already defines how far the pen must travel to cross the beatmap; scaling the mapping would break the
        /// pen-to-beatmap correspondence.
        /// </remarks>
        private void applyPlayfieldScaledLook(Vector2 screenSpacePosition)
        {
            Vector2 angles = ScreenToPlayfieldAngles?.Invoke(screenSpacePosition) ?? Vector2.Zero;

            camera.LookAt(angles.X, pitchFor(angles.Y));
        }

        /// <summary>
        /// Applies the inverted-look setting to a pitch delta.
        /// </summary>
        private float pitchFor(float pitch) => invertPitch.Value ? -pitch : pitch;

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
            => FPSosuSensitivityConverter.EffectiveRadiansPerPixel(cursorSensitivity?.Value ?? 1, relativeMode?.Value ?? false) * sensitivity.Value;

        protected override void Update()
        {
            base.Update();

            var parent = parentInputManager;

            // UseParentInput is only true during active gameplay: the drawable ruleset clears it while paused and a
            // replay handler clears it during replays. Bailing out here leaves the cursor free for the pause menu, and
            // forgetting the last position means the movement made over the menu is not applied on resume.
            if (parent is null || !AllowCameraControl || !UseParentInput)
            {
                resetTracking();
                return;
            }

            // Start measuring from wherever the cursor is when control becomes active, so movement made while
            // paused (or by the player loader centring the cursor) is not applied as a jump.
            if (inputSource.Value == FPSosuInputSource.Mouse && lastEventPosition is null && parent.CurrentState.Mouse.IsPositionValid)
                lastEventPosition = parent.CurrentState.Mouse.Position;

            // The screen-space point the crosshair is held at, which is also the rest orientation of the camera and
            // therefore the origin absolute input is measured from.
            Vector2 target = LockPosition?.Invoke() ?? ToScreenSpace(DrawSize / 2);

            if (parent.CurrentState.Mouse.IsPositionValid)
            {
                Vector2 position = parent.CurrentState.Mouse.Position;

                // Mouse movement is applied by handleMouseMove from the event delta, not by polling the cursor.
                switch (inputSource.Value)
                {
                    case FPSosuInputSource.Tablet:
                        // The pen's position is absolute, so it points the camera directly. The parent cursor is left
                        // alone: a tablet re-asserts its absolute position every report, so overriding it here would
                        // be undone immediately and the mismatch would show up as the next frame's delta.
                        applyAbsoluteLook(position, target);
                        break;

                    case FPSosuInputSource.TabletStandard:
                        // The pen points at a beatmap position, as a tablet does in standard osu!, and the camera
                        // turns so that position is under the crosshair. The parent cursor is left alone for the
                        // same reason as above.
                        applyPlayfieldScaledLook(position);
                        break;
                }
            }

            // Hold this manager's own cursor at the crosshair, where hits are registered.
            if (CurrentState.Mouse.Position != target)
                new MousePositionAbsoluteInput { Position = target }.Apply(CurrentState, this);
        }

        private void resetTracking()
        {
            lastEventPosition = null;
            lastMoveEvent = null;
        }
    }
}
