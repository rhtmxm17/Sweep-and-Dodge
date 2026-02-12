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
    /// 위험탄-플레이어 충돌 요청 생성.
    /// - 프레임당 1회만 충돌 처리
    /// - 제거 요청이 이미 걸린 탄은 충돌 대상에서 제외(제거 우선)
    /// - 실제 효과 적용은 Execution 단계에서 소비한다
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(BulletVacuumRequestSystem))]
    public partial struct PlayerHazardCollisionRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var playerRadiusLookup = SystemAPI.GetComponentLookup<PlayerRadiusComponent>(isReadOnly: true);
            var bulletRadiusLookup = SystemAPI.GetComponentLookup<BulletRadiusComponent>(isReadOnly: true);
            var despawnRequestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);
            var playerHitRequestLookup = SystemAPI.GetComponentLookup<PlayerHazardHitRequestTag>(isReadOnly: false);

            txLookup.Update(ref state);
            playerRadiusLookup.Update(ref state);
            bulletRadiusLookup.Update(ref state);
            despawnRequestLookup.Update(ref state);
            playerHitRequestLookup.Update(ref state);

            if (!playerHitRequestLookup.HasComponent(playerEntity))
                return;

            // 같은 프레임의 중복 요청을 막아 "프레임당 1회"를 고정한다.
            if (playerHitRequestLookup.IsComponentEnabled(playerEntity))
                return;

            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);

            state.Dependency = new PlayerHazardCollisionRequestFromCellMapJob
            {
                PlayerEntity = playerEntity,
                InvCellSize = cfg.InvCellSize,

                CellMap = BulletFieldShared.HazardCellMap,
                TxLookup = txLookup,
                PlayerRadiusLookup = playerRadiusLookup,
                BulletRadiusLookup = bulletRadiusLookup,
                DespawnRequestLookup = despawnRequestLookup,
                PlayerHitRequestLookup = playerHitRequestLookup,
            }.Schedule(deps);

            // Simulation 단계가 Request read를 기다릴 수 있도록 fence 갱신
            BulletFieldShared.CellMapFence = state.Dependency;
        }

        [BurstCompile]
        private struct PlayerHazardCollisionRequestFromCellMapJob : IJob
        {
            public Entity PlayerEntity;
            public float InvCellSize;

            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<PlayerRadiusComponent> PlayerRadiusLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;
            public ComponentLookup<BulletDespawnRequestTag> DespawnRequestLookup;
            public ComponentLookup<PlayerHazardHitRequestTag> PlayerHitRequestLookup;

            public void Execute()
            {
                if (!TxLookup.HasComponent(PlayerEntity))
                    return;
                if (!PlayerRadiusLookup.HasComponent(PlayerEntity))
                    return;
                if (!PlayerHitRequestLookup.HasComponent(PlayerEntity))
                    return;

                float3 playerPos = TxLookup[PlayerEntity].Position;
                float playerRadius = math.max(0f, PlayerRadiusLookup[PlayerEntity].Value);

                int2 center = SpatialHashUtility.ToCell(playerPos, InvCellSize);
                int cellRadius = (int)math.ceil(playerRadius * InvCellSize) + 1;

                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                {
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        int2 c = center + new int2(dx, dy);
                        int key = SpatialHashUtility.Hash(c);

                        if (!CellMap.TryGetFirstValue(key, out var bullet, out var it))
                            continue;

                        do
                        {
                            if (!TxLookup.HasComponent(bullet)) continue;
                            if (!BulletRadiusLookup.HasComponent(bullet)) continue;
                            if (!DespawnRequestLookup.HasComponent(bullet)) continue;

                            // 제거 요청이 있으면 충돌 후보에서 제외(제거 우선)
                            if (DespawnRequestLookup.IsComponentEnabled(bullet)) continue;

                            float bulletRadius = math.max(0f, BulletRadiusLookup[bullet].Value);
                            float combined = playerRadius + bulletRadius;
                            float3 bp = TxLookup[bullet].Position;
                            float dxp = bp.x - playerPos.x;
                            float dzp = bp.z - playerPos.z;
                            float distSq = dxp * dxp + dzp * dzp;
                            if (distSq > combined * combined) continue;

                            // 충돌 확인 단계에서도 즉시 제거 요청을 남겨 중복 충돌을 완화한다.
                            DespawnRequestLookup.SetComponentEnabled(bullet, true);
                            PlayerHitRequestLookup.SetComponentEnabled(PlayerEntity, true);
                            return;
                        }
                        while (CellMap.TryGetNextValue(out bullet, ref it));
                    }
                }
            }
        }
    }

    /// <summary>
    /// 위험탄 충돌 요청 소비(현재 단계: 로그 출력).
    /// 이후 넉백/패널티/무적 프레임 로직을 이 시스템에 확장한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateBefore(typeof(BulletDespawnExecutionSystem))]
    public partial struct PlayerHazardCollisionExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var hitReq in SystemAPI.Query<EnabledRefRW<PlayerHazardHitRequestTag>>().WithAll<PlayerTag>())
            {
                if (!hitReq.ValueRO)
                    continue;

                Debug.Log("[HazardCollision] 플레이어 위험탄 충돌 감지 (frame당 1회)");
                hitReq.ValueRW = false;
            }
        }
    }
}
