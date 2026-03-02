using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
    /// - 현재는 소비 확장 전 단계이므로 clear만 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerUiFeedbackConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerUiFeedbackEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var uiBuffer = SystemAPI.GetBuffer<PlayerUiFeedbackEventBufferElement>(playerEntity);
            if (uiBuffer.Length == 0)
                return;

            for (int i = 0; i < uiBuffer.Length; i++)
            {
                var evt = uiBuffer[i];
                Debug.Log($"[PlayerUiFeedbackConsume] i={i}, type={evt.Type}, reason={evt.Reason}, value={evt.Value}, related={evt.RelatedEntity}, frame={evt.Frame}, seq={evt.Sequence}");
            }
            Debug.Log($"[PlayerUiFeedbackConsume] consumed={uiBuffer.Length}");

            uiBuffer.Clear();
        }
    }

    /// <summary>
    /// Impulse 피드백 소비 지점.
    /// - GO Bridge/컨트롤러 연동 시 이 시스템에서 소비한다.
    /// - 현재는 소비 확장 전 단계이므로 clear만 수행한다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct PlayerImpulseConsumeSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<PlayerImpulseEventBufferElement>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var impulseBuffer = SystemAPI.GetBuffer<PlayerImpulseEventBufferElement>(playerEntity);
            if (impulseBuffer.Length == 0)
                return;

            for (int i = 0; i < impulseBuffer.Length; i++)
            {
                var evt = impulseBuffer[i];
                Debug.Log($"[PlayerImpulseConsume] i={i}, reason={evt.Reason}, dir=({evt.DirX:0.###},{evt.DirZ:0.###}), magnitude={evt.Magnitude:0.###}, frame={evt.Frame}, seq={evt.Sequence}");
            }
            Debug.Log($"[PlayerImpulseConsume] consumed={impulseBuffer.Length}");

            impulseBuffer.Clear();
        }
    }
}
