using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Deposit 접촉 요청 생성.
    /// - grid authoritative DepositRegionId를 읽어 Request 단계에서 요청을 남긴다.
    /// - 실제 CarryBin 변경은 Execution 단계에서만 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerHazardCollisionRequestSystem))]
    public partial struct PlayerCarryBinDepositRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerCarryBinComponent>();
            state.RequireForUpdate<PlayerCarryBinDepositRequestTag>();
            state.RequireForUpdate<PlayerCarryBinDepositContextComponent>();
            state.RequireForUpdate<PlayerRadiusComponent>();
            state.RequireForUpdate<LocalTransform>();
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

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var carryBin = SystemAPI.GetComponent<PlayerCarryBinComponent>(playerEntity);
            if (math.max(0, carryBin.Load) <= 0)
                return;

            var grid = SystemAPI.GetSingleton<StageRuntimeGridComponent>();
            if (!StageRuntimeGridUtility.IsReady(in grid))
                return;

            var cells = SystemAPI.GetSingletonBuffer<StageRuntimeGridCellBufferElement>(isReadOnly: true);
            var tx = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            var radius = SystemAPI.GetComponent<PlayerRadiusComponent>(playerEntity);
            uint touchedRegionId = FindTouchedDepositRegion(
                new float2(tx.Position.x, tx.Position.z),
                math.max(0f, radius.Value),
                in grid,
                cells);
            if (touchedRegionId == 0)
                return;

            var context = SystemAPI.GetComponentRW<PlayerCarryBinDepositContextComponent>(playerEntity);
            context.ValueRW.DepositRegionId = touchedRegionId;
            SystemAPI.SetComponentEnabled<PlayerCarryBinDepositRequestTag>(playerEntity, true);
        }

        private static uint FindTouchedDepositRegion(
            float2 centerXZ,
            float radius,
            in StageRuntimeGridComponent grid,
            DynamicBuffer<StageRuntimeGridCellBufferElement> cells)
        {
            StageRuntimeGridUtility.ComputeCircleCellBounds(centerXZ, radius, in grid, out int2 minCell, out int2 maxCell);
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                for (int x = minCell.x; x <= maxCell.x; x++)
                {
                    int index = StageRuntimeGridUtility.GetCellIndex(x, y, in grid);
                    if (index < 0)
                        continue;

                    uint depositRegionId = cells[index].DepositRegionId;
                    if (depositRegionId != 0)
                        return depositRegionId;
                }
            }

            return 0u;
        }
    }

    /// <summary>
    /// Deposit 요청 소비.
    /// - MVP 규칙: CarryBin.Load를 즉시 0으로 비운다.
    /// - MetaScrap 정산은 후속 단계에서 연결한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(PlayerHazardCollisionExecutionSystem))]
    [UpdateBefore(typeof(BulletDespawnExecutionSystem))]
    public partial struct PlayerCarryBinDepositExecutionSystem : ISystem
    {
        private EntityQuery _combatEventChannelQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerCarryBinComponent>();
            state.RequireForUpdate<PlayerHazardRiskRequestComponent>();
            state.RequireForUpdate<PlayerCarryBinDepositRequestTag>();
            state.RequireForUpdate<PlayerCarryBinDepositContextComponent>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
            _combatEventChannelQuery = SystemAPI.QueryBuilder()
                .WithAll<CombatEventChannelSingletonTag>()
                .WithAll<CombatEventBufferElement>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());
            Entity combatChannelEntity = ResolveFirstEntity(ref _combatEventChannelQuery);
            DynamicBuffer<CombatEventBufferElement> combatBuffer = default;
            bool hasCombatBuffer = false;
            if (combatChannelEntity != Entity.Null)
            {
                combatBuffer = SystemAPI.GetBuffer<CombatEventBufferElement>(combatChannelEntity);
                hasCombatBuffer = true;
            }

            foreach (var (depositRequest, carryBin, depositContext, riskRequest) in
                     SystemAPI.Query<
                         EnabledRefRW<PlayerCarryBinDepositRequestTag>,
                         RefRW<PlayerCarryBinComponent>,
                         RefRW<PlayerCarryBinDepositContextComponent>,
                         RefRW<PlayerHazardRiskRequestComponent>>().WithAll<PlayerTag>())
            {
                if (!depositRequest.ValueRO)
                    continue;

                int depositedLoad = math.max(0, carryBin.ValueRO.Load);
                if (depositedLoad > 0)
                {
                    carryBin.ValueRW.Load = 0;
                    if (hasCombatBuffer)
                    {
                        combatBuffer.Add(new CombatEventBufferElement
                        {
                            Type = CombatEventTypeId.Cleanup,
                            SourceEntity = Entity.Null,
                            RelatedEntity = Entity.Null,
                            Count = 1,
                            Value = depositedLoad,
                            Frame = frame,
                            Sequence = (uint)combatBuffer.Length,
                        });
                    }

                    Debug.Log($"[CarryBinDeposit] load={depositedLoad}, depositRegionId={depositContext.ValueRO.DepositRegionId}");
                }

                riskRequest.ValueRW.ResetRequested = 1;
                depositContext.ValueRW.DepositRegionId = 0u;
                depositRequest.ValueRW = false;
            }
        }

        private static Entity ResolveFirstEntity(ref EntityQuery query)
        {
            int count = query.CalculateEntityCount();
            if (count <= 0)
                return Entity.Null;
            if (count == 1)
                return query.GetSingletonEntity();

            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }
    }
}
