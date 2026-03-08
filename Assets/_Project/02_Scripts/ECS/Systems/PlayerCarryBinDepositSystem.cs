using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Deposit 접촉 요청 생성.
    /// - 접촉 즉시 비우기(MVP) 규칙을 Request 단계에서 요청으로 남긴다.
    /// - 실제 CarryBin 변경은 Execution 단계에서만 수행한다.
    /// </summary>
    [BurstCompile]
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
            state.RequireForUpdate<DepositPointComponent>();
        }

        [BurstCompile]
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

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var playerRadiusLookup = SystemAPI.GetComponentLookup<PlayerRadiusComponent>(isReadOnly: true);
            var depositRequestLookup = SystemAPI.GetComponentLookup<PlayerCarryBinDepositRequestTag>(isReadOnly: false);
            var depositContextLookup = SystemAPI.GetComponentLookup<PlayerCarryBinDepositContextComponent>(isReadOnly: false);

            txLookup.Update(ref state);
            playerRadiusLookup.Update(ref state);
            depositRequestLookup.Update(ref state);
            depositContextLookup.Update(ref state);

            var touchedDeposit = new NativeReference<Entity>(Allocator.TempJob);
            touchedDeposit.Value = Entity.Null;

            state.Dependency = new FindTouchedDepositJob
            {
                PlayerEntity = playerEntity,
                TxLookup = txLookup,
                PlayerRadiusLookup = playerRadiusLookup,
                TouchedDeposit = touchedDeposit,
            }.Schedule(state.Dependency);

            state.Dependency = new ApplyDepositRequestJob
            {
                PlayerEntity = playerEntity,
                DepositRequestLookup = depositRequestLookup,
                DepositContextLookup = depositContextLookup,
                TouchedDeposit = touchedDeposit,
            }.Schedule(state.Dependency);

            state.Dependency = touchedDeposit.Dispose(state.Dependency);
        }

        [BurstCompile]
        private partial struct FindTouchedDepositJob : IJobEntity
        {
            public Entity PlayerEntity;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<PlayerRadiusComponent> PlayerRadiusLookup;
            public NativeReference<Entity> TouchedDeposit;

            private void Execute(Entity depositEntity, in DepositPointComponent deposit, in LocalTransform depositTx)
            {
                if (TouchedDeposit.Value != Entity.Null)
                    return;
                if (!TxLookup.HasComponent(PlayerEntity))
                    return;

                float3 playerPos = TxLookup[PlayerEntity].Position;
                float playerRadius = PlayerRadiusLookup.HasComponent(PlayerEntity)
                    ? math.max(0f, PlayerRadiusLookup[PlayerEntity].Value)
                    : 0f;

                float reach = math.max(0f, deposit.Radius) + playerRadius;
                float3 delta = depositTx.Position - playerPos;
                float distSq = delta.x * delta.x + delta.z * delta.z;
                if (distSq <= reach * reach)
                    TouchedDeposit.Value = depositEntity;
            }
        }

        [BurstCompile]
        private struct ApplyDepositRequestJob : IJob
        {
            public Entity PlayerEntity;
            public ComponentLookup<PlayerCarryBinDepositRequestTag> DepositRequestLookup;
            public ComponentLookup<PlayerCarryBinDepositContextComponent> DepositContextLookup;
            [ReadOnly] public NativeReference<Entity> TouchedDeposit;

            public void Execute()
            {
                Entity touched = TouchedDeposit.Value;
                if (touched == Entity.Null)
                    return;
                if (!DepositRequestLookup.HasComponent(PlayerEntity))
                    return;
                if (!DepositContextLookup.HasComponent(PlayerEntity))
                    return;

                var context = DepositContextLookup[PlayerEntity];
                context.DepositEntity = touched;
                DepositContextLookup[PlayerEntity] = context;
                DepositRequestLookup.SetComponentEnabled(PlayerEntity, true);
            }
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

            foreach (var (depositRequest, carryBin, depositContext) in
                     SystemAPI.Query<
                         EnabledRefRW<PlayerCarryBinDepositRequestTag>,
                         RefRW<PlayerCarryBinComponent>,
                         RefRW<PlayerCarryBinDepositContextComponent>>().WithAll<PlayerTag>())
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
                            RelatedEntity = depositContext.ValueRO.DepositEntity,
                            Count = 1,
                            Value = depositedLoad,
                            Frame = frame,
                            Sequence = (uint)combatBuffer.Length,
                        });
                    }
                    Debug.Log($"[CarryBinDeposit] load={depositedLoad}, deposit={depositContext.ValueRO.DepositEntity}");
                }

                depositContext.ValueRW.DepositEntity = Entity.Null;
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

            using var entities = query.ToEntityArray(Allocator.Temp);
            return entities.Length > 0 ? entities[0] : Entity.Null;
        }
    }
}

