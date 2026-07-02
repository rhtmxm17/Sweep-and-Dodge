using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct SourceClipRequestBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<SpawnRequestPolicyComponent>();
            state.RequireForUpdate<DiscreteEmitChannelSingletonTag>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();
            state.RequireForUpdate<SpawnRunSeedComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceRunDirectorStateComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<SourceShapeDerivedComponent>();
            state.RequireForUpdate<SourceStableIdComponent>();
            state.RequireForUpdate<SourceClipPatternBuffer>();
            state.RequireForUpdate<SourceSustainSlotCandidateBuffer>();
            state.RequireForUpdate<SourceSustainRuntimeLaneBuffer>();
            state.RequireForUpdate<SourceEventRuntimeComponent>();
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
            var discreteChannelEntity = SystemAPI.GetSingletonEntity<DiscreteEmitChannelSingletonTag>();
            var discreteRequests = SystemAPI.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannelEntity);

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
            var eventRuntimeLookup = SystemAPI.GetComponentLookup<SourceEventRuntimeComponent>(true);
            var clipPatternLookup = SystemAPI.GetBufferLookup<SourceClipPatternBuffer>(false);
            var sustainCandidateLookup = SystemAPI.GetBufferLookup<SourceSustainSlotCandidateBuffer>(false);
            var sustainLaneLookup = SystemAPI.GetBufferLookup<SourceSustainRuntimeLaneBuffer>(false);
            var activeCountLookup = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            var pollutionCellsLookup = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(true);

            stableIdLookup.Update(ref state);
            derivedLookup.Update(ref state);
            directorStateLookup.Update(ref state);
            eventRuntimeLookup.Update(ref state);
            clipPatternLookup.Update(ref state);
            sustainCandidateLookup.Update(ref state);
            sustainLaneLookup.Update(ref state);
            activeCountLookup.Update(ref state);
            requestLookup.Update(ref state);
            pollutionCellsLookup.Update(ref state);

            var sourceQuery = SystemAPI.QueryBuilder()
                .WithAll<SourceSpawnComponent>()
                .WithAll<SourceRunDirectorStateComponent>()
                .WithAll<SourceStableIdComponent>()
                .WithAll<BulletFieldAreaComponent>()
                .WithAll<SourceShapeDerivedComponent>()
                .WithAll<SourceClipPatternBuffer>()
                .WithAll<SourceSustainSlotCandidateBuffer>()
                .WithAll<SourceSustainRuntimeLaneBuffer>()
                .WithAll<SourceEventRuntimeComponent>()
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
                var sustainCandidates = sustainCandidateLookup[sourceEntity];
                var sustainLanes = sustainLaneLookup[sourceEntity];
                var activeCounts = activeCountLookup[sourceEntity];
                var requests = requestLookup[sourceEntity];
                var eventRuntime = eventRuntimeLookup[sourceEntity];

                if (clipPatterns.Length <= 0)
                    continue;

                uint sourceStableId = math.max(1u, stableId.Value);
                var clipState = ResolveClipSelectionState(in directorState);
                float densityScale = ResolveDensityScale(in directorState);
                float fieldSamplingAreaScale = ResolveFieldSamplingAreaScale(sourceEntity, ref pollutionCellsLookup);
                bool restrictFinishToTrashLane = directorState.State == RunDirectorSourceStateId.Finish;
                var sustainLanesRW = sustainLanes;
                var clipPatternsRW = clipPatterns;
                var requestsRW = requests;

                if (eventRuntime.IsPlaying != 0)
                {
                    SpawnRequestCommonUtility.CompactRequestBuffer(requestsRW);
                    continue;
                }

                ProcessSustainLanes(
                    sourceEntity,
                    clipState,
                    runSeed,
                    sourceStableId,
                    frame,
                    deltaTime,
                    derived.ComputedArea,
                    fieldSamplingAreaScale,
                    densityScale,
                    restrictFinishToTrashLane,
                    ref clipPatternsRW,
                    ref sustainCandidates,
                    ref sustainLanesRW,
                    ref activeCounts,
                    ref requestsRW,
                    discreteRequests,
                    ref pendingTotal,
                    ref remainingCapacity,
                    ref droppedByCapacity);

                SpawnRequestCommonUtility.CompactRequestBuffer(requestsRW);
            }

            var metricsRW = SystemAPI.GetSingletonRW<SpawnBacklogMetricsComponent>();
            var metrics = metricsRW.ValueRO;
            metrics.PendingCount = pendingTotal;
            metrics.LastFrameDroppedByCapacity = math.max(0, droppedByCapacity);
            if (droppedByCapacity > 0)
                metrics.DroppedByCapacity = SpawnRequestCommonUtility.SafeAdd(metrics.DroppedByCapacity, droppedByCapacity);
            metricsRW.ValueRW = metrics;
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

        private static void ProcessSustainLanes(
            Entity sourceEntity,
            SourceStateId sourceState,
            uint runSeed,
            uint stableId,
            uint frame,
            float deltaTime,
            float fullArea,
            float fieldSamplingAreaScale,
            float densityScale,
            bool restrictToTrashLane,
            ref DynamicBuffer<SourceClipPatternBuffer> patterns,
            ref DynamicBuffer<SourceSustainSlotCandidateBuffer> sustainCandidates,
            ref DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainLanes,
            ref DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            ref int pendingTotal,
            ref int remainingCapacity,
            ref int droppedByCapacity)
        {
            if (restrictToTrashLane)
            {
                int removed = RemoveSustainPendingRequestsExceptLane(ref requests, SourceSpawnLaneId.Trash);
                if (removed > 0)
                {
                    pendingTotal = math.max(0, pendingTotal - removed);
                    remainingCapacity = SpawnRequestCommonUtility.SafeAdd(remainingCapacity, removed);
                }
            }

            for (int i = 0; i < sustainLanes.Length; i++)
            {
                var laneRuntime = sustainLanes[i];
                if (restrictToTrashLane && laneRuntime.Lane != SourceSpawnLaneId.Trash)
                {
                    if (laneRuntime.ActiveClipId > 0)
                        laneRuntime.LastClipId = laneRuntime.ActiveClipId;
                    laneRuntime.ActiveClipId = 0;
                    laneRuntime.ElapsedSec = 0f;
                    sustainLanes[i] = laneRuntime;
                    continue;
                }

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
                            ref laneRuntime,
                            suppressMissingLog: restrictToTrashLane))
                    {
                        sustainLanes[i] = laneRuntime;
                        continue;
                    }
                }

                float elapsed = laneRuntime.ElapsedSec;
                float maxEnd = 0f;
                float clipDurationSec = 0f;
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
                    clipDurationSec = math.max(clipDurationSec, pattern.ClipDurationSec);
                    if (elapsed < pattern.LocalStartSec || elapsed >= pattern.LocalEndSec)
                    {
                        patterns[p] = pattern;
                        continue;
                    }

                    int requested = ResolveSpawnCount(
                        sourceEntity,
                        ref pattern,
                        runSeed,
                        stableId,
                        frame,
                        activeCounts,
                        requests,
                        discreteRequests,
                        fullArea,
                        fieldSamplingAreaScale,
                        deltaTime,
                        densityScale);
                    patterns[p] = pattern;
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

                laneRuntime.ElapsedSec += deltaTime;
                float clipEndSec = clipDurationSec > 0f ? clipDurationSec : maxEnd;
                if (!hasAnySegment || laneRuntime.ElapsedSec >= clipEndSec)
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
            ref SourceSustainRuntimeLaneBuffer laneRuntime,
            bool suppressMissingLog)
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
                if (!suppressMissingLog && (frame == 0 || frame - laneRuntime.LastMissingLogFrame >= 60u))
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
            Entity sourceEntity,
            ref SourceClipPatternBuffer pattern,
            uint runSeed,
            uint sourceStableId,
            uint frame,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts,
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
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
                requests,
                effectiveArea,
                deltaTime,
                0xD8A89AF5u);

            if (requested <= 0 || pattern.SpawnMode != SourceSpawnModeId.CapAndMaxDensity)
                return requested;

            int active = ResolveActiveCount(activeCounts, pattern.BulletTypeKey);
            int combinedPending = ResolveCombinedPendingForSourceType(
                sourceEntity,
                pattern.BulletTypeKey,
                requests,
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

        private static int RemoveSustainPendingRequestsExceptLane(
            ref DynamicBuffer<SourceSpawnRequestBuffer> requests,
            SourceSpawnLaneId allowedLane)
        {
            int removed = 0;
            for (int i = requests.Length - 1; i >= 0; i--)
            {
                var item = requests[i];
                if (item.Count <= 0 || item.Phase != SourceWavePhaseId.Sustain)
                    continue;
                if (item.Lane == allowedLane)
                    continue;

                removed = SpawnRequestCommonUtility.SafeAdd(removed, item.Count);
                requests.RemoveAtSwapBack(i);
            }

            return removed;
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
                pattern.ProfileRefId,
                pattern.Phase,
                pattern.Lane,
                pattern.LanePriority,
                pattern.BulletTypeKey,
                pattern.HasSpeedOverride,
                pattern.SpeedOverride,
                pattern.HasLifetimeOverride,
                pattern.LifetimeOverride,
                pattern.HasMovementOverride,
                pattern.MovementFamily,
                pattern.DampedLinear,
                pattern.HomingLite,
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

        private static Unity.Mathematics.Random CreateSelectionRandom(
            uint runSeed,
            uint stableId,
            uint slotKey,
            uint selectionSequence)
        {
            uint seed = math.hash(new uint4(runSeed, stableId, slotKey, math.max(1u, selectionSequence)));
            return Unity.Mathematics.Random.CreateFromIndex(math.max(1u, seed));
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
