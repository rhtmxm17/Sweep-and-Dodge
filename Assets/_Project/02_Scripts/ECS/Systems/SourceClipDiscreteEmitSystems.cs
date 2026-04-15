using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
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
            state.RequireForUpdate<SpawnRunSeedComponent>();
            state.RequireForUpdate<DiscreteEmitChannelSingletonTag>();
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

            uint runSeed = math.max(1u, SystemAPI.GetSingleton<SpawnRunSeedComponent>().Value);
            float3 playerPosition = float3.zero;
            bool hasPlayer = false;
            foreach (var tx in SystemAPI.Query<RefRO<LocalTransform>>().WithAll<PlayerTag>())
            {
                playerPosition = tx.ValueRO.Position;
                hasPlayer = true;
                break;
            }

            var discreteChannelEntity = SystemAPI.GetSingletonEntity<DiscreteEmitChannelSingletonTag>();
            var discreteRequests = SystemAPI.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannelEntity);

            var stableIdLookup = SystemAPI.GetComponentLookup<SourceStableIdComponent>(true);
            var anchorLookup = SystemAPI.GetComponentLookup<SourceAnchorComponent>(true);
            var derivedLookup = SystemAPI.GetComponentLookup<SourceShapeDerivedComponent>(true);
            var directorStateLookup = SystemAPI.GetComponentLookup<SourceRunDirectorStateComponent>(true);
            var sustainRuntimeLookup = SystemAPI.GetComponentLookup<SourceSustainRuntimeComponent>(false);
            var eventRuntimeLookup = SystemAPI.GetComponentLookup<SourceEventRuntimeComponent>(false);
            var clipPatternLookup = SystemAPI.GetBufferLookup<SourceClipPatternBuffer>(false);
            var sustainLaneLookup = SystemAPI.GetBufferLookup<SourceSustainRuntimeLaneBuffer>(false);
            var eventQueueLookup = SystemAPI.GetBufferLookup<SourceEventQueueBuffer>(false);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(true);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            var pollutionConfigLookup = SystemAPI.GetComponentLookup<SourcePollutionConfigComponent>(true);
            var pollutionGridLookup = SystemAPI.GetComponentLookup<SourcePollutionGridComponent>(true);
            var pollutionCellsLookup = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(true);
            var pollutionValidCellIndicesLookup = SystemAPI.GetBufferLookup<SourcePollutionValidCellIndexBuffer>(true);

            stableIdLookup.Update(ref state);
            anchorLookup.Update(ref state);
            derivedLookup.Update(ref state);
            directorStateLookup.Update(ref state);
            sustainRuntimeLookup.Update(ref state);
            eventRuntimeLookup.Update(ref state);
            clipPatternLookup.Update(ref state);
            sustainLaneLookup.Update(ref state);
            eventQueueLookup.Update(ref state);
            activeCountLookup.Update(ref state);
            requestLookup.Update(ref state);
            pollutionConfigLookup.Update(ref state);
            pollutionGridLookup.Update(ref state);
            pollutionCellsLookup.Update(ref state);
            pollutionValidCellIndicesLookup.Update(ref state);

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
                    ref requestsRW);

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
                        hasPlayer,
                        playerPosition,
                        ref clipPatternsRW,
                        activeCounts,
                        requestsRW,
                        ref discreteRequests,
                        ref eventRuntime,
                        ref anchorLookup,
                        ref pollutionConfigLookup,
                        ref pollutionGridLookup,
                        ref pollutionCellsLookup,
                        ref pollutionValidCellIndicesLookup);
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
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests)
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
                RemoveSustainPendingRequests(ref requests);

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
            bool hasPlayer,
            float3 playerPosition,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> legacyRequests,
            ref DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            ref SourceEventRuntimeComponent eventRuntime,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
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

                int requestedBullets = ResolveSpawnCount(
                    sourceEntity,
                    ref pattern,
                    runSeed,
                    sourceStableId,
                    frame,
                    activeCounts,
                    legacyRequests,
                    discreteRequests,
                    fullArea,
                    fieldSamplingAreaScale,
                    deltaTime,
                    densityScale);
                if (requestedBullets > 0)
                {
                    int shotsPerEvent = math.max(1, SpawnRequestCommonUtility.ResolvePerEventBulletCount(in pattern));
                    int occurrenceCount = math.max(0, requestedBullets / shotsPerEvent);
                    for (int occurrenceIndex = 0; occurrenceIndex < occurrenceCount; occurrenceIndex++)
                    {
                        float3 anchorPosition = ResolveDiscreteEventAnchorPosition(
                            sourceEntity,
                            in pattern,
                            hasPlayer,
                            playerPosition,
                            runSeed,
                            sourceStableId,
                            frame,
                            occurrenceIndex,
                            ref sourceAnchorLookup,
                            ref pollutionConfigLookup,
                            ref pollutionGridLookup,
                            ref pollutionCellsLookup,
                            ref pollutionValidCellIndicesLookup);
                        var seed = DiscreteEmitRequestUtility.BuildDiscreteEmitSeedFromWaveEvent(
                            sourceEntity,
                            in pattern,
                            anchorPosition,
                            pattern.DirectiveId,
                            priority: 0);
                        discreteRequests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, frame));
                    }

                    patterns[i] = pattern;
                }

                patterns[i] = pattern;
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
            Entity sourceEntity,
            ref SourceClipPatternBuffer pattern,
            uint runSeed,
            uint sourceStableId,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> legacyRequests,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
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
            int requested = SpawnRequestCommonUtility.ResolveSpawnCountCore(
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
                legacyRequests,
                effectiveArea,
                deltaTime,
                0xD8A89AF5u);

            if (requested <= 0 || pattern.SpawnMode != SourceSpawnModeId.CapAndMaxDensity)
                return requested;

            int active = ResolveActiveCount(activeCounts, pattern.BulletTypeKey);
            int combinedPending = ResolveCombinedPendingForSourceType(
                sourceEntity,
                pattern.BulletTypeKey,
                legacyRequests,
                discreteRequests);
            int maxActive = (int)math.floor(math.max(0f, pattern.MaxActiveDensityPerArea) * effectiveArea);
            int room = math.max(0, maxActive - active - combinedPending);
            if (!SpawnRequestCommonUtility.UsesDiscreteEventIdentity(in pattern))
                return math.min(requested, room);

            int shotsPerEvent = math.max(1, SpawnRequestCommonUtility.ResolvePerEventBulletCount(in pattern));
            int roomEventCount = room / shotsPerEvent;
            int requestedEventCount = requested / shotsPerEvent;
            return math.max(0, math.min(requestedEventCount, roomEventCount)) * shotsPerEvent;
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

        private static Unity.Mathematics.Random CreateDiscreteOccurrenceRandom(
            uint runSeed,
            uint sourceStableId,
            int directiveId,
            uint frame,
            int occurrenceIndexWithinFrame)
        {
            uint seed = math.hash(new uint4(
                math.max(1u, runSeed),
                math.max(1u, sourceStableId),
                (uint)math.max(0, directiveId + 1),
                math.hash(new uint2(frame, (uint)math.max(0, occurrenceIndexWithinFrame)))));
            return Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
        }

        private static float3 ResolveDiscreteEventAnchorPosition(
            Entity sourceEntity,
            in SourceClipPatternBuffer pattern,
            bool hasPlayer,
            float3 playerPosition,
            uint runSeed,
            uint sourceStableId,
            uint frame,
            int occurrenceIndexWithinFrame,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
        {
            var random = CreateDiscreteOccurrenceRandom(runSeed, sourceStableId, pattern.DirectiveId, frame, occurrenceIndexWithinFrame);
            float3 center = ResolveSpawnCenter(
                sourceEntity,
                in pattern,
                ref sourceAnchorLookup,
                hasPlayer,
                playerPosition);
            float3 sourceAnchor = sourceAnchorLookup.HasComponent(sourceEntity)
                ? sourceAnchorLookup[sourceEntity].Position
                : center;
            return SampleEventAnchorPosition(
                ref random,
                sourceEntity,
                in pattern,
                center,
                sourceAnchor,
                hasPlayer,
                playerPosition,
                ref pollutionConfigLookup,
                ref pollutionGridLookup,
                ref pollutionCellsLookup,
                ref pollutionValidCellIndicesLookup);
        }

        private static float3 ResolveSpawnCenter(
            Entity sourceEntity,
            in SourceClipPatternBuffer pattern,
            ref ComponentLookup<SourceAnchorComponent> sourceAnchorLookup,
            bool hasPlayer,
            float3 playerPosition)
        {
            float3 sourceCenter = sourceAnchorLookup.HasComponent(sourceEntity)
                ? sourceAnchorLookup[sourceEntity].Position
                : float3.zero;

            switch (pattern.SamplingAnchorMode)
            {
                case WaveSamplingAnchorModeId.FixedPoint:
                    return new float3(pattern.FixedPoint.x, sourceCenter.y, pattern.FixedPoint.y);
                case WaveSamplingAnchorModeId.PlayerRelative:
                    if (hasPlayer)
                    {
                        return new float3(
                            playerPosition.x + pattern.SpawnOffset.x,
                            playerPosition.y,
                            playerPosition.z + pattern.SpawnOffset.y);
                    }

                    return sourceCenter;
                default:
                    return sourceCenter;
            }
        }

        private static float3 SampleEventAnchorPosition(
            ref Unity.Mathematics.Random random,
            Entity sourceEntity,
            in SourceClipPatternBuffer pattern,
            float3 center,
            float3 sourceAnchor,
            bool hasPlayer,
            float3 playerPosition,
            ref ComponentLookup<SourcePollutionConfigComponent> pollutionConfigLookup,
            ref ComponentLookup<SourcePollutionGridComponent> pollutionGridLookup,
            ref BufferLookup<SourcePollutionCellBuffer> pollutionCellsLookup,
            ref BufferLookup<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndicesLookup)
        {
            int sampleBudget = math.max(1, pattern.SpawnSampleBudget);
            float noSpawnRadius = math.max(0f, pattern.PlayerNoSpawnRadius);
            float noSpawnRadiusSq = noSpawnRadius * noSpawnRadius;
            float3 lastSample = center;

            for (int i = 0; i < sampleBudget; i++)
            {
                if (pattern.AreaSamplerMode == WaveAreaSamplerModeId.PollutionTopK)
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
                else if (pattern.AreaSamplerMode == WaveAreaSamplerModeId.UniformField)
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
            if (!pollutionConfigLookup.HasComponent(sourceEntity)
                || !pollutionGridLookup.HasComponent(sourceEntity)
                || !pollutionCellsLookup.HasBuffer(sourceEntity)
                || !pollutionValidCellIndicesLookup.HasBuffer(sourceEntity))
                return false;

            var config = pollutionConfigLookup[sourceEntity];
            var grid = pollutionGridLookup[sourceEntity];
            var cells = pollutionCellsLookup[sourceEntity];
            var validIndices = pollutionValidCellIndicesLookup[sourceEntity];
            int activeCount = CountActiveValidCells(cells, validIndices);
            if (activeCount <= 0)
                return false;

            int topK = math.clamp(config.TopKSampleCount, 1, activeCount);
            int bestCellIndex = -1;
            float bestWeight = -1f;
            for (int i = 0; i < topK; i++)
            {
                int cellIndex = SelectNthActiveValidCell(validIndices, cells, random.NextInt(0, activeCount));
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

            position = SampleInsidePollutionCell(
                ref random,
                bestCellIndex,
                center,
                sourceAnchor,
                math.max(1, grid.Cols),
                math.max(1, grid.Rows),
                in grid);
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
            if (!pollutionGridLookup.HasComponent(sourceEntity)
                || !pollutionCellsLookup.HasBuffer(sourceEntity)
                || !pollutionValidCellIndicesLookup.HasBuffer(sourceEntity))
                return false;

            var grid = pollutionGridLookup[sourceEntity];
            var cells = pollutionCellsLookup[sourceEntity];
            var validIndices = pollutionValidCellIndicesLookup[sourceEntity];
            int activeCount = CountActiveValidCells(cells, validIndices);
            if (activeCount <= 0)
                return false;

            int cellIndex = SelectNthActiveValidCell(validIndices, cells, random.NextInt(0, activeCount));
            if (cellIndex < 0 || GetValidCellWeight(cells, cellIndex) < 0f)
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
            return new float3(
                math.clamp(worldX, worldMinX, worldMaxX),
                center.y,
                math.clamp(worldZ, worldMinZ, worldMaxZ));
        }

        private static int ResolveCombinedPendingForSourceType(
            Entity sourceEntity,
            int bulletTypeKey,
            DynamicBuffer<SourceSpawnRequestBuffer> legacyRequests,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests)
        {
            return SpawnRequestCommonUtility.SafeAdd(
                ResolveLegacyPendingCount(legacyRequests, bulletTypeKey),
                ResolvePendingDiscreteBulletEquivalent(discreteRequests, sourceEntity, bulletTypeKey));
        }

        private static int ResolvePendingDiscreteBulletEquivalent(
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            Entity sourceEntity,
            int bulletTypeKey)
        {
            int pending = 0;
            for (int i = 0; i < discreteRequests.Length; i++)
            {
                var item = discreteRequests[i];
                if (item.SourceEntity != sourceEntity || item.BulletTypeKey != bulletTypeKey)
                    continue;

                pending = SpawnRequestCommonUtility.SafeAdd(
                    pending,
                    DiscreteEmitRequestUtility.ResolvePendingBulletEquivalent(in item));
            }

            return pending;
        }

        private static int ResolveLegacyPendingCount(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            int bulletTypeKey)
        {
            int pending = 0;
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.BulletTypeKey != bulletTypeKey || item.Count <= 0)
                    continue;

                pending = SpawnRequestCommonUtility.SafeAdd(pending, item.Count);
            }

            return pending;
        }

        private static int ResolveActiveCount(
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            int bulletTypeKey)
        {
            for (int i = 0; i < activeCounts.Length; i++)
            {
                if (activeCounts[i].BulletTypeKey == bulletTypeKey)
                    return activeCounts[i].ActiveCount;
            }

            return 0;
        }
    }
}
