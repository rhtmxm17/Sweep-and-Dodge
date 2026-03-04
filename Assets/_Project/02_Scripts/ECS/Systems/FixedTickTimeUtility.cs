using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    internal static class FixedTickTimeUtility
    {
        public static bool ShouldRunLogicStep(
            bool hasRuntime,
            in FixedTickStepRuntimeComponent runtime,
            float frameDeltaTime)
        {
            if (!hasRuntime || runtime.UsingFixedTick == 0)
                return math.max(0f, frameDeltaTime) > 0f;

            return runtime.HasStep != 0 && math.max(0f, runtime.LogicDeltaTime) > 0f;
        }

        public static bool TryResolveLogicDeltaTime(
            bool hasRuntime,
            in FixedTickStepRuntimeComponent runtime,
            float frameDeltaTime,
            out float deltaTime)
        {
            float fallbackDelta = math.max(0f, frameDeltaTime);
            deltaTime = (!hasRuntime || runtime.UsingFixedTick == 0)
                ? fallbackDelta
                : math.max(0f, runtime.LogicDeltaTime);
            return ShouldRunLogicStep(hasRuntime, in runtime, frameDeltaTime) && deltaTime > 0f;
        }
    }
}
