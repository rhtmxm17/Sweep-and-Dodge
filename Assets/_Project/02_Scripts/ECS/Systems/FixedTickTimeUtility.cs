using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    internal static class FixedTickTimeUtility
    {
        public static bool TryResolveLogicDeltaTime(
            bool hasRuntime,
            in FixedTickStepRuntimeComponent runtime,
            float frameDeltaTime,
            out float deltaTime)
        {
            float fallbackDelta = math.max(0f, frameDeltaTime);
            if (!hasRuntime || runtime.UsingFixedTick == 0)
            {
                deltaTime = fallbackDelta;
                return deltaTime > 0f;
            }

            deltaTime = math.max(0f, runtime.LogicDeltaTime);
            return runtime.HasStep != 0 && deltaTime > 0f;
        }
    }
}
