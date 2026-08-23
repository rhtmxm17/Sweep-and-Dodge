using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    internal static class SpawnRequestCommonUtility
    {
        private const float BurstScheduleEpsilon = 1e-5f;

        public static int SafeAdd(int lhs, int rhs)
        {
            long v = (long)lhs + rhs;
            if (v > int.MaxValue)
                return int.MaxValue;
            if (v < int.MinValue)
                return int.MinValue;
            return (int)v;
        }

        public static void CompactRequestBuffer(DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                if (requests[i].Count > 0)
                    continue;

                requests.RemoveAtSwapBack(i);
            }
        }

        public static int ResolveSpawnCountCore(
            ref float spawnAccumulator,
            ref int burstEventsEmitted,
            SourceSpawnEmissionModeId emissionMode,
            SourceSpawnModeId spawnMode,
            float meanEventsPerSec,
            float burstIntervalSec,
            int burstShotsPerEvent,
            int burstRepeatCount,
            float spawnDensityPerSecPerArea,
            float maxActiveDensityPerArea,
            int bulletTypeKey,
            uint runSeed,
            uint sourceStableId,
            int directiveId,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            float area,
            float deltaTime,
            uint deterministicSalt)
        {
            int shotsPerEvent = math.max(1, burstShotsPerEvent);
            int spawnCount;
            if (emissionMode == SourceSpawnEmissionModeId.Poisson)
            {
                spawnAccumulator = 0f;
                float lambda = math.max(0f, meanEventsPerSec) * math.max(0f, deltaTime);
                if (lambda <= 0f)
                    return 0;

                var random = CreateDeterministicRandom(runSeed, sourceStableId, directiveId, frame, deterministicSalt);
                int eventCount = SamplePoisson(lambda, ref random);
                spawnCount = SafeAdd(0, eventCount * shotsPerEvent);
            }
            else if (emissionMode == SourceSpawnEmissionModeId.EventBurst)
            {
                float interval = math.max(0.001f, burstIntervalSec);
                float previousActiveSec = math.max(0f, spawnAccumulator);
                float nextActiveSec = previousActiveSec + math.max(0f, deltaTime);
                int previousEventCount = CountEventBurstTriggers(previousActiveSec, interval);
                int eventCount = CountEventBurstTriggers(nextActiveSec, interval) - previousEventCount;
                if (eventCount <= 0)
                {
                    spawnAccumulator = nextActiveSec;
                    return 0;
                }

                if (burstRepeatCount >= 0)
                {
                    int remaining = math.max(0, burstRepeatCount - burstEventsEmitted);
                    if (remaining <= 0)
                    {
                        spawnAccumulator = nextActiveSec;
                        return 0;
                    }

                    eventCount = math.min(eventCount, remaining);
                }

                spawnAccumulator = nextActiveSec;
                burstEventsEmitted = SafeAdd(burstEventsEmitted, eventCount);
                spawnCount = SafeAdd(0, eventCount * shotsPerEvent);
            }
            else
            {
                float density = math.max(0f, spawnDensityPerSecPerArea);
                float rate = density * area;
                if (rate <= 0f)
                {
                    spawnAccumulator = 0f;
                    return 0;
                }

                spawnAccumulator += rate * deltaTime;
                spawnCount = (int)spawnAccumulator;
                spawnAccumulator -= spawnCount;
            }

            if (spawnCount <= 0)
                return 0;

            if (spawnMode != SourceSpawnModeId.CapAndMaxDensity)
                return spawnCount;

            int active = GetActiveCount(activeCounts, bulletTypeKey);
            int pending = GetPendingCount(requests, bulletTypeKey);
            int maxActive = (int)math.floor(math.max(0f, maxActiveDensityPerArea) * area);
            int room = math.max(0, maxActive - active - pending);

            if (emissionMode == SourceSpawnEmissionModeId.Poisson
                || emissionMode == SourceSpawnEmissionModeId.EventBurst)
            {
                int requestedEventCount = spawnCount / shotsPerEvent;
                int roomEventCount = room / shotsPerEvent;
                return math.max(0, math.min(requestedEventCount, roomEventCount)) * shotsPerEvent;
            }

            return math.min(spawnCount, room);
        }

        private static int CountEventBurstTriggers(float activeDurationSec, float intervalSec)
        {
            float safeDuration = math.max(0f, activeDurationSec);
            if (safeDuration <= 0f)
                return 0;

            float safeInterval = math.max(0.001f, intervalSec);
            return 1 + (int)math.floor(math.max(0f, safeDuration - BurstScheduleEpsilon) / safeInterval);
        }

        public static SourceSpawnRequestBuffer CreateRequestTemplate(
            int directiveId,
            int profileRefId,
            SourceWavePhaseId phase,
            SourceSpawnLaneId lane,
            int lanePriority,
            int bulletTypeKey,
            byte hasSpeedOverride,
            float speedOverride,
            byte hasLifetimeOverride,
            float lifetimeOverride,
            byte hasMovementOverride,
            BulletMovementFamilyId movementFamily,
            BulletDampedLinearDefinition dampedLinear,
            BulletHomingLiteDefinition homingLite,
            SourceSpawnEmissionModeId emissionMode,
            SourceSpawnModeId spawnMode,
            WaveSamplingAnchorModeId samplingAnchorMode,
            WaveAreaSamplerModeId areaSamplerMode,
            WavePositionPatternModeId positionPatternMode,
            WaveAimModeId aimMode,
            WaveAimSnapshotTimingId aimSnapshotTiming,
            float aimAngleOffsetDeg,
            WaveLineNormalSideId lineNormalSide,
            float lineNormalAngleOffsetDeg,
            WaveShotPatternModeId shotPatternMode,
            int shotCount,
            float nWayAngleSpacingDeg,
            int eventRepeatCount,
            float2 fixedPoint,
            float2 spawnOffset,
            float2 lineStart,
            float2 lineEnd,
            float sampleSpacing,
            int pointSetCount,
            float2 point0,
            float2 point1,
            float2 point2,
            float2 point3,
            int spawnSampleBudget,
            float playerNoSpawnRadius,
            float baseAngleDeg,
            float spiralStepDeg,
            SourceSpawnEventShotScheduleId eventShotSchedule,
            float eventShotIntervalSec)
        {
            return new SourceSpawnRequestBuffer
            {
                DirectiveId = directiveId,
                ProfileRefId = profileRefId,
                Phase = phase,
                Lane = lane,
                LanePriority = lanePriority,
                BulletTypeKey = bulletTypeKey,
                HasSpeedOverride = hasSpeedOverride,
                SpeedOverride = math.max(0.001f, speedOverride),
                HasLifetimeOverride = hasLifetimeOverride,
                LifetimeOverride = math.max(0.001f, lifetimeOverride),
                HasMovementOverride = hasMovementOverride,
                MovementFamily = movementFamily,
                DampedLinear = dampedLinear,
                HomingLite = homingLite,
                EmissionMode = emissionMode,
                SpawnMode = spawnMode,
                SamplingAnchorMode = samplingAnchorMode,
                AreaSamplerMode = areaSamplerMode,
                PositionPatternMode = positionPatternMode,
                AimMode = aimMode,
                AimSnapshotTiming = aimSnapshotTiming,
                AimAngleOffsetDeg = aimAngleOffsetDeg,
                LineNormalSide = lineNormalSide,
                LineNormalAngleOffsetDeg = lineNormalAngleOffsetDeg,
                ShotPatternMode = shotPatternMode,
                ShotCount = math.max(1, shotCount),
                NWayAngleSpacingDeg = shotPatternMode == WaveShotPatternModeId.NWay ? nWayAngleSpacingDeg : 0f,
                EventRepeatCount = math.max(1, eventRepeatCount),
                FixedPoint = fixedPoint,
                SpawnOffset = spawnOffset,
                LineStart = lineStart,
                LineEnd = lineEnd,
                SampleSpacing = math.max(0.001f, sampleSpacing),
                PointSetCount = math.clamp(pointSetCount, 0, 4),
                Point0 = point0,
                Point1 = point1,
                Point2 = point2,
                Point3 = point3,
                SpawnSampleBudget = math.max(1, spawnSampleBudget),
                PlayerNoSpawnRadius = math.max(0f, playerNoSpawnRadius),
                BaseAngleDeg = baseAngleDeg,
                SpiralStepDeg = spiralStepDeg,
                EventShotSchedule = eventShotSchedule,
                EventShotIntervalSec = math.max(0f, eventShotIntervalSec),
                EventShotElapsedSec = 0f,
                EventAnchorInitialized = 0,
                EventAnchorPosition = float3.zero,
                EventAimInitialized = 0,
                EventAimTargetPosition = float3.zero,
                SpawnSequence = 0u,
                Count = 0,
                OldestFrame = 0u,
            };
        }

        public static void AddOrMergeRequest(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            in SourceSpawnRequestBuffer requestTemplate,
            int count,
            uint frame)
        {
            if (count <= 0)
                return;

            if (UsesDiscreteEventIdentity(in requestTemplate))
            {
                int shotsPerEvent = ResolvePerEventBulletCount(in requestTemplate);
                int remaining = count;
                while (remaining > 0)
                {
                    int eventShotCount = math.min(shotsPerEvent, remaining);
                    var timedItem = requestTemplate;
                    timedItem.Count = eventShotCount;
                    timedItem.OldestFrame = frame;
                    timedItem.EventShotElapsedSec = 0f;
                    timedItem.EventAnchorInitialized = 0;
                    timedItem.EventAnchorPosition = float3.zero;
                    timedItem.EventAimInitialized = 0;
                    timedItem.EventAimTargetPosition = float3.zero;
                    requests.Add(timedItem);
                    remaining -= eventShotCount;
                }

                return;
            }

            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.DirectiveId != requestTemplate.DirectiveId)
                    continue;

                if (item.Count <= 0)
                    item.OldestFrame = frame;

                item.Count = SafeAdd(item.Count, count);
                requests[i] = item;
                return;
            }

            var itemToAdd = requestTemplate;
            itemToAdd.Count = count;
            itemToAdd.OldestFrame = frame;
            requests.Add(itemToAdd);
        }

        public static int ResolveShotPatternUnitCount(in SourceClipPatternBuffer pattern)
        {
            return pattern.ShotPatternMode switch
            {
                WaveShotPatternModeId.NWay => math.max(1, pattern.ShotCount),
                WaveShotPatternModeId.Radial => math.max(1, pattern.ShotCount),
                _ => 1,
            };
        }

        public static int ResolveShotPatternUnitCount(in SourceSpawnRequestBuffer request)
        {
            return request.ShotPatternMode switch
            {
                WaveShotPatternModeId.NWay => math.max(1, request.ShotCount),
                WaveShotPatternModeId.Radial => math.max(1, request.ShotCount),
                _ => 1,
            };
        }

        public static int ResolvePerEventBulletCount(in SourceClipPatternBuffer pattern)
        {
            return math.max(1, pattern.EventRepeatCount) * ResolveShotPatternUnitCount(in pattern);
        }

        public static int ResolvePerEventBulletCount(in SourceSpawnRequestBuffer request)
        {
            return math.max(1, request.EventRepeatCount) * ResolveShotPatternUnitCount(in request);
        }

        public static bool UsesDiscreteEventIdentity(in SourceClipPatternBuffer pattern)
        {
            return pattern.EmissionMode == SourceSpawnEmissionModeId.Poisson
                || pattern.EmissionMode == SourceSpawnEmissionModeId.EventBurst;
        }

        public static bool UsesDiscreteEventIdentity(in SourceSpawnRequestBuffer request)
        {
            return request.EmissionMode == SourceSpawnEmissionModeId.Poisson
                || request.EmissionMode == SourceSpawnEmissionModeId.EventBurst;
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
                if (item.BulletTypeKey != typeKey || item.Count <= 0)
                    continue;

                pending = SafeAdd(pending, item.Count);
            }

            return pending;
        }

        private static Unity.Mathematics.Random CreateDeterministicRandom(
            uint runSeed,
            uint sourceStableId,
            int directiveId,
            uint frame,
            uint salt)
        {
            uint seed = math.hash(new uint4(
                math.max(1u, runSeed),
                math.max(1u, sourceStableId),
                (uint)math.max(0, directiveId + 1),
                frame ^ salt));
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

        public static bool TryDequeueByKey(
            ref NativeParallelMultiHashMap<int, Entity> freeByKey,
            int key,
            out Entity entity)
        {
            if (!freeByKey.TryGetFirstValue(key, out entity, out var iterator))
                return false;

            freeByKey.Remove(iterator);
            return true;
        }

        public static int CountFreeByKey(ref NativeParallelMultiHashMap<int, Entity> freeByKey, int key)
        {
            if (!freeByKey.TryGetFirstValue(key, out var _, out var iterator))
                return 0;

            int count = 1;
            while (freeByKey.TryGetNextValue(out _, ref iterator))
                count++;

            return count;
        }

        public static void IncrementActiveCount(DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey)
        {
            IncrementActiveCount(activeCounts, typeKey, 1);
        }

        public static void IncrementActiveCount(
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            int typeKey,
            int amount)
        {
            for (int i = 0; i < activeCounts.Length; i++)
            {
                var item = activeCounts[i];
                if (item.BulletTypeKey != typeKey)
                    continue;

                item.ActiveCount = SafeAdd(item.ActiveCount, amount);
                activeCounts[i] = item;
                return;
            }

            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = typeKey,
                ActiveCount = math.max(0, amount)
            });
        }

        public static SpawnedBulletRuntimeTuning CreateRuntimeTuning(in SourceSpawnRequestBuffer request)
        {
            return new SpawnedBulletRuntimeTuning
            {
                ProfileRefId = request.ProfileRefId,
                HasSpeedOverride = request.HasSpeedOverride,
                SpeedOverride = math.max(0.001f, request.SpeedOverride),
                HasLifetimeOverride = request.HasLifetimeOverride,
                LifetimeOverride = math.max(0.001f, request.LifetimeOverride),
                HasMovementOverride = request.HasMovementOverride,
                MovementFamily = request.MovementFamily,
                DampedLinear = request.DampedLinear,
                HomingLite = request.HomingLite,
            };
        }

        public static SpawnedBulletRuntimeTuning CreateRuntimeTuning(in DiscreteEmitRequestBuffer request)
        {
            return new SpawnedBulletRuntimeTuning
            {
                ProfileRefId = request.ProfileRefId,
                HasSpeedOverride = request.HasSpeedOverride,
                SpeedOverride = math.max(0.001f, request.SpeedOverride),
                HasLifetimeOverride = request.HasLifetimeOverride,
                LifetimeOverride = math.max(0.001f, request.LifetimeOverride),
                HasMovementOverride = request.HasMovementOverride,
                MovementFamily = request.MovementFamily,
                DampedLinear = request.DampedLinear,
                HomingLite = request.HomingLite,
            };
        }

        public static void ApplySpawnedBulletState(
            Entity bulletEntity,
            Entity sourceEntity,
            int requestedTypeKey,
            in SpawnedBulletRuntimeTuning runtimeTuning,
            float3 pos,
            float2 dir,
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
            ref ComponentLookup<Parent> parentLookup)
        {
            float2 safeDir = math.normalizesafe(dir, new float2(1f, 0f));
            var rot = quaternion.LookRotationSafe(new float3(safeDir.x, 0f, safeDir.y), math.up());
            float bulletSpeed = speedLookup.HasComponent(bulletEntity)
                ? math.max(0f, speedLookup[bulletEntity].Value)
                : 0f;
            float bulletLifetime = lifeMaxLookup.HasComponent(bulletEntity)
                ? math.max(0f, lifeMaxLookup[bulletEntity].Value)
                : 0f;
            if (runtimeTuning.HasSpeedOverride != 0)
                bulletSpeed = math.max(0.001f, runtimeTuning.SpeedOverride);
            if (runtimeTuning.HasLifetimeOverride != 0)
                bulletLifetime = math.max(0.001f, runtimeTuning.LifetimeOverride);

            if (txLookup.HasComponent(bulletEntity))
                txLookup[bulletEntity] = LocalTransform.FromPositionRotationScale(pos, rot, 1f);

            var rootWorldMatrix = float4x4.TRS(pos, rot, new float3(1f, 1f, 1f));
            if (localToWorldLookup.HasComponent(bulletEntity))
                localToWorldLookup[bulletEntity] = new LocalToWorld { Value = rootWorldMatrix };

            if (velLookup.HasComponent(bulletEntity))
                velLookup[bulletEntity] = new BulletVelocityComponent { Value = safeDir * bulletSpeed };
            if (runtimeTuning.HasSpeedOverride != 0 && speedLookup.HasComponent(bulletEntity))
                speedLookup[bulletEntity] = new BulletSpeedComponent { Value = bulletSpeed };
            if (lifeLookup.HasComponent(bulletEntity))
                lifeLookup[bulletEntity] = new BulletLifetimeComponent { Value = bulletLifetime };
            if (runtimeTuning.HasLifetimeOverride != 0 && lifeMaxLookup.HasComponent(bulletEntity))
                lifeMaxLookup[bulletEntity] = new BulletLifetimeMaxComponent { Value = bulletLifetime };
            if (runtimeTuning.HasMovementOverride != 0 && movementRuntimeLookup.HasComponent(bulletEntity))
            {
                movementRuntimeLookup[bulletEntity] = new BulletMovementRuntimeComponent
                {
                    Family = runtimeTuning.MovementFamily,
                    DampedLinear = runtimeTuning.DampedLinear,
                    HomingLite = runtimeTuning.HomingLite,
                };
            }
            if (emissionProfileRefLookup.HasComponent(bulletEntity))
                emissionProfileRefLookup[bulletEntity] = new BulletEmissionProfileRefComponent { ProfileRefId = runtimeTuning.ProfileRefId };
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

            BulletLifecycleRequestUtility.ResetLifecycleRequestState(
                bulletEntity,
                ref despawnRequestLookup,
                ref lifecycleRequestLookup,
                ref lifecycleContactLookup);
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

                if (!toggled && renderLookup.HasComponent(bulletEntity))
                    renderLookup.SetComponentEnabled(bulletEntity, true);
            }
            else if (renderLookup.HasComponent(bulletEntity))
            {
                renderLookup.SetComponentEnabled(bulletEntity, true);
            }
        }
    }
}
