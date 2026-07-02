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
    [UpdateAfter(typeof(SecondarySpawnExecutionSystem))]
    [UpdateAfter(typeof(DiscreteEmitExecutionSystem))]
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
                        : SpawnRequestCommonUtility.CountFreeByKey(ref freeByKey, item.BulletTypeKey) >= requiredCount;
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
                else if (SpawnRequestCommonUtility.CountFreeByKey(ref freeByKey, item.BulletTypeKey) < requiredCount)
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
            return SpawnRequestCommonUtility.ResolveShotPatternUnitCount(in request);
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
            ref BufferLookup<SourceActiveBulletCountBuffer> activeCountLookup,
            bool hasPlayer,
            float3 playerPosition,
            uint frame,
            out int consumedCount,
            out uint sequenceAdvance)
        {
            consumedCount = 0;
            sequenceAdvance = 0u;

            int shotUnitCount = math.max(1, SpawnRequestCommonUtility.ResolveShotPatternUnitCount(in request));
            if (shotUnitCount <= 1)
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
            var runtimeTuning = SpawnRequestCommonUtility.CreateRuntimeTuning(in request);
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
                out uint repeatSequence);

            for (int slotIndex = 0; slotIndex < shotUnitCount; slotIndex++)
            {
                if (!SpawnRequestCommonUtility.TryDequeueByKey(ref BulletFieldShared.FreeByKey, request.BulletTypeKey, out var bulletEntity))
                    return false;

                float2 dir = ResolveSpawnDirection(
                    ref random,
                    ref request,
                    pos,
                    hasPlayer,
                    playerPosition,
                    repeatSequence,
                    slotIndex);
                SpawnRequestCommonUtility.ApplySpawnedBulletState(
                    bulletEntity,
                    sourceEntity,
                    request.BulletTypeKey,
                    in runtimeTuning,
                    pos,
                    dir,
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
            }

            if (activeCountLookup.TryGetBuffer(sourceEntity, out var activeCounts))
                SpawnRequestCommonUtility.IncrementActiveCount(activeCounts, request.BulletTypeKey, shotUnitCount);

            consumedCount = shotUnitCount;
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
            ref BufferLookup<SourceActiveBulletCountBuffer> activeCountLookup,
            bool hasPlayer,
            float3 playerPosition,
            uint frame)
        {
            int requestedTypeKey = request.BulletTypeKey;
            if (!SpawnRequestCommonUtility.TryDequeueByKey(ref BulletFieldShared.FreeByKey, requestedTypeKey, out var bulletEntity))
                return false;

            var random = CreateSourceRandom(
                runSeed,
                sourceStableId,
                request.DirectiveId,
                sourceEntity,
                ref sourceRuntimeLookup);
            var runtimeTuning = SpawnRequestCommonUtility.CreateRuntimeTuning(in request);
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
                out uint repeatSequence);

            float2 dir = ResolveSpawnDirection(
                ref random,
                ref request,
                pos,
                hasPlayer,
                playerPosition,
                repeatSequence,
                -1);
            SpawnRequestCommonUtility.ApplySpawnedBulletState(
                bulletEntity,
                sourceEntity,
                requestedTypeKey,
                in runtimeTuning,
                pos,
                dir,
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

            if (activeCountLookup.TryGetBuffer(sourceEntity, out var activeCounts))
                SpawnRequestCommonUtility.IncrementActiveCount(activeCounts, requestedTypeKey);

            return true;
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

            switch (request.SamplingAnchorMode)
            {
                case WaveSamplingAnchorModeId.FixedPoint:
                    return new float3(request.FixedPoint.x, sourceCenter.y, request.FixedPoint.y);
                case WaveSamplingAnchorModeId.PlayerRelative:
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

            float3 sourceAnchor = sourceAnchorLookup.HasComponent(sourceEntity)
                ? sourceAnchorLookup[sourceEntity].Position
                : center;
            sampledSequence = request.SpawnSequence;

            if (!SpawnRequestCommonUtility.UsesDiscreteEventIdentity(in request))
            {
                float3 eventAnchor = SampleEventAnchorPosition(
                    ref random,
                    sourceEntity,
                    in request,
                    center,
                    sourceAnchor,
                    hasPlayer,
                    playerPosition,
                    ref pollutionConfigLookup,
                    ref pollutionGridLookup,
                    ref pollutionCellsLookup,
                    ref pollutionValidCellIndicesLookup,
                    sampledSequence);
                return ResolveRepeatOriginPosition(eventAnchor, in request, sampledSequence);
            }

            if (request.EventAnchorInitialized == 0)
            {
                request.EventAnchorPosition = SampleEventAnchorPosition(
                    ref random,
                    sourceEntity,
                    in request,
                    center,
                    sourceAnchor,
                    hasPlayer,
                    playerPosition,
                    ref pollutionConfigLookup,
                    ref pollutionGridLookup,
                    ref pollutionCellsLookup,
                    ref pollutionValidCellIndicesLookup,
                    sampledSequence);
                request.EventAnchorInitialized = 1;
                request.EventShotElapsedSec = 0f;
                if (request.AimMode == WaveAimModeId.PlayerPosition
                    && request.AimSnapshotTiming == WaveAimSnapshotTimingId.EventStart)
                {
                    request.EventAimTargetPosition = hasPlayer ? playerPosition : request.EventAnchorPosition;
                    request.EventAimInitialized = 1;
                }
            }

            return ResolveRepeatOriginPosition(request.EventAnchorPosition, in request, sampledSequence);
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
            ref SourceSpawnRequestBuffer request,
            float3 repeatOrigin,
            bool hasPlayer,
            float3 playerPosition,
            uint repeatSequence,
            int forcedSlotIndex)
        {
            float angle = ResolveBaseAimAngleRad(
                ref random,
                ref request,
                repeatOrigin,
                hasPlayer,
                playerPosition,
                repeatSequence);

            int slotCount = ResolveDirectionalSlotCount(in request);
            int slot = slotCount <= 1
                ? 0
                : (forcedSlotIndex >= 0
                    ? math.abs(forcedSlotIndex) % slotCount
                    : (int)(repeatSequence % (uint)slotCount));
            if (slotCount > 1)
                angle += ResolveShotPatternAngleOffsetRad(in request, slotCount, slot);

            float2 dir = new float2(math.cos(angle), math.sin(angle));
            float lenSq = math.lengthsq(dir);
            if (lenSq <= 1e-6f)
                return new float2(1f, 0f);

            return dir * math.rsqrt(lenSq);
        }

        private static int ResolveDirectionalSlotCount(in SourceSpawnRequestBuffer request)
        {
            return request.ShotPatternMode switch
            {
                WaveShotPatternModeId.NWay => math.max(1, request.ShotCount),
                WaveShotPatternModeId.Radial => math.max(1, request.ShotCount),
                _ => 1,
            };
        }

        private static float ResolveShotPatternAngleOffsetRad(
            in SourceSpawnRequestBuffer request,
            int slotCount,
            int slot)
        {
            if (slotCount <= 1)
                return 0f;

            return request.ShotPatternMode switch
            {
                WaveShotPatternModeId.NWay => math.radians(request.NWayAngleSpacingDeg) * (slot - ((slotCount - 1) * 0.5f)),
                WaveShotPatternModeId.Radial => (math.PI * 2f * slot) / slotCount,
                _ => 0f,
            };
        }

        private static float3 SampleEventAnchorPosition(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            in SourceSpawnRequestBuffer request,
            float3 center,
            float3 sourceAnchor,
            bool hasPlayer,
            float3 playerPosition,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup,
            uint sampledSequence)
        {
            int sampleBudget = math.max(1, request.SpawnSampleBudget);
            float noSpawnRadius = math.max(0f, request.PlayerNoSpawnRadius);
            float noSpawnRadiusSq = noSpawnRadius * noSpawnRadius;
            float3 lastSample = center;

            for (int i = 0; i < sampleBudget; i++)
            {
                if (request.AreaSamplerMode == WaveAreaSamplerModeId.PollutionTopK)
                {
                    if (TrySampleSpawnPositionFromPollution(
                            ref random,
                            sourceEntity,
                            center,
                            sourceAnchor,
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
                else if (request.AreaSamplerMode == WaveAreaSamplerModeId.UniformField)
                {
                    if (TrySampleSpawnPositionUniform(
                            ref random,
                            sourceEntity,
                            center,
                            sourceAnchor,
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

        private static float3 ResolveRepeatOriginPosition(
            float3 eventAnchorPosition,
            in SourceSpawnRequestBuffer request,
            uint repeatSequence)
        {
            switch (request.PositionPatternMode)
            {
                case WavePositionPatternModeId.LineEven:
                    return SampleSpawnPositionLineEven(eventAnchorPosition, in request, repeatSequence);
                case WavePositionPatternModeId.PointSet:
                    return TrySampleSpawnPositionPointSet(eventAnchorPosition, in request, repeatSequence, out var pointSetPos)
                        ? pointSetPos
                        : eventAnchorPosition;
                default:
                    return eventAnchorPosition;
            }
        }

        private static float ResolveBaseAimAngleRad(
            ref Unity.Mathematics.Random random,
            ref SourceSpawnRequestBuffer request,
            float3 repeatOrigin,
            bool hasPlayer,
            float3 playerPosition,
            uint repeatSequence)
        {
            switch (request.AimMode)
            {
                case WaveAimModeId.Fixed:
                    return math.radians(request.BaseAngleDeg);
                case WaveAimModeId.LineNormal:
                    return ResolveLineNormalAimAngleRad(in request);
                case WaveAimModeId.Spiral:
                    return math.radians(request.BaseAngleDeg + request.SpiralStepDeg * repeatSequence);
                case WaveAimModeId.PlayerPosition:
                {
                    bool useEventStartSnapshot = SpawnRequestCommonUtility.UsesDiscreteEventIdentity(in request)
                        && request.AimSnapshotTiming == WaveAimSnapshotTimingId.EventStart;
                    float3 aimTarget = playerPosition;

                    if (useEventStartSnapshot)
                    {
                        if (request.EventAimInitialized == 0)
                        {
                            request.EventAimTargetPosition = hasPlayer ? playerPosition : request.EventAnchorPosition;
                            request.EventAimInitialized = 1;
                        }

                        aimTarget = request.EventAimTargetPosition;
                    }
                    else if (request.AimSnapshotTiming == WaveAimSnapshotTimingId.PerShot)
                    {
                        aimTarget = hasPlayer ? playerPosition : repeatOrigin;
                    }
                    else if (!hasPlayer)
                    {
                        aimTarget = repeatOrigin;
                    }

                    float2 aimDelta = new float2(aimTarget.x - repeatOrigin.x, aimTarget.z - repeatOrigin.z);
                    float angle = math.lengthsq(aimDelta) > 1e-6f
                        ? math.atan2(aimDelta.y, aimDelta.x)
                        : 0f;
                    return angle + math.radians(request.AimAngleOffsetDeg);
                }
                default:
                    return random.NextFloat(0f, math.PI * 2f);
            }
        }

        private static float ResolveLineNormalAimAngleRad(in SourceSpawnRequestBuffer request)
        {
            float2 line = request.LineEnd - request.LineStart;
            if (math.lengthsq(line) <= 1e-6f)
                return math.radians(request.LineNormalAngleOffsetDeg);

            float2 tangent = math.normalize(line);
            float2 normal = request.LineNormalSide == WaveLineNormalSideId.Right
                ? new float2(tangent.y, -tangent.x)
                : new float2(-tangent.y, tangent.x);
            return math.atan2(normal.y, normal.x) + math.radians(request.LineNormalAngleOffsetDeg);
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
            float3 sourceAnchor,
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

            int activeCount = CountActiveValidCells(cells, validIndices);
            if (activeCount <= 0)
                return false;

            int topK = math.clamp(config.TopKSampleCount, 1, activeCount);
            int bestCellIndex = -1;
            float bestWeight = -1f;

            for (int i = 0; i < topK; i++)
            {
                int cellIndex = SelectNthActiveValidCell(validIndices, cells, random.NextInt(0, activeCount));
                if (cellIndex < 0)
                    continue;
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
            position = SampleInsidePollutionCell(ref random, bestCellIndex, center, sourceAnchor, cols, rows, in grid);
            return true;
        }

        private static bool TrySampleSpawnPositionUniform(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            float3 center,
            float3 sourceAnchor,
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

            int activeCount = CountActiveValidCells(cells, validIndices);
            if (activeCount <= 0)
                return false;

            int cellIndex = SelectNthActiveValidCell(validIndices, cells, random.NextInt(0, activeCount));
            if (cellIndex < 0)
                return false;
            if (GetValidCellWeight(cells, cellIndex) < 0f)
                return false;

            position = SampleInsidePollutionCell(
                ref random,
                cellIndex,
                center,
                sourceAnchor,
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
            if (cell.IsValid == 0 || cell.IsActive == 0)
                return -1f;

            return math.max(0f, cell.Value);
        }

        private static int CountActiveValidCells(
            DynamicBuffer<SourcePollutionCellBuffer> cells,
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> validIndices)
        {
            int activeCount = 0;
            for (int i = 0; i < validIndices.Length; i++)
            {
                if (GetValidCellWeight(cells, validIndices[i].Value) >= 0f)
                    activeCount++;
            }

            return activeCount;
        }

        private static int SelectNthActiveValidCell(
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> validIndices,
            DynamicBuffer<SourcePollutionCellBuffer> cells,
            int activeOrdinal)
        {
            int current = 0;
            for (int i = 0; i < validIndices.Length; i++)
            {
                int cellIndex = validIndices[i].Value;
                if (GetValidCellWeight(cells, cellIndex) < 0f)
                    continue;

                if (current == activeOrdinal)
                    return cellIndex;

                current++;
            }

            return -1;
        }

        private static float3 SampleInsidePollutionCell(
            ref Unity.Mathematics.Random random,
            int cellIndex,
            float3 center,
            float3 sourceAnchor,
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
            float2 halfExtents = math.max(float2.zero, grid.HalfExtents);
            float originOffsetX = center.x - sourceAnchor.x;
            float originOffsetZ = center.z - sourceAnchor.z;
            float worldMinX = grid.OriginX + originOffsetX;
            float worldMinZ = grid.OriginZ + originOffsetZ;
            float worldMaxX = worldMinX + (halfExtents.x * 2f);
            float worldMaxZ = worldMinZ + (halfExtents.y * 2f);
            float worldX = worldMinX + (cellX + random.NextFloat(0f, 1f)) * cellSize;
            float worldZ = worldMinZ + (cellY + random.NextFloat(0f, 1f)) * cellSize;
            worldX = math.clamp(worldX, worldMinX, worldMaxX);
            worldZ = math.clamp(worldZ, worldMinZ, worldMaxZ);
            return new float3(worldX, center.y, worldZ);
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

    }

}
