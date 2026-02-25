using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(SourceSpawnRequestBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct SourceClipRequestBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();
            state.RequireForUpdate<SpawnRunSeedComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceClipPatternBuffer>();
            state.RequireForUpdate<SourceSustainSlotCandidateBuffer>();
            state.RequireForUpdate<SourceSustainRuntimeLaneBuffer>();
            state.RequireForUpdate<SourceSustainRuntimeComponent>();
            state.RequireForUpdate<SourceEventRuntimeComponent>();
            state.RequireForUpdate<SourceEventQueueBuffer>();
            state.RequireForUpdate<SourceActiveBulletCountBuffer>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            float deltaTime = SystemAPI.Time.DeltaTime;
            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();
            uint runSeed = math.max(1u, SystemAPI.GetSingleton<SpawnRunSeedComponent>().Value);

            int pendingTotal = 0;
            foreach (var requests in SystemAPI.Query<DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                for (int i = 0; i < requests.Length; i++)
                    pendingTotal = SafeAdd(pendingTotal, math.max(0, requests[i].Count));
            }

            int remainingCapacity = math.max(0, policy.MaxPendingCount - pendingTotal);
            int droppedByCapacity = 0;

            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var areaLookup = SystemAPI.GetComponentLookup<BulletFieldAreaComponent>(true);
            var sustainRuntimeLookup = SystemAPI.GetComponentLookup<SourceSustainRuntimeComponent>(false);
            var eventRuntimeLookup = SystemAPI.GetComponentLookup<SourceEventRuntimeComponent>(false);
            var clipPatternLookup = SystemAPI.GetBufferLookup<SourceClipPatternBuffer>(false);
            var sustainCandidateLookup = SystemAPI.GetBufferLookup<SourceSustainSlotCandidateBuffer>(false);
            var sustainLaneLookup = SystemAPI.GetBufferLookup<SourceSustainRuntimeLaneBuffer>(false);
            var eventQueueLookup = SystemAPI.GetBufferLookup<SourceEventQueueBuffer>(false);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);

            sourceLookup.Update(ref state);
            stableIdLookup.Update(ref state);
            areaLookup.Update(ref state);
            sustainRuntimeLookup.Update(ref state);
            eventRuntimeLookup.Update(ref state);
            clipPatternLookup.Update(ref state);
            sustainCandidateLookup.Update(ref state);
            sustainLaneLookup.Update(ref state);
            eventQueueLookup.Update(ref state);
            activeCountLookup.Update(ref state);
            requestLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<SourceSpawnComponent>()
                .WithAll<SourceStableIdComponent>()
                .WithAll<BulletFieldAreaComponent>()
                .WithAll<SourceSustainRuntimeComponent>()
                .WithAll<SourceEventRuntimeComponent>()
                .WithAll<SourceClipPatternBuffer>()
                .WithAll<SourceSustainSlotCandidateBuffer>()
                .WithAll<SourceSustainRuntimeLaneBuffer>()
                .WithAll<SourceEventQueueBuffer>()
                .WithAll<SourceActiveBulletCountBuffer>()
                .WithAll<SourceSpawnRequestBuffer>()
                .Build();

            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            for (int si = 0; si < sourceEntities.Length; si++)
            {
                var sourceEntity = sourceEntities[si];
                var source = sourceLookup[sourceEntity];
                var stableId = stableIdLookup[sourceEntity];
                var area = areaLookup[sourceEntity];
                var clipPatterns = clipPatternLookup[sourceEntity];
                var sustainCandidates = sustainCandidateLookup[sourceEntity];
                var sustainLanes = sustainLaneLookup[sourceEntity];
                var eventQueue = eventQueueLookup[sourceEntity];
                var activeCounts = activeCountLookup[sourceEntity];
                var requests = requestLookup[sourceEntity];
                var sustainRuntime = sustainRuntimeLookup[sourceEntity];
                var eventRuntime = eventRuntimeLookup[sourceEntity];

                if (clipPatterns.Length <= 0)
                    continue;

                uint sourceStableId = math.max(1u, stableId.Value);
                var sourceState = source.State;
                var sustainLanesRW = sustainLanes;
                var eventQueueRW = eventQueue;
                var clipPatternsRW = clipPatterns;
                var requestsRW = requests;

                if (sourceState != sustainRuntime.ActiveState)
                {
                    sustainRuntime.ActiveState = sourceState;
                    StopAllSustain(ref sustainLanesRW, preserveLastClip: true);
                    QueueEvent(ref eventQueueRW, sourceState, frame);
                }

                TryStartQueuedEvent(
                    sourceEntity,
                    sourceState,
                    runSeed,
                    sourceStableId,
                    frame,
                    ref clipPatternsRW,
                    ref eventQueueRW,
                    ref eventRuntime,
                    ref sustainLanesRW,
                    ref requestsRW,
                    ref pendingTotal,
                    ref remainingCapacity);

                if (eventRuntime.IsPlaying != 0)
                {
                    ProcessActiveEventClip(
                        sourceEntity,
                        sourceState,
                        frame,
                        deltaTime,
                        area.ComputedArea,
                        ref clipPatternsRW,
                        ref activeCounts,
                        ref requestsRW,
                        ref eventRuntime,
                        ref pendingTotal,
                        ref remainingCapacity,
                        ref droppedByCapacity);
                }
                else
                {
                    ProcessSustainLanes(
                        sourceEntity,
                        sourceState,
                        runSeed,
                        sourceStableId,
                        frame,
                        deltaTime,
                        area.ComputedArea,
                        ref clipPatternsRW,
                        ref sustainCandidates,
                        ref sustainLanesRW,
                        ref activeCounts,
                        ref requestsRW,
                        ref pendingTotal,
                        ref remainingCapacity,
                        ref droppedByCapacity);
                }

                CompactRequestBuffer(requestsRW);
                sustainRuntimeLookup[sourceEntity] = sustainRuntime;
                eventRuntimeLookup[sourceEntity] = eventRuntime;
            }

            var metricsRW = SystemAPI.GetSingletonRW<SpawnBacklogMetricsComponent>();
            var metrics = metricsRW.ValueRO;
            metrics.PendingCount = pendingTotal;
            metrics.LastFrameDroppedByCapacity = SafeAdd(metrics.LastFrameDroppedByCapacity, droppedByCapacity);
            if (droppedByCapacity > 0)
                metrics.DroppedByCapacity = SafeAdd(metrics.DroppedByCapacity, droppedByCapacity);
            metricsRW.ValueRW = metrics;
        }

        private static void QueueEvent(ref DynamicBuffer<SourceEventQueueBuffer> queue, SourceStateId triggerState, uint frame)
        {
            queue.Add(new SourceEventQueueBuffer
            {
                TriggerState = triggerState,
                QueuedFrame = frame
            });
        }

        private static void TryStartQueuedEvent(
            Entity sourceEntity,
            SourceStateId sourceState,
            uint runSeed,
            uint stableId,
            uint frame,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref DynamicBuffer<SourceEventQueueBuffer> eventQueue,
            ref SourceEventRuntimeComponent eventRuntime,
            ref DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainLanes,
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests,
            ref int pendingTotal,
            ref int remainingCapacity)
        {
            if (eventRuntime.IsPlaying != 0 || eventQueue.Length <= 0)
                return;

            while (eventQueue.Length > 0)
            {
                var queued = eventQueue[0];
                eventQueue.RemoveAt(0);

                if (!TrySelectEventClipId(
                        runSeed,
                        stableId,
                        queued.TriggerState,
                        ref patterns,
                        ref eventRuntime,
                        out int selectedClipId))
                {
                    Debug.LogError(
                        $"[WaveClipV3] Event trigger has no clip. source={sourceEntity.Index}, state={queued.TriggerState}");
                    continue;
                }

                eventRuntime.IsPlaying = 1;
                eventRuntime.ActiveEventClipId = selectedClipId;
                eventRuntime.TriggerState = queued.TriggerState;
                eventRuntime.ElapsedSec = 0f;

                ResetClipAccumulators(
                    ref patterns,
                    selectedClipId,
                    SourceWavePhaseId.OnStateEnterOnce,
                    queued.TriggerState,
                    default,
                    useLaneFilter: false);

                StopAllSustain(ref sustainLanes, preserveLastClip: true);

                int removed = RemoveSustainPendingRequests(ref requests);
                if (removed > 0)
                {
                    pendingTotal = math.max(0, pendingTotal - removed);
                    remainingCapacity = SafeAdd(remainingCapacity, removed);
                }

                break;
            }
        }

        private static bool TrySelectEventClipId(
            uint runSeed,
            uint stableId,
            SourceStateId triggerState,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref SourceEventRuntimeComponent eventRuntime,
            out int clipId)
        {
            clipId = 0;
            using var clipCandidates = new NativeList<int>(Allocator.Temp);
            for (int i = 0; i < patterns.Length; i++)
            {
                var p = patterns[i];
                if (p.Phase != SourceWavePhaseId.OnStateEnterOnce)
                    continue;
                if (p.TriggerState != triggerState)
                    continue;

                if (!ContainsClipId(clipCandidates, p.ClipId))
                    clipCandidates.Add(p.ClipId);
            }

            if (clipCandidates.Length <= 0)
                return false;

            uint slotKey = math.hash(new uint4((uint)triggerState, (uint)SourceWavePhaseId.OnStateEnterOnce, 0u, 0x7F4A7C15u));
            var random = CreateSelectionRandom(runSeed, stableId, slotKey, eventRuntime.SelectionSequence);
            eventRuntime.SelectionSequence += 1u;
            int idx = random.NextInt(0, clipCandidates.Length);
            clipId = clipCandidates[idx];
            return true;
        }

        private static void ProcessActiveEventClip(
            Entity sourceEntity,
            SourceStateId sourceState,
            uint frame,
            float deltaTime,
            float area,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests,
            ref SourceEventRuntimeComponent eventRuntime,
            ref int pendingTotal,
            ref int remainingCapacity,
            ref int droppedByCapacity)
        {
            if (eventRuntime.IsPlaying == 0 || eventRuntime.ActiveEventClipId <= 0)
                return;

            float elapsed = eventRuntime.ElapsedSec;
            float maxEnd = 0f;
            bool hasAnySegment = false;
            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (pattern.Phase != SourceWavePhaseId.OnStateEnterOnce)
                    continue;
                if (pattern.ClipId != eventRuntime.ActiveEventClipId)
                    continue;
                if (pattern.TriggerState != eventRuntime.TriggerState)
                    continue;

                hasAnySegment = true;
                maxEnd = math.max(maxEnd, pattern.LocalEndSec);
                if (elapsed < pattern.LocalStartSec || elapsed >= pattern.LocalEndSec)
                {
                    patterns[i] = pattern;
                    continue;
                }

                int requested = ResolveSpawnCount(ref pattern, sourceEntity, frame, activeCounts, requests, area, deltaTime);
                patterns[i] = pattern;
                if (requested <= 0)
                    continue;

                int accepted = math.min(requested, remainingCapacity);
                if (accepted > 0)
                {
                    AddOrMergeRequest(requests, in pattern, accepted, frame);
                    pendingTotal = SafeAdd(pendingTotal, accepted);
                    remainingCapacity -= accepted;
                }

                int dropped = requested - accepted;
                if (dropped > 0)
                    droppedByCapacity = SafeAdd(droppedByCapacity, dropped);
            }

            eventRuntime.ElapsedSec += deltaTime;
            if (!hasAnySegment || eventRuntime.ElapsedSec >= maxEnd || sourceState != eventRuntime.TriggerState)
            {
                eventRuntime.IsPlaying = 0;
                eventRuntime.ActiveEventClipId = 0;
                eventRuntime.ElapsedSec = 0f;
            }
        }

        private static void ProcessSustainLanes(
            Entity sourceEntity,
            SourceStateId sourceState,
            uint runSeed,
            uint stableId,
            uint frame,
            float deltaTime,
            float area,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref DynamicBuffer<SourceSustainSlotCandidateBuffer> sustainCandidates,
            ref DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainLanes,
            ref DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests,
            ref int pendingTotal,
            ref int remainingCapacity,
            ref int droppedByCapacity)
        {
            for (int i = 0; i < sustainLanes.Length; i++)
            {
                var laneRuntime = sustainLanes[i];
                if (laneRuntime.ActiveClipId <= 0)
                {
                    if (!TrySelectNextSustainClip(
                            sourceEntity,
                            sourceState,
                            runSeed,
                            stableId,
                            frame,
                            ref sustainCandidates,
                            ref patterns,
                            ref laneRuntime))
                    {
                        sustainLanes[i] = laneRuntime;
                        continue;
                    }
                }

                float elapsed = laneRuntime.ElapsedSec;
                float maxEnd = 0f;
                bool hasAnySegment = false;

                for (int p = 0; p < patterns.Length; p++)
                {
                    var pattern = patterns[p];
                    if (pattern.Phase != SourceWavePhaseId.Sustain)
                        continue;
                    if (pattern.ClipId != laneRuntime.ActiveClipId)
                        continue;
                    if (pattern.TriggerState != sourceState)
                        continue;
                    if (pattern.Lane != laneRuntime.Lane)
                        continue;

                    hasAnySegment = true;
                    maxEnd = math.max(maxEnd, pattern.LocalEndSec);
                    if (elapsed < pattern.LocalStartSec || elapsed >= pattern.LocalEndSec)
                    {
                        patterns[p] = pattern;
                        continue;
                    }

                    int requested = ResolveSpawnCount(ref pattern, sourceEntity, frame, activeCounts, requests, area, deltaTime);
                    patterns[p] = pattern;
                    if (requested <= 0)
                        continue;

                    int accepted = math.min(requested, remainingCapacity);
                    if (accepted > 0)
                    {
                        AddOrMergeRequest(requests, in pattern, accepted, frame);
                        pendingTotal = SafeAdd(pendingTotal, accepted);
                        remainingCapacity -= accepted;
                    }

                    int dropped = requested - accepted;
                    if (dropped > 0)
                        droppedByCapacity = SafeAdd(droppedByCapacity, dropped);
                }

                laneRuntime.ElapsedSec += deltaTime;
                if (!hasAnySegment || laneRuntime.ElapsedSec >= maxEnd)
                {
                    laneRuntime.LastClipId = laneRuntime.ActiveClipId;
                    laneRuntime.ActiveClipId = 0;
                    laneRuntime.ElapsedSec = 0f;
                }

                sustainLanes[i] = laneRuntime;
            }
        }

        private static bool TrySelectNextSustainClip(
            Entity sourceEntity,
            SourceStateId sourceState,
            uint runSeed,
            uint stableId,
            uint frame,
            ref DynamicBuffer<SourceSustainSlotCandidateBuffer> sustainCandidates,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref SourceSustainRuntimeLaneBuffer laneRuntime)
        {
            int totalMatching = 0;
            int nonLastMatching = 0;
            for (int i = 0; i < sustainCandidates.Length; i++)
            {
                var item = sustainCandidates[i];
                if (item.State != sourceState || item.Lane != laneRuntime.Lane)
                    continue;

                totalMatching++;
                if (item.ClipId != laneRuntime.LastClipId)
                    nonLastMatching++;
            }

            if (totalMatching <= 0)
            {
                if (frame == 0 || frame - laneRuntime.LastMissingLogFrame >= 60u)
                {
                    Debug.LogError(
                        $"[WaveClipV3] Sustain lane has no clip candidates. source={sourceEntity.Index}, state={sourceState}, lane={laneRuntime.Lane}");
                    laneRuntime.LastMissingLogFrame = frame;
                }

                return false;
            }

            bool excludeLast = laneRuntime.LastClipId > 0 && nonLastMatching > 0;
            float totalWeight = 0f;
            int eligibleCount = 0;
            for (int i = 0; i < sustainCandidates.Length; i++)
            {
                var item = sustainCandidates[i];
                if (item.State != sourceState || item.Lane != laneRuntime.Lane)
                    continue;
                if (excludeLast && item.ClipId == laneRuntime.LastClipId)
                    continue;

                eligibleCount++;
                totalWeight += math.max(0.0001f, item.Weight);
            }

            if (eligibleCount <= 0 || totalWeight <= 0f)
                return false;

            uint slotKey = math.hash(new uint4((uint)sourceState, (uint)SourceWavePhaseId.Sustain, (uint)laneRuntime.Lane, 0xA341316Cu));
            var random = CreateSelectionRandom(runSeed, stableId, slotKey, laneRuntime.SelectionSequence);
            laneRuntime.SelectionSequence += 1u;
            float pick = random.NextFloat(0f, totalWeight);
            float accum = 0f;
            int selectedClipId = 0;
            for (int i = 0; i < sustainCandidates.Length; i++)
            {
                var item = sustainCandidates[i];
                if (item.State != sourceState || item.Lane != laneRuntime.Lane)
                    continue;
                if (excludeLast && item.ClipId == laneRuntime.LastClipId)
                    continue;

                accum += math.max(0.0001f, item.Weight);
                if (pick > accum)
                    continue;

                selectedClipId = item.ClipId;
                break;
            }

            if (selectedClipId <= 0)
                selectedClipId = sustainCandidates[sustainCandidates.Length - 1].ClipId;

            laneRuntime.ActiveClipId = selectedClipId;
            laneRuntime.ElapsedSec = 0f;
            laneRuntime.LastMissingLogFrame = 0u;

            ResetClipAccumulators(
                ref patterns,
                selectedClipId,
                SourceWavePhaseId.Sustain,
                sourceState,
                laneRuntime.Lane,
                useLaneFilter: true);

            return true;
        }

        private static int ResolveSpawnCount(
            ref SourceClipPatternBuffer pattern,
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

                var random = CreateDeterministicRandom(sourceEntity, pattern.DirectiveId, frame, 0xD8A89AF5u);
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
                if (item.BulletTypeKey != typeKey || item.Count <= 0)
                    continue;

                pending = SafeAdd(pending, item.Count);
            }

            return pending;
        }

        private static int RemoveSustainPendingRequests(ref DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            int removed = 0;
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                if (item.Count <= 0 || item.Phase != SourceWavePhaseId.Sustain)
                    continue;

                removed = SafeAdd(removed, item.Count);
                requests.RemoveAtSwapBack(i);
            }

            return removed;
        }

        private static void StopAllSustain(ref DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainLanes, bool preserveLastClip)
        {
            for (int i = 0; i < sustainLanes.Length; i++)
            {
                var lane = sustainLanes[i];
                if (preserveLastClip && lane.ActiveClipId > 0)
                    lane.LastClipId = lane.ActiveClipId;

                lane.ActiveClipId = 0;
                lane.ElapsedSec = 0f;
                sustainLanes[i] = lane;
            }
        }

        private static void ResetClipAccumulators(
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            int clipId,
            SourceWavePhaseId phase,
            SourceStateId triggerState,
            SourceSpawnLaneId lane,
            bool useLaneFilter)
        {
            for (int i = 0; i < patterns.Length; i++)
            {
                var p = patterns[i];
                if (p.ClipId != clipId || p.Phase != phase || p.TriggerState != triggerState)
                    continue;
                if (useLaneFilter && p.Lane != lane)
                    continue;

                p.SpawnAccumulator = 0f;
                p.BurstEventsEmitted = 0;
                patterns[i] = p;
            }
        }

        private static void AddOrMergeRequest(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            in SourceClipPatternBuffer pattern,
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
                Phase = pattern.Phase,
                Lane = pattern.Lane,
                LanePriority = pattern.LanePriority,
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
                SpawnPriority = pattern.LanePriority,
                SpawnSequence = 0u,
                Count = count,
                OldestFrame = frame,
            });
        }

        private static bool ContainsClipId(NativeList<int> list, int value)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == value)
                    return true;
            }

            return false;
        }

        private static Unity.Mathematics.Random CreateSelectionRandom(
            uint runSeed,
            uint stableId,
            uint slotKey,
            uint selectionSequence)
        {
            uint seed = math.hash(new uint4(runSeed, stableId, slotKey, math.max(1u, selectionSequence)));
            return Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
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
}
