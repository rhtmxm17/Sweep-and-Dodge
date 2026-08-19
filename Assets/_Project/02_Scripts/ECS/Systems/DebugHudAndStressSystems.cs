using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(SourceClipRequestBuildSystem))]
    public partial struct DebugStressSwitchRequestSystem : ISystem
    {
        private struct StressTarget
        {
            public Entity Entity;
            public int DirectiveId;
            public SourceSpawnLaneId Lane;
            public int LanePriority;
            public int BulletTypeKey;
            public SourceSpawnEmissionModeId EmissionMode;
            public SourceSpawnModeId SpawnMode;
            public WaveSamplingAnchorModeId SamplingAnchorMode;
            public WaveAreaSamplerModeId AreaSamplerMode;
            public WavePositionPatternModeId PositionPatternMode;
            public WaveAimModeId AimMode;
            public WaveAimSnapshotTimingId AimSnapshotTiming;
            public float AimAngleOffsetDeg;
            public WaveLineNormalSideId LineNormalSide;
            public float LineNormalAngleOffsetDeg;
            public WaveShotPatternModeId ShotPatternMode;
            public int ShotCount;
            public float NWayAngleSpacingDeg;
            public int EventRepeatCount;
            public float2 FixedPoint;
            public float2 SpawnOffset;
            public float2 LineStart;
            public float2 LineEnd;
            public float SampleSpacing;
            public int PointSetCount;
            public float2 Point0;
            public float2 Point1;
            public float2 Point2;
            public float2 Point3;
            public int SpawnSampleBudget;
            public float PlayerNoSpawnRadius;
            public float BaseAngleDeg;
            public float SpiralStepDeg;
            public SourceSpawnEventShotScheduleId EventShotSchedule;
            public float EventShotIntervalSec;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<StressSwitchStateComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceSpawnRequestBuffer>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var stressRW = SystemAPI.GetSingletonRW<StressSwitchStateComponent>();
            var stress = stressRW.ValueRO;

            bool hasPendingSustain = stress.Mode == (byte)StressSwitchModeId.Sustain && stress.RemainingFrames > 0;
            if (stress.RequestExecute == 0 && !hasPendingSustain)
                return;

            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            if (stress.RequestExecute != 0)
            {
                switch ((StressSwitchModeId)stress.Mode)
                {
                    case StressSwitchModeId.BurstOnce:
                        EnqueueStressRequests(ref state, math.max(0, stress.BurstCount), frame, stress.PreferredBulletTypeKey);
                        stress.RemainingFrames = 0;
                        stress.Mode = (byte)StressSwitchModeId.None;
                        break;
                    case StressSwitchModeId.Sustain:
                        stress.RemainingFrames = math.max(0, stress.SustainFrames);
                        break;
                    case StressSwitchModeId.StopSustain:
                        stress.RemainingFrames = 0;
                        stress.Mode = (byte)StressSwitchModeId.None;
                        break;
                    default:
                        stress.RemainingFrames = 0;
                        stress.Mode = (byte)StressSwitchModeId.None;
                        break;
                }
            }

            if (stress.Mode == (byte)StressSwitchModeId.Sustain && stress.RemainingFrames > 0)
            {
                EnqueueStressRequests(ref state, math.max(0, stress.SustainPerFrame), frame, stress.PreferredBulletTypeKey);
                stress.RemainingFrames = math.max(0, stress.RemainingFrames - 1);
                if (stress.RemainingFrames <= 0)
                    stress.Mode = (byte)StressSwitchModeId.None;
            }

            stress.RequestExecute = 0;
            stressRW.ValueRW = stress;
        }

        private void EnqueueStressRequests(ref SystemState state, int totalCount, uint frame, int preferredTypeKey)
        {
            if (totalCount <= 0)
                return;

            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var clipPatternLookup = SystemAPI.GetBufferLookup<SourceClipPatternBuffer>(true);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            sourceLookup.Update(ref state);
            clipPatternLookup.Update(ref state);
            requestLookup.Update(ref state);

            using var sourceQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SourceSpawnComponent>(),
                ComponentType.ReadWrite<SourceSpawnRequestBuffer>());
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            using var targets = new NativeList<StressTarget>(Allocator.Temp);
            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                if (!sourceLookup.HasComponent(sourceEntity))
                    continue;
                var sourceState = sourceLookup[sourceEntity].State;

                if (clipPatternLookup.TryGetBuffer(sourceEntity, out var clipPatterns)
                    && clipPatterns.Length > 0
                    && TryResolveV3Directive(preferredTypeKey, sourceState, clipPatterns, out var resolvedV3))
                {
                    targets.Add(new StressTarget
                    {
                        Entity = sourceEntity,
                        DirectiveId = resolvedV3.DirectiveId,
                        Lane = resolvedV3.Lane,
                        LanePriority = resolvedV3.LanePriority,
                        BulletTypeKey = resolvedV3.BulletTypeKey,
                        EmissionMode = resolvedV3.EmissionMode,
                        SpawnMode = resolvedV3.SpawnMode,
                        SamplingAnchorMode = resolvedV3.SamplingAnchorMode,
                        AreaSamplerMode = resolvedV3.AreaSamplerMode,
                        PositionPatternMode = resolvedV3.PositionPatternMode,
                        AimMode = resolvedV3.AimMode,
                        AimSnapshotTiming = resolvedV3.AimSnapshotTiming,
                        AimAngleOffsetDeg = resolvedV3.AimAngleOffsetDeg,
                        LineNormalSide = resolvedV3.LineNormalSide,
                        LineNormalAngleOffsetDeg = resolvedV3.LineNormalAngleOffsetDeg,
                        ShotPatternMode = resolvedV3.ShotPatternMode,
                        ShotCount = resolvedV3.ShotCount,
                        NWayAngleSpacingDeg = resolvedV3.NWayAngleSpacingDeg,
                        EventRepeatCount = resolvedV3.EventRepeatCount,
                        FixedPoint = resolvedV3.FixedPoint,
                        SpawnOffset = resolvedV3.SpawnOffset,
                        LineStart = resolvedV3.LineStart,
                        LineEnd = resolvedV3.LineEnd,
                        SampleSpacing = resolvedV3.SampleSpacing,
                        PointSetCount = resolvedV3.PointSetCount,
                        Point0 = resolvedV3.Point0,
                        Point1 = resolvedV3.Point1,
                        Point2 = resolvedV3.Point2,
                        Point3 = resolvedV3.Point3,
                        SpawnSampleBudget = resolvedV3.SpawnSampleBudget,
                        PlayerNoSpawnRadius = resolvedV3.PlayerNoSpawnRadius,
                        BaseAngleDeg = resolvedV3.BaseAngleDeg,
                        SpiralStepDeg = resolvedV3.SpiralStepDeg,
                        EventShotSchedule = resolvedV3.EventShotSchedule,
                        EventShotIntervalSec = resolvedV3.EventShotIntervalSec,
                    });
                }
            }

            if (targets.Length <= 0)
                return;

            int perSource = totalCount / targets.Length;
            int remainder = totalCount % targets.Length;

            for (int i = 0; i < targets.Length; i++)
            {
                int count = perSource + (i < remainder ? 1 : 0);
                if (count <= 0)
                    continue;

                var target = targets[i];
                if (!requestLookup.TryGetBuffer(target.Entity, out var requests))
                    continue;

                AddOrMergeRequest(requests, in target, count, frame);
            }
        }

        private bool TryResolveV3Directive(
            int preferredTypeKey,
            SourceStateId sourceState,
            DynamicBuffer<SourceClipPatternBuffer> patterns,
            out SourceClipPatternBuffer resolved)
        {
            resolved = default;
            if (patterns.Length <= 0)
                return false;

            // v3 stress target selection:
            // 1) Sustain in current state, preferred bullet first.
            // 2) Sustain in current state.
            // 3) Any sustain.
            // 4) Any clip.
            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (pattern.Phase != SourceWavePhaseId.Sustain || pattern.TriggerState != sourceState)
                    continue;
                if (preferredTypeKey >= 0 && pattern.BulletTypeKey != preferredTypeKey)
                    continue;

                resolved = pattern;
                return true;
            }

            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (pattern.Phase != SourceWavePhaseId.Sustain || pattern.TriggerState != sourceState)
                    continue;

                resolved = pattern;
                return true;
            }

            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (pattern.Phase != SourceWavePhaseId.Sustain)
                    continue;
                if (preferredTypeKey >= 0 && pattern.BulletTypeKey != preferredTypeKey)
                    continue;

                resolved = pattern;
                return true;
            }

            resolved = patterns[0];
            return true;
        }

        private void AddOrMergeRequest(
            DynamicBuffer<SourceSpawnRequestBuffer> requests,
            in StressTarget target,
            int count,
            uint frame)
        {
            var template = SpawnRequestCommonUtility.CreateRequestTemplate(
                target.DirectiveId,
                0,
                SourceWavePhaseId.Sustain,
                target.Lane,
                target.LanePriority,
                target.BulletTypeKey,
                0,
                0f,
                0,
                0f,
                0,
                BulletMovementFamilyId.Linear,
                default,
                default,
                target.EmissionMode,
                target.SpawnMode,
                target.SamplingAnchorMode,
                target.AreaSamplerMode,
                target.PositionPatternMode,
                target.AimMode,
                target.AimSnapshotTiming,
                target.AimAngleOffsetDeg,
                target.LineNormalSide,
                target.LineNormalAngleOffsetDeg,
                target.ShotPatternMode,
                target.ShotCount,
                target.NWayAngleSpacingDeg,
                target.EventRepeatCount,
                target.FixedPoint,
                target.SpawnOffset,
                target.LineStart,
                target.LineEnd,
                target.SampleSpacing,
                target.PointSetCount,
                target.Point0,
                target.Point1,
                target.Point2,
                target.Point3,
                target.SpawnSampleBudget,
                target.PlayerNoSpawnRadius,
                target.BaseAngleDeg,
                target.SpiralStepDeg,
                target.EventShotSchedule,
                target.EventShotIntervalSec);
            SpawnRequestCommonUtility.AddOrMergeRequest(requests, in template, count, frame);
        }
    }

    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    public partial struct DebugHudMetricsCollectSystem : ISystem
    {
        private EntityQuery _activeBulletQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<DebugHudMetricsComponent>();
            state.RequireForUpdate<SpawnBacklogMetricsComponent>();

            _activeBulletQuery = SystemAPI.QueryBuilder()
                .WithAll<BulletActiveTag>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var spawnMetrics = SystemAPI.GetSingleton<SpawnBacklogMetricsComponent>();
            var hudRW = SystemAPI.GetSingletonRW<DebugHudMetricsComponent>();
            var hud = hudRW.ValueRO;
            var traceMetrics = SystemAPI.TryGetSingleton<BulletRenderTraceMetricsComponent>(out var trace)
                ? trace
                : default;

            int activeBullets = _activeBulletQuery.CalculateEntityCount();
            int spawned = math.max(0, spawnMetrics.LastFrameBudgetUsed);
            int despawned = math.max(0, hud.PreviousActiveBullets + spawned - activeBullets);

            hud.ActiveBullets = activeBullets;
            hud.SpawnedThisFrame = spawned;
            hud.DespawnedThisFrame = despawned;
            hud.PendingBacklog = math.max(0, spawnMetrics.PendingCount);
            hud.DeferredByBudget = math.max(0, spawnMetrics.DeferredByBudget);
            hud.DeferredByPool = math.max(0, spawnMetrics.DeferredByPool);
            hud.DroppedThisFrame = math.max(0, spawnMetrics.LastFrameDroppedByCapacity);
            hud.ExpiredThisFrame = math.max(0, spawnMetrics.LastFrameExpiredByAge);
            hud.GhostInactiveRendered = math.max(0, traceMetrics.GhostInactiveRendered);
            hud.RequestedRendered = math.max(0, traceMetrics.RequestedRendered);
            hud.ActiveHidden = math.max(0, traceMetrics.ActiveHidden);
            hud.NonPositiveLifeRendered = math.max(0, traceMetrics.NonPositiveLifeRendered);
            hud.FrameTimeMs = math.max(0f, SystemAPI.Time.DeltaTime * 1000f);
            hud.PreviousActiveBullets = activeBullets;
            hudRW.ValueRW = hud;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    [UpdateAfter(typeof(DebugHudMetricsCollectSystem))]
    public partial struct BulletRenderTraceInvariantSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<BulletRenderTraceConfigComponent>();
            state.RequireForUpdate<BulletRenderTraceMetricsComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<BulletRenderTraceConfigComponent>();
            var frameCounter = SystemAPI.GetSingleton<BulletFrameCounterComponent>();
            uint frame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            var metrics = new BulletRenderTraceMetricsComponent
            {
                Frame = frame,
            };

            if (config.EnableInvariantLog == 0)
            {
                SystemAPI.GetSingletonRW<BulletRenderTraceMetricsComponent>().ValueRW = metrics;
                return;
            }

            var activeLookup = SystemAPI.GetComponentLookup<BulletActiveTag>(true);
            var requestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(true);
            var lifeLookup = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(true);
            var traceLookup = SystemAPI.GetComponentLookup<BulletLifecycleTraceComponent>(true);
            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var renderLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);
            var renderPartsLookup = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);

            activeLookup.Update(ref state);
            requestLookup.Update(ref state);
            lifeLookup.Update(ref state);
            traceLookup.Update(ref state);
            txLookup.Update(ref state);
            renderLookup.Update(ref state);
            renderPartsLookup.Update(ref state);

            int scanCap = config.MaxEntitiesToScanPerFrame <= 0
                ? int.MaxValue
                : config.MaxEntitiesToScanPerFrame;
            int maxLogs = math.max(0, config.MaxLogsPerFrame);

            foreach (var (typeKeyRO, bullet) in SystemAPI
                         .Query<RefRO<BulletTypeKeyComponent>>()
                         .WithAll<BulletActiveTag, BulletDespawnRequestTag>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState)
                         .WithEntityAccess())
            {
                if (metrics.Scanned >= scanCap)
                    break;

                metrics.Scanned++;

                bool active = activeLookup.HasComponent(bullet) && activeLookup.IsComponentEnabled(bullet);
                bool requested = requestLookup.HasComponent(bullet) && requestLookup.IsComponentEnabled(bullet);
                bool rendered = IsRendered(
                    bullet,
                    ref renderLookup,
                    ref renderPartsLookup,
                    out int enabledRenderParts,
                    out int totalRenderParts);

                float life = lifeLookup.HasComponent(bullet) ? lifeLookup[bullet].Value : 0f;
                bool lifeNonPositive = life <= 0f;

                bool ghostInactiveRendered = !active && rendered;
                bool requestedRendered = requested && rendered;
                bool activeHidden = active && !rendered;
                bool nonPositiveLifeRendered = rendered && lifeNonPositive;

                if (ghostInactiveRendered) metrics.GhostInactiveRendered++;
                if (requestedRendered) metrics.RequestedRendered++;
                if (activeHidden) metrics.ActiveHidden++;
                if (nonPositiveLifeRendered) metrics.NonPositiveLifeRendered++;

                if (!ghostInactiveRendered && !requestedRendered && !activeHidden && !nonPositiveLifeRendered)
                    continue;

                if (metrics.Logged >= maxLogs)
                    continue;

                var pos = txLookup.HasComponent(bullet) ? txLookup[bullet].Position : float3.zero;
                int typeKey = typeKeyRO.ValueRO.Value;
                var trace = traceLookup.HasComponent(bullet)
                    ? traceLookup[bullet]
                    : default;

                Debug.LogWarning(
                    $"[BulletTraceInvariant] frame={frame} entity={bullet.Index}:{bullet.Version} type={typeKey} " +
                    $"active={active} request={requested} rendered={rendered} life={life:0.000} pos=({pos.x:0.000},{pos.y:0.000},{pos.z:0.000}) " +
                    $"spawnFrame={trace.LastSpawnFrame} despawnFrame={trace.LastDespawnFrame} " +
                    $"renderEnabledParts={enabledRenderParts}/{math.max(1, totalRenderParts)}");
                metrics.Logged++;
            }

            SystemAPI.GetSingletonRW<BulletRenderTraceMetricsComponent>().ValueRW = metrics;
        }

        private static bool IsRendered(
            Entity bullet,
            ref ComponentLookup<MaterialMeshInfo> renderLookup,
            ref BufferLookup<EntityRenderElementBuffer> renderPartsLookup,
            out int enabledRenderParts,
            out int totalRenderParts)
        {
            enabledRenderParts = 0;
            totalRenderParts = 0;

            if (renderPartsLookup.HasBuffer(bullet))
            {
                var parts = renderPartsLookup[bullet];
                totalRenderParts = parts.Length;
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i].Value;
                    if (!renderLookup.HasComponent(part))
                        continue;

                    totalRenderParts = math.max(totalRenderParts, 1);
                    if (renderLookup.IsComponentEnabled(part))
                        enabledRenderParts++;
                }

                if (enabledRenderParts > 0)
                    return true;
            }

            if (!renderLookup.HasComponent(bullet))
                return false;

            totalRenderParts = math.max(totalRenderParts, 1);
            if (!renderLookup.IsComponentEnabled(bullet))
                return false;

            enabledRenderParts = math.max(enabledRenderParts, 1);
            return true;
        }
    }
#endif
}
