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
    /// Vacuum 제거 행동:
    /// - Trash  : 활성 시간 동안 Range 내 즉시 디스폰 요청
    /// - Hazard : Vacuum ON 직후 Capture 타이밍 동안 Ring 밴드 내에서만 디스폰 요청
    /// - 실제 비활성/풀 반납은 BulletExecutionEndGroup의 BulletDespawnExecutionSystem이 단일 책임으로 수행
    /// - LocalTransform 타입 충돌 방지: 메인 스레드에서 LocalTransform을 직접 읽지 않고 Job으로 스케줄
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    public partial struct BulletVacuumRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            // Vacuum 상태 갱신(플레이어 단일)
            var vacuumRW = SystemAPI.GetComponentRW<VacuumBurstComponent>(playerEntity);
            UpdateVacuumState(ref vacuumRW.ValueRW, dt);

            if (vacuumRW.ValueRO.IsActive == 0)
                return;

            if (!BulletFieldShared.IsInitialized)
                return;

            Debug.Log($"[Vacuum System] 흡입 작동 중... / dt: {dt}");

            // LocalTransform은 메인 스레드에서 읽지 않는다 (타입 충돌 방지).
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var kindLookup = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: true);
            var bulletSourceLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(isReadOnly: true);
            var reqLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);
            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(isReadOnly: false);

            txLookup.Update(ref state);
            kindLookup.Update(ref state);
            bulletSourceLookup.Update(ref state);
            reqLookup.Update(ref state);
            sourceLookup.Update(ref state);

            // 점수 반영: 새로 요청된 탄 개수만 누적
            var scoreEntity = SystemAPI.GetSingletonEntity<BulletFieldConfigComponent>();
            var scoreLookup = SystemAPI.GetComponentLookup<ScoreComponent>(isReadOnly: false);
            scoreLookup.Update(ref state);

            var newlyRequested = new NativeReference<int>(Allocator.TempJob);
            newlyRequested.Value = 0;

            // CellMap은 SharedStatic이며, Simulation에서 Write → Request에서 ReadOnly로 소비한다.
            // 이전 프레임/이전 Request의 read가 끝난 뒤에만 다음 Simulation이 Clear/Build 하도록 fence를 갱신한다.
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);

            state.Dependency = new VacuumRequestFromCellMapJob
            {
                PlayerEntity = playerEntity,
                InvCellSize = cfg.InvCellSize,

                Range = vacuumRW.ValueRO.Range,
                IsHazardCaptureActive = vacuumRW.ValueRO.CaptureActiveTimer > 0f ? (byte)1 : (byte)0,
                HazardRingInnerSq = GetHazardRingInnerSq(in vacuumRW.ValueRO),
                HazardRingOuterSq = GetHazardRingOuterSq(in vacuumRW.ValueRO),

                CellMap = BulletFieldShared.CellMap,
                TxLookup = txLookup,
                KindLookup = kindLookup,
                BulletSourceLookup = bulletSourceLookup,
                RequestLookup = reqLookup,
                SourceLookup = sourceLookup,
                NewlyRequested = newlyRequested,
            }.Schedule(deps);

            state.Dependency = new ApplyVacuumScoreJob
            {
                ScoreEntity = scoreEntity,
                ScoreLookup = scoreLookup,
                Add = newlyRequested,
            }.Schedule(state.Dependency);

            state.Dependency = newlyRequested.Dispose(state.Dependency);

            // 다음 프레임 Simulation의 Clear/Build가 안전하게 기다릴 수 있도록 fence 갱신
            BulletFieldShared.CellMapFence = state.Dependency;
        }

        private static void UpdateVacuumState(ref VacuumBurstComponent v, float dt)
        {
            if (v.CooldownTimer > 0f)
                v.CooldownTimer = math.max(0f, v.CooldownTimer - dt);
            if (v.CaptureCooldownTimer > 0f)
                v.CaptureCooldownTimer = math.max(0f, v.CaptureCooldownTimer - dt);
            if (v.CaptureActiveTimer > 0f)
                v.CaptureActiveTimer = math.max(0f, v.CaptureActiveTimer - dt);

            if (v.IsActive != 0)
            {
                v.ActiveTimer = math.max(0f, v.ActiveTimer - dt);
                if (v.ActiveTimer <= 0f)
                {
                    v.IsActive = 0;
                    v.CooldownTimer = v.Cooldown;
                }
                return;
            }

            if (v.ActivateRequested != 0 && v.CooldownTimer <= 0f && v.CaptureCooldownTimer <= 0f)
            {
                v.ActivateRequested = 0;
                v.IsActive = 1;
                v.ActiveTimer = v.ActiveTime;
                v.CaptureActiveTimer = v.CaptureActiveTime;
                v.CaptureCooldownTimer = v.CaptureCooldown;
            }
            else
            {
                // 선입력 버림(쿨타임 중 요청은 폐기)
                v.ActivateRequested = 0;
            }
        }

        private static float GetHazardRingInnerSq(in VacuumBurstComponent v)
        {
            float halfWidth = math.max(0f, v.CaptureRingWidth * 0.5f);
            float inner = math.max(0f, v.CaptureRingRadius - halfWidth);
            return inner * inner;
        }

        private static float GetHazardRingOuterSq(in VacuumBurstComponent v)
        {
            float halfWidth = math.max(0f, v.CaptureRingWidth * 0.5f);
            float inner = math.max(0f, v.CaptureRingRadius - halfWidth);
            float outer = math.max(inner, v.CaptureRingRadius + halfWidth);
            return outer * outer;
        }

        [BurstCompile]
        private struct VacuumRequestFromCellMapJob : IJob
        {
            public Entity PlayerEntity;
            public float InvCellSize;
            public float Range;
            public byte IsHazardCaptureActive;
            public float HazardRingInnerSq;
            public float HazardRingOuterSq;

            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<BulletKindComponent> KindLookup;
            [ReadOnly] public ComponentLookup<BulletSourceRefComponent> BulletSourceLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;
            public ComponentLookup<SourceSpawnComponent> SourceLookup;

            public NativeReference<int> NewlyRequested;

            public void Execute()
            {
                if (!TxLookup.HasComponent(PlayerEntity))
                    return;

                float3 playerPos = TxLookup[PlayerEntity].Position;
                float rangeSq = Range * Range;

                int2 center = SpatialHashUtility.ToCell(playerPos, InvCellSize);
                int cellRadius = (int)math.ceil(Range * InvCellSize);

                int add = 0;

                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        int2 c = center + new int2(dx, dy);
                        int key = SpatialHashUtility.Hash(c);

                        if (!CellMap.TryGetFirstValue(key, out var bullet, out var it))
                            continue;

                        do
                        {
                            if (!TxLookup.HasComponent(bullet)) continue;
                            if (!KindLookup.HasComponent(bullet)) continue;
                            if (!RequestLookup.HasComponent(bullet)) continue;
                            if (RequestLookup.IsComponentEnabled(bullet)) continue;

                            var p = TxLookup[bullet].Position;
                            float dxp = p.x - playerPos.x;
                            float dzp = p.z - playerPos.z;
                            float distSq = dxp * dxp + dzp * dzp;
                            var kind = KindLookup[bullet].Value;

                            bool canCapture = false;
                            if (kind == BulletKindId.Trash)
                            {
                                canCapture = distSq <= rangeSq;
                            }
                            else if (kind == BulletKindId.Hazard && IsHazardCaptureActive != 0)
                            {
                                canCapture = distSq >= HazardRingInnerSq && distSq <= HazardRingOuterSq;
                            }

                            if (!canCapture) continue;

                            RequestLookup.SetComponentEnabled(bullet, true);
                            TryAccumulateDepletion(bullet);
                            add++;
                        }
                        while (CellMap.TryGetNextValue(out bullet, ref it));
                    }

                NewlyRequested.Value += add;
                Debug.Log($"[Vacuum Job] 흡입 대상 Bullet: {add} 개");
            }

            private void TryAccumulateDepletion(Entity bullet)
            {
                if (!BulletSourceLookup.HasComponent(bullet))
                    return;

                var sourceEntity = BulletSourceLookup[bullet].Value;
                if (sourceEntity == Entity.Null)
                    return;
                if (!SourceLookup.HasComponent(sourceEntity))
                    return;

                var source = SourceLookup[sourceEntity];
                source.CollectedCount++;

                int weakenedThreshold = math.max(0, source.ThresholdWeakened);
                int depletedThreshold = math.max(weakenedThreshold, source.ThresholdDepleted);

                if (source.CollectedCount >= depletedThreshold)
                    source.State = SourceStateId.Depleted;
                else if (source.CollectedCount >= weakenedThreshold)
                    source.State = SourceStateId.Weakened;
                else
                    source.State = SourceStateId.Normal;

                SourceLookup[sourceEntity] = source;
            }
        }

        [BurstCompile]
        private struct ApplyVacuumScoreJob : IJob
        {
            public Entity ScoreEntity;
            public ComponentLookup<ScoreComponent> ScoreLookup;
            [ReadOnly] public NativeReference<int> Add;

            public void Execute()
            {
                int add = Add.Value;
                if (add <= 0) return;

                var score = ScoreLookup[ScoreEntity];
                score.Value += add;
                ScoreLookup[ScoreEntity] = score;
            }
        }
    }
}
