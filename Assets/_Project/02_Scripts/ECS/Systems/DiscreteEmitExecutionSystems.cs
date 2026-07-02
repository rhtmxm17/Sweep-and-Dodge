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
    [UpdateBefore(typeof(SpawnRequestRoundRobinExecutionSystem))]
    public partial struct DiscreteEmitExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<DiscreteEmitChannelSingletonTag>();
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

            var channelEntity = SystemAPI.GetSingletonEntity<DiscreteEmitChannelSingletonTag>();
            var requests = SystemAPI.GetBuffer<DiscreteEmitRequestBuffer>(channelEntity);
            var policy = SystemAPI.GetComponent<DiscreteEmitPolicyComponent>(channelEntity);
            var metrics = SystemAPI.GetComponent<DiscreteEmitBacklogMetricsComponent>(channelEntity);
            metrics.DeferredByBudget = 0;
            metrics.DeferredByPool = 0;
            metrics.LastFrameDroppedByCapacity = 0;
            metrics.LastFrameExpiredByAge = 0;
            metrics.LastFrameBudgetUsed = 0;

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

            bool hasPlayer = false;
            float3 playerPosition = float3.zero;
            foreach (var tx in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                hasPlayer = true;
                playerPosition = tx.ValueRO.Position;
                break;
            }

            int pending = 0;
            int expiredByAge = 0;
            PruneExpiredAndAdvanceRequests(requests, frame, policy.MaxPendingAgeFrames, deltaTime, ref pending, ref expiredByAge);
            if (expiredByAge > 0)
                metrics.ExpiredByAge = SpawnRequestCommonUtility.SafeAdd(metrics.ExpiredByAge, expiredByAge);
            metrics.LastFrameExpiredByAge = expiredByAge;

            int droppedByCapacity = 0;
            TrimOverflowTail(requests, policy.MaxPendingCount, ref pending, ref droppedByCapacity);
            if (droppedByCapacity > 0)
                metrics.DroppedByCapacity = SpawnRequestCommonUtility.SafeAdd(metrics.DroppedByCapacity, droppedByCapacity);
            metrics.LastFrameDroppedByCapacity = droppedByCapacity;

            int remainingBudget = math.max(0, policy.BudgetPerFrame);
            int budgetUsed = 0;
            while (remainingBudget > 0 && pending > 0)
            {
                int requestIndex = FindBestReadyRequestIndex(requests, ref BulletFieldShared.FreeByKey, remainingBudget, frame);
                if (requestIndex < 0)
                    break;

                var request = requests[requestIndex];
                int shotsPerRepeat = ResolveShotsPerRepeat(in request);
                if (!TrySpawnRepeat(
                        ref request,
                        hasPlayer,
                        playerPosition,
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
                        ref activeCountLookup))
                {
                    break;
                }

                request.RemainingRepeats = math.max(0, request.RemainingRepeats - 1);
                request.RepeatSequence += 1u;
                if (request.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed)
                {
                    float interval = math.max(0.001f, request.EventShotIntervalSec);
                    request.EventShotElapsedSec = math.max(0f, request.EventShotElapsedSec - interval);
                }

                pending = math.max(0, pending - shotsPerRepeat);
                remainingBudget = math.max(0, remainingBudget - shotsPerRepeat);
                budgetUsed = SpawnRequestCommonUtility.SafeAdd(budgetUsed, shotsPerRepeat);

                if (request.RemainingRepeats <= 0)
                    requests.RemoveAtSwapBack(requestIndex);
                else
                    requests[requestIndex] = request;
            }

            CompactCompletedRequests(requests);

            metrics.PendingCount = pending;
            metrics.LastFrameBudgetUsed = budgetUsed;
            if (pending > 0)
            {
                if (HasBudgetBlockedPendingRequest(requests, ref BulletFieldShared.FreeByKey, remainingBudget, frame))
                    metrics.DeferredByBudget = pending;
                else
                    metrics.DeferredByPool = pending;
            }

            SystemAPI.SetComponent(channelEntity, metrics);
            BulletFieldShared.PoolFence = state.Dependency;
        }

        private static void PruneExpiredAndAdvanceRequests(
            DynamicBuffer<DiscreteEmitRequestBuffer> requests,
            uint frame,
            uint maxAgeFrames,
            float deltaTime,
            ref int pending,
            ref int expiredByAge)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                if (item.RemainingRepeats <= 0)
                {
                    requests.RemoveAtSwapBack(i);
                    continue;
                }

                if (maxAgeFrames > 0 && frame - item.OldestFrame > maxAgeFrames)
                {
                    expiredByAge = SpawnRequestCommonUtility.SafeAdd(
                        expiredByAge,
                        DiscreteEmitRequestUtility.ResolvePendingBulletEquivalent(in item));
                    requests.RemoveAtSwapBack(i);
                    continue;
                }

                if (item.EventShotSchedule == SourceSpawnEventShotScheduleId.Timed)
                    item.EventShotElapsedSec = math.max(0f, item.EventShotElapsedSec + deltaTime);

                pending = SpawnRequestCommonUtility.SafeAdd(
                    pending,
                    DiscreteEmitRequestUtility.ResolvePendingBulletEquivalent(in item));
                requests[i] = item;
            }
        }

        private static void TrimOverflowTail(
            DynamicBuffer<DiscreteEmitRequestBuffer> requests,
            int maxPendingCount,
            ref int pending,
            ref int droppedByCapacity)
        {
            if (maxPendingCount <= 0)
            {
                droppedByCapacity = pending;
                pending = 0;
                requests.Clear();
                return;
            }

            for (int i = requests.Length - 1; i >= 0 && pending > maxPendingCount; i--)
            {
                var item = requests[i];
                int bulletEquivalent = DiscreteEmitRequestUtility.ResolvePendingBulletEquivalent(in item);
                droppedByCapacity = SpawnRequestCommonUtility.SafeAdd(droppedByCapacity, bulletEquivalent);
                pending = math.max(0, pending - bulletEquivalent);
                requests.RemoveAtSwapBack(i);
            }
        }

        private static int FindBestReadyRequestIndex(
            DynamicBuffer<DiscreteEmitRequestBuffer> requests,
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int remainingBudget,
            uint frame)
        {
            int bestIndex = -1;
            int bestPriority = int.MinValue;
            uint bestOldest = uint.MaxValue;
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.AnchorMode != DiscreteEmitAnchorMode.FixedWorld || item.RemainingRepeats <= 0)
                    continue;
                if (!IsReadyForConsume(in item, frame))
                    continue;

                int shotsPerRepeat = ResolveShotsPerRepeat(in item);
                if (remainingBudget < shotsPerRepeat)
                    continue;
                if (SpawnRequestCommonUtility.CountFreeByKey(ref freeByKey, item.BulletTypeKey) < shotsPerRepeat)
                    continue;

                if (bestIndex < 0
                    || item.Priority > bestPriority
                    || (item.Priority == bestPriority && item.OldestFrame < bestOldest))
                {
                    bestIndex = i;
                    bestPriority = item.Priority;
                    bestOldest = item.OldestFrame;
                }
            }

            return bestIndex;
        }

        private static bool HasBudgetBlockedPendingRequest(
            DynamicBuffer<DiscreteEmitRequestBuffer> requests,
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int remainingBudget,
            uint frame)
        {
            if (remainingBudget <= 0)
                return true;

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.AnchorMode != DiscreteEmitAnchorMode.FixedWorld || item.RemainingRepeats <= 0)
                    continue;
                if (!IsReadyForConsume(in item, frame))
                    continue;

                int shotsPerRepeat = ResolveShotsPerRepeat(in item);
                if (SpawnRequestCommonUtility.CountFreeByKey(ref freeByKey, item.BulletTypeKey) < shotsPerRepeat)
                    continue;
                if (remainingBudget < shotsPerRepeat)
                    return true;
            }

            return false;
        }

        private static bool IsReadyForConsume(in DiscreteEmitRequestBuffer request, uint frame)
        {
            if (request.ReadyFrame > frame)
                return false;
            if (request.EventShotSchedule != SourceSpawnEventShotScheduleId.Timed)
                return true;
            if (request.RepeatSequence == 0u)
                return true;

            return request.EventShotElapsedSec >= math.max(0.001f, request.EventShotIntervalSec);
        }

        private static int ResolveShotsPerRepeat(in DiscreteEmitRequestBuffer request)
        {
            return DiscreteEmitRequestUtility.ResolveShotPatternUnitCount(in request);
        }

        private static void CompactCompletedRequests(DynamicBuffer<DiscreteEmitRequestBuffer> requests)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                if (requests[i].RemainingRepeats > 0)
                    continue;

                requests.RemoveAtSwapBack(i);
            }
        }

        private static bool TrySpawnRepeat(
            ref DiscreteEmitRequestBuffer request,
            bool hasPlayer,
            float3 playerPosition,
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
            int shotsPerRepeat = ResolveShotsPerRepeat(in request);
            float3 repeatOrigin = ResolveRepeatOriginPosition(request.AnchorPosition, in request, request.RepeatSequence);
            var runtimeTuning = SpawnRequestCommonUtility.CreateRuntimeTuning(in request);
            for (int slotIndex = 0; slotIndex < shotsPerRepeat; slotIndex++)
            {
                if (!SpawnRequestCommonUtility.TryDequeueByKey(ref BulletFieldShared.FreeByKey, request.BulletTypeKey, out var bulletEntity))
                    return false;

                float2 direction = ResolveSpawnDirection(ref request, repeatOrigin, hasPlayer, playerPosition, request.RepeatSequence, slotIndex);
                SpawnRequestCommonUtility.ApplySpawnedBulletState(
                    bulletEntity,
                    request.SourceEntity,
                    request.BulletTypeKey,
                    in runtimeTuning,
                    repeatOrigin,
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
            }

            if (request.SourceEntity != Entity.Null && activeCountLookup.TryGetBuffer(request.SourceEntity, out var activeCounts))
                SpawnRequestCommonUtility.IncrementActiveCount(activeCounts, request.BulletTypeKey, shotsPerRepeat);

            return true;
        }

        private static float2 ResolveSpawnDirection(
            ref DiscreteEmitRequestBuffer request,
            float3 repeatOrigin,
            bool hasPlayer,
            float3 playerPosition,
            uint repeatSequence,
            int forcedSlotIndex)
        {
            float angle = ResolveBaseAimAngleRad(ref request, repeatOrigin, hasPlayer, playerPosition, repeatSequence);
            int slotCount = ResolveShotsPerRepeat(in request);
            int slot = slotCount <= 1 ? 0 : math.abs(forcedSlotIndex) % slotCount;
            if (slotCount > 1)
                angle += ResolveShotPatternAngleOffsetRad(in request, slotCount, slot);

            float2 dir = new float2(math.cos(angle), math.sin(angle));
            return math.normalizesafe(dir, new float2(1f, 0f));
        }

        private static float ResolveBaseAimAngleRad(
            ref DiscreteEmitRequestBuffer request,
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
                    float3 aimTarget = playerPosition;
                    if (request.AimSnapshotTiming == WaveAimSnapshotTimingId.EventStart)
                    {
                        if (request.EventAimInitialized == 0)
                        {
                            request.EventAimTargetPosition = hasPlayer ? playerPosition : repeatOrigin;
                            request.EventAimInitialized = 1;
                        }

                        aimTarget = request.EventAimTargetPosition;
                    }
                    else if (!hasPlayer)
                    {
                        aimTarget = repeatOrigin;
                    }

                    float2 delta = new float2(aimTarget.x - repeatOrigin.x, aimTarget.z - repeatOrigin.z);
                    float angle = math.lengthsq(delta) > 1e-6f ? math.atan2(delta.y, delta.x) : 0f;
                    return angle + math.radians(request.AimAngleOffsetDeg);
                }
                default:
                    return 0f;
            }
        }

        private static float ResolveLineNormalAimAngleRad(in DiscreteEmitRequestBuffer request)
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

        private static float ResolveShotPatternAngleOffsetRad(
            in DiscreteEmitRequestBuffer request,
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

        private static float3 ResolveRepeatOriginPosition(
            float3 anchorPosition,
            in DiscreteEmitRequestBuffer request,
            uint repeatSequence)
        {
            switch (request.PositionPatternMode)
            {
                case WavePositionPatternModeId.LineEven:
                    return SampleSpawnPositionLineEven(anchorPosition, in request, repeatSequence);
                case WavePositionPatternModeId.PointSet:
                    return TrySampleSpawnPositionPointSet(anchorPosition, in request, repeatSequence, out var pointSetPos)
                        ? pointSetPos
                        : anchorPosition;
                default:
                    return anchorPosition;
            }
        }

        private static bool TrySampleSpawnPositionPointSet(
            float3 center,
            in DiscreteEmitRequestBuffer request,
            uint sequence,
            out float3 position)
        {
            position = center;
            int pointCount = math.clamp(request.PointSetCount, 0, 4);
            if (pointCount <= 0)
                return false;

            int pointIndex = (int)(sequence % (uint)pointCount);
            float2 local = pointIndex switch
            {
                0 => request.Point0,
                1 => request.Point1,
                2 => request.Point2,
                3 => request.Point3,
                _ => float2.zero,
            };
            position = new float3(center.x + local.x, center.y, center.z + local.y);
            return true;
        }

        private static float3 SampleSpawnPositionLineEven(
            float3 center,
            in DiscreteEmitRequestBuffer request,
            uint sequence)
        {
            float2 segment = request.LineEnd - request.LineStart;
            float length = math.length(segment);
            if (length <= 1e-5f)
            {
                float2 mid = (request.LineStart + request.LineEnd) * 0.5f;
                return new float3(center.x + mid.x, center.y, center.z + mid.y);
            }

            float spacing = math.max(0.001f, request.SampleSpacing);
            int slotCount = math.max(1, (int)math.floor(length / spacing) + 1);
            int slotIndex = slotCount <= 1 ? 0 : (int)(sequence % (uint)slotCount);
            float t = slotCount <= 1 ? 0.5f : slotIndex / (float)(slotCount - 1);
            float2 local = math.lerp(request.LineStart, request.LineEnd, t);
            return new float3(center.x + local.x, center.y, center.z + local.y);
        }
    }
}
