using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(ReplayTickInputApplySystem))]
    [UpdateBefore(typeof(PlayerIntentMovementSystem))]
    public partial struct PlayerPreviousPositionCaptureSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerPreviousPositionComponent>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            foreach (var (tx, previous) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<PlayerPreviousPositionComponent>>().WithAll<PlayerTag>())
            {
                previous.ValueRW.Position = tx.ValueRO.Position;
            }
        }
    }

    [UpdateInGroup(typeof(PlayerFixedStepGroup))]
    [UpdateAfter(typeof(PlayerIntentMovementSystem))]
    [UpdateBefore(typeof(PlayerIntentConsumeSystem))]
    public partial struct PlayerObstacleBlockSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerRadiusComponent>();
            state.RequireForUpdate<PlayerPreviousPositionComponent>();
            state.RequireForUpdate<LocalTransform>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<StageRuntimeGridComponent>();
            state.RequireForUpdate<StageRuntimeGridCellBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            if (hasTopologyState
                && (!hasStageState || !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState)))
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.ShouldRunLogicStep(in fixedTickRuntime))
                return;

            var grid = SystemAPI.GetSingleton<StageRuntimeGridComponent>();
            if (!StageRuntimeGridUtility.IsReady(in grid))
                return;

            var cells = SystemAPI.GetSingletonBuffer<StageRuntimeGridCellBufferElement>(isReadOnly: true);
            foreach (var (tx, previous, radius) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<PlayerPreviousPositionComponent>, RefRO<PlayerRadiusComponent>>().WithAll<PlayerTag>())
            {
                float3 prev = previous.ValueRO.Position;
                float3 next = tx.ValueRO.Position;
                float2 prevXZ = new float2(prev.x, prev.z);
                float2 nextXZ = new float2(next.x, next.z);
                float playerRadius = math.max(0f, radius.ValueRO.Value);

                if (IsCandidateValid(prevXZ, nextXZ, playerRadius, in grid, cells))
                    continue;

                float2 delta = nextXZ - prevXZ;
                float2 xOnly = new float2(nextXZ.x, prevXZ.y);
                float2 zOnly = new float2(prevXZ.x, nextXZ.y);
                bool xValid = IsCandidateValid(prevXZ, xOnly, playerRadius, in grid, cells);
                bool zValid = IsCandidateValid(prevXZ, zOnly, playerRadius, in grid, cells);

                float2 resolved = prevXZ;
                if (xValid && zValid)
                {
                    float xDistanceSq = math.lengthsq(xOnly - prevXZ);
                    float zDistanceSq = math.lengthsq(zOnly - prevXZ);
                    if (math.abs(xDistanceSq - zDistanceSq) <= 1e-6f)
                        resolved = math.abs(delta.x) >= math.abs(delta.y) ? xOnly : zOnly;
                    else
                        resolved = xDistanceSq >= zDistanceSq ? xOnly : zOnly;
                }
                else if (xValid)
                {
                    resolved = xOnly;
                }
                else if (zValid)
                {
                    resolved = zOnly;
                }

                var corrected = tx.ValueRO;
                corrected.Position = new float3(resolved.x, prev.y, resolved.y);
                tx.ValueRW = corrected;
            }
        }

        private static bool IsCandidateValid(
            float2 prevXZ,
            float2 nextXZ,
            float radius,
            in StageRuntimeGridComponent grid,
            DynamicBuffer<StageRuntimeGridCellBufferElement> cells)
        {
            return !StageRuntimeBlockQuery.BlocksPlayerFullCell(prevXZ, nextXZ, radius, in grid, cells);
        }
    }

}
