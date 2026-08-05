// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.FPSosu.Objects;
using osu.Game.Rulesets.Replays;

namespace osu.Game.Rulesets.FPSosu.Replays
{
    public class FPSosuAutoGenerator : AutoGenerator<FPSosuReplayFrame>
    {
        public new Beatmap<FPSosuHitObject> Beatmap => (Beatmap<FPSosuHitObject>)base.Beatmap;

        public FPSosuAutoGenerator(IBeatmap beatmap)
            : base(beatmap)
        {
        }

        protected override void GenerateFrames()
        {
            Frames.Add(new FPSosuReplayFrame());

            foreach (FPSosuHitObject hitObject in Beatmap.HitObjects)
            {
                Frames.Add(new FPSosuReplayFrame
                {
                    Time = hitObject.StartTime,
                    Position = hitObject.Position,
                    // todo: add required inputs and extra frames.
                });
            }
        }
    }
}
