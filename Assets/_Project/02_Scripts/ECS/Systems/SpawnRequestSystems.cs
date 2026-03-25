using Unity.Burst;
using Unity.Collections;
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
    public partial struct SpawnRequestRoundRobinExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();
            state.RequireForUpdate<SpawnBudgetCursorComponent>();
            state.RequireForUpdate<SpawnRunSeedComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceAnchorComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<Shape2DComponent>();
            state.RequireForUpdate<SourceSpawnRuntimeComponent>();
            state.RequireForUpdate<SourceActiveBulletCountBuffer>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
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

            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;
            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();
            uint runSeed = math.max(1u, SystemAPI.GetSingleton<SpawnRunSeedComponent>().Value);

            var metricsRW = SystemAPI.GetSingletonRW<SpawnBacklogMetricsComponent>();
            var cursorRW = SystemAPI.GetSingletonRW<SpawnBudgetCursorComponent>();
            var metrics = metricsRW.ValueRO;
            metrics.LastFrameBudgetUsed = 0;
            metrics.DeferredByBudget = 0;
            metrics.DeferredByPool = 0;
            metrics.LastFrameExpiredByAge = 0;

            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(false);
            var velLookup = SystemAPI.GetComponentLookup<BulletVelocityComponent>(false);
            var lifeLookup = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(false);
            var speedLookup = SystemAPI.GetComponentLookup<BulletSpeedComponent>(true);
            var lifeMaxLookup = SystemAPI.GetComponentLookup<BulletLifetimeMaxComponent>(true);
            var typeKeyLookup = SystemAPI.GetComponentLookup<BulletTypeKeyComponent>(false);
            var sourceRefLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(false);
            var lifeCycleLookup = SystemAPI.GetComponentLookup<BulletLifecycleTraceComponent>(false);
            var activeLookup = SystemAPI.GetComponentLookup<BulletActiveTag>(false);
            var despawnRequestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            var renderLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            var renderPartsLookup = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
            var sourceAnchorLookup = SystemAPI.GetComponentLookup<SourceAnchorComponent>(true);
            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var sourceRuntimeLookup = SystemAPI.GetComponentLookup<SourceSpawnRuntimeComponent>(false);
            var pollutionConfigLookup = SystemAPI.GetComponentLookup<SourcePollutionConfigComponent>(true);
            var pollutionGridLookup = SystemAPI.GetComponentLookup<SourcePollutionGridComponent>(true);
            var pollutionCellsLookup = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(true);
            var pollutionValidCellIndicesLookup = SystemAPI.GetBufferLookup<SourcePollutionValidCellIndexBuffer>(true);

            requestLookup.Update(ref state);
            activeCountLookup.Update(ref state);
            txLookup.Update(ref state);
            localToWorldLookup.Update(ref state);
            velLookup.Update(ref state);
            lifeLookup.Update(ref state);
            speedLookup.Update(ref state);
            lifeMaxLookup.Update(ref state);
            typeKeyLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
            lifeCycleLookup.Update(ref state);
            activeLookup.Update(ref state);
            despawnRequestLookup.Update(ref state);
            renderLookup.Update(ref state);
            renderPartsLookup.Update(ref state);
            parentLookup.Update(ref state);
            sourceAnchorLookup.Update(ref state);
            stableIdLookup.Update(ref state);
            sourceRuntimeLookup.Update(ref state);
            pollutionConfigLookup.Update(ref state);
            pollutionGridLookup.Update(ref state);
            pollutionCellsLookup.Update(ref state);
            pollutionValidCellIndicesLookup.Update(ref state);

            using var sourceEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<SourceSpawnComponent>>()
                         .WithAll<SourceStableIdComponent>()
                         .WithAll<SourceSpawnRequestBuffer>()
                         .WithEntityAccess())
            {
                sourceEntities.Add(entity);
            }

            if (sourceEntities.Length <= 0)
            {
                metrics.PendingCount = 0;
                metricsRW.ValueRW = metrics;
                return;
            }

            int pending = 0;
            int expiredByAge = 0;
            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                if (!requestLookup.TryGetBuffer(sourceEntity, out var requests))
                    continue;

                PruneExpiredAndCompactRequests(requests, frame, policy.MaxPendingAgeFrames, deltaTime, ref pending, ref expiredByAge);
            }

            if (expiredByAge > 0)
                metrics.ExpiredByAge = SpawnRequestCommonUtility.SafeAdd(metrics.ExpiredByAge, expiredByAge);
            metrics.LastFrameExpiredByAge = expiredByAge;

            int remainingBudget = math.max(0, policy.BudgetPerFrame);
            int sourceCount = sourceEntities.Length;
            int cursorIndex = math.clamp(cursorRW.ValueRO.SourceStartIndex, 0, math.max(0, sourceCount - 1));
            int budgetUsed = 0;
            int noSpawnPasses = 0;
            bool hasPlayer = false;
            float3 playerPosition = float3.zero;
            foreach (var tx in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                hasPlayer = true;
                playerPosition = tx.ValueRO.Position;
                break;
            }

            while (remainingBudget > 0 && pending > 0)
            {
                bool attempted = false;
                bool spawned = false;
                int chosenSourceIndex = -1;

                for (int s = 0; s < sourceCount; s++)
                {
                    int sourceIndex = (cursorIndex + s) % sourceCount;
                    var sourceEntity = sourceEntities[sourceIndex];
                    if (!requestLookup.TryGetBuffer(sourceEntity, out var requests))
                        continue;

                    int pendingRequestIndex = FindFirstPendingRequestIndex(requests);
                    if (pendingRequestIndex < 0)
                        continue;

                    attempted = true;
                    int requestIndex = FindFirstSpawnableRequestIndex(
                        requests,
                        ref BulletFieldShared.FreeByKey,
                        remainingBudget);
                    if (requestIndex < 0)
                        continue;

                    uint sourceStableId = math.max(1u, stableIdLookup[sourceEntity].Value);
                    var requestItem = requests[requestIndex];
                    spawned = TrySpawnFromRequest(
                        sourceEntity,
                        runSeed,
                        sourceStableId,
                        ref requestItem,
                        ref sourceRuntimeLookup,
                        ref sourceAnchorLookup,
                        ref pollutionConfigLookup,
                        ref pollutionGridLookup,
                        ref pollutionCellsLookup,
                        ref pollutionValidCellIndicesLookup,
                        ref txLookup,
                        ref localToWorldLookup,
                        ref velLookup,
                        ref lifeLookup,
                        ref speedLookup,
                        ref lifeMaxLookup,
                        ref typeKeyLookup,
                        ref sourceRefLookup,
                        ref lifeCycleLookup,
                        ref activeLookup,
                        ref despawnRequestLookup,
                        ref renderPartsLookup,
                        ref renderLookup,
                        ref parentLookup,
                        ref activeCountLookup,
                        hasPlayer,
                        playerPosition,
                        frame,
                        out int consumedCount,
                        out uint sequenceAdvance);

                    if (spawned)
                    {
                        requestItem.Count = math.max(0, requestItem.Count - consumedCount);
                        requestItem.SpawnSequence = requestItem.SpawnSequence + sequenceAdvance;
                        if (requestItem.Count <= 0)
                        {
                            requestItem.OldestFrame = frame;
                        }
                        else if (requestItem.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed)
                        {
                            // Timed 이벤트는 정상적인 간격 분할 소비가 진행 중이면 age 기준을 갱신한다.
                            requestItem.OldestFrame = frame;
                        }
                        requests[requestIndex] = requestItem;
                        pending = math.max(0, pending - consumedCount);
                        remainingBudget = math.max(0, remainingBudget - consumedCount);
                        budgetUsed = SpawnRequestCommonUtility.SafeAdd(budgetUsed, consumedCount);
                    }
                    else
                    {
                        requests[requestIndex] = requestItem;
                    }

                    chosenSourceIndex = sourceIndex;
                    break;
                }

                if (!attempted)
                    break;

                if (chosenSourceIndex >= 0)
                    cursorIndex = (chosenSourceIndex + 1) % sourceCount;

                if (spawned)
                {
                    noSpawnPasses = 0;
                    continue;
                }

                if (chosenSourceIndex < 0)
                    break;

                noSpawnPasses++;
                if (noSpawnPasses >= sourceCount)
                    break;
            }

            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                if (!requestLookup.TryGetBuffer(sourceEntity, out var requests))
                    continue;

                SpawnRequestCommonUtility.CompactRequestBuffer(requests);
            }

            metrics.PendingCount = pending;
            metrics.LastFrameBudgetUsed = budgetUsed;
            if (pending > 0)
            {
                bool deferredByBudget = remainingBudget <= 0
                    || HasBudgetBlockedPendingRequest(
                        sourceEntities,
                        ref requestLookup,
                        ref BulletFieldShared.FreeByKey,
                        remainingBudget);
                if (deferredByBudget)
                    metrics.DeferredByBudget = pending;
                else
                    metrics.DeferredByPool = pending;
            }

            metricsRW.ValueRW = metrics;
            cursorRW.ValueRW = new SpawnBudgetCursorComponent
            {
                SourceStartIndex = cursorIndex
            };
            BulletFieldShared.PoolFence = state.Dependency;
        }

        private static bool HasBudgetBlockedPendingRequest(
            NativeList<Entity> sourceEntities,
            ref BufferLookup<SourceSpawnRequestBuffer> requestLookup,
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int remainingBudget)
        {
            if (remainingBudget <= 0)
                return true;

            for (int s = 0; s < sourceEntities.Length; s++)
            {
                var sourceEntity = sourceEntities[s];
                if (!requestLookup.TryGetBuffer(sourceEntity, out var requests))
                    continue;

                for (int i = 0; i < requests.Length; i++)
                {
                    var item = requests[i];
                    if (item.Count <= 0)
                        continue;
                    if (!IsRequestReadyForConsume(in item))
                        continue;

                    int requiredCount = ResolveRequestConsumeUnitCount(in item);
                    if (item.Count < requiredCount)
                        continue;

                    bool hasPoolCapacity = requiredCount <= 1
                        ? freeByKey.ContainsKey(item.BulletTypeKey)
                        : CountFreeByKey(ref freeByKey, item.BulletTypeKey) >= requiredCount;
                    if (!hasPoolCapacity)
                        continue;

                    if (remainingBudget < requiredCount)
                        return true;
                }
            }

            return false;
        }

        private static void PruneExpiredAndCompactRequests(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            uint frame,
            uint maxAge,
            float deltaTime,
            ref int pending,
            ref int expiredByAge)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                item.Count = math.max(0, item.Count);
                if (item.Count <= 0)
                {
                    requests.RemoveAtSwapBack(i);
                    continue;
                }

                if (maxAge > 0)
                {
                    uint age = frame - item.OldestFrame;
                    if (age > maxAge)
                    {
                        expiredByAge = SpawnRequestCommonUtility.SafeAdd(expiredByAge, item.Count);
                        requests.RemoveAtSwapBack(i);
                        continue;
                    }
                }

                if (item.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed)
                    item.EventShotElapsedSec = math.max(0f, item.EventShotElapsedSec + deltaTime);

                pending = SpawnRequestCommonUtility.SafeAdd(pending, item.Count);
                requests[i] = item;
            }
        }

        private static int FindFirstPendingRequestIndex(DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                if (requests[i].Count > 0)
                    return i;
            }

            return -1;
        }

        private static int FindFirstSpawnableRequestIndex(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int remainingBudget)
        {
            int bestIndex = -1;
            int bestLanePriority = int.MinValue;
            uint bestOldest = uint.MaxValue;

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.Count <= 0)
                    continue;
                if (!IsRequestReadyForConsume(in item))
                    continue;

                int requiredCount = ResolveRequestConsumeUnitCount(in item);
                if (item.Count < requiredCount)
                    continue;
                if (remainingBudget < requiredCount)
                    continue;
                if (requiredCount <= 1)
                {
                    if (!freeByKey.ContainsKey(item.BulletTypeKey))
                        continue;
                }
                else if (CountFreeByKey(ref freeByKey, item.BulletTypeKey) < requiredCount)
                    continue;

                if (bestIndex < 0
                    || item.LanePriority > bestLanePriority
                    || (item.LanePriority == bestLanePriority && item.OldestFrame < bestOldest))
                {
                    bestIndex = i;
                    bestLanePriority = item.LanePriority;
                    bestOldest = item.OldestFrame;
                }
            }

            return bestIndex;
        }

        private static bool IsRequestReadyForConsume(in SourceSpawnRequestBuffer request)
        {
            if (request.EventShotSchedule != SourceSpawnEventShotScheduleId.Timed)
                return true;

            if (request.EventAnchorInitialized == 0)
                return true;

            float interval = math.max(0.001f, request.EventShotIntervalSec);
            return request.EventShotElapsedSec >= interval;
        }

        private static int ResolveRequestConsumeUnitCount(in SourceSpawnRequestBuffer request)
        {
            // NWay는 "샘플 1지점의 NWay 1세트"를 원자 단위로 소비한다.
            if (request.DirectionMode == SourceSpawnDirectionModeId.NWay)
                return math.max(1, request.NWayCount);

            return 1;
        }

        private static bool TrySpawnFromRequest(
            Entity sourceEntity,
            uint runSeed,
            uint sourceStableId,
            ref SourceSpawnRequestBuffer request,
            ref ComponentLookup<SourceSpawnRuntimeComponent> sourceRuntimeLookup,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<LocalToWorld> localToWorldLookup,
            ref ComponentLookup<BulletVelocityComponent> velLookup,
            ref ComponentLookup<BulletLifetimeComponent> lifeLookup,
            ref ComponentLookup<BulletSpeedComponent> speedLookup,
            ref ComponentLookup<BulletLifetimeMaxComponent> lifeMaxLookup,
            ref ComponentLookup<BulletTypeKeyComponent> typeKeyLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletLifecycleTraceComponent> lifeCycleLookup,
            ref ComponentLookup<BulletActiveTag> activeLookup,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref BufferLookup<EntityRenderElementBuffer> renderPartsLookup,
            ref ComponentLookup<MaterialMeshInfo> renderLookup,
            ref ComponentLookup<Parent> parentLookup,
            ref BufferLookup<SourceActiveBulletCountBuffer> activeCountLookup,
            bool hasPlayer,
            float3 playerPosition,
            uint frame,
            out int consumedCount,
            out uint sequenceAdvance)
        {
            consumedCount = 0;
            sequenceAdvance = 0u;

            if (request.DirectionMode != SourceSpawnDirectionModeId.NWay)
            {
                bool singleSpawned = TrySpawnOneFromRequest(
                    sourceEntity,
                    runSeed,
                    sourceStableId,
                    ref request,
                    ref sourceRuntimeLookup,
                    ref sourceAnchorLookup,
                    ref pollutionConfigLookup,
                    ref pollutionGridLookup,
                    ref pollutionCellsLookup,
                    ref pollutionValidCellIndicesLookup,
                    ref txLookup,
                    ref localToWorldLookup,
                    ref velLookup,
                    ref lifeLookup,
                    ref speedLookup,
                    ref lifeMaxLookup,
                    ref typeKeyLookup,
                    ref sourceRefLookup,
                    ref lifeCycleLookup,
                    ref activeLookup,
                    ref despawnRequestLookup,
                    ref renderPartsLookup,
                    ref renderLookup,
                    ref parentLookup,
                    ref activeCountLookup,
                    hasPlayer,
                    playerPosition,
                    frame);
                if (!singleSpawned)
                    return false;

                consumedCount = 1;
                sequenceAdvance = 1u;
                ConsumeTimedEventSchedule(ref request, consumedCount);
                return true;
            }

            int slotCount = math.max(1, request.NWayCount);
            if (slotCount <= 1)
            {
                bool singleSpawned = TrySpawnOneFromRequest(
                    sourceEntity,
                    runSeed,
                    sourceStableId,
                    ref request,
                    ref sourceRuntimeLookup,
                    ref sourceAnchorLookup,
                    ref pollutionConfigLookup,
                    ref pollutionGridLookup,
                    ref pollutionCellsLookup,
                    ref pollutionValidCellIndicesLookup,
                    ref txLookup,
                    ref localToWorldLookup,
                    ref velLookup,
                    ref lifeLookup,
                    ref speedLookup,
                    ref lifeMaxLookup,
                    ref typeKeyLookup,
                    ref sourceRefLookup,
                    ref lifeCycleLookup,
                    ref activeLookup,
                    ref despawnRequestLookup,
                    ref renderPartsLookup,
                    ref renderLookup,
                    ref parentLookup,
                    ref activeCountLookup,
                    hasPlayer,
                    playerPosition,
                    frame);
                if (!singleSpawned)
                    return false;

                consumedCount = 1;
                sequenceAdvance = 1u;
                ConsumeTimedEventSchedule(ref request, consumedCount);
                return true;
            }

            var random = CreateSourceRandom(
                runSeed,
                sourceStableId,
                request.DirectiveId,
                sourceEntity,
                ref sourceRuntimeLookup);
            float3 pos = ResolveSpawnPositionForRequest(
                ref random,
                sourceEntity,
                ref request,
                ref sourceAnchorLookup,
                ref txLookup,
                hasPlayer,
                playerPosition,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup,
                out uint sampledSequence);

            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
            {
                if (!TryDequeueByKey(ref BulletFieldShared.FreeByKey, request.BulletTypeKey, out var bulletEntity))
                    return false;

                float2 dir = ResolveSpawnDirection(ref random, in request, sampledSequence, slotIndex);
                ApplySpawnedBulletState(
                    bulletEntity,
                    sourceEntity,
                    request.BulletTypeKey,
                    pos,
                    dir,
                    frame,
                    ref txLookup,
                    ref localToWorldLookup,
                    ref velLookup,
                    ref lifeLookup,
                    ref speedLookup,
                    ref lifeMaxLookup,
                    ref typeKeyLookup,
                    ref sourceRefLookup,
                    ref lifeCycleLookup,
                    ref activeLookup,
                    ref despawnRequestLookup,
                    ref renderPartsLookup,
                    ref renderLookup,
                    ref parentLookup);
            }

            if (activeCountLookup.TryGetBuffer(sourceEntity, out var activeCounts))
                IncrementActiveCount(activeCounts, request.BulletTypeKey, slotCount);

            consumedCount = slotCount;
            sequenceAdvance = 1u;
            ConsumeTimedEventSchedule(ref request, consumedCount);
            return true;
        }

        private static bool TrySpawnOneFromRequest(
            Entity sourceEntity,
            uint runSeed,
            uint sourceStableId,
            ref SourceSpawnRequestBuffer request,
            ref ComponentLookup<SourceSpawnRuntimeComponent> sourceRuntimeLookup,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<LocalToWorld> localToWorldLookup,
            ref ComponentLookup<BulletVelocityComponent> velLookup,
            ref ComponentLookup<BulletLifetimeComponent> lifeLookup,
            ref ComponentLookup<BulletSpeedComponent> speedLookup,
            ref ComponentLookup<BulletLifetimeMaxComponent> lifeMaxLookup,
            ref ComponentLookup<BulletTypeKeyComponent> typeKeyLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletLifecycleTraceComponent> lifeCycleLookup,
            ref ComponentLookup<BulletActiveTag> activeLookup,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref BufferLookup<EntityRenderElementBuffer> renderPartsLookup,
            ref ComponentLookup<MaterialMeshInfo> renderLookup,
            ref ComponentLookup<Parent> parentLookup,
            ref BufferLookup<SourceActiveBulletCountBuffer> activeCountLookup,
            bool hasPlayer,
            float3 playerPosition,
            uint frame)
        {
            int requestedTypeKey = request.BulletTypeKey;
            if (!TryDequeueByKey(ref BulletFieldShared.FreeByKey, requestedTypeKey, out var bulletEntity))
                return false;

            var random = CreateSourceRandom(
                runSeed,
                sourceStableId,
                request.DirectiveId,
                sourceEntity,
                ref sourceRuntimeLookup);
            float3 pos = ResolveSpawnPositionForRequest(
                ref random,
                sourceEntity,
                ref request,
                ref sourceAnchorLookup,
                ref txLookup,
                hasPlayer,
                playerPosition,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup,
                out uint sampledSequence);

            float2 dir = ResolveSpawnDirection(ref random, in request, sampledSequence, -1);
            ApplySpawnedBulletState(
                bulletEntity,
                sourceEntity,
                requestedTypeKey,
                pos,
                dir,
                frame,
                ref txLookup,
                ref localToWorldLookup,
                ref velLookup,
                ref lifeLookup,
                ref speedLookup,
                ref lifeMaxLookup,
                ref typeKeyLookup,
                ref sourceRefLookup,
                ref lifeCycleLookup,
                ref activeLookup,
                ref despawnRequestLookup,
                ref renderPartsLookup,
                ref renderLookup,
                ref parentLookup);

            if (activeCountLookup.TryGetBuffer(sourceEntity, out var activeCounts))
                IncrementActiveCount(activeCounts, requestedTypeKey);

            return true;
        }

        private static void ApplySpawnedBulletState(
            Entity bulletEntity,
            Entity sourceEntity,
            int requestedTypeKey,
            float3 pos,
            float2 dir,
            uint frame,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<LocalToWorld> localToWorldLookup,
            ref ComponentLookup<BulletVelocityComponent> velLookup,
            ref ComponentLookup<BulletLifetimeComponent> lifeLookup,
            ref ComponentLookup<BulletSpeedComponent> speedLookup,
            ref ComponentLookup<BulletLifetimeMaxComponent> lifeMaxLookup,
            ref ComponentLookup<BulletTypeKeyComponent> typeKeyLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletLifecycleTraceComponent> lifeCycleLookup,
            ref ComponentLookup<BulletActiveTag> activeLookup,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref BufferLookup<EntityRenderElementBuffer> renderPartsLookup,
            ref ComponentLookup<MaterialMeshInfo> renderLookup,
            ref ComponentLookup<Parent> parentLookup)
        {
            var rot = quaternion.LookRotationSafe(new float3(dir.x, 0f, dir.y), math.up());
            float bulletSpeed = speedLookup.HasComponent(bulletEntity)
                ? math.max(0f, speedLookup[bulletEntity].Value)
                : 0f;
            float bulletLifetime = lifeMaxLookup.HasComponent(bulletEntity)
                ? math.max(0f, lifeMaxLookup[bulletEntity].Value)
                : 0f;

            if (txLookup.HasComponent(bulletEntity))
                txLookup[bulletEntity] = LocalTransform.FromPositionRotationScale(pos, rot, 1f);

            var rootWorldMatrix = float4x4.TRS(pos, rot, new float3(1f, 1f, 1f));
            if (localToWorldLookup.HasComponent(bulletEntity))
                localToWorldLookup[bulletEntity] = new LocalToWorld { Value = rootWorldMatrix };

            if (velLookup.HasComponent(bulletEntity))
                velLookup[bulletEntity] = new BulletVelocityComponent { Value = dir * bulletSpeed };
            if (lifeLookup.HasComponent(bulletEntity))
                lifeLookup[bulletEntity] = new BulletLifetimeComponent { Value = bulletLifetime };
            if (typeKeyLookup.HasComponent(bulletEntity))
                typeKeyLookup[bulletEntity] = new BulletTypeKeyComponent { Value = requestedTypeKey };
            if (sourceRefLookup.HasComponent(bulletEntity))
                sourceRefLookup[bulletEntity] = new BulletSourceRefComponent { Value = sourceEntity };
            if (lifeCycleLookup.HasComponent(bulletEntity))
            {
                var trace = lifeCycleLookup[bulletEntity];
                trace.LastSpawnFrame = frame;
                lifeCycleLookup[bulletEntity] = trace;
            }

            if (despawnRequestLookup.HasComponent(bulletEntity))
                despawnRequestLookup.SetComponentEnabled(bulletEntity, false);
            if (activeLookup.HasComponent(bulletEntity))
                activeLookup.SetComponentEnabled(bulletEntity, true);

            if (renderPartsLookup.HasBuffer(bulletEntity))
            {
                var parts = renderPartsLookup[bulletEntity];
                bool toggled = false;
                for (int i = 0; i < parts.Length; i++)
                {
                    var partEntity = parts[i].Value;
                    if (localToWorldLookup.HasComponent(partEntity))
                    {
                        float4x4 partWorldMatrix = rootWorldMatrix;
                        if (parentLookup.HasComponent(partEntity) && txLookup.HasComponent(partEntity))
                            partWorldMatrix = math.mul(rootWorldMatrix, txLookup[partEntity].ToMatrix());
                        localToWorldLookup[partEntity] = new LocalToWorld { Value = partWorldMatrix };
                    }

                    if (renderLookup.HasComponent(partEntity))
                    {
                        renderLookup.SetComponentEnabled(partEntity, true);
                        toggled = true;
                    }
                }

                // Guard: render-parts buffer exists but no valid render entity in it.
                if (!toggled && renderLookup.HasComponent(bulletEntity))
                    renderLookup.SetComponentEnabled(bulletEntity, true);
            }
            else if (renderLookup.HasComponent(bulletEntity))
            {
                renderLookup.SetComponentEnabled(bulletEntity, true);
            }
        }

        private static float3 ResolveSpawnCenter(
            Entity sourceEntity,
            in SourceSpawnRequestBuffer request,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            bool hasPlayer,
            float3 playerPosition)
        {
            float3 sourceCenter = sourceAnchorLookup.HasComponent(sourceEntity)
                ? sourceAnchorLookup[sourceEntity].Position
                : float3.zero;

            switch (request.CenterMode)
            {
                case SourceSpawnCenterModeId.FixedPoint:
                    return new float3(request.FixedPoint.x, sourceCenter.y, request.FixedPoint.y);
                case SourceSpawnCenterModeId.PlayerRelative:
                    if (hasPlayer)
                    {
                        return new float3(
                            playerPosition.x + request.SpawnOffset.x,
                            playerPosition.y,
                            playerPosition.z + request.SpawnOffset.y);
                    }

                    return sourceCenter;
                default:
                    return sourceCenter;
            }
        }

        private static float3 ResolveSpawnPositionForRequest(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            ref SourceSpawnRequestBuffer request,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<LocalTransform> sourceTransformLookup,
            bool hasPlayer,
            float3 playerPosition,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup,
            out uint sampledSequence)
        {
            float3 center = ResolveSpawnCenter(
                sourceEntity,
                in request,
                ref sourceAnchorLookup,
                hasPlayer,
                playerPosition);

            if (request.EventShotSchedule != SourceSpawnEventShotScheduleId.Timed)
            {
                return SampleSpawnPosition(
                    ref random,
                    sourceEntity,
                    in request,
                    center,
                    hasPlayer,
                    playerPosition,
                    ref pollutionConfigLookup,
                    ref pollutionGridLookup,
                    ref pollutionCellsLookup,
                    ref pollutionValidCellIndicesLookup,
                    out sampledSequence);
            }

            if (request.EventAnchorInitialized == 0)
            {
                sampledSequence = request.SpawnSequence;
                if (request.SamplingMode == SourceSpawnSamplingModeId.UniformField
                    || request.SamplingMode == SourceSpawnSamplingModeId.PollutionTopK)
                {
                    float3 anchoredPosition = SampleSpawnPosition(
                        ref random,
                        sourceEntity,
                        in request,
                        center,
                        hasPlayer,
                        playerPosition,
                        ref pollutionConfigLookup,
                        ref pollutionGridLookup,
                        ref pollutionCellsLookup,
                        ref pollutionValidCellIndicesLookup,
                        out sampledSequence);
                    request.EventAnchorUseFixedPosition = 1;
                    request.EventAnchorPosition = anchoredPosition;
                    request.EventAnchorCenter = center;
                    request.EventAnchorInitialized = 1;
                    request.EventShotElapsedSec = 0f;
                    return anchoredPosition;
                }

                request.EventAnchorUseFixedPosition = 0;
                request.EventAnchorCenter = center;
                request.EventAnchorPosition = float3.zero;
                request.EventAnchorInitialized = 1;
                request.EventShotElapsedSec = 0f;
            }

            if (request.EventAnchorUseFixedPosition != 0)
            {
                sampledSequence = request.SpawnSequence;
                return request.EventAnchorPosition;
            }

            return SampleSpawnPosition(
                ref random,
                sourceEntity,
                in request,
                request.EventAnchorCenter,
                false,
                playerPosition,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup,
                out sampledSequence);
        }

        private static void ConsumeTimedEventSchedule(ref SourceSpawnRequestBuffer request, int consumedCount)
        {
            if (request.EventShotSchedule != SourceSpawnEventShotScheduleId.Timed || consumedCount <= 0)
                return;

            // Timed 모드는 샷 소비를 이벤트 내부 간격으로 진행한다.
            if (request.EventAnchorInitialized == 0)
            {
                request.EventShotElapsedSec = 0f;
                return;
            }

            float interval = math.max(0.001f, request.EventShotIntervalSec);
            request.EventShotElapsedSec = math.max(0f, request.EventShotElapsedSec - interval);
        }

        private static Unity.Mathematics.Random CreateSourceRandom(
            uint runSeed,
            uint sourceStableId,
            int directiveId,
            Entity sourceEntity,
            ref ComponentLookup<SourceSpawnRuntimeComponent> sourceRuntimeLookup)
        {
            uint sequence = 1u;
            if (sourceRuntimeLookup.HasComponent(sourceEntity))
            {
                var runtime = sourceRuntimeLookup[sourceEntity];
                sequence = math.max(1u, runtime.SpawnSequence);
                runtime.SpawnSequence = sequence + 1u;
                sourceRuntimeLookup[sourceEntity] = runtime;
            }

            uint seed = math.hash(new uint4(
                math.max(1u, runSeed),
                math.max(1u, sourceStableId),
                sequence,
                ((uint)math.max(0, directiveId + 1)) ^ 0xA5A5A5A5u));
            return Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
        }

        private static float2 ResolveSpawnDirection(
            ref Unity.Mathematics.Random random,
            in SourceSpawnRequestBuffer request,
            uint spawnSequence,
            int forcedSlotIndex)
        {
            float baseRad = math.radians(request.BaseAngleDeg);
            uint directionSequence = spawnSequence;
            if (request.SamplingMode == SourceSpawnSamplingModeId.PointSet)
            {
                int pointCount = ResolvePointSetCount(in request);
                if (pointCount > 0)
                {
                    // PointSet에서는 포인트별 로컬 시퀀스로 방향(Spiral/NWay)을 계산해 동시 패턴 위상을 맞춘다.
                    directionSequence = spawnSequence / (uint)pointCount;
                }
            }

            float angle;
            switch (request.DirectionMode)
            {
                case SourceSpawnDirectionModeId.Fixed:
                    angle = baseRad;
                    break;
                case SourceSpawnDirectionModeId.Spiral:
                {
                    float stepRad = math.radians(request.SpiralStepDeg);
                    angle = baseRad + stepRad * directionSequence;
                    break;
                }
                case SourceSpawnDirectionModeId.NWay:
                case SourceSpawnDirectionModeId.RadialBurst:
                {
                    int slotCount = ResolveDirectionalSlotCount(in request);
                    int slot = slotCount <= 1
                        ? 0
                        : (forcedSlotIndex >= 0
                            ? math.abs(forcedSlotIndex) % slotCount
                            : (int)(directionSequence % (uint)slotCount));
                    angle = baseRad + (slotCount <= 1 ? 0f : (math.PI * 2f * slot) / slotCount);
                    break;
                }
                default:
                    angle = random.NextFloat(0f, math.PI * 2f);
                    break;
            }

            float2 dir = new float2(math.cos(angle), math.sin(angle));
            float lenSq = math.lengthsq(dir);
            if (lenSq <= 1e-6f)
                return new float2(1f, 0f);

            return dir * math.rsqrt(lenSq);
        }

        // NWay와 RadialBurst는 공통 슬롯 분배 로직으로 통합한다.
        private static int ResolveDirectionalSlotCount(in SourceSpawnRequestBuffer request)
        {
            if (request.DirectionMode == SourceSpawnDirectionModeId.RadialBurst)
                return math.max(1, request.BurstShotsPerEvent);

            return math.max(1, request.NWayCount);
        }

        private static float3 SampleSpawnPosition(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            in SourceSpawnRequestBuffer request,
            float3 center,
            bool hasPlayer,
            float3 playerPosition,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup,
            out uint sampledSequence)
        {
            int sampleBudget = math.max(1, request.SpawnSampleBudget);
            float noSpawnRadius = math.max(0f, request.PlayerNoSpawnRadius);
            float noSpawnRadiusSq = noSpawnRadius * noSpawnRadius;
            float3 lastSample = center;
            sampledSequence = request.SpawnSequence;

            for (int i = 0; i < sampleBudget; i++)
            {
                uint sequence = request.SpawnSequence + (uint)i;
                sampledSequence = sequence;
                if (request.SamplingMode == SourceSpawnSamplingModeId.PollutionTopK)
                {
                    if (TrySampleSpawnPositionFromPollution(
                            ref random,
                            sourceEntity,
                            center,
                            out var pollutionPos,
                            ref pollutionConfigLookup,
                            ref pollutionGridLookup,
                            ref pollutionCellsLookup,
                            ref pollutionValidCellIndicesLookup))
                    {
                        lastSample = pollutionPos;
                    }
                    else
                    {
                        lastSample = center;
                    }
                }
                else if (request.SamplingMode == SourceSpawnSamplingModeId.UniformField)
                {
                    if (TrySampleSpawnPositionUniform(
                            ref random,
                            sourceEntity,
                            center,
                            out var uniformPos,
                            ref pollutionGridLookup,
                            ref pollutionCellsLookup,
                            ref pollutionValidCellIndicesLookup))
                    {
                        lastSample = uniformPos;
                    }
                    else
                    {
                        lastSample = center;
                    }
                }
                else if (request.SamplingMode == SourceSpawnSamplingModeId.LineEven)
                {
                    lastSample = SampleSpawnPositionLineEven(center, in request, sequence);
                }
                else if (request.SamplingMode == SourceSpawnSamplingModeId.PointSet)
                {
                    if (TrySampleSpawnPositionPointSet(center, in request, sequence, out var pointSetPos))
                        lastSample = pointSetPos;
                    else
                        lastSample = center;
                }
                else
                {
                    lastSample = center;
                }

                if (!hasPlayer || noSpawnRadius <= 0f)
                    return lastSample;

                float2 delta = new float2(lastSample.x - playerPosition.x, lastSample.z - playerPosition.z);
                if (math.lengthsq(delta) >= noSpawnRadiusSq)
                    return lastSample;
            }

            return lastSample;
        }

        private static bool TrySampleSpawnPositionPointSet(
            float3 center,
            in SourceSpawnRequestBuffer request,
            uint sequence,
            out float3 position)
        {
            position = center;
            int pointCount = ResolvePointSetCount(in request);
            if (pointCount <= 0)
                return false;

            int pointIndex = (int)(sequence % (uint)pointCount);
            float2 local = ResolvePointSetPoint(in request, pointIndex);
            position = new float3(center.x + local.x, center.y, center.z + local.y);
            return true;
        }

        private static int ResolvePointSetCount(in SourceSpawnRequestBuffer request)
        {
            return math.clamp(request.PointSetCount, 0, 4);
        }

        private static float2 ResolvePointSetPoint(in SourceSpawnRequestBuffer request, int index)
        {
            switch (index)
            {
                case 0:
                    return request.Point0;
                case 1:
                    return request.Point1;
                case 2:
                    return request.Point2;
                case 3:
                    return request.Point3;
                default:
                    return float2.zero;
            }
        }

        private static bool TrySampleSpawnPositionFromPollution(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            float3 center,
            out float3 position,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
        {
            position = center;
            if (!pollutionConfigLookup.HasComponent(sourceEntity))
                return false;
            if (!pollutionGridLookup.HasComponent(sourceEntity))
                return false;
            if (!pollutionCellsLookup.HasBuffer(sourceEntity))
                return false;
            if (!pollutionValidCellIndicesLookup.HasBuffer(sourceEntity))
                return false;

            var config = pollutionConfigLookup[sourceEntity];
            var grid = pollutionGridLookup[sourceEntity];
            var cells = pollutionCellsLookup[sourceEntity];
            var validIndices = pollutionValidCellIndicesLookup[sourceEntity];
            int validCount = validIndices.Length;
            if (validCount <= 0)
                return false;

            int topK = math.clamp(config.TopKSampleCount, 1, validCount);
            int bestCellIndex = -1;
            float bestWeight = -1f;

            for (int i = 0; i < topK; i++)
            {
                int sampledListIndex = random.NextInt(0, validCount);
                int cellIndex = validIndices[sampledListIndex].Value;
                float weight = GetValidCellWeight(cells, cellIndex);
                if (weight < 0f)
                    continue;

                if (bestCellIndex < 0 || weight > bestWeight)
                {
                    bestCellIndex = cellIndex;
                    bestWeight = weight;
                }
            }

            if (bestCellIndex < 0)
                return false;

            int cols = math.max(1, grid.Cols);
            int rows = math.max(1, grid.Rows);
            position = SampleInsidePollutionCell(ref random, bestCellIndex, center, cols, rows, in grid);
            return true;
        }

        private static bool TrySampleSpawnPositionUniform(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            float3 center,
            out float3 position,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
        {
            position = center;
            if (!pollutionGridLookup.HasComponent(sourceEntity))
                return false;
            if (!pollutionCellsLookup.HasBuffer(sourceEntity))
                return false;
            if (!pollutionValidCellIndicesLookup.HasBuffer(sourceEntity))
                return false;

            var grid = pollutionGridLookup[sourceEntity];
            var cells = pollutionCellsLookup[sourceEntity];
            var validIndices = pollutionValidCellIndicesLookup[sourceEntity];
            int validCount = validIndices.Length;
            if (validCount <= 0)
                return false;

            int sampledListIndex = random.NextInt(0, validCount);
            int cellIndex = validIndices[sampledListIndex].Value;
            if (GetValidCellWeight(cells, cellIndex) < 0f)
                return false;

            position = SampleInsidePollutionCell(
                ref random,
                cellIndex,
                center,
                math.max(1, grid.Cols),
                math.max(1, grid.Rows),
                in grid);
            return true;
        }

        private static float GetValidCellWeight(DynamicBuffer<SourcePollutionCellBuffer> cells, int cellIndex)
        {
            if ((uint)cellIndex >= (uint)cells.Length)
                return -1f;

            var cell = cells[cellIndex];
            if (cell.IsValid == 0)
                return -1f;

            return math.max(0f, cell.Value);
        }

        private static float3 SampleInsidePollutionCell(
            ref Unity.Mathematics.Random random,
            int cellIndex,
            float3 center,
            int cols,
            int rows,
            in SourcePollutionGridComponent grid)
        {
            int safeCols = math.max(1, cols);
            int safeRows = math.max(1, rows);
            int clampedCellIndex = math.clamp(cellIndex, 0, safeCols * safeRows - 1);
            int cellX = clampedCellIndex % safeCols;
            int cellY = math.clamp(clampedCellIndex / safeCols, 0, safeRows - 1);

            float cellSize = math.max(0.001f, grid.CellSize);
            float2 halfExtents = math.max(0f, grid.HalfExtents);
            float localX = -halfExtents.x + (cellX + random.NextFloat(0f, 1f)) * cellSize;
            float localZ = -halfExtents.y + (cellY + random.NextFloat(0f, 1f)) * cellSize;
            localX = math.clamp(localX, -halfExtents.x, halfExtents.x);
            localZ = math.clamp(localZ, -halfExtents.y, halfExtents.y);
            return new float3(center.x + localX, center.y, center.z + localZ);
        }

        private static float3 SampleSpawnPositionLineEven(
            float3 center,
            in SourceSpawnRequestBuffer request,
            uint sequence)
        {
            float2 startOffset = request.LineStart;
            float2 endOffset = request.LineEnd;
            float2 segment = endOffset - startOffset;
            float length = math.length(segment);
            if (length <= 1e-5f)
            {
                float2 mid = (startOffset + endOffset) * 0.5f;
                return new float3(center.x + mid.x, center.y, center.z + mid.y);
            }

            float spacing = math.max(0.001f, request.SampleSpacing);
            int slotCount = ComputeEvenSlotCount(length, spacing);
            int slotIndex = slotCount <= 1 ? 0 : (int)(sequence % (uint)slotCount);
            float t = slotCount <= 1 ? 0.5f : slotIndex / (float)(slotCount - 1);
            float2 local = math.lerp(startOffset, endOffset, t);
            return new float3(center.x + local.x, center.y, center.z + local.y);
        }

        private static int ComputeEvenSlotCount(float length, float spacing)
        {
            float safeLength = math.max(0f, length);
            float safeSpacing = math.max(0.001f, spacing);
            return math.max(1, (int)math.floor(safeLength / safeSpacing) + 1);
        }

        private static bool TryDequeueByKey(
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int key,
            out Entity entity)
        {
            if (!freeByKey.TryGetFirstValue(key, out entity, out var iterator))
                return false;

            freeByKey.Remove(key, entity);
            return true;
        }

        private static int CountFreeByKey(ref NativeParallelMultiHashMap<int, Entity> freeByKey, int key)
        {
            if (!freeByKey.TryGetFirstValue(key, out var _, out var iterator))
                return 0;

            int count = 1;
            while (freeByKey.TryGetNextValue(out _, ref iterator))
                count++;

            return count;
        }

        private static void IncrementActiveCount(DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey)
        {
            IncrementActiveCount(activeCounts, typeKey, 1);
        }

        private static void IncrementActiveCount(DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey, int amount)
        {
            for (int i = 0; i < activeCounts.Length; i++)
            {
                var item = activeCounts[i];
                if (item.BulletTypeKey != typeKey)
                    continue;

                item.ActiveCount = SpawnRequestCommonUtility.SafeAdd(item.ActiveCount, amount);
                activeCounts[i] = item;
                return;
            }

            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = typeKey,
                ActiveCount = math.max(0, amount)
            });
        }

    }

}
