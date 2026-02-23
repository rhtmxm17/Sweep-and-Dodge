using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateBefore(typeof(SourceSpawnRequestBuildSystem))]
    public partial struct SourceOpeningWaveRequestBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceOpeningWavePatternBuffer>();
            state.RequireForUpdate<SourceOpeningWaveRuntimeComponent>();
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

            int pendingTotal = 0;
            foreach (var requests in SystemAPI.Query<DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                for (int i = 0; i < requests.Length; i++)
                    pendingTotal = SafeAdd(pendingTotal, math.max(0, requests[i].Count));
            }

            int remainingCapacity = math.max(0, policy.MaxPendingCount - pendingTotal);

            foreach (var (source, fieldArea, openingPatterns, runtimeRW, activeCounts, requests, sourceEntity) in
                     SystemAPI.Query<
                         RefRO<SourceSpawnComponent>,
                         RefRO<BulletFieldAreaComponent>,
                         DynamicBuffer<SourceOpeningWavePatternBuffer>,
                         RefRW<SourceOpeningWaveRuntimeComponent>,
                         DynamicBuffer<SourceActiveBulletCountBuffer>,
                         DynamicBuffer<SourceSpawnRequestBuffer>>()
                         .WithEntityAccess())
            {
                var runtime = runtimeRW.ValueRO;
                var sourceState = source.ValueRO.State;
                var patternsRW = openingPatterns;
                var requestsRW = requests;

                if (sourceState != runtime.LastState)
                {
                    if (HasTriggerPattern(patternsRW, sourceState))
                    {
                        runtime.IsPlaying = 1;
                        runtime.ActiveTriggerState = sourceState;
                        runtime.ElapsedSec = 0f;
                        ResetAccumulators(ref patternsRW, sourceState);
                    }
                    else if (runtime.IsPlaying != 0 && sourceState != runtime.ActiveTriggerState)
                    {
                        runtime.IsPlaying = 0;
                        runtime.ElapsedSec = 0f;
                    }

                    runtime.LastState = sourceState;
                }

                if (runtime.IsPlaying == 0 || sourceState != runtime.ActiveTriggerState)
                {
                    runtimeRW.ValueRW = runtime;
                    continue;
                }

                float area = math.max(0f, fieldArea.ValueRO.ComputedArea);
                float elapsed = runtime.ElapsedSec;
                float maxEnd = 0f;
                bool hasAnySegmentForTrigger = false;

                for (int i = 0; i < patternsRW.Length; i++)
                {
                    var pattern = patternsRW[i];
                    if (pattern.TriggerState != runtime.ActiveTriggerState)
                        continue;

                    hasAnySegmentForTrigger = true;
                    maxEnd = math.max(maxEnd, pattern.EndSec);

                    if (elapsed < pattern.StartSec || elapsed >= pattern.EndSec)
                    {
                        patternsRW[i] = pattern;
                        continue;
                    }

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
                }

                runtime.ElapsedSec += deltaTime;
                if (!hasAnySegmentForTrigger || runtime.ElapsedSec >= maxEnd)
                {
                    runtime.IsPlaying = 0;
                    runtime.ElapsedSec = 0f;
                }

                runtimeRW.ValueRW = runtime;
            }
        }

        private static bool HasTriggerPattern(DynamicBuffer<SourceOpeningWavePatternBuffer> patterns, SourceStateId triggerState)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                if (patterns[i].TriggerState == triggerState)
                    return true;
            }

            return false;
        }

        private static void ResetAccumulators(ref DynamicBuffer<SourceOpeningWavePatternBuffer> patterns, SourceStateId triggerState)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                var item = patterns[i];
                if (item.TriggerState != triggerState)
                    continue;

                item.SpawnAccumulator = 0f;
                patterns[i] = item;
            }
        }

        private static int ResolveSpawnCount(
            ref SourceOpeningWavePatternBuffer pattern,
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

                var random = CreateDeterministicRandom(sourceEntity, pattern.DirectiveId, frame, 0x68E31DA4u);
                spawnCount = SamplePoisson(lambda, ref random);
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
                if (item.BulletTypeKey != typeKey || item.Count <= 0)
                    continue;

                pending = SafeAdd(pending, item.Count);
            }

            return pending;
        }

        private static void AddOrMergeRequest(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            in SourceOpeningWavePatternBuffer pattern,
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
                FixedPoint = pattern.FixedPoint,
                SpawnOffset = pattern.SpawnOffset,
                SpawnSampleBudget = math.max(1, pattern.SpawnSampleBudget),
                PlayerNoSpawnRadius = math.max(0f, pattern.PlayerNoSpawnRadius),
                Count = count,
                OldestFrame = frame
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
