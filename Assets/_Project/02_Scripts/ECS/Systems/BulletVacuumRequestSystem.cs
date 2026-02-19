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
            state.RequireForUpdate<PlayerCarryBinComponent>();
            state.RequireForUpdate<PlayerHazardPenaltyStateComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            // Vacuum 상태 갱신(플레이어 단일)
            var vacuumRW = SystemAPI.GetComponentRW<VacuumBurstComponent>(playerEntity);
            var penaltyRW = SystemAPI.GetComponentRW<PlayerHazardPenaltyStateComponent>(playerEntity);
            CarryBinRules.TickPenaltyTimers(ref penaltyRW.ValueRW, dt);
            UpdateVacuumState(ref vacuumRW.ValueRW, in penaltyRW.ValueRO, dt);

            if (vacuumRW.ValueRO.IsActive == 0)
                return;

            if (!BulletFieldShared.IsInitialized)
                return;

            Debug.Log($"[Vacuum System] 흡입 작동 중... / dt: {dt}");

            // LocalTransform은 메인 스레드에서 읽지 않는다 (타입 충돌 방지).
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var captureRuleLookup = SystemAPI.GetComponentLookup<BulletCaptureRuleComponent>(isReadOnly: true);
            var bulletRadiusLookup = SystemAPI.GetComponentLookup<BulletRadiusComponent>(isReadOnly: true);
            var scoreValueLookup = SystemAPI.GetComponentLookup<BulletScoreValueComponent>(isReadOnly: true);
            var bulletSourceLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(isReadOnly: true);
            var reqLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);
            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(isReadOnly: false);

            txLookup.Update(ref state);
            captureRuleLookup.Update(ref state);
            bulletRadiusLookup.Update(ref state);
            scoreValueLookup.Update(ref state);
            bulletSourceLookup.Update(ref state);
            reqLookup.Update(ref state);
            sourceLookup.Update(ref state);

            var carryLookup = SystemAPI.GetComponentLookup<PlayerCarryBinComponent>(isReadOnly: false);
            carryLookup.Update(ref state);

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
                HazardRingInner = GetHazardRingInner(in vacuumRW.ValueRO),
                HazardRingOuter = GetHazardRingOuter(in vacuumRW.ValueRO),

                CellMap = BulletFieldShared.CellMap,
                TxLookup = txLookup,
                CaptureRuleLookup = captureRuleLookup,
                BulletRadiusLookup = bulletRadiusLookup,
                ScoreValueLookup = scoreValueLookup,
                BulletSourceLookup = bulletSourceLookup,
                RequestLookup = reqLookup,
                SourceLookup = sourceLookup,
                NewlyRequested = newlyRequested,
            }.Schedule(deps);

            state.Dependency = new ApplyVacuumCarryLoadJob
            {
                PlayerEntity = playerEntity,
                CarryLookup = carryLookup,
                Add = newlyRequested,
            }.Schedule(state.Dependency);

            state.Dependency = newlyRequested.Dispose(state.Dependency);

            // 다음 프레임 Simulation의 Clear/Build가 안전하게 기다릴 수 있도록 fence 갱신
            BulletFieldShared.CellMapFence = state.Dependency;
        }

        private static void UpdateVacuumState(ref VacuumBurstComponent v, in PlayerHazardPenaltyStateComponent penalty, float dt)
        {
            if (v.CooldownTimer > 0f)
                v.CooldownTimer = math.max(0f, v.CooldownTimer - dt);
            if (v.CaptureCooldownTimer > 0f)
                v.CaptureCooldownTimer = math.max(0f, v.CaptureCooldownTimer - dt);
            if (v.CaptureActiveTimer > 0f)
                v.CaptureActiveTimer = math.max(0f, v.CaptureActiveTimer - dt);

            if (CarryBinRules.ApplyVacuumLock(ref v, in penalty))
                return;

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

        private static float GetHazardRingInner(in VacuumBurstComponent v)
        {
            float halfWidth = math.max(0f, v.CaptureRingWidth * 0.5f);
            return math.max(0f, v.CaptureRingRadius - halfWidth);
        }

        private static float GetHazardRingOuter(in VacuumBurstComponent v)
        {
            float halfWidth = math.max(0f, v.CaptureRingWidth * 0.5f);
            float inner = math.max(0f, v.CaptureRingRadius - halfWidth);
            return math.max(inner, v.CaptureRingRadius + halfWidth);
        }

        [BurstCompile]
        private struct VacuumRequestFromCellMapJob : IJob
        {
            public Entity PlayerEntity;
            public float InvCellSize;
            public float Range;
            public byte IsHazardCaptureActive;
            public float HazardRingInner;
            public float HazardRingOuter;

            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<BulletCaptureRuleComponent> CaptureRuleLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;
            [ReadOnly] public ComponentLookup<BulletScoreValueComponent> ScoreValueLookup;
            [ReadOnly] public ComponentLookup<BulletSourceRefComponent> BulletSourceLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;
            public ComponentLookup<SourceSpawnComponent> SourceLookup;

            public NativeReference<int> NewlyRequested;

            public void Execute()
            {
                if (!TxLookup.HasComponent(PlayerEntity))
                    return;

                float3 playerPos = TxLookup[PlayerEntity].Position;

                int2 center = SpatialHashUtility.ToCell(playerPos, InvCellSize);
                int cellRadius = (int)math.ceil(Range * InvCellSize) + 1;

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
                            if (!CaptureRuleLookup.HasComponent(bullet)) continue;
                            if (!RequestLookup.HasComponent(bullet)) continue;
                            if (RequestLookup.IsComponentEnabled(bullet)) continue;

                            var p = TxLookup[bullet].Position;
                            float dxp = p.x - playerPos.x;
                            float dzp = p.z - playerPos.z;
                            float distSq = dxp * dxp + dzp * dzp;
                            float bulletRadius = BulletRadiusLookup.HasComponent(bullet)
                                ? math.max(0f, BulletRadiusLookup[bullet].Value)
                                : 0f;
                            var captureRule = CaptureRuleLookup[bullet].Value;

                            bool canCapture = false;
                            if (captureRule == BulletCaptureRuleId.StandardCollectible)
                            {
                                float collectRange = Range + bulletRadius;
                                canCapture = distSq <= collectRange * collectRange;
                            }
                            else if (captureRule == BulletCaptureRuleId.RiskTimedResolve && IsHazardCaptureActive != 0)
                            {
                                float inner = math.max(0f, HazardRingInner - bulletRadius);
                                float outer = math.max(inner, HazardRingOuter + bulletRadius);
                                canCapture = distSq >= inner * inner && distSq <= outer * outer;
                            }

                            if (!canCapture) continue;

                            RequestLookup.SetComponentEnabled(bullet, true);
                            TryAccumulateDepletion(bullet);
                            int scoreValue = 1;
                            if (ScoreValueLookup.HasComponent(bullet))
                                scoreValue = math.max(0, ScoreValueLookup[bullet].Value);
                            add += scoreValue;
                        }
                        while (CellMap.TryGetNextValue(out bullet, ref it));
                    }

                NewlyRequested.Value += add;
                Debug.Log($"[Vacuum Job] 이번 프레임 CarryBin 증가량: {add}");
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
                SourceStateId nextStateByCount;
                if (source.CollectedCount >= depletedThreshold)
                    nextStateByCount = SourceStateId.Depleted;
                else if (source.CollectedCount >= weakenedThreshold)
                    nextStateByCount = SourceStateId.Weakened;
                else
                    nextStateByCount = SourceStateId.Normal;

                // 상태 전이는 단방향만 허용한다(Normal -> Weakened -> Depleted).
                if ((byte)nextStateByCount > (byte)source.State)
                    source.State = nextStateByCount;

                SourceLookup[sourceEntity] = source;
            }
        }

        [BurstCompile]
        private struct ApplyVacuumCarryLoadJob : IJob
        {
            public Entity PlayerEntity;
            public ComponentLookup<PlayerCarryBinComponent> CarryLookup;
            [ReadOnly] public NativeReference<int> Add;

            public void Execute()
            {
                int add = Add.Value;
                if (add <= 0) return;
                if (!CarryLookup.HasComponent(PlayerEntity)) return;

                var carry = CarryLookup[PlayerEntity];
                carry.Load = CarryBinRules.AddLoadClamped(carry.Load, add, carry.Capacity);
                CarryLookup[PlayerEntity] = carry;
            }
        }
    }
}
