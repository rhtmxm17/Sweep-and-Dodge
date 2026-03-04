using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    internal static class FixedTickTimeUtility
    {
        public static bool ShouldRunLogicStep(in FixedTickStepRuntimeComponent runtime)
        {
            if (runtime.UsingFixedTick == 0)
                return math.max(0f, runtime.FrameDeltaTime) > 0f;

            return runtime.HasStep != 0 && math.max(0f, runtime.LogicDeltaTime) > 0f;
        }

        public static bool TryResolveLogicDeltaTime(in FixedTickStepRuntimeComponent runtime, out float deltaTime)
        {
            deltaTime = runtime.UsingFixedTick == 0
                ? math.max(0f, runtime.FrameDeltaTime)
                : math.max(0f, runtime.LogicDeltaTime);
            return ShouldRunLogicStep(in runtime) && deltaTime > 0f;
        }
    }
}
