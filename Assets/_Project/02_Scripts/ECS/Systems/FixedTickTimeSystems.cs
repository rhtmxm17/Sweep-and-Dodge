using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(FixedTickRootGroup), OrderFirst = true)]
    public partial struct FixedTickTimeResolveSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;

            var fixedTickQuery = SystemAPI.QueryBuilder().WithAll<FixedTickTimeComponent>().Build();
            if (fixedTickQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(typeof(FixedTickTimeComponent));
                em.SetComponentData(e, new FixedTickTimeComponent
                {
                    EnableFixedTick = 0,
                    PauseRequested = 0,
                    StepRequested = 0,
                    Reserved = 0,
                    MaxSubSteps = 4,
                    FixedDeltaTime = 1f / 60f,
                    Accumulator = 0f,
                    Tick = 0u,
                });
            }

            var runtimeQuery = SystemAPI.QueryBuilder().WithAll<FixedTickStepRuntimeComponent>().Build();
            if (runtimeQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(typeof(FixedTickStepRuntimeComponent));
                em.SetComponentData(e, new FixedTickStepRuntimeComponent
                {
                    FrameDeltaTime = 0f,
                    LogicDeltaTime = 0f,
                    LogicStepCount = 0,
                    HasStep = 0,
                    UsingFixedTick = 0,
                });
            }

            state.RequireForUpdate<FixedTickTimeComponent>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float frameDeltaTime = math.max(0f, SystemAPI.Time.DeltaTime);
            var fixedTickRW = SystemAPI.GetSingletonRW<FixedTickTimeComponent>();
            var fixedTick = fixedTickRW.ValueRO;

            bool usingFixedTick = fixedTick.EnableFixedTick != 0;
            bool hasStep;
            float logicDeltaTime;
            int logicStepCount;

            if (!usingFixedTick)
            {
                fixedTick.Accumulator = 0f;
                hasStep = frameDeltaTime > 0f;
                logicDeltaTime = hasStep ? frameDeltaTime : 0f;
                logicStepCount = hasStep ? 1 : 0;
            }
            else
            {
                float fixedDelta = math.max(0.000001f, fixedTick.FixedDeltaTime);
                int maxSubSteps = math.max(1, fixedTick.MaxSubSteps);
                float maxAccumulatedTime = fixedDelta * maxSubSteps;
                float accumulatedTime = math.max(0f, fixedTick.Accumulator + frameDeltaTime);
                if (accumulatedTime > maxAccumulatedTime)
                    accumulatedTime = maxAccumulatedTime;

                bool paused = fixedTick.PauseRequested != 0;
                if (paused)
                {
                    hasStep = fixedTick.StepRequested != 0;
                    if (hasStep)
                    {
                        if (accumulatedTime < fixedDelta)
                            accumulatedTime = fixedDelta;
                        fixedTick.StepRequested = 0;
                    }
                }
                else
                {
                    hasStep = accumulatedTime >= fixedDelta;
                }

                if (hasStep)
                    accumulatedTime = math.max(0f, accumulatedTime - fixedDelta);

                fixedTick.Accumulator = accumulatedTime;
                logicDeltaTime = hasStep ? fixedDelta : 0f;
                logicStepCount = hasStep ? 1 : 0;
            }

            fixedTickRW.ValueRW = fixedTick;

            var runtimeRW = SystemAPI.GetSingletonRW<FixedTickStepRuntimeComponent>();
            runtimeRW.ValueRW = new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = frameDeltaTime,
                LogicDeltaTime = logicDeltaTime,
                LogicStepCount = logicStepCount,
                HasStep = (byte)(hasStep ? 1 : 0),
                UsingFixedTick = (byte)(usingFixedTick ? 1 : 0),
            };
        }
    }
}
