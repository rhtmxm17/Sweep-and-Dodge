using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateAfter(typeof(BulletFieldAreaUpdateSystem))]
    [UpdateBefore(typeof(SpawnRequestRoundRobinExecutionSystem))]
    public partial struct SecondarySpawnExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SecondarySpawnPolicyComponent>();
            state.RequireForUpdate<SecondarySpawnBacklogMetricsComponent>();
            state.RequireForUpdate<BulletSecondarySpawnChannelSingletonTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            bool hasStageState = SystemAPI.TryGetSingleton<RunDirectorStageStateComponent>(out var stageState);
            if (hasTopologyState
                && (!hasStageState || !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState)))
                return;

            if (!BulletFieldShared.IsInitialized)
                return;

            var poolDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);
            poolDeps.Complete();
            state.CompleteDependency();
            state.Dependency = default;

            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());
            var policy = SystemAPI.GetSingleton<SecondarySpawnPolicyComponent>();
            var metricsRW = SystemAPI.GetSingletonRW<SecondarySpawnBacklogMetricsComponent>();
            var metrics = metricsRW.ValueRO;
            metrics.LastFrameBudgetUsed = 0;
            metrics.DeferredByDelay = 0;
            metrics.DeferredByBudget = 0;
            metrics.DeferredByPool = 0;
            metrics.LastFrameDroppedByCapacity = 0;
            metrics.LastFrameExpiredByAge = 0;

            var channelEntity = SystemAPI.GetSingletonEntity<BulletSecondarySpawnChannelSingletonTag>();
            var requests = state.EntityManager.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            if (requests.Length <= 0)
            {
                metrics.PendingCount = 0;
                metricsRW.ValueRW = metrics;
                BulletFieldShared.PoolFence = state.Dependency;
                return;
            }

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false);
            var velLookup = SystemAPI.GetComponentLookup<BulletVelocityComponent>(false);
            var lifeLookup = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(false);
            var speedLookup = SystemAPI.GetComponentLookup<BulletSpeedComponent>(false);
            var lifeMaxLookup = SystemAPI.GetComponentLookup<BulletLifetimeMaxComponent>(false);
            var movementRuntimeLookup = SystemAPI.GetComponentLookup<BulletMovementRuntimeComponent>(false);
            var emissionProfileRefLookup = SystemAPI.GetComponentLookup<BulletEmissionProfileRefComponent>(false);
            var lifecycleRequestLookup = SystemAPI.GetComponentLookup<BulletLifecycleRequestComponent>(false);
            var lifecycleContactLookup = SystemAPI.GetComponentLookup<BulletLifecycleContactComponent>(false);
            var typeKeyLookup = SystemAPI.GetComponentLookup<BulletTypeKeyComponent>(false);
            var sourceRefLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(false);
            var lifeCycleLookup = SystemAPI.GetComponentLookup<BulletLifecycleTraceComponent>(false);
            var activeLookup = SystemAPI.GetComponentLookup<BulletActiveTag>(false);
            var despawnRequestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            var renderLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            var renderPartsLookup = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);

            txLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
            velLookup.Update(ref state);
            lifeLookup.Update(ref state);
            speedLookup.Update(ref state);
            lifeMaxLookup.Update(ref state);
            movementRuntimeLookup.Update(ref state);
            emissionProfileRefLookup.Update(ref state);
            lifecycleRequestLookup.Update(ref state);
            lifecycleContactLookup.Update(ref state);
            typeKeyLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
            lifeCycleLookup.Update(ref state);
            activeLookup.Update(ref state);
            despawnRequestLookup.Update(ref state);
            renderLookup.Update(ref state);
            renderPartsLookup.Update(ref state);
            parentLookup.Update(ref state);
            activeCountLookup.Update(ref state);

            int pending = 0;
            int expiredByAge = 0;
            int droppedByCapacity = 0;
            PruneExpiredAndClampRequests(
                requests,
                frame,
                policy.MaxPendingCount,
                policy.MaxPendingAgeFrames,
                ref pending,
                ref expiredByAge,
                ref droppedByCapacity);

            if (expiredByAge > 0)
                metrics.ExpiredByAge = SpawnRequestCommonUtility.SafeAdd(metrics.ExpiredByAge, expiredByAge);
            if (droppedByCapacity > 0)
                metrics.DroppedByCapacity = SpawnRequestCommonUtility.SafeAdd(metrics.DroppedByCapacity, droppedByCapacity);
            metrics.LastFrameExpiredByAge = expiredByAge;
            metrics.LastFrameDroppedByCapacity = droppedByCapacity;

            int remainingBudget = math.max(0, policy.BudgetPerFrame);
            int budgetUsed = 0;
            for (int i = 0; i < requests.Length && remainingBudget > 0; i++)
            {
                var item = requests[i];
                if (item.Count <= 0)
                    continue;
                if (frame < item.ReadyFrame)
                    continue;

                int availablePoolCount = SpawnRequestCommonUtility.CountFreeByKey(ref BulletFieldShared.FreeByKey, item.BulletTypeKey);
                if (availablePoolCount <= 0)
                    continue;

                int spawnCount = math.min(item.Count, math.min(remainingBudget, availablePoolCount));
                int spawned = TrySpawnSecondaryRequest(
                    in item,
                    spawnCount,
                    frame,
                    ref txLookup,
                    ref localToWorldLookup,
                    ref velLookup,
                    ref lifeLookup,
                    ref speedLookup,
                    ref lifeMaxLookup,
                    ref movementRuntimeLookup,
                    ref emissionProfileRefLookup,
                    ref lifecycleRequestLookup,
                    ref lifecycleContactLookup,
                    ref typeKeyLookup,
                    ref sourceRefLookup,
                    ref lifeCycleLookup,
                    ref activeLookup,
                    ref despawnRequestLookup,
                    ref renderPartsLookup,
                    ref renderLookup,
                    ref parentLookup,
                    ref activeCountLookup);
                if (spawned <= 0)
                    continue;

                item.Count = math.max(0, item.Count - spawned);
                item.Sequence += (uint)spawned;
                requests[i] = item;

                pending = math.max(0, pending - spawned);
                remainingBudget = math.max(0, remainingBudget - spawned);
                budgetUsed = SpawnRequestCommonUtility.SafeAdd(budgetUsed, spawned);
            }

            CompactRequestBuffer(requests);

            RecountPendingRequests(requests, frame, out int pendingCount, out int delayedPendingCount);
            int readyPendingCount = math.max(0, pendingCount - delayedPendingCount);

            metrics.PendingCount = pendingCount;
            metrics.LastFrameBudgetUsed = budgetUsed;
            metrics.DeferredByDelay = delayedPendingCount;
            if (readyPendingCount > 0)
            {
                if (remainingBudget <= 0)
                    metrics.DeferredByBudget = readyPendingCount;
                else
                    metrics.DeferredByPool = readyPendingCount;
            }

            metricsRW.ValueRW = metrics;
            BulletFieldShared.PoolFence = state.Dependency;
        }

        private static void PruneExpiredAndClampRequests(
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> requests,
            uint frame,
            int maxPendingCount,
            uint maxPendingAgeFrames,
            ref int pending,
            ref int expiredByAge,
            ref int droppedByCapacity)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                item.Count = math.max(0, item.Count);
                if (item.Count <= 0)
                {
                    requests.RemoveAt(i);
                    continue;
                }

                if (maxPendingAgeFrames > 0)
                {
                    uint age = frame - item.OldestFrame;
                    if (age > maxPendingAgeFrames)
                    {
                        expiredByAge = SpawnRequestCommonUtility.SafeAdd(expiredByAge, item.Count);
                        requests.RemoveAt(i);
                        continue;
                    }
                }

                requests[i] = item;
                pending = SpawnRequestCommonUtility.SafeAdd(pending, item.Count);
            }

            int safeMaxPending = math.max(0, maxPendingCount);
            if (safeMaxPending <= 0 || pending <= safeMaxPending)
                return;

            int overflow = pending - safeMaxPending;
            for (int i = requests.Length - 1; i >= 0 && overflow > 0; i--)
            {
                var item = requests[i];
                int removed = math.min(item.Count, overflow);
                item.Count -= removed;
                droppedByCapacity = SpawnRequestCommonUtility.SafeAdd(droppedByCapacity, removed);
                overflow -= removed;
                if (item.Count <= 0)
                    requests.RemoveAt(i);
                else
                    requests[i] = item;
            }

            pending = safeMaxPending;
        }

        private static void CompactRequestBuffer(DynamicBuffer<BulletSecondarySpawnRequestBuffer> requests)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                if (requests[i].Count > 0)
                    continue;

                requests.RemoveAt(i);
            }
        }

        private static void RecountPendingRequests(
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> requests,
            uint frame,
            out int pendingCount,
            out int delayedPendingCount)
        {
            pendingCount = 0;
            delayedPendingCount = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                int count = math.max(0, requests[i].Count);
                if (count <= 0)
                    continue;

                pendingCount = SpawnRequestCommonUtility.SafeAdd(pendingCount, count);
                if (frame < requests[i].ReadyFrame)
                    delayedPendingCount = SpawnRequestCommonUtility.SafeAdd(delayedPendingCount, count);
            }
        }

        private static int TrySpawnSecondaryRequest(
            in BulletSecondarySpawnRequestBuffer request,
            int spawnCount,
            uint frame,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<LocalToWorld> localToWorldLookup,
            ref ComponentLookup<BulletVelocityComponent> velLookup,
            ref ComponentLookup<BulletLifetimeComponent> lifeLookup,
            ref ComponentLookup<BulletSpeedComponent> speedLookup,
            ref ComponentLookup<BulletLifetimeMaxComponent> lifeMaxLookup,
            ref ComponentLookup<BulletMovementRuntimeComponent> movementRuntimeLookup,
            ref ComponentLookup<BulletEmissionProfileRefComponent> emissionProfileRefLookup,
            ref ComponentLookup<BulletLifecycleRequestComponent> lifecycleRequestLookup,
            ref ComponentLookup<BulletLifecycleContactComponent> lifecycleContactLookup,
            ref ComponentLookup<BulletTypeKeyComponent> typeKeyLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletLifecycleTraceComponent> lifeCycleLookup,
            ref ComponentLookup<BulletActiveTag> activeLookup,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref BufferLookup<EntityRenderElementBuffer> renderPartsLookup,
            ref ComponentLookup<MaterialMeshInfo> renderLookup,
            ref ComponentLookup<Parent> parentLookup,
            ref BufferLookup<SourceActiveBulletCountBuffer> activeCountLookup)
        {
            int spawned = 0;
            int patternCount = math.max(1, request.Count);
            var runtimeTuning = default(SpawnedBulletRuntimeTuning);
            for (int slotIndex = 0; slotIndex < spawnCount; slotIndex++)
            {
                if (!SpawnRequestCommonUtility.TryDequeueByKey(ref BulletFieldShared.FreeByKey, request.BulletTypeKey, out var bulletEntity))
                    break;

                ResolveSpawnPose(
                    in request,
                    patternCount,
                    request.Sequence + (uint)slotIndex,
                    out var spawnPosition,
                    out var direction);
                SpawnRequestCommonUtility.ApplySpawnedBulletState(
                    bulletEntity,
                    request.SourceEntity,
                    request.BulletTypeKey,
                    in runtimeTuning,
                    spawnPosition,
                    direction,
                    frame,
                    ref txLookup,
                    ref localToWorldLookup,
                    ref velLookup,
                    ref lifeLookup,
                    ref speedLookup,
                    ref lifeMaxLookup,
                    ref movementRuntimeLookup,
                    ref emissionProfileRefLookup,
                    ref lifecycleRequestLookup,
                    ref lifecycleContactLookup,
                    ref typeKeyLookup,
                    ref sourceRefLookup,
                    ref lifeCycleLookup,
                    ref activeLookup,
                    ref despawnRequestLookup,
                    ref renderPartsLookup,
                    ref renderLookup,
                    ref parentLookup);
                spawned++;
            }

            if (spawned > 0
                && request.SourceEntity != Entity.Null
                && activeCountLookup.TryGetBuffer(request.SourceEntity, out var activeCounts))
            {
                SpawnRequestCommonUtility.IncrementActiveCount(activeCounts, request.BulletTypeKey, spawned);
            }

            return spawned;
        }

        private static void ResolveSpawnPose(
            in BulletSecondarySpawnRequestBuffer request,
            int patternCount,
            uint sequence,
            out float3 spawnPosition,
            out float2 direction)
        {
            float2 baseDirection = math.normalizesafe(request.BaseDirection, new float2(1f, 0f));
            float baseAngle = math.atan2(baseDirection.y, baseDirection.x);
            float radius = math.max(0f, request.SpawnRadius);
            float spreadRad = math.radians(request.SpreadAngleDeg);
            int patternIndex = math.max(0, (int)(sequence % (uint)math.max(1, patternCount)));

            switch (request.Shape)
            {
                case BulletSecondarySpawnShapeId.ForwardSpread:
                {
                    float t = patternCount <= 1 ? 0.5f : patternIndex / (float)(patternCount - 1);
                    float angle = baseAngle + math.lerp(-spreadRad * 0.5f, spreadRad * 0.5f, t);
                    direction = new float2(math.cos(angle), math.sin(angle));
                    spawnPosition = request.OriginPosition + new float3(direction.x * radius, 0f, direction.y * radius);
                    break;
                }
                case BulletSecondarySpawnShapeId.PointBurst:
                {
                    float angle = baseAngle + (math.PI * 2f * patternIndex) / math.max(1, patternCount);
                    direction = new float2(math.cos(angle), math.sin(angle));
                    spawnPosition = request.OriginPosition + new float3(direction.x * radius, 0f, direction.y * radius);
                    break;
                }
                case BulletSecondarySpawnShapeId.SingleForward:
                default:
                    direction = baseDirection;
                    spawnPosition = request.OriginPosition + new float3(direction.x * radius, 0f, direction.y * radius);
                    break;
            }
        }
    }
}
