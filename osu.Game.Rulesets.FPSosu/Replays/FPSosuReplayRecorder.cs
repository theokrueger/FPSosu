// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.FPSosu.Projection;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.FPSosu.Replays
{
    /// <summary>
    /// Records FPSosu gameplay into a standard osu! replay.
    /// </summary>
    /// <remarks>
    /// The raw cursor is pinned to the centre of the screen and carries no aiming information, so the position
    /// handed to us is ignored. What gets recorded instead is the playfield point the camera is aiming at, which
    /// makes the resulting replay directly comparable with a standard osu! one.
    /// </remarks>
    public partial class FPSosuReplayRecorder : ReplayRecorder<OsuAction>
    {
        private readonly FPSosuCamera camera;
        private readonly Func<FPSosuProjector> projector;

        public FPSosuReplayRecorder(Score score, FPSosuCamera camera, Func<FPSosuProjector> projector)
            : base(score)
        {
            this.camera = camera;
            this.projector = projector;
        }

        protected override ReplayFrame HandleFrame(Vector2 mousePosition, List<OsuAction> actions, ReplayFrame previousFrame)
            => new OsuReplayFrame(Time.Current, projector().CameraAnglesToPlayfield(camera.Yaw, camera.Pitch), actions.ToArray());
    }
}
