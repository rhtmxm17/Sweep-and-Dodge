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
    /// - ActionId별 프로파일 기반으로 Trash/Hazard 판정 기하를 분기한다.
    /// - 기본 제공:
    ///   - RadialRing: 원형 흡입 + 외곽 링 위험탄 처리
    ///   - ForwardFanLine: 전방 부채꼴 + 전방 직선 위험탄 처리
    /// - 실제 비활성/풀 반납은 BulletExecutionEndGroup의 BulletDespawnExecutionSystem이 단일 책임으로 수행
    /// - LocalTransform 타입 충돌 방지: 메인 스레드에서 LocalTransform을 직접 읽지 않고 Job으로 스케줄
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    public partial struct BulletVacuumRequestSystem : ISystem
    {
        private const float FallbackRadialTrashRange = 3.2f;
        private const float FallbackRadialHazardRingRadius = 2.88f;
        private const float FallbackRadialHazardRingWidth = 0.8f;
        private const float FallbackForwardTrashRange = 3.2f;
        private const float FallbackForwardTrashHalfAngleDeg = 40f;
        private const float FallbackForwardHazardLineLength = 3.2f;
        private const float FallbackForwardHazardLineHalfWidth = 0.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerGoSyncComponent>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<VacuumActivationConfigComponent>();
            state.RequireForUpdate<VacuumRuntimeStateComponent>();
            state.RequireForUpdate<PlayerCarryBinComponent>();
            state.RequireForUpdate<PlayerHazardPenaltyStateComponent>();
            state.RequireForUpdate<PlayerUiFeedbackEventBufferElement>();
            state.RequireForUpdate<PlayerCleanupActionStateComponent>();
            state.RequireForUpdate<PlayerCleanupActionProfileBufferElement>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var time = SystemAPI.Time;
            var dt = time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            uint frame = FrameSequenceUtility.EstimateFrame(time.ElapsedTime, dt);

            // Vacuum 상태 갱신(플레이어 단일)
            var vacuumConfigRO = SystemAPI.GetComponent<VacuumActivationConfigComponent>(playerEntity);
            var vacuumStateRW = SystemAPI.GetComponentRW<VacuumRuntimeStateComponent>(playerEntity);
            var penaltyRW = SystemAPI.GetComponentRW<PlayerHazardPenaltyStateComponent>(playerEntity);
            var carryBinRO = SystemAPI.GetComponent<PlayerCarryBinComponent>(playerEntity);
            var goSyncRO = SystemAPI.GetComponent<PlayerGoSyncComponent>(playerEntity);
            var actionStateRO = SystemAPI.GetComponent<PlayerCleanupActionStateComponent>(playerEntity);
            var actionProfiles = SystemAPI.GetBuffer<PlayerCleanupActionProfileBufferElement>(playerEntity);
            var uiFeedbackBuffer = SystemAPI.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
            CarryBinRules.TickPenaltyTimers(ref penaltyRW.ValueRW, dt);
            byte blockReason = UpdateVacuumState(
                in vacuumConfigRO,
                ref vacuumStateRW.ValueRW,
                in penaltyRW.ValueRO,
                in carryBinRO,
                dt);

            if (blockReason != (byte)PlayerUiFeedbackReasonId.None)
            {
                uiFeedbackBuffer.Add(new PlayerUiFeedbackEventBufferElement
                {
                    Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                    Reason = blockReason,
                    Value = 0,
                    RelatedEntity = Entity.Null,
                    Frame = frame,
                    Sequence = (uint)uiFeedbackBuffer.Length,
                });
            }

            if (vacuumStateRW.ValueRO.IsActive == 0)
                return;

            if (!BulletFieldShared.IsInitialized)
                return;

            var actionId = NormalizeActionId(actionStateRO.SelectedActionId);
            var actionProfile = ResolveActionProfile(actionProfiles, actionId);
            float3 playerForward = GetPlayerForward(in goSyncRO);
            float searchRange = ComputeSearchRange(in actionProfile);

            Debug.Log($"[Vacuum System] 흡입 작동 중... / dt: {dt}");

            // LocalTransform은 메인 스레드에서 읽지 않는다 (타입 충돌 방지).
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var captureRuleLookup = SystemAPI.GetComponentLookup<BulletCaptureRuleComponent>(isReadOnly: true);
            var bulletRadiusLookup = SystemAPI.GetComponentLookup<BulletRadiusComponent>(isReadOnly: true);
            var scoreValueLookup = SystemAPI.GetComponentLookup<BulletScoreValueComponent>(isReadOnly: true);
            var bulletSourceLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(isReadOnly: true);
            var reqLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);
            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(isReadOnly: false);
            var sourceAnchorLookup = SystemAPI.GetComponentLookup<SourceAnchorComponent>(isReadOnly: true);
            var sourcePollutionGridLookup = SystemAPI.GetComponentLookup<SourcePollutionGridComponent>(isReadOnly: true);
            var sourcePollutionCellLookup = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(isReadOnly: true);
            var sourcePollutionDropRequestLookup = SystemAPI.GetBufferLookup<SourcePollutionDropRequestBuffer>(isReadOnly: false);
            var uiFeedbackLookup = SystemAPI.GetBufferLookup<PlayerUiFeedbackEventBufferElement>(isReadOnly: false);

            txLookup.Update(ref state);
            captureRuleLookup.Update(ref state);
            bulletRadiusLookup.Update(ref state);
            scoreValueLookup.Update(ref state);
            bulletSourceLookup.Update(ref state);
            reqLookup.Update(ref state);
            sourceLookup.Update(ref state);
            sourceAnchorLookup.Update(ref state);
            sourcePollutionGridLookup.Update(ref state);
            sourcePollutionCellLookup.Update(ref state);
            sourcePollutionDropRequestLookup.Update(ref state);
            uiFeedbackLookup.Update(ref state);

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
                SearchRange = searchRange,
                IsHazardCaptureActive = vacuumStateRW.ValueRO.CaptureActiveTimer > 0f ? (byte)1 : (byte)0,
                ActionId = actionId,
                PlayerForward = playerForward,
                RadialTrashRange = actionProfile.TrashRange,
                RadialHazardRingInner = GetHazardRingInner(in actionProfile),
                RadialHazardRingOuter = GetHazardRingOuter(in actionProfile),
                ForwardTrashRange = actionProfile.TrashRange,
                ForwardTrashCosHalfAngle = math.cos(math.radians(actionProfile.TrashFanHalfAngleDeg)),
                ForwardHazardLineLength = actionProfile.HazardLineLength,
                ForwardHazardLineHalfWidth = actionProfile.HazardLineHalfWidth,

                CellMap = BulletFieldShared.CellMap,
                TxLookup = txLookup,
                CaptureRuleLookup = captureRuleLookup,
                BulletRadiusLookup = bulletRadiusLookup,
                ScoreValueLookup = scoreValueLookup,
                BulletSourceLookup = bulletSourceLookup,
                RequestLookup = reqLookup,
                SourceLookup = sourceLookup,
                SourceAnchorLookup = sourceAnchorLookup,
                SourcePollutionGridLookup = sourcePollutionGridLookup,
                SourcePollutionCellLookup = sourcePollutionCellLookup,
                SourcePollutionDropRequestLookup = sourcePollutionDropRequestLookup,
                UiFeedbackLookup = uiFeedbackLookup,
                NewlyRequested = newlyRequested,
                Frame = frame,
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

        private static byte UpdateVacuumState(
            in VacuumActivationConfigComponent config,
            ref VacuumRuntimeStateComponent state,
            in PlayerHazardPenaltyStateComponent penalty,
            in PlayerCarryBinComponent carry,
            float dt)
        {
            if (state.CooldownTimer > 0f)
                state.CooldownTimer = math.max(0f, state.CooldownTimer - dt);
            if (state.CaptureCooldownTimer > 0f)
                state.CaptureCooldownTimer = math.max(0f, state.CaptureCooldownTimer - dt);
            if (state.CaptureActiveTimer > 0f)
                state.CaptureActiveTimer = math.max(0f, state.CaptureActiveTimer - dt);

            bool hadActivateRequest = state.ActivateRequested != 0;
            if (CarryBinRules.ApplyVacuumLock(ref state, in penalty))
            {
                return hadActivateRequest
                    ? (byte)PlayerUiFeedbackReasonId.VacuumLocked
                    : (byte)PlayerUiFeedbackReasonId.None;
            }

            if (state.IsActive != 0)
            {
                // 기존 동작 중 들어온 발동 입력은 다음 프레임으로 넘기지 않고 즉시 소비한다.
                state.ActivateRequested = 0;
                state.ActiveTimer = math.max(0f, state.ActiveTimer - dt);
                if (state.ActiveTimer <= 0f)
                {
                    state.IsActive = 0;
                    state.CooldownTimer = config.Cooldown;
                }
                return (byte)PlayerUiFeedbackReasonId.None;
            }

            if (state.ActivateRequested != 0)
            {
                if (state.CooldownTimer > 0f || state.CaptureCooldownTimer > 0f)
                {
                    state.ActivateRequested = 0;
                    return (byte)PlayerUiFeedbackReasonId.CooldownActive;
                }

                state.ActivateRequested = 0;

                if (CarryBinRules.IsFull(in carry))
                {
                    return (byte)PlayerUiFeedbackReasonId.CarryBinFull;
                }

                state.IsActive = 1;
                state.ActiveTimer = config.ActiveTime;
                state.CaptureActiveTimer = config.CaptureActiveTime;
                state.CaptureCooldownTimer = config.CaptureCooldown;
                return (byte)PlayerUiFeedbackReasonId.None;
            }

            return (byte)PlayerUiFeedbackReasonId.None;
        }

        private static PlayerCleanupActionId NormalizeActionId(PlayerCleanupActionId actionId)
        {
            return actionId switch
            {
                PlayerCleanupActionId.ForwardFanLine => PlayerCleanupActionId.ForwardFanLine,
                _ => PlayerCleanupActionId.RadialRing,
            };
        }

        private static PlayerCleanupActionProfileBufferElement ResolveActionProfile(
            DynamicBuffer<PlayerCleanupActionProfileBufferElement> profiles,
            PlayerCleanupActionId actionId)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i].ActionId == actionId)
                {
                    var profile = profiles[i];
                    profile.TrashRange = math.max(0f, profile.TrashRange);
                    profile.TrashFanHalfAngleDeg = math.clamp(profile.TrashFanHalfAngleDeg, 0f, 180f);
                    profile.HazardRingRadius = math.max(0f, profile.HazardRingRadius);
                    profile.HazardRingWidth = math.max(0f, profile.HazardRingWidth);
                    profile.HazardLineLength = math.max(0f, profile.HazardLineLength);
                    profile.HazardLineHalfWidth = math.max(0f, profile.HazardLineHalfWidth);
                    return profile;
                }
            }

            if (actionId == PlayerCleanupActionId.ForwardFanLine)
            {
                return new PlayerCleanupActionProfileBufferElement
                {
                    ActionId = PlayerCleanupActionId.ForwardFanLine,
                    TrashRange = FallbackForwardTrashRange,
                    TrashFanHalfAngleDeg = FallbackForwardTrashHalfAngleDeg,
                    HazardRingRadius = 0f,
                    HazardRingWidth = 0f,
                    HazardLineLength = FallbackForwardHazardLineLength,
                    HazardLineHalfWidth = FallbackForwardHazardLineHalfWidth,
                };
            }

            return new PlayerCleanupActionProfileBufferElement
            {
                ActionId = PlayerCleanupActionId.RadialRing,
                TrashRange = FallbackRadialTrashRange,
                TrashFanHalfAngleDeg = 180f,
                HazardRingRadius = FallbackRadialHazardRingRadius,
                HazardRingWidth = FallbackRadialHazardRingWidth,
                HazardLineLength = 0f,
                HazardLineHalfWidth = 0f,
            };
        }

        private static float3 GetPlayerForward(in PlayerGoSyncComponent sync)
        {
            float3 forward = math.forward(sync.Rotation);
            forward.y = 0f;
            if (math.lengthsq(forward) < 1e-8f)
                return new float3(0f, 0f, 1f);
            return math.normalize(forward);
        }

        private static float ComputeSearchRange(in PlayerCleanupActionProfileBufferElement profile)
        {
            float radialOuter = GetHazardRingOuter(in profile);
            float forwardRange = math.max(profile.TrashRange, profile.HazardLineLength + profile.HazardLineHalfWidth);
            return math.max(0f, math.max(radialOuter, forwardRange));
        }

        private static float GetHazardRingInner(in PlayerCleanupActionProfileBufferElement profile)
        {
            float halfWidth = math.max(0f, profile.HazardRingWidth * 0.5f);
            return math.max(0f, profile.HazardRingRadius - halfWidth);
        }

        private static float GetHazardRingOuter(in PlayerCleanupActionProfileBufferElement profile)
        {
            float halfWidth = math.max(0f, profile.HazardRingWidth * 0.5f);
            float inner = math.max(0f, profile.HazardRingRadius - halfWidth);
            return math.max(inner, profile.HazardRingRadius + halfWidth);
        }

        [BurstCompile]
        private struct VacuumRequestFromCellMapJob : IJob
        {
            public Entity PlayerEntity;
            public float InvCellSize;
            public float SearchRange;
            public byte IsHazardCaptureActive;
            public PlayerCleanupActionId ActionId;
            public float3 PlayerForward;
            public float RadialTrashRange;
            public float RadialHazardRingInner;
            public float RadialHazardRingOuter;
            public float ForwardTrashRange;
            public float ForwardTrashCosHalfAngle;
            public float ForwardHazardLineLength;
            public float ForwardHazardLineHalfWidth;

            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;
            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<BulletCaptureRuleComponent> CaptureRuleLookup;
            [ReadOnly] public ComponentLookup<BulletRadiusComponent> BulletRadiusLookup;
            [ReadOnly] public ComponentLookup<BulletScoreValueComponent> ScoreValueLookup;
            [ReadOnly] public ComponentLookup<BulletSourceRefComponent> BulletSourceLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;
            public ComponentLookup<SourceSpawnComponent> SourceLookup;
            [ReadOnly] public ComponentLookup<SourceAnchorComponent> SourceAnchorLookup;
            [ReadOnly] public ComponentLookup<SourcePollutionGridComponent> SourcePollutionGridLookup;
            [ReadOnly] public BufferLookup<SourcePollutionCellBuffer> SourcePollutionCellLookup;
            public BufferLookup<SourcePollutionDropRequestBuffer> SourcePollutionDropRequestLookup;
            public BufferLookup<PlayerUiFeedbackEventBufferElement> UiFeedbackLookup;

            public NativeReference<int> NewlyRequested;
            public uint Frame;

            public void Execute()
            {
                if (!TxLookup.HasComponent(PlayerEntity))
                    return;

                float3 playerPos = TxLookup[PlayerEntity].Position;

                int2 center = SpatialHashUtility.ToCell(playerPos, InvCellSize);
                int cellRadius = (int)math.ceil(SearchRange * InvCellSize) + 1;

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

                            bool canCapture = EvaluateCapture(
                                captureRule,
                                distSq,
                                dxp,
                                dzp,
                                bulletRadius);

                            if (!canCapture) continue;

                            RequestLookup.SetComponentEnabled(bullet, true);
                            TryAccumulateDepletionAndPollution(bullet, p);
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

            private bool EvaluateCapture(
                BulletCaptureRuleId captureRule,
                float distSq,
                float dxp,
                float dzp,
                float bulletRadius)
            {
                if (captureRule == BulletCaptureRuleId.StandardCollectible)
                    return EvaluateTrashCapture(distSq, dxp, dzp, bulletRadius);

                if (captureRule == BulletCaptureRuleId.RiskTimedResolve && IsHazardCaptureActive != 0)
                    return EvaluateHazardCapture(distSq, dxp, dzp, bulletRadius);

                return false;
            }

            private bool EvaluateTrashCapture(float distSq, float dxp, float dzp, float bulletRadius)
            {
                if (ActionId == PlayerCleanupActionId.ForwardFanLine)
                {
                    float range = math.max(0f, ForwardTrashRange + bulletRadius);
                    if (distSq > range * range)
                        return false;

                    float lenSq = math.max(1e-8f, distSq);
                    float invLen = math.rsqrt(lenSq);
                    float dotForward = (dxp * PlayerForward.x + dzp * PlayerForward.z) * invLen;
                    float cosHalf = math.clamp(ForwardTrashCosHalfAngle, -1f, 1f);
                    return dotForward >= cosHalf;
                }

                float collectRange = math.max(0f, RadialTrashRange + bulletRadius);
                return distSq <= collectRange * collectRange;
            }

            private bool EvaluateHazardCapture(float distSq, float dxp, float dzp, float bulletRadius)
            {
                if (ActionId == PlayerCleanupActionId.ForwardFanLine)
                {
                    float forwardProjection = dxp * PlayerForward.x + dzp * PlayerForward.z;
                    if (forwardProjection < -bulletRadius)
                        return false;

                    float lineLength = math.max(0f, ForwardHazardLineLength + bulletRadius);
                    if (forwardProjection > lineLength)
                        return false;

                    float sideX = -PlayerForward.z;
                    float sideZ = PlayerForward.x;
                    float lateral = math.abs(dxp * sideX + dzp * sideZ);
                    float halfWidth = math.max(0f, ForwardHazardLineHalfWidth + bulletRadius);
                    return lateral <= halfWidth;
                }

                float inner = math.max(0f, RadialHazardRingInner - bulletRadius);
                float outer = math.max(inner, RadialHazardRingOuter + bulletRadius);
                return distSq >= inner * inner && distSq <= outer * outer;
            }

            private void TryAccumulateDepletionAndPollution(Entity bullet, in float3 bulletPos)
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
                {
                    source.State = nextStateByCount;
                    EmitSourceStateChanged(sourceEntity, nextStateByCount);
                }

                SourceLookup[sourceEntity] = source;
                AppendPollutionDropRequest(sourceEntity, bulletPos);
            }

            private void AppendPollutionDropRequest(Entity sourceEntity, in float3 bulletPos)
            {
                if (!SourcePollutionGridLookup.HasComponent(sourceEntity))
                    return;
                if (!SourceAnchorLookup.HasComponent(sourceEntity))
                    return;
                if (!SourcePollutionCellLookup.HasBuffer(sourceEntity))
                    return;
                if (!SourcePollutionDropRequestLookup.TryGetBuffer(sourceEntity, out var requests))
                    return;

                var grid = SourcePollutionGridLookup[sourceEntity];
                var cells = SourcePollutionCellLookup[sourceEntity];
                int cols = math.max(1, grid.Cols);
                int rows = math.max(1, grid.Rows);
                if (grid.InvCellSize <= 0f)
                    return;

                float2 local = new float2(
                    bulletPos.x - SourceAnchorLookup[sourceEntity].Position.x,
                    bulletPos.z - SourceAnchorLookup[sourceEntity].Position.z);
                float2 uv = (local + grid.HalfExtents) * grid.InvCellSize;
                int cellX = (int)math.floor(uv.x);
                int cellY = (int)math.floor(uv.y);
                if ((uint)cellX >= (uint)cols || (uint)cellY >= (uint)rows)
                    return;

                int cellIndex = cellY * cols + cellX;
                if ((uint)cellIndex >= (uint)cells.Length)
                    return;
                if (cells[cellIndex].IsValid == 0)
                    return;

                for (int i = 0; i < requests.Length; i++)
                {
                    var item = requests[i];
                    if (item.CellIndex != cellIndex)
                        continue;

                    item.Count = math.min(int.MaxValue, item.Count + 1);
                    requests[i] = item;
                    return;
                }

                requests.Add(new SourcePollutionDropRequestBuffer
                {
                    CellIndex = cellIndex,
                    Count = 1,
                });
            }

            private void EmitSourceStateChanged(Entity sourceEntity, SourceStateId nextState)
            {
                if (!UiFeedbackLookup.TryGetBuffer(PlayerEntity, out var uiFeedbackBuffer))
                    return;

                byte reason = nextState switch
                {
                    SourceStateId.Weakened => (byte)PlayerUiFeedbackReasonId.SourceToWeakened,
                    SourceStateId.Depleted => (byte)PlayerUiFeedbackReasonId.SourceToDepleted,
                    _ => (byte)PlayerUiFeedbackReasonId.None,
                };

                if (reason == (byte)PlayerUiFeedbackReasonId.None)
                    return;

                uiFeedbackBuffer.Add(new PlayerUiFeedbackEventBufferElement
                {
                    Type = PlayerUiFeedbackEventType.SourceStateChanged,
                    Reason = reason,
                    Value = 0,
                    RelatedEntity = sourceEntity,
                    Frame = Frame,
                    Sequence = (uint)uiFeedbackBuffer.Length,
                });
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
