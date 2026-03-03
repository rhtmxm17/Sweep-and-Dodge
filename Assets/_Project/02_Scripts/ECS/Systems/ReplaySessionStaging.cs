using System.Collections.Generic;
using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public static class ReplaySessionStaging
    {
        private static readonly List<ReplayInputFrameBufferElement> StagedFrames = new List<ReplayInputFrameBufferElement>(1024);
        private static uint _stagedRunSeed = 1u;
        private static bool _hasPendingPlayback;

        public static bool IsPlaybackStartupPending => _hasPendingPlayback;

        public static void StagePlayback(IReadOnlyList<ReplayInputFrameBufferElement> frames, uint runSeed)
        {
            StagedFrames.Clear();
            if (frames != null)
            {
                for (int i = 0; i < frames.Count; i++)
                    StagedFrames.Add(frames[i]);
            }

            _stagedRunSeed = runSeed > 0u ? runSeed : 1u;
            _hasPendingPlayback = true;
        }

        public static bool TryConsumePlayback(DynamicBuffer<ReplayInputFrameBufferElement> targetBuffer, out uint runSeed)
        {
            runSeed = 1u;
            if (!_hasPendingPlayback)
                return false;

            runSeed = _stagedRunSeed;
            if (targetBuffer.IsCreated)
            {
                targetBuffer.Clear();
                for (int i = 0; i < StagedFrames.Count; i++)
                    targetBuffer.Add(StagedFrames[i]);
            }

            _hasPendingPlayback = false;
            StagedFrames.Clear();
            return true;
        }
    }
}
