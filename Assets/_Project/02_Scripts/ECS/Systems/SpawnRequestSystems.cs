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
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct SourceSpawnRequestBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceSpawnPatternBuffer>();
            state.RequireForUpdate<SourceActiveBulletCountBuffer>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            float deltaTime = SystemAPI.Time.DeltaTime;
            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();

            var metricsRW = SystemAPI.GetSingletonRW<SpawnBacklogMetricsComponent>();
            var metrics = metricsRW.ValueRO;
            metrics.LastFrameDroppedByCapacity = 0;

            int pendingTotal = 0;
            foreach (var requests in SystemAPI.Query<DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                for (int i = 0; i < requests.Length; i++)
                {
                    int count = math.max(0, requests[i].Count);
                    pendingTotal = SafeAdd(pendingTotal, count);
                }
            }
            int remainingCapacity = math.max(0, policy.MaxPendingCount - pendingTotal);
            int droppedByCapacity = 0;

            foreach (var (source, fieldArea, patterns, activeCounts, requests) in
                     SystemAPI.Query<
                         RefRO<SourceSpawnComponent>,
                         RefRO<BulletFieldAreaComponent>,
                         DynamicBuffer<SourceSpawnPatternBuffer>,
                         DynamicBuffer<SourceActiveBulletCountBuffer>,
                         DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                var patternsRW = patterns;
                var requestsRW = requests;

                if (source.ValueRO.State == SourceStateId.Depleted)
                    continue;
                if (patternsRW.Length <= 0)
                    continue;

                float area = math.max(0f, fieldArea.ValueRO.ComputedArea);
                for (int i = 0; i < patternsRW.Length; i++)
                {
                    var pattern = patternsRW[i];
                    if (pattern.State != source.ValueRO.State)
                        continue;

                    int requested = ResolveSpawnCount(ref pattern, activeCounts, requestsRW, area, deltaTime);
                    patternsRW[i] = pattern;
                    if (requested <= 0)
                        continue;

                    int accepted = math.min(requested, remainingCapacity);
                    if (accepted > 0)
                    {
                        AddOrMergeRequest(requestsRW, pattern.BulletTypeKey, accepted, frame);
                        pendingTotal = SafeAdd(pendingTotal, accepted);
                        remainingCapacity -= accepted;
                    }

                    int dropped = requested - accepted;
                    if (dropped > 0)
                        droppedByCapacity = SafeAdd(droppedByCapacity, dropped);
                }

                CompactRequestBuffer(requestsRW);
            }

            metrics.PendingCount = pendingTotal;
            metrics.LastFrameDroppedByCapacity = droppedByCapacity;
            if (droppedByCapacity > 0)
                metrics.DroppedByCapacity = SafeAdd(metrics.DroppedByCapacity, droppedByCapacity);
            metricsRW.ValueRW = metrics;
        }

        private static int ResolveSpawnCount(
            ref SourceSpawnPatternBuffer pattern,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            float area,
            float deltaTime)
        {
            float density = math.max(0f, pattern.SpawnDensityPerSecPerArea);
            float rate = density * area;

            if (rate <= 0f)
            {
                pattern.SpawnAccumulator = 0f;
                return 0;
            }

            pattern.SpawnAccumulator += rate * deltaTime;
            int spawnCount = (int)pattern.SpawnAccumulator;
            pattern.SpawnAccumulator -= spawnCount;

            if (spawnCount <= 0)
                return 0;

            if (pattern.SpawnMode != SourceSpawnModeId.CapAndMaxDensity)
                return spawnCount;

            int active = GetActiveCount(activeCounts, pattern.BulletTypeKey);
            int pending = GetPendingCount(requests, pattern.BulletTypeKey);
            int maxActive = (int)math.floor(math.max(0f, pattern.MaxActiveDensityPerArea) * area);
            int room = math.max(0, maxActive - active - pending);
            return math.min(spawnCount, room);
        }

        private static int GetActiveCount(DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey)
        {
            for (int i = 0; i < activeCounts.Length; i++)
            {
                if (activeCounts[i].BulletTypeKey == typeKey)
                    return activeCounts[i].ActiveCount;
            }

            return 0;
        }

        private static int GetPendingCount(DynamicBuffer<SourceSpawnRequestBuffer> requests, int typeKey)
        {
            int pending = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.BulletTypeKey != typeKey)
                    continue;
                if (item.Count <= 0)
                    continue;

                pending = SafeAdd(pending, item.Count);
            }

            return pending;
        }

        private static void AddOrMergeRequest(DynamicBuffer<SourceSpawnRequestBuffer> requests, int typeKey, int count, uint frame)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.BulletTypeKey != typeKey)
                    continue;

                if (item.Count <= 0)
                    item.OldestFrame = frame;

                item.Count = SafeAdd(item.Count, count);
                requests[i] = item;
                return;
            }

            requests.Add(new SourceSpawnRequestBuffer
            {
                BulletTypeKey = typeKey,
                Count = count,
                OldestFrame = frame,
            });
        }

        private static void CompactRequestBuffer(DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                if (requests[i].Count > 0)
                    continue;

                requests.RemoveAtSwapBack(i);
            }
        }

        private static int SafeAdd(int lhs, int rhs)
        {
            long v = (long)lhs + rhs;
            if (v > int.MaxValue)
                return int.MaxValue;
            if (v < int.MinValue)
                return int.MinValue;
            return (int)v;
        }
    }

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
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceAnchorComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceSpawnRuntimeComponent>();
            state.RequireForUpdate<SourceActiveBulletCountBuffer>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            var poolDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);
            poolDeps.Complete();
            state.Dependency = default;

            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();

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
            var sourceAreaLookup = SystemAPI.GetComponentLookup<BulletFieldAreaComponent>(true);
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
            sourceAreaLookup.Update(ref state);
            sourceRuntimeLookup.Update(ref state);
            pollutionConfigLookup.Update(ref state);
            pollutionGridLookup.Update(ref state);
            pollutionCellsLookup.Update(ref state);
            pollutionValidCellIndicesLookup.Update(ref state);

            using var sourceEntities = new NativeList<Entity>(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<SourceSpawnComponent>>()
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

                PruneExpiredAndCompactRequests(requests, frame, policy.MaxPendingAgeFrames, ref pending, ref expiredByAge);
            }

            if (expiredByAge > 0)
                metrics.ExpiredByAge = SafeAdd(metrics.ExpiredByAge, expiredByAge);
            metrics.LastFrameExpiredByAge = expiredByAge;

            int remainingBudget = math.max(0, policy.BudgetPerFrame);
            int sourceCount = sourceEntities.Length;
            int cursorIndex = math.clamp(cursorRW.ValueRO.SourceStartIndex, 0, math.max(0, sourceCount - 1));
            int budgetUsed = 0;
            int noSpawnPasses = 0;

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
                    int requestIndex = FindFirstSpawnableRequestIndex(requests, ref BulletFieldShared.FreeByKey);
                    if (requestIndex < 0)
                        continue;

                    int typeKey = requests[requestIndex].BulletTypeKey;
                    spawned = TrySpawnOneFromRequest(
                        sourceEntity,
                        typeKey,
                        ref sourceRuntimeLookup,
                        ref sourceAnchorLookup,
                        ref sourceAreaLookup,
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
                        frame);

                    if (spawned)
                    {
                        var item = requests[requestIndex];
                        item.Count = math.max(0, item.Count - 1);
                        if (item.Count <= 0)
                            item.OldestFrame = frame;
                        requests[requestIndex] = item;
                        pending--;
                        remainingBudget--;
                        budgetUsed++;
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

                CompactRequestBuffer(requests);
            }

            metrics.PendingCount = pending;
            metrics.LastFrameBudgetUsed = budgetUsed;
            if (pending > 0 && remainingBudget <= 0)
                metrics.DeferredByBudget = pending;
            if (pending > 0 && remainingBudget > 0)
                metrics.DeferredByPool = pending;

            metricsRW.ValueRW = metrics;
            cursorRW.ValueRW = new SpawnBudgetCursorComponent
            {
                SourceStartIndex = cursorIndex
            };
            BulletFieldShared.PoolFence = state.Dependency;
        }

        private static void PruneExpiredAndCompactRequests(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            uint frame,
            uint maxAge,
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
                        expiredByAge = SafeAdd(expiredByAge, item.Count);
                        requests.RemoveAtSwapBack(i);
                        continue;
                    }
                }

                pending = SafeAdd(pending, item.Count);
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
            ref NativeParallelMultiHashMap<int, Entity> freeByKey)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.Count <= 0)
                    continue;
                if (!freeByKey.ContainsKey(item.BulletTypeKey))
                    continue;

                return i;
            }

            return -1;
        }

        private static bool TrySpawnOneFromRequest(
            Entity sourceEntity,
            int requestedTypeKey,
            ref ComponentLookup<SourceSpawnRuntimeComponent> sourceRuntimeLookup,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<BulletFieldAreaComponent> sourceAreaLookup,
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
            uint frame)
        {
            if (!TryDequeueByKey(ref BulletFieldShared.FreeByKey, requestedTypeKey, out var bulletEntity))
                return false;

            var random = CreateSourceRandom(sourceEntity, ref sourceRuntimeLookup);
            float3 center = sourceAnchorLookup.HasComponent(sourceEntity)
                ? sourceAnchorLookup[sourceEntity].Position
                : float3.zero;
            var fieldArea = sourceAreaLookup.HasComponent(sourceEntity)
                ? sourceAreaLookup[sourceEntity]
                : default;

            float3 pos = SampleSpawnPosition(
                ref random,
                sourceEntity,
                center,
                in fieldArea,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup);

            float angle = random.NextFloat(0f, math.PI * 2f);
            float2 dir = new float2(math.cos(angle), math.sin(angle));
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

            if (activeCountLookup.TryGetBuffer(sourceEntity, out var activeCounts))
                IncrementActiveCount(activeCounts, requestedTypeKey);

            return true;
        }

        private static Unity.Mathematics.Random CreateSourceRandom(
            Entity sourceEntity,
            ref ComponentLookup<SourceSpawnRuntimeComponent> sourceRuntimeLookup)
        {
            uint seed = math.max(1u, (uint)(sourceEntity.Index + 1));
            if (sourceRuntimeLookup.HasComponent(sourceEntity))
            {
                var runtime = sourceRuntimeLookup[sourceEntity];
                seed = math.max(1u, runtime.SpawnSequence);
                runtime.SpawnSequence = seed + 1u;
                sourceRuntimeLookup[sourceEntity] = runtime;
            }

            return Unity.Mathematics.Random.CreateFromIndex(seed ^ (uint)sourceEntity.Index);
        }

        private static float3 SampleSpawnPosition(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            float3 center,
            in BulletFieldAreaComponent fieldArea,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
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
                return pollutionPos;
            }

            return SampleSpawnPositionUniform(ref random, center, fieldArea);
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
            if (config.SamplingMode != SourcePollutionSamplingModeId.TopK)
                return false;

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

        private static float3 SampleSpawnPositionUniform(
            ref Unity.Mathematics.Random random,
            float3 center,
            in BulletFieldAreaComponent fieldArea)
        {
            if (fieldArea.Shape == BulletFieldShapeId.Rectangle)
            {
                float2 half = math.max(0f, fieldArea.Size) * 0.5f;
                float2 offset = new float2(
                    random.NextFloat(-half.x, half.x),
                    random.NextFloat(-half.y, half.y));
                return new float3(center.x + offset.x, center.y, center.z + offset.y);
            }

            float radius = math.max(0f, fieldArea.Radius);
            float angle = random.NextFloat(0f, math.PI * 2f);
            float dist = math.sqrt(random.NextFloat(0f, 1f)) * radius;
            float2 offsetCircle = new float2(math.cos(angle), math.sin(angle)) * dist;
            return new float3(center.x + offsetCircle.x, center.y, center.z + offsetCircle.y);
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

        private static void IncrementActiveCount(DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey)
        {
            for (int i = 0; i < activeCounts.Length; i++)
            {
                var item = activeCounts[i];
                if (item.BulletTypeKey != typeKey)
                    continue;

                item.ActiveCount = SafeAdd(item.ActiveCount, 1);
                activeCounts[i] = item;
                return;
            }

            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = typeKey,
                ActiveCount = 1
            });
        }

        private static void CompactRequestBuffer(DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                if (requests[i].Count > 0)
                    continue;

                requests.RemoveAtSwapBack(i);
            }
        }

        private static int SafeAdd(int lhs, int rhs)
        {
            long v = (long)lhs + rhs;
            if (v > int.MaxValue)
                return int.MaxValue;
            if (v < int.MinValue)
                return int.MinValue;
            return (int)v;
        }
    }

}
