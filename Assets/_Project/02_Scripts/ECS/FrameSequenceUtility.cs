using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public static class FrameSequenceUtility
    {
        public static uint EstimateFrame(double elapsedTime, float deltaTime)
        {
            float safeDt = math.max(1e-6f, deltaTime);
            return (uint)math.floor(elapsedTime / safeDt);
        }
    }
}
