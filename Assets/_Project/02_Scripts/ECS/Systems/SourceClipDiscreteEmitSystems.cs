using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct SourceClipDiscreteEmitBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<SpawnRunSeedComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceRunDirectorStateComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceShapeDerivedComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceClipPatternBuffer>();
            state.RequireForUpdate<SourceSustainRuntimeLaneBuffer>();
            state.RequireForUpdate<SourceSustainRuntimeComponent>();
            state.RequireForUpdate<SourceEventRuntimeComponent>();
            state.RequireForUpdate<SourceEventQueueBuffer>();
            state.RequireForUpdate<SourceActiveBulletCountBuffer>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();

            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
                return;

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;

            var policy = SystemAPI.GetSingleton<SpawnRequestPolicyComponent>();
            uint runSeed = math.max(1u, SystemAPI.GetSingleton<SpawnRunSeedComponent>().Value);

            int pendingTotal = 0;
            foreach (var requests in SystemAPI.Query<DynamicBuffer<SourceSpawnRequestBuffer>>())
            {
                for (int i = 0; i < requests.Length; i++)
                    pendingTotal = SpawnRequestCommonUtility.SafeAdd(pendingTotal, math.max(0, requests[i].Count));
            }

            int remainingCapacity = math.max(0, policy.MaxPendingCount - pendingTotal);
            int droppedByCapacity = 0;

            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var derivedLookup = SystemAPI.GetComponentLookup<SourceShapeDerivedComponent>(true);
            var directorStateLookup = SystemAPI.GetComponentLookup<SourceRunDirectorStateComponent>(true);
            var sustainRuntimeLookup = SystemAPI.GetComponentLookup<SourceSustainRuntimeComponent>(false);
            var eventRuntimeLookup = SystemAPI.GetComponentLookup<SourceEventRuntimeComponent>(false);
            var clipPatternLookup = SystemAPI.GetBufferLookup<SourceClipPatternBuffer>(false);
            var sustainLaneLookup = SystemAPI.GetBufferLookup<SourceSustainRuntimeLaneBuffer>(false);
            var eventQueueLookup = SystemAPI.GetBufferLookup<SourceEventQueueBuffer>(false);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            var pollutionCellsLookup = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(true);

            stableIdLookup.Update(ref state);
            derivedLookup.Update(ref state);
            directorStateLookup.Update(ref state);
            sustainRuntimeLookup.Update(ref state);
            eventRuntimeLookup.Update(ref state);
            clipPatternLookup.Update(ref state);
            sustainLaneLookup.Update(ref state);
            eventQueueLookup.Update(ref state);
            activeCountLookup.Update(ref state);
            requestLookup.Update(ref state);
            pollutionCellsLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<SourceSpawnComponent>()
                .WithAll<SourceRunDirectorStateComponent>()
                .WithAll<SourceStableIdComponent>()
                .WithAll<BulletFieldAreaComponent>()
                .WithAll<SourceShapeDerivedComponent>()
                .WithAll<SourceSustainRuntimeComponent>()
                .WithAll<SourceEventRuntimeComponent>()
                .WithAll<SourceClipPatternBuffer>()
                .WithAll<SourceSustainRuntimeLaneBuffer>()
                .WithAll<SourceEventQueueBuffer>()
                .WithAll<SourceActiveBulletCountBuffer>()
                .WithAll<SourceSpawnRequestBuffer>()
                .Build();

            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            for (int si = 0; si < sourceEntities.Length; si++)
            {
                var sourceEntity = sourceEntities[si];
                var stableId = stableIdLookup[sourceEntity];
                var derived = derivedLookup[sourceEntity];
                var directorState = directorStateLookup[sourceEntity];
                var clipPatterns = clipPatternLookup[sourceEntity];
                var sustainLanes = sustainLaneLookup[sourceEntity];
                var eventQueue = eventQueueLookup[sourceEntity];
                var activeCounts = activeCountLookup[sourceEntity];
                var requests = requestLookup[sourceEntity];
                var sustainRuntime = sustainRuntimeLookup[sourceEntity];
                var eventRuntime = eventRuntimeLookup[sourceEntity];

                if (clipPatterns.Length <= 0)
                    continue;

                uint sourceStableId = math.max(1u, stableId.Value);
                var clipState = ResolveClipSelectionState(in directorState);
                float densityScale = ResolveDensityScale(in directorState);
                float fieldSamplingAreaScale = ResolveFieldSamplingAreaScale(sourceEntity, ref pollutionCellsLookup);
                var sustainLanesRW = sustainLanes;
                var eventQueueRW = eventQueue;
                var clipPatternsRW = clipPatterns;
                var requestsRW = requests;

                if (clipState != sustainRuntime.ActiveState)
                {
                    sustainRuntime.ActiveState = clipState;
                    StopAllSustain(ref sustainLanesRW, preserveLastClip: true);
                    QueueEventIfNeeded(ref eventQueueRW, in eventRuntime, clipState, frame);
                }

                TryStartQueuedEvent(
                    sourceEntity,
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
                        clipState,
                        runSeed,
                        sourceStableId,
                        frame,
                        deltaTime,
                        derived.ComputedArea,
                        fieldSamplingAreaScale,
                        densityScale,
                        ref clipPatternsRW,
                        ref activeCounts,
                        ref requestsRW,
                        ref eventRuntime,
                        ref pendingTotal,
                        ref remainingCapacity,
                        ref droppedByCapacity);
                }

                SpawnRequestCommonUtility.CompactRequestBuffer(requestsRW);
                sustainRuntimeLookup[sourceEntity] = sustainRuntime;
                eventRuntimeLookup[sourceEntity] = eventRuntime;
            }
        }

        private static SourceStateId ResolveClipSelectionState(in SourceRunDirectorStateComponent directorState)
        {
            if (directorState.State == RunDirectorSourceStateId.Finish)
                return SourceStateId.Depleted;

            var selected = directorState.SelectedClipState;
            return selected switch
            {
                SourceStateId.Normal => SourceStateId.Normal,
                SourceStateId.Weakened => SourceStateId.Weakened,
                SourceStateId.Depleted => SourceStateId.Depleted,
                _ => SourceStateId.Normal,
            };
        }

        private static float ResolveDensityScale(in SourceRunDirectorStateComponent directorState)
        {
            if (directorState.State == RunDirectorSourceStateId.Finish)
                return 1f;

            return math.max(0f, directorState.DensityScale);
        }

        private static void QueueEventIfNeeded(
            ref DynamicBuffer<SourceEventQueueBuffer> queue,
            in SourceEventRuntimeComponent eventRuntime,
            SourceStateId triggerState,
            uint frame)
        {
            if (eventRuntime.IsPlaying != 0 && eventRuntime.TriggerState == triggerState)
                return;

            for (int i = 0; i < queue.Length; i++)
            {
                if (queue[i].TriggerState == triggerState)
                    return;
            }

            queue.Add(new SourceEventQueueBuffer
            {
                TriggerState = triggerState,
                QueuedFrame = frame
            });
        }

        private static void TryStartQueuedEvent(
            Entity sourceEntity,
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
                    if (queued.TriggerState != SourceStateId.Depleted)
                    {
                        Debug.LogWarning(
                            $"[WaveClipV3] Event trigger has no clip. source={sourceEntity.Index}, state={queued.TriggerState}");
                    }
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
                    remainingCapacity = SpawnRequestCommonUtility.SafeAdd(remainingCapacity, removed);
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
            uint runSeed,
            uint sourceStableId,
            uint frame,
            float deltaTime,
            float fullArea,
            float fieldSamplingAreaScale,
            float densityScale,
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
            float clipDurationSec = 0f;
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
                clipDurationSec = math.max(clipDurationSec, pattern.ClipDurationSec);
                if (elapsed < pattern.LocalStartSec || elapsed >= pattern.LocalEndSec)
                {
                    patterns[i] = pattern;
                    continue;
                }

                int requested = ResolveSpawnCount(
                    ref pattern,
                    runSeed,
                    sourceStableId,
                    frame,
                    activeCounts,
                    requests,
                    fullArea,
                    fieldSamplingAreaScale,
                    deltaTime,
                    densityScale);
                patterns[i] = pattern;
                if (requested <= 0)
                    continue;

                int accepted = math.min(requested, remainingCapacity);
                if (accepted > 0)
                {
                    AddOrMergeRequest(requests, in pattern, accepted, frame);
                    pendingTotal = SpawnRequestCommonUtility.SafeAdd(pendingTotal, accepted);
                    remainingCapacity -= accepted;
                }

                int dropped = requested - accepted;
                if (dropped > 0)
                    droppedByCapacity = SpawnRequestCommonUtility.SafeAdd(droppedByCapacity, dropped);
            }

            eventRuntime.ElapsedSec += deltaTime;
            float clipEndSec = clipDurationSec > 0f ? clipDurationSec : maxEnd;
            if (!hasAnySegment || eventRuntime.ElapsedSec >= clipEndSec || sourceState != eventRuntime.TriggerState)
            {
                eventRuntime.IsPlaying = 0;
                eventRuntime.ActiveEventClipId = 0;
                eventRuntime.ElapsedSec = 0f;
            }
        }

        private static int ResolveSpawnCount(
            ref SourceClipPatternBuffer pattern,
            uint runSeed,
            uint sourceStableId,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            float fullArea,
            float fieldSamplingAreaScale,
            float deltaTime,
            float densityScale)
        {
            float spawnDensityPerSecPerArea = pattern.SpawnDensityPerSecPerArea;
            if (pattern.Phase == SourceWavePhaseId.Sustain
                && pattern.Lane == SourceSpawnLaneId.Trash
                && pattern.EmissionMode == SourceSpawnEmissionModeId.RateField)
            {
                spawnDensityPerSecPerArea *= math.max(0f, densityScale);
            }

            float effectiveArea = ResolveEffectiveSpawnArea(in pattern, fullArea, fieldSamplingAreaScale);
            return SpawnRequestCommonUtility.ResolveSpawnCountCore(
                ref pattern.SpawnAccumulator,
                ref pattern.BurstEventsEmitted,
                pattern.EmissionMode,
                pattern.SpawnMode,
                pattern.MeanEventsPerSec,
                pattern.BurstIntervalSec,
                SpawnRequestCommonUtility.UsesDiscreteEventIdentity(in pattern)
                    ? SpawnRequestCommonUtility.ResolvePerEventBulletCount(in pattern)
                    : 1,
                pattern.BurstRepeatCount,
                spawnDensityPerSecPerArea,
                pattern.MaxActiveDensityPerArea,
                pattern.BulletTypeKey,
                runSeed,
                sourceStableId,
                pattern.DirectiveId,
                frame,
                activeCounts,
                requests,
                effectiveArea,
                deltaTime,
                0xD8A89AF5u);
        }

        private static float ResolveFieldSamplingAreaScale(
            Entity sourceEntity,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup)
        {
            if (!pollutionCellsLookup.HasBuffer(sourceEntity))
                return 1f;

            var cells = pollutionCellsLookup[sourceEntity];
            int validCount = 0;
            int activeValidCount = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell.IsValid == 0)
                    continue;

                validCount++;
                if (cell.IsActive != 0)
                    activeValidCount++;
            }

            if (validCount <= 0)
                return 1f;

            return math.clamp((float)activeValidCount / validCount, 0f, 1f);
        }

        private static float ResolveEffectiveSpawnArea(
            in SourceClipPatternBuffer pattern,
            float fullArea,
            float fieldSamplingAreaScale)
        {
            if (!UsesFieldSamplingAreaScale(pattern.AreaSamplerMode))
                return math.max(0f, fullArea);

            return math.max(0f, fullArea) * math.clamp(fieldSamplingAreaScale, 0f, 1f);
        }

        private static bool UsesFieldSamplingAreaScale(WaveAreaSamplerModeId areaSamplerMode)
        {
            return areaSamplerMode == WaveAreaSamplerModeId.UniformField
                || areaSamplerMode == WaveAreaSamplerModeId.PollutionTopK;
        }

        private static int RemoveSustainPendingRequests(ref DynamicBuffer<SourceSpawnRequestBuffer> requests)
        {
            int removed = 0;
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                if (item.Count <= 0 || item.Phase != SourceWavePhaseId.Sustain)
                    continue;

                removed = SpawnRequestCommonUtility.SafeAdd(removed, item.Count);
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
            var template = SpawnRequestCommonUtility.CreateRequestTemplate(
                pattern.DirectiveId,
                pattern.Phase,
                pattern.Lane,
                pattern.LanePriority,
                pattern.BulletTypeKey,
                pattern.EmissionMode,
                pattern.SpawnMode,
                pattern.SamplingAnchorMode,
                pattern.AreaSamplerMode,
                pattern.PositionPatternMode,
                pattern.AimMode,
                pattern.AimSnapshotTiming,
                pattern.AimAngleOffsetDeg,
                pattern.LineNormalSide,
                pattern.LineNormalAngleOffsetDeg,
                pattern.ShotPatternMode,
                pattern.ShotCount,
                pattern.NWayAngleSpacingDeg,
                pattern.EventRepeatCount,
                pattern.FixedPoint,
                pattern.SpawnOffset,
                pattern.LineStart,
                pattern.LineEnd,
                pattern.SampleSpacing,
                pattern.PointSetCount,
                pattern.Point0,
                pattern.Point1,
                pattern.Point2,
                pattern.Point3,
                pattern.SpawnSampleBudget,
                pattern.PlayerNoSpawnRadius,
                pattern.BaseAngleDeg,
                pattern.SpiralStepDeg,
                pattern.EventShotSchedule,
                pattern.EventShotIntervalSec);
            SpawnRequestCommonUtility.AddOrMergeRequest(requests, in template, count, frame);
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
    }
}
