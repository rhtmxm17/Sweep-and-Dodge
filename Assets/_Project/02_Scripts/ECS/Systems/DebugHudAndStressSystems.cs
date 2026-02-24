using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(SourceSpawnRequestBuildSystem))]
    public partial struct DebugStressSwitchRequestSystem : ISystem
    {
        private struct StressTarget
        {
            public Entity Entity;
            public int DirectiveId;
            public int BulletTypeKey;
            public SourceSpawnSamplingModeId SamplingMode;
            public SourceSpawnCenterModeId CenterMode;
            public SourceSpawnDirectionModeId DirectionMode;
            public float2 FixedPoint;
            public float2 SpawnOffset;
            public float2 LineStart;
            public float2 LineEnd;
            public float SampleSpacing;
            public SourceSpawnWallMaskId WallMask;
            public float WallInset;
            public int SpawnSampleBudget;
            public float PlayerNoSpawnRadius;
            public float BaseAngleDeg;
            public int NWayCount;
            public float SpiralStepDeg;
            public int BurstShotsPerEvent;
            public int SpawnPriority;
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFrameCounterComponent>();
            state.RequireForUpdate<StressSwitchStateComponent>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<SourceSpawnPatternBuffer>();
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
            var patternLookup = SystemAPI.GetBufferLookup<SourceSpawnPatternBuffer>(true);
            var requestLookup = SystemAPI.GetBufferLookup<SourceSpawnRequestBuffer>(false);
            sourceLookup.Update(ref state);
            patternLookup.Update(ref state);
            requestLookup.Update(ref state);

            using var sourceQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<SourceSpawnComponent>(),
                ComponentType.ReadOnly<SourceSpawnPatternBuffer>(),
                ComponentType.ReadWrite<SourceSpawnRequestBuffer>());
            using var sourceEntities = sourceQuery.ToEntityArray(Allocator.Temp);
            using var targets = new NativeList<StressTarget>(Allocator.Temp);
            for (int i = 0; i < sourceEntities.Length; i++)
            {
                var sourceEntity = sourceEntities[i];
                if (!sourceLookup.HasComponent(sourceEntity))
                    continue;
                if (!patternLookup.TryGetBuffer(sourceEntity, out var patterns))
                    continue;

                if (!TryResolveDirective(preferredTypeKey, sourceLookup[sourceEntity].State, patterns, out var resolved))
                    continue;

                targets.Add(new StressTarget
                {
                    Entity = sourceEntity,
                    DirectiveId = resolved.DirectiveId,
                    BulletTypeKey = resolved.BulletTypeKey,
                    SamplingMode = resolved.SamplingMode,
                    CenterMode = resolved.CenterMode,
                    DirectionMode = resolved.DirectionMode,
                    FixedPoint = resolved.FixedPoint,
                    SpawnOffset = resolved.SpawnOffset,
                    LineStart = resolved.LineStart,
                    LineEnd = resolved.LineEnd,
                    SampleSpacing = resolved.SampleSpacing,
                    WallMask = resolved.WallMask,
                    WallInset = resolved.WallInset,
                    SpawnSampleBudget = resolved.SpawnSampleBudget,
                    PlayerNoSpawnRadius = resolved.PlayerNoSpawnRadius,
                    BaseAngleDeg = resolved.BaseAngleDeg,
                    NWayCount = resolved.NWayCount,
                    SpiralStepDeg = resolved.SpiralStepDeg,
                    BurstShotsPerEvent = resolved.BurstShotsPerEvent,
                    SpawnPriority = resolved.SpawnPriority,
                });
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

        private bool TryResolveDirective(
            int preferredTypeKey,
            SourceStateId sourceState,
            DynamicBuffer<SourceSpawnPatternBuffer> patterns,
            out SourceSpawnPatternBuffer resolved)
        {
            resolved = default;
            if (patterns.Length <= 0)
                return false;

            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (preferredTypeKey >= 0 && pattern.BulletTypeKey != preferredTypeKey)
                    continue;
                if (pattern.State == sourceState)
                {
                    resolved = pattern;
                    return true;
                }
            }

            if (preferredTypeKey >= 0)
            {
                for (int i = 0; i < patterns.Length; i++)
                {
                    if (patterns[i].BulletTypeKey != preferredTypeKey)
                        continue;

                    resolved = patterns[i];
                    return true;
                }
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
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.DirectiveId != target.DirectiveId)
                    continue;

                if (item.Count <= 0)
                    item.OldestFrame = frame;

                item.Count = SafeAdd(item.Count, count);
                requests[i] = item;
                return;
            }

            requests.Add(new SourceSpawnRequestBuffer
            {
                DirectiveId = target.DirectiveId,
                BulletTypeKey = target.BulletTypeKey,
                SamplingMode = target.SamplingMode,
                CenterMode = target.CenterMode,
                DirectionMode = target.DirectionMode,
                FixedPoint = target.FixedPoint,
                SpawnOffset = target.SpawnOffset,
                LineStart = target.LineStart,
                LineEnd = target.LineEnd,
                SampleSpacing = math.max(0.001f, target.SampleSpacing),
                WallMask = target.WallMask,
                WallInset = math.max(0f, target.WallInset),
                SpawnSampleBudget = math.max(1, target.SpawnSampleBudget),
                PlayerNoSpawnRadius = math.max(0f, target.PlayerNoSpawnRadius),
                BaseAngleDeg = target.BaseAngleDeg,
                NWayCount = math.max(1, target.NWayCount),
                SpiralStepDeg = target.SpiralStepDeg,
                BurstShotsPerEvent = math.max(1, target.BurstShotsPerEvent),
                SpawnPriority = target.SpawnPriority,
                SpawnSequence = 0u,
                Count = count,
                OldestFrame = frame,
            });
        }

        private int SafeAdd(int lhs, int rhs)
        {
            long v = (long)lhs + rhs;
            if (v > int.MaxValue)
                return int.MaxValue;
            if (v < int.MinValue)
                return int.MinValue;
            return (int)v;
        }
    }

    [UpdateInGroup(typeof(BulletExecutionEndGroup), OrderLast = true)]
    [UpdateAfter(typeof(PlayerUiFeedbackConsumeSystem))]
    [UpdateAfter(typeof(PlayerImpulseConsumeSystem))]
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
