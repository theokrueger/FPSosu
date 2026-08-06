// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Input.StateChanges;
using osu.Framework.Utils;
using osu.Game.Replays;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Replays
{
    /// <summary>
    /// Plays back a replay by aiming the camera, rather than by moving a cursor.
    /// </summary>
    /// <remarks>
    /// Replay frames store the aim point in osu! playfield coordinates, the same as standard osu!. Since the
    /// crosshair is locked to the centre of the screen, reproducing a frame means rotating the camera so that the
    /// recorded point sits under the crosshair. The cursor itself is still reported at the centre so that hit
    /// detection, slider tracking and spinner rotation behave identically to live play.
    /// </remarks>
    public class FPSosuFramedReplayInputHandler : FramedReplayInputHandler<OsuReplayFrame>
    {
        private readonly FPSosuCamera camera;
        private readonly Func<FPSosuProjector> projector;
        private readonly Func<Vector2> crosshairPosition;

        /// <param name="replay">The replay to play back.</param>
        /// <param name="camera">The camera to aim.</param>
        /// <param name="projector">Provides the projection currently in use, which may change as settings change.</param>
        /// <param name="crosshairPosition">Provides the screen-space position the cursor should be reported at.</param>
        public FPSosuFramedReplayInputHandler(Replay replay, FPSosuCamera camera, Func<FPSosuProjector> projector, Func<Vector2> crosshairPosition)
            : base(replay)
        {
            this.camera = camera;
            this.projector = projector;
            this.crosshairPosition = crosshairPosition;
        }

        protected override bool IsImportant(OsuReplayFrame frame) => frame.Actions.Any();

        protected override void CollectReplayInputs(List<IInput> inputs)
        {
            var position = Interpolation.ValueAt(CurrentTime, StartFrame.Position, EndFrame.Position, StartFrame.Time, EndFrame.Time);

            // Aim the camera at the recorded playfield position.
            camera.LookAt(projector().PlayfieldToCameraAngles(position));

            // The cursor stays centred, exactly as it does during live play.
            inputs.Add(new MousePositionAbsoluteInput { Position = crosshairPosition() });
            inputs.Add(new ReplayState<OsuAction> { PressedActions = CurrentFrame?.Actions ?? new List<OsuAction>() });
        }
    }
}
