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
            var openingWaveRuntimeLookup = SystemAPI.GetComponentLookup<SourceOpeningWaveRuntimeComponent>(true);
            openingWaveRuntimeLookup.Update(ref state);

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

            foreach (var (source, fieldArea, patterns, activeCounts, requests, sourceEntity) in
                     SystemAPI.Query<
                         RefRO<SourceSpawnComponent>,
                         RefRO<BulletFieldAreaComponent>,
                         DynamicBuffer<SourceSpawnPatternBuffer>,
                         DynamicBuffer<SourceActiveBulletCountBuffer>,
                         DynamicBuffer<SourceSpawnRequestBuffer>>()
                         .WithEntityAccess())
            {
                var patternsRW = patterns;
                var requestsRW = requests;

                if (source.ValueRO.State == SourceStateId.Depleted)
                    continue;
                if (patternsRW.Length <= 0)
                    continue;

                bool suppressStateSustainedPattern = false;
                if (openingWaveRuntimeLookup.HasComponent(sourceEntity))
                {
                    var openingRuntime = openingWaveRuntimeLookup[sourceEntity];
                    suppressStateSustainedPattern = openingRuntime.IsPlaying != 0
                        && openingRuntime.ActiveTriggerState == source.ValueRO.State;
                }

                float area = math.max(0f, fieldArea.ValueRO.ComputedArea);
                for (int i = 0; i < patternsRW.Length; i++)
                {
                    var pattern = patternsRW[i];
                    if (pattern.State != source.ValueRO.State)
                        continue;
                    if (suppressStateSustainedPattern)
                        continue;

                    int requested = ResolveSpawnCount(ref pattern, sourceEntity, frame, activeCounts, requestsRW, area, deltaTime);
                    patternsRW[i] = pattern;
                    if (requested <= 0)
                        continue;

                    int accepted = math.min(requested, remainingCapacity);
                    if (accepted > 0)
                    {
                        AddOrMergeRequest(requestsRW, in pattern, accepted, frame);
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
            Entity sourceEntity,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            float area,
            float deltaTime)
        {
            int spawnCount;
            if (pattern.EmissionMode == SourceSpawnEmissionModeId.Poisson)
            {
                pattern.SpawnAccumulator = 0f;
                float lambda = math.max(0f, pattern.MeanEventsPerSec) * math.max(0f, deltaTime);
                if (lambda <= 0f)
                    return 0;

                var random = CreateDeterministicRandom(sourceEntity, pattern.DirectiveId, frame, 0xB5297A4Du);
                spawnCount = SamplePoisson(lambda, ref random);
            }
            else if (pattern.EmissionMode == SourceSpawnEmissionModeId.EventBurst)
            {
                float interval = math.max(0.001f, pattern.BurstIntervalSec);
                int shotsPerEvent = math.max(1, pattern.BurstShotsPerEvent);
                pattern.SpawnAccumulator += math.max(0f, deltaTime);
                int eventCount = (int)math.floor(pattern.SpawnAccumulator / interval);
                if (eventCount <= 0)
                    return 0;

                if (pattern.BurstRepeatCount >= 0)
                {
                    int remaining = math.max(0, pattern.BurstRepeatCount - pattern.BurstEventsEmitted);
                    if (remaining <= 0)
                    {
                        pattern.SpawnAccumulator = 0f;
                        return 0;
                    }

                    eventCount = math.min(eventCount, remaining);
                }

                pattern.SpawnAccumulator -= eventCount * interval;
                pattern.BurstEventsEmitted = SafeAdd(pattern.BurstEventsEmitted, eventCount);
                spawnCount = SafeAdd(0, eventCount * shotsPerEvent);
            }
            else
            {
                float density = math.max(0f, pattern.SpawnDensityPerSecPerArea);
                float rate = density * area;
                if (rate <= 0f)
                {
                    pattern.SpawnAccumulator = 0f;
                    return 0;
                }

                pattern.SpawnAccumulator += rate * deltaTime;
                spawnCount = (int)pattern.SpawnAccumulator;
                pattern.SpawnAccumulator -= spawnCount;
            }

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

        private static void AddOrMergeRequest(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            in SourceSpawnPatternBuffer pattern,
            int count,
            uint frame)
        {
            if (count <= 0)
                return;

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.DirectiveId != pattern.DirectiveId)
                    continue;

                if (item.Count <= 0)
                    item.OldestFrame = frame;

                item.Count = SafeAdd(item.Count, count);
                requests[i] = item;
                return;
            }

            requests.Add(new SourceSpawnRequestBuffer
            {
                DirectiveId = pattern.DirectiveId,
                BulletTypeKey = pattern.BulletTypeKey,
                SamplingMode = pattern.SamplingMode,
                CenterMode = pattern.CenterMode,
                DirectionMode = pattern.DirectionMode,
                FixedPoint = pattern.FixedPoint,
                SpawnOffset = pattern.SpawnOffset,
                LineStart = pattern.LineStart,
                LineEnd = pattern.LineEnd,
                SampleSpacing = math.max(0.001f, pattern.SampleSpacing),
                SpawnSampleBudget = math.max(1, pattern.SpawnSampleBudget),
                PlayerNoSpawnRadius = math.max(0f, pattern.PlayerNoSpawnRadius),
                BaseAngleDeg = pattern.BaseAngleDeg,
                NWayCount = math.max(1, pattern.NWayCount),
                SpiralStepDeg = pattern.SpiralStepDeg,
                BurstShotsPerEvent = math.max(1, pattern.BurstShotsPerEvent),
                SpawnPriority = pattern.SpawnPriority,
                SpawnSequence = 0u,
                Count = count,
                OldestFrame = frame,
            });
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(Entity sourceEntity, int directiveId, uint frame, uint salt)
        {
            uint seed = math.hash(new uint4(
                frame,
                (uint)math.max(0, sourceEntity.Index + 1),
                (uint)math.max(0, directiveId + 1),
                salt));
            return Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
        }

        private static int SamplePoisson(float lambda, ref Unity.Mathematics.Random random)
        {
            if (lambda <= 0f)
                return 0;

            if (lambda < 30f)
            {
                float l = math.exp(-lambda);
                int k = 0;
                float p = 1f;
                do
                {
                    k++;
                    p *= random.NextFloat(0f, 1f);
                } while (p > l);

                return math.max(0, k - 1);
            }

            float stdDev = math.sqrt(lambda);
            float n = SampleStandardNormal(ref random);
            return math.max(0, (int)math.round(lambda + stdDev * n));
        }

        private static float SampleStandardNormal(ref Unity.Mathematics.Random random)
        {
            float u1 = math.max(1e-7f, random.NextFloat(0f, 1f));
            float u2 = random.NextFloat(0f, 1f);
            return math.sqrt(-2f * math.log(u1)) * math.cos(2f * math.PI * u2);
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
                    int requestIndex = FindFirstSpawnableRequestIndex(requests, ref BulletFieldShared.FreeByKey);
                    if (requestIndex < 0)
                        continue;

                    var requestItem = requests[requestIndex];
                    spawned = TrySpawnOneFromRequest(
                        sourceEntity,
                        in requestItem,
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
                        hasPlayer,
                        playerPosition,
                        frame);

                    if (spawned)
                    {
                        var item = requests[requestIndex];
                        item.Count = math.max(0, item.Count - 1);
                        item.SpawnSequence = item.SpawnSequence + 1u;
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
            int bestIndex = -1;
            int bestPriority = int.MinValue;
            uint bestOldest = uint.MaxValue;

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.Count <= 0)
                    continue;
                if (!freeByKey.ContainsKey(item.BulletTypeKey))
                    continue;

                if (bestIndex < 0
                    || item.SpawnPriority > bestPriority
                    || (item.SpawnPriority == bestPriority && item.OldestFrame < bestOldest))
                {
                    bestIndex = i;
                    bestPriority = item.SpawnPriority;
                    bestOldest = item.OldestFrame;
                }
            }

            return bestIndex;
        }

        private static bool TrySpawnOneFromRequest(
            Entity sourceEntity,
            in SourceSpawnRequestBuffer request,
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
            bool hasPlayer,
            float3 playerPosition,
            uint frame)
        {
            int requestedTypeKey = request.BulletTypeKey;
            if (!TryDequeueByKey(ref BulletFieldShared.FreeByKey, requestedTypeKey, out var bulletEntity))
                return false;

            var random = CreateSourceRandom(sourceEntity, ref sourceRuntimeLookup);
            float3 center = ResolveSpawnCenter(
                sourceEntity,
                in request,
                ref sourceAnchorLookup,
                hasPlayer,
                playerPosition);
            var fieldArea = sourceAreaLookup.HasComponent(sourceEntity)
                ? sourceAreaLookup[sourceEntity]
                : default;

            float3 pos = SampleSpawnPosition(
                ref random,
                sourceEntity,
                in request,
                center,
                in fieldArea,
                hasPlayer,
                playerPosition,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup);

            float2 dir = ResolveSpawnDirection(ref random, in request);
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

        private static float2 ResolveSpawnDirection(
            ref Unity.Mathematics.Random random,
            in SourceSpawnRequestBuffer request)
        {
            float baseRad = math.radians(request.BaseAngleDeg);
            float angle;
            switch (request.DirectionMode)
            {
                case SourceSpawnDirectionModeId.Fixed:
                    angle = baseRad;
                    break;
                case SourceSpawnDirectionModeId.Spiral:
                {
                    float stepRad = math.radians(request.SpiralStepDeg);
                    angle = baseRad + stepRad * request.SpawnSequence;
                    break;
                }
                case SourceSpawnDirectionModeId.NWay:
                case SourceSpawnDirectionModeId.RadialBurst:
                {
                    int slotCount = ResolveDirectionalSlotCount(in request);
                    int slot = slotCount <= 1 ? 0 : (int)(request.SpawnSequence % (uint)slotCount);
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
            in BulletFieldAreaComponent fieldArea,
            bool hasPlayer,
            float3 playerPosition,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
        {
            int sampleBudget = math.max(1, request.SpawnSampleBudget);
            float noSpawnRadius = math.max(0f, request.PlayerNoSpawnRadius);
            float noSpawnRadiusSq = noSpawnRadius * noSpawnRadius;
            float3 lastSample = center;

            for (int i = 0; i < sampleBudget; i++)
            {
                uint sequence = request.SpawnSequence + (uint)i;
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
                        lastSample = SampleSpawnPositionUniform(ref random, center, fieldArea);
                    }
                }
                else if (request.SamplingMode == SourceSpawnSamplingModeId.LineEven)
                {
                    lastSample = SampleSpawnPositionLineEven(center, in request, sequence);
                }
                else if (request.SamplingMode == SourceSpawnSamplingModeId.PointSet)
                {
                    // PointSet 1차는 계약만 반영한다. 샘플러는 추후 전용 버퍼와 함께 활성화한다.
                    lastSample = SampleSpawnPositionUniform(ref random, center, fieldArea);
                }
                else
                {
                    lastSample = SampleSpawnPositionUniform(ref random, center, fieldArea);
                }

                if (!hasPlayer || noSpawnRadius <= 0f)
                    return lastSample;

                float2 delta = new float2(lastSample.x - playerPosition.x, lastSample.z - playerPosition.z);
                if (math.lengthsq(delta) >= noSpawnRadiusSq)
                    return lastSample;
            }

            return lastSample;
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
