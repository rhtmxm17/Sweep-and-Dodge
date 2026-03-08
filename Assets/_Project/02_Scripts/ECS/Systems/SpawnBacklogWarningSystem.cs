using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(SpawnRequestRoundRobinExecutionSystem))]
    public partial struct SpawnBacklogWarningSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();
            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            var metricsRW = SystemAPI.GetSingletonRW<SpawnBacklogMetricsComponent>();
            var metrics = metricsRW.ValueRO;

            if (SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState)
                && stageState.State != RunDirectorStageStateId.Running)
            {
                metrics.LastFrameBudgetUsed = 0;
                metrics.DeferredByBudget = 0;
                metrics.DeferredByPool = 0;
                metrics.LastFrameDroppedByCapacity = 0;
                metrics.LastFrameExpiredByAge = 0;
                metricsRW.ValueRW = metrics;
                return;
            }

            bool topologyInactive = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState)
                && topologyState.SelectedStageId <= 0
                && topologyState.AppliedStageId <= 0
                && topologyState.Ready == 0;
            if (topologyInactive)
            {
                metrics.LastFrameDroppedByCapacity = 0;
                metrics.LastFrameExpiredByAge = 0;
                metricsRW.ValueRW = metrics;
                return;
            }

            if (metrics.LastFrameDroppedByCapacity > 0 || metrics.LastFrameExpiredByAge > 0)
            {
                Debug.LogError(
                    $"[SpawnBacklog] hard-limit triggered frame={frame} dropped={metrics.LastFrameDroppedByCapacity} expired={metrics.LastFrameExpiredByAge}");
            }

            int pending = math.max(0, metrics.PendingCount);
            if (pending <= 0)
            {
                metricsRW.ValueRW = metrics;
                return;
            }

            int capacity = math.max(1, policy.MaxPendingCount);
            int percent = (int)math.clamp((long)pending * 100L / capacity, 0L, 100L);

            uint oldestAge = 0;
            foreach (var requests in SystemAPI.Query<DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                for (int i = 0; i < requests.Length; i++)
                {
                    var item = requests[i];
                    if (item.Count <= 0)
                        continue;

                    uint age = frame - item.OldestFrame;
                    oldestAge = math.max(oldestAge, age);
                }
            }

            bool nearAgeLimit = policy.MaxPendingAgeFrames > 0
                && oldestAge >= policy.MaxPendingAgeFrames - 1;
            int warningThreshold = math.max(0, policy.WarningBacklogPercent);
            int warningHighThreshold = math.max(warningThreshold, policy.WarningHighBacklogPercent);
            bool isWarning = percent >= warningThreshold;
            bool isWarningHigh = percent >= warningHighThreshold;
            bool shouldWarn = isWarning || nearAgeLimit;
            if (!shouldWarn)
            {
                metricsRW.ValueRW = metrics;
                return;
            }

            uint cooldown = math.max(1u, policy.WarningLogCooldownFrames);
            uint elapsedSinceLast = frame - metrics.LastWarningFrame;
            if (metrics.LastWarningFrame != 0 && elapsedSinceLast < cooldown)
            {
                metricsRW.ValueRW = metrics;
                return;
            }

            string level = nearAgeLimit
                ? "Critical"
                : (isWarningHigh ? "High" : "Warning");
            Debug.LogWarning(
                $"[SpawnBacklog:{level}] frame={frame} pending={pending}/{capacity} ({percent}%) oldestAge={oldestAge} budgetUsed={metrics.LastFrameBudgetUsed} deferredBudget={metrics.DeferredByBudget} deferredPool={metrics.DeferredByPool}");

            metrics.LastWarningFrame = frame;
            metricsRW.ValueRW = metrics;
        }
    }
}
