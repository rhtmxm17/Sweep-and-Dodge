using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateBefore(typeof(SourceSpawnRequestBuildSystem))]
    public partial struct DebugStressSwitchRequestSystem : ISystem
    {
        private struct StressTarget
        {
            public Entity Entity;
            public int BulletTypeKey;
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

                int typeKey = ResolveTypeKey(preferredTypeKey, sourceLookup[sourceEntity].State, patterns);
                if (typeKey == int.MinValue)
                    continue;

                targets.Add(new StressTarget
                {
                    Entity = sourceEntity,
                    BulletTypeKey = typeKey,
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

                AddOrMergeRequest(requests, target.BulletTypeKey, count, frame);
            }
        }

        private int ResolveTypeKey(int preferredTypeKey, SourceStateId sourceState, DynamicBuffer<SourceSpawnPatternBuffer> patterns)
        {
            if (preferredTypeKey >= 0)
                return preferredTypeKey;

            for (int i = 0; i < patterns.Length; i++)
            {
                var pattern = patterns[i];
                if (pattern.State == sourceState)
                    return pattern.BulletTypeKey;
            }

            if (patterns.Length > 0)
                return patterns[0].BulletTypeKey;

            return int.MinValue;
        }

        private void AddOrMergeRequest(DynamicBuffer<SourceSpawnRequestBuffer> requests, int typeKey, int count, uint frame)
        {
            for (int i = 0; i < requests.Length; i++)
            {
                var item = requests[i];
                if (item.BulletTypeKey != typeKey)
                    continue;

                if (item.Count <= 0)
                    item.OldestFrame = frame;

                item.Count = SafeAdd(item.Count, count);
                requests[i] = item;
                return;
            }

            requests.Add(new SourceSpawnRequestBuffer
            {
                BulletTypeKey = typeKey,
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
            hud.FrameTimeMs = math.max(0f, SystemAPI.Time.DeltaTime * 1000f);
            hud.PreviousActiveBullets = activeBullets;
            hudRW.ValueRW = hud;
        }
    }
}
