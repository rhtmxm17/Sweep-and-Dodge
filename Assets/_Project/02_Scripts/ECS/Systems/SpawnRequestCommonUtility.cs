using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    internal static class SpawnRequestCommonUtility
    {
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
            Entity sourceEntity,
            int directiveId,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            float area,
            float deltaTime,
            uint deterministicSalt)
        {
            int spawnCount;
            if (emissionMode == SourceSpawnEmissionModeId.Poisson)
            {
                spawnAccumulator = 0f;
                float lambda = math.max(0f, meanEventsPerSec) * math.max(0f, deltaTime);
                if (lambda <= 0f)
                    return 0;

                var random = CreateDeterministicRandom(sourceEntity, directiveId, frame, deterministicSalt);
                spawnCount = SamplePoisson(lambda, ref random);
            }
            else if (emissionMode == SourceSpawnEmissionModeId.EventBurst)
            {
                float interval = math.max(0.001f, burstIntervalSec);
                int shotsPerEvent = math.max(1, burstShotsPerEvent);
                spawnAccumulator += math.max(0f, deltaTime);
                int eventCount = (int)math.floor(spawnAccumulator / interval);
                if (eventCount <= 0)
                    return 0;

                if (burstRepeatCount >= 0)
                {
                    int remaining = math.max(0, burstRepeatCount - burstEventsEmitted);
                    if (remaining <= 0)
                    {
                        spawnAccumulator = 0f;
                        return 0;
                    }

                    eventCount = math.min(eventCount, remaining);
                }

                spawnAccumulator -= eventCount * interval;
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
            return math.min(spawnCount, room);
        }

        public static SourceSpawnRequestBuffer CreateRequestTemplate(
            int directiveId,
            SourceWavePhaseId phase,
            SourceSpawnLaneId lane,
            int lanePriority,
            int bulletTypeKey,
            SourceSpawnSamplingModeId samplingMode,
            SourceSpawnCenterModeId centerMode,
            SourceSpawnDirectionModeId directionMode,
            float2 fixedPoint,
            float2 spawnOffset,
            float2 lineStart,
            float2 lineEnd,
            float sampleSpacing,
            int spawnSampleBudget,
            float playerNoSpawnRadius,
            float baseAngleDeg,
            int nWayCount,
            float spiralStepDeg,
            int burstShotsPerEvent,
            int spawnPriority)
        {
            return new SourceSpawnRequestBuffer
            {
                DirectiveId = directiveId,
                Phase = phase,
                Lane = lane,
                LanePriority = lanePriority,
                BulletTypeKey = bulletTypeKey,
                SamplingMode = samplingMode,
                CenterMode = centerMode,
                DirectionMode = directionMode,
                FixedPoint = fixedPoint,
                SpawnOffset = spawnOffset,
                LineStart = lineStart,
                LineEnd = lineEnd,
                SampleSpacing = math.max(0.001f, sampleSpacing),
                SpawnSampleBudget = math.max(1, spawnSampleBudget),
                PlayerNoSpawnRadius = math.max(0f, playerNoSpawnRadius),
                BaseAngleDeg = baseAngleDeg,
                NWayCount = math.max(1, nWayCount),
                SpiralStepDeg = spiralStepDeg,
                BurstShotsPerEvent = math.max(1, burstShotsPerEvent),
                SpawnPriority = spawnPriority,
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
    }
}
