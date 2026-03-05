using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// 공통 전투 이벤트 채널 소비/집계.
    /// - Hit/Collect/Cleanup 프레임 집계를 누적한다.
    /// - Hit는 기존 UI 피드백 경로로 브리지한다.
    /// - 채널 clear는 본 시스템 단일 책임으로 유지한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositExecutionSystem))]
    [UpdateBefore(typeof(PlayerUiFeedbackConsumeSystem))]
    public partial struct CombatEventChannelConsumeSystem : ISystem
    {
        private EntityQuery _combatEventChannelQuery;
        private EntityQuery _playerQuery;

        public void OnCreate(ref SystemState state)
        {
            _combatEventChannelQuery = SystemAPI.QueryBuilder()
                .WithAll<CombatEventChannelSingletonTag>()
                .WithAll<CombatEventMetricsComponent>()
                .WithAll<CombatEventBufferElement>()
                .Build();
            _playerQuery = SystemAPI.QueryBuilder()
                .WithAll<PlayerTag>()
                .Build();
            state.RequireForUpdate(_combatEventChannelQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            Entity channelEntity = ResolveFirstEntity(ref _combatEventChannelQuery);
            if (channelEntity == Entity.Null)
                return;

            var metricsRW = SystemAPI.GetComponentRW<CombatEventMetricsComponent>(channelEntity);
            var combatBuffer = SystemAPI.GetBuffer<CombatEventBufferElement>(channelEntity);

            var metrics = metricsRW.ValueRO;
            metrics.LastFrameHitCount = 0;
            metrics.LastFrameCollectCount = 0;
            metrics.LastFrameCleanupCount = 0;
            metrics.LastFrameHitValue = 0;
            metrics.LastFrameCollectValue = 0;
            metrics.LastFrameCleanupValue = 0;

            Entity playerEntity = ResolveFirstEntity(ref _playerQuery);
            bool hasUiFeedbackBuffer = playerEntity != Entity.Null
                && SystemAPI.HasBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
            DynamicBuffer<PlayerUiFeedbackEventBufferElement> uiFeedbackBuffer = default;
            if (hasUiFeedbackBuffer)
                uiFeedbackBuffer = SystemAPI.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);

            for (int i = 0; i < combatBuffer.Length; i++)
            {
                var evt = combatBuffer[i];
                int safeCount = math.max(0, evt.Count);
                int safeValue = math.max(0, evt.Value);
                if (safeCount <= 0 && safeValue <= 0)
                    continue;

                switch (evt.Type)
                {
                    case CombatEventTypeId.Hit:
                        metrics.LastFrameHitCount += safeCount;
                        metrics.LastFrameHitValue += safeValue;
                        metrics.TotalHitCount += safeCount;
                        metrics.TotalHitValue += safeValue;
                        if (hasUiFeedbackBuffer)
                        {
                            uiFeedbackBuffer.Add(new PlayerUiFeedbackEventBufferElement
                            {
                                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                                Value = safeValue,
                                RelatedEntity = evt.SourceEntity != Entity.Null
                                    ? evt.SourceEntity
                                    : evt.RelatedEntity,
                                Frame = evt.Frame,
                                Sequence = (uint)uiFeedbackBuffer.Length,
                            });
                        }
                        break;
                    case CombatEventTypeId.Collect:
                        metrics.LastFrameCollectCount += safeCount;
                        metrics.LastFrameCollectValue += safeValue;
                        metrics.TotalCollectCount += safeCount;
                        metrics.TotalCollectValue += safeValue;
                        break;
                    case CombatEventTypeId.Cleanup:
                        metrics.LastFrameCleanupCount += safeCount;
                        metrics.LastFrameCleanupValue += safeValue;
                        metrics.TotalCleanupCount += safeCount;
                        metrics.TotalCleanupValue += safeValue;
                        break;
                }
            }

            if (combatBuffer.Length > 0)
                metrics.LastConsumedFrame = combatBuffer[combatBuffer.Length - 1].Frame;

            metricsRW.ValueRW = metrics;
            combatBuffer.Clear();
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

    /// <summary>
    /// UI/HUD/VFX 피드백 소비 지점.
    /// - 실제 브리지 적용 위치를 이 시스템으로 고정한다.
    /// - 소비 규칙(dedupe/cooldown) 적용 후 표현 스냅샷 단일 writer로 갱신한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerUiFeedbackConsumeSystem : ISystem
    {
        private const float UiFeedbackDurationSec = 1.25f;
        private const float GeneralCooldownSec = 0.15f;
        private const float HitCooldownSec = 0.10f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerUiFeedbackEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var uiBuffer = SystemAPI.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
            if (!SystemAPI.HasComponent<PlayerUiFeedbackPresentationSnapshotComponent>(playerEntity))
            {
                if (uiBuffer.Length > 0)
                    uiBuffer.Clear();
                return;
            }

            var snapshotRW = SystemAPI.GetComponentRW<PlayerUiFeedbackPresentationSnapshotComponent>(playerEntity);
            var snapshot = snapshotRW.ValueRO;
            float dt = 0f;
            if (SystemAPI.TryGetSingleton<FixedTickStepRuntimeComponent>(out var fixedTickRuntime)
                && FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float resolvedDt))
            {
                dt = math.max(0f, resolvedDt);
            }
            snapshot.ClockSec = math.max(0f, snapshot.ClockSec + dt);
            snapshot.RemainingSec = math.max(0f, snapshot.RemainingSec - dt);

            if (uiBuffer.Length > 0)
            {
                int selectedIndex = -1;
                int selectedPriority = int.MinValue;

                for (int i = 0; i < uiBuffer.Length; i++)
                {
                    if (!IsEventApproved(uiBuffer, i, in snapshot))
                        continue;

                    var evt = uiBuffer[i];
                    int priority = GetFeedbackPriority(evt.Type);
                    if (selectedIndex >= 0 && priority < selectedPriority)
                        continue;
                    if (selectedIndex >= 0 && priority == selectedPriority && evt.Sequence < uiBuffer[selectedIndex].Sequence)
                        continue;

                    selectedIndex = i;
                    selectedPriority = priority;
                }

                if (selectedIndex >= 0)
                {
                    var selected = uiBuffer[selectedIndex];
                    snapshot.Version = snapshot.Version >= uint.MaxValue ? 1u : snapshot.Version + 1u;
                    snapshot.Type = selected.Type;
                    snapshot.Reason = selected.Reason;
                    snapshot.Value = math.max(0, selected.Value);
                    snapshot.RelatedEntity = selected.RelatedEntity;
                    snapshot.Frame = selected.Frame;
                    snapshot.RemainingSec = UiFeedbackDurationSec;
                    SetNextCooldown(ref snapshot, selected.Type, snapshot.ClockSec);
                }

                uiBuffer.Clear();
            }

            snapshotRW.ValueRW = snapshot;
        }

        private static bool IsEventApproved(
            DynamicBuffer<PlayerUiFeedbackEventBufferElement> buffer,
            int index,
            in PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            var evt = buffer[index];
            if (evt.Type == PlayerUiFeedbackEventType.None)
                return false;

            if (IsDuplicateInBuffer(buffer, index))
                return false;

            return IsCooldownReady(evt.Type, snapshot.ClockSec, in snapshot);
        }

        private static bool IsDuplicateInBuffer(DynamicBuffer<PlayerUiFeedbackEventBufferElement> buffer, int index)
        {
            var evt = buffer[index];
            for (int i = 0; i < index; i++)
            {
                var prev = buffer[i];
                if (prev.Frame != evt.Frame)
                    continue;
                if (prev.Type != evt.Type)
                    continue;
                if (prev.RelatedEntity != evt.RelatedEntity)
                    continue;
                return true;
            }

            return false;
        }

        private static bool IsCooldownReady(
            PlayerUiFeedbackEventType type,
            float clockSec,
            in PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            float nextAllowedSec = type switch
            {
                PlayerUiFeedbackEventType.VacuumStartBlocked => snapshot.NextAllowedVacuumBlockedSec,
                PlayerUiFeedbackEventType.SourceStateChanged => snapshot.NextAllowedSourceStateChangedSec,
                PlayerUiFeedbackEventType.HazardCaptured => snapshot.NextAllowedHazardCapturedSec,
                PlayerUiFeedbackEventType.HazardRemoved => snapshot.NextAllowedHazardRemovedSec,
                PlayerUiFeedbackEventType.PlayerHazardHit => snapshot.NextAllowedHitSec,
                _ => 0f,
            };

            return clockSec >= nextAllowedSec;
        }

        private static void SetNextCooldown(
            ref PlayerUiFeedbackPresentationSnapshotComponent snapshot,
            PlayerUiFeedbackEventType type,
            float clockSec)
        {
            float nextAllowedSec = clockSec + (type == PlayerUiFeedbackEventType.PlayerHazardHit ? HitCooldownSec : GeneralCooldownSec);
            switch (type)
            {
                case PlayerUiFeedbackEventType.VacuumStartBlocked:
                    snapshot.NextAllowedVacuumBlockedSec = nextAllowedSec;
                    break;
                case PlayerUiFeedbackEventType.SourceStateChanged:
                    snapshot.NextAllowedSourceStateChangedSec = nextAllowedSec;
                    break;
                case PlayerUiFeedbackEventType.HazardCaptured:
                    snapshot.NextAllowedHazardCapturedSec = nextAllowedSec;
                    break;
                case PlayerUiFeedbackEventType.HazardRemoved:
                    snapshot.NextAllowedHazardRemovedSec = nextAllowedSec;
                    break;
                case PlayerUiFeedbackEventType.PlayerHazardHit:
                    snapshot.NextAllowedHitSec = nextAllowedSec;
                    break;
            }
        }

        private static int GetFeedbackPriority(PlayerUiFeedbackEventType type)
        {
            return type switch
            {
                PlayerUiFeedbackEventType.PlayerHazardHit => 500,
                PlayerUiFeedbackEventType.HazardCaptured => 400,
                PlayerUiFeedbackEventType.HazardRemoved => 300,
                PlayerUiFeedbackEventType.SourceStateChanged => 200,
                PlayerUiFeedbackEventType.VacuumStartBlocked => 100,
                _ => 0,
            };
        }
    }

    /// <summary>
    /// Impulse 피드백 소비 지점.
    /// - GO Bridge/컨트롤러 연동 시 이 시스템에서 소비한다.
    /// - 동일 프레임 다건 이벤트를 합산해 표현 스냅샷 단일 writer로 갱신한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerImpulseConsumeSystem : ISystem
    {
        // 원칙적으로는 Request+iFrame 규칙으로 동프레임 다건 hit가 발생하지 않는다.
        // 다만 예외 입력/회귀에 대비해 표현 임펄스는 프레임 상한으로 방어한다.
        private const float MaxMergedImpulseMagnitudePerFrame = 1.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerImpulseEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var impulseBuffer = SystemAPI.GetBuffer<PlayerImpulseEventBufferElement>(playerEntity);
            if (!SystemAPI.HasComponent<PlayerImpulsePresentationSnapshotComponent>(playerEntity))
            {
                if (impulseBuffer.Length > 0)
                    impulseBuffer.Clear();
                return;
            }

            var snapshotRW = SystemAPI.GetComponentRW<PlayerImpulsePresentationSnapshotComponent>(playerEntity);
            var snapshot = snapshotRW.ValueRO;

            if (impulseBuffer.Length > 0)
            {
                float2 mergedVector = float2.zero;
                uint latestFrame = 0u;
                byte latestReason = (byte)PlayerImpulseReasonId.None;
                int mergedCount = 0;

                for (int i = 0; i < impulseBuffer.Length; i++)
                {
                    var evt = impulseBuffer[i];
                    float magnitude = math.max(0f, evt.Magnitude);
                    if (magnitude <= 0f)
                        continue;

                    float2 dir = math.normalizesafe(new float2(evt.DirX, evt.DirZ), float2.zero);
                    if (math.lengthsq(dir) <= 1e-6f)
                        continue;

                    mergedVector += dir * magnitude;
                    latestFrame = math.max(latestFrame, evt.Frame);
                    latestReason = evt.Reason;
                    mergedCount++;
                }

                if (mergedCount > 0)
                {
                    float mergedMagnitude = math.length(mergedVector);
                    if (mergedMagnitude > 1e-6f)
                    {
                        float2 mergedDir = mergedVector / mergedMagnitude;
                        mergedMagnitude = math.min(mergedMagnitude, MaxMergedImpulseMagnitudePerFrame);
                        snapshot.Version = snapshot.Version >= uint.MaxValue ? 1u : snapshot.Version + 1u;
                        snapshot.Reason = latestReason;
                        snapshot.DirX = mergedDir.x;
                        snapshot.DirZ = mergedDir.y;
                        snapshot.Magnitude = mergedMagnitude;
                        snapshot.Frame = latestFrame;
                        snapshot.MergedEventCount = mergedCount;
                    }
                }

                impulseBuffer.Clear();
            }

            snapshotRW.ValueRW = snapshot;
        }
    }

}
