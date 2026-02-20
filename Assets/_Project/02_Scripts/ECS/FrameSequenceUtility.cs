using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public static class FrameSequenceUtility
    {
        public static uint GetCurrentFrame(in BulletFrameCounterComponent counter)
        {
            return counter.Value;
        }
    }
}
