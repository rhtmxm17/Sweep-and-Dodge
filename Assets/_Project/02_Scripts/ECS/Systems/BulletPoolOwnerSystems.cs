using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    // ----------------------------------------------------------------------
    // Pool Owner: Bootstrap
    // ----------------------------------------------------------------------

    /// <summary>
    /// 풀/SpatialHash 컨테이너 초기화 및 해제.
    /// - FreeByKey, CellMap, HazardCellMap을 Persistent로 생성
    /// - BulletPoolDefinitionBuffer 기반으로 타입별 Instantiate 후 FreeByKey에 반납
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    public partial struct BulletPoolOwnerBootstrapSystem : ISystem, ISystemStartStop
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletPoolRegistryTag>();
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            if (BulletFieldShared.IsInitialized)
                return;

            var em = state.EntityManager;

            // Config + MetaScrap 싱글톤이 없으면 기본값 생성(테스트 편의)
            if (!SystemAPI.TryGetSingleton<BulletFieldConfigComponent>(out _))
            {
                var configEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(MetaScrapComponent));

                var cfg = new BulletFieldConfigComponent
                {
                    PoolSize = 120_000,
                    InvCellSize = 1f / 1.6f,
                };

                em.SetComponentData(configEntity, cfg);
                em.SetComponentData(configEntity, new MetaScrapComponent { Value = 0 });
            }

            EnsureSpawnRequestRuntimeSingletons(em);

            var poolRegistryEntity = SystemAPI.GetSingletonEntity<BulletPoolRegistryTag>();
            var poolDefs = SystemAPI.GetBuffer<BulletPoolDefinitionBuffer>(poolRegistryEntity);
            var poolDefsSnapshot = new NativeArray<BulletPoolDefinitionBuffer>(poolDefs.Length, Allocator.Temp);
            for (int i = 0; i < poolDefs.Length; i++)
                poolDefsSnapshot[i] = poolDefs[i];

            int totalPoolSize = 0;
            for (int i = 0; i < poolDefsSnapshot.Length; i++)
                totalPoolSize += math.max(0, poolDefsSnapshot[i].PoolSize);

            var cfgSingleton = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            int poolCapacity = math.max(math.max(1, totalPoolSize), cfgSingleton.PoolSize);

            BulletFieldShared.FreeByKey = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.PoolFence = default;

            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.HazardCellMap = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.CellMapFence = default;

            // Key-Pool 구성: 타입 정의별로 프리팹 인스턴스를 생성해 FreeByKey로 반납
            for (int i = 0; i < poolDefsSnapshot.Length; i++)
            {
                var def = poolDefsSnapshot[i];
                if (def.Prefab == Entity.Null || def.PoolSize <= 0)
                    continue;

                using var pool = new NativeArray<Entity>(def.PoolSize, Allocator.Temp);
                em.Instantiate(def.Prefab, pool);

                for (int p = 0; p < pool.Length; p++)
                {
                    var b = pool[p];

                    if (em.HasComponent<BulletActiveTag>(b))
                        em.SetComponentEnabled<BulletActiveTag>(b, false);
                    BulletLifecycleRequestUtility.ResetLifecycleRequestState(em, b);

                    if (em.HasBuffer<EntityRenderElementBuffer>(b))
                    {
                        var parts = em.GetBuffer<EntityRenderElementBuffer>(b);
                        bool toggled = false;
                        for (int k = 0; k < parts.Length; k++)
                        {
                            var pe = parts[k].Value;
                            if (em.HasComponent<MaterialMeshInfo>(pe))
                            {
                                em.SetComponentEnabled<MaterialMeshInfo>(pe, false);
                                toggled = true;
                            }
                        }

                        // Guard: render-parts buffer exists but no valid render entity in it.
                        if (!toggled && em.HasComponent<MaterialMeshInfo>(b))
                            em.SetComponentEnabled<MaterialMeshInfo>(b, false);
                    }
                    else if (em.HasComponent<MaterialMeshInfo>(b))
                    {
                        em.SetComponentEnabled<MaterialMeshInfo>(b, false);
                    }

                    if (em.HasComponent<LocalTransform>(b))
                        em.SetComponentData(b, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                    if (em.HasComponent<BulletVelocityComponent>(b))
                        em.SetComponentData(b, new BulletVelocityComponent { Value = float2.zero });
                    if (em.HasComponent<BulletSpeedComponent>(b))
                        em.SetComponentData(b, new BulletSpeedComponent { Value = def.Speed });
                    if (em.HasComponent<BulletLifetimeMaxComponent>(b))
                        em.SetComponentData(b, new BulletLifetimeMaxComponent { Value = def.Lifetime });
                    if (em.HasComponent<BulletLifetimeComponent>(b))
                        em.SetComponentData(b, new BulletLifetimeComponent { Value = 0f });
                    if (em.HasComponent<BulletRadiusComponent>(b))
                        em.SetComponentData(b, new BulletRadiusComponent { Value = def.Radius });
                    if (em.HasComponent<BulletScoreValueComponent>(b))
                        em.SetComponentData(b, new BulletScoreValueComponent { Value = def.ScoreValue });
                    if (em.HasComponent<BulletTypeKeyComponent>(b))
                        em.SetComponentData(b, new BulletTypeKeyComponent { Value = def.TypeKey });
                    if (em.HasComponent<BulletCaptureRuleComponent>(b))
                        em.SetComponentData(b, new BulletCaptureRuleComponent { Value = def.CaptureRule });
                    if (em.HasComponent<BulletHazardTag>(b))
                        em.SetComponentEnabled<BulletHazardTag>(b, def.CaptureRule == BulletCaptureRuleId.RiskTimedResolve);
                    if (em.HasComponent<BulletSourceRefComponent>(b))
                        em.SetComponentData(b, new BulletSourceRefComponent { Value = Entity.Null });
                    if (em.HasComponent<BulletLifecycleTraceComponent>(b))
                    {
                        em.SetComponentData(b, new BulletLifecycleTraceComponent
                        {
                            LastSpawnFrame = 0,
                            LastDespawnFrame = 0
                        });
                    }

                    ApplyDefinitionBehaviorComponents(em, b, in def);

                    BulletFieldShared.FreeByKey.Add(def.TypeKey, b);
                }
            }

            poolDefsSnapshot.Dispose();

            BulletFieldShared.MarkInitialized();
        }

        public void OnStopRunning(ref SystemState state)
        {
            // SharedStatic 컨테이너 접근을 안전하게 마무리
            JobHandle.CombineDependencies(BulletFieldShared.PoolFence, BulletFieldShared.CellMapFence).Complete();

            if (BulletFieldShared.CellMap.IsCreated)
                BulletFieldShared.CellMap.Dispose();
            if (BulletFieldShared.HazardCellMap.IsCreated)
                BulletFieldShared.HazardCellMap.Dispose();
            if (BulletFieldShared.FreeByKey.IsCreated)
                BulletFieldShared.FreeByKey.Dispose();

            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkUninitialized();
        }

        private static void EnsureSpawnRequestRuntimeSingletons(EntityManager em)
        {
            if (!HasSingleton<SpawnRequestPolicyComponent>(em))
            {
                var e = em.CreateEntity(typeof(SpawnRequestPolicyComponent));
                em.SetComponentData(e, new SpawnRequestPolicyComponent
                {
                    BudgetPerFrame = 1024,
                    MaxPendingCount = 32768,
                    MaxPendingAgeFrames = 120,
                    WarningLogCooldownFrames = 60,
                    WarningBacklogPercent = 70,
                    WarningHighBacklogPercent = 85,
                });
            }

            if (!HasSingleton<SpawnBacklogMetricsComponent>(em))
            {
                var e = em.CreateEntity(typeof(SpawnBacklogMetricsComponent));
                em.SetComponentData(e, default(SpawnBacklogMetricsComponent));
            }

            if (!HasSingleton<SpawnBudgetCursorComponent>(em))
            {
                var e = em.CreateEntity(typeof(SpawnBudgetCursorComponent));
                em.SetComponentData(e, new SpawnBudgetCursorComponent
                {
                    SourceStartIndex = 0,
                });
            }

            if (!HasSingleton<SpawnRunSeedComponent>(em))
            {
                var e = em.CreateEntity(typeof(SpawnRunSeedComponent));
                em.SetComponentData(e, new SpawnRunSeedComponent
                {
                    Value = 1u,
                });
            }

            EnsureSecondarySpawnRuntimeSingleton(em);

            if (!HasSingleton<RunProgressDirectorConfigComponent>(em))
            {
                var e = em.CreateEntity(typeof(RunProgressDirectorConfigComponent));
                em.SetComponentData(e, new RunProgressDirectorConfigComponent
                {
                    PressureHoldSec = 0.35f,
                    BaselineTrashDensityScale = 0.45f,
                    PressureDensityScale = 1.0f,
                });
            }

            if (!HasSingleton<RunDirectorStageConfigComponent>(em))
            {
                var e = em.CreateEntity(typeof(RunDirectorStageConfigComponent));
                em.SetComponentData(e, new RunDirectorStageConfigComponent
                {
                    InitialState = RunDirectorStageStateId.Idle,
                    MinIdleDurationSec = 0f,
                    ClearAutoAdvanceTimeoutSec = 10f,
                });
            }

            if (!HasSingleton<RunDirectorStageStateComponent>(em))
            {
                var stageConfig = em.CreateEntityQuery(ComponentType.ReadOnly<RunDirectorStageConfigComponent>())
                    .GetSingleton<RunDirectorStageConfigComponent>();
                var e = em.CreateEntity(typeof(RunDirectorStageStateComponent));
                em.SetComponentData(e, new RunDirectorStageStateComponent
                {
                    State = stageConfig.InitialState,
                    StateElapsedSec = 0f,
                    EnteredFrame = 0u,
                    LastTransitionReason = RunDirectorStageTransitionReasonId.None,
                });
            }

            if (!HasSingleton<RunDirectorStageGateComponent>(em))
            {
                var e = em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.SetComponentData(e, new RunDirectorStageGateComponent
                {
                    // 기본값은 즉시 시작 가능한 호환 모드.
                    IntroPresentationDone = 1,
                    ClearPresentationDone = 1,
                    MinIdleDurationElapsed = 1,
                    AutoAdvanceTimeoutElapsed = 0,
                });
            }

            if (!HasSingleton<RunDirectorStageRequestComponent>(em))
            {
                var e = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(e, default(RunDirectorStageRequestComponent));
            }

            if (!HasSingleton<RunDirectorStageSignalComponent>(em))
            {
                var e = em.CreateEntity(typeof(RunDirectorStageSignalComponent));
                em.SetComponentData(e, default(RunDirectorStageSignalComponent));
            }

            if (!HasSingleton<RunDirectorPressureWeightSingletonTag>(em))
            {
                var e = em.CreateEntity(typeof(RunDirectorPressureWeightSingletonTag));
                var weights = em.AddBuffer<RunDirectorPressureWeightBuffer>(e);
                weights.Add(new RunDirectorPressureWeightBuffer
                {
                    Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                    Weight = 1.0f,
                });
                weights.Add(new RunDirectorPressureWeightBuffer
                {
                    Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                    Weight = 1.0f,
                });
            }

            if (!HasSingleton<DebugHudMetricsComponent>(em))
            {
                var e = em.CreateEntity(typeof(DebugHudMetricsComponent));
                em.SetComponentData(e, default(DebugHudMetricsComponent));
            }

            if (!HasSingleton<PlayerHudSnapshotComponent>(em))
            {
                var e = em.CreateEntity(typeof(PlayerHudSnapshotComponent));
                em.SetComponentData(e, new PlayerHudSnapshotComponent
                {
                    CarryLoad = 0,
                    CarryCapacity = 0,
                    DepletedSourceCount = 0,
                    TotalSourceCount = 0,
                    PressureSourceStableId = 0u,
                    PressureSourceCollected = 0,
                    PressureSourceThresholdWeakened = 0,
                    PressureSourceThresholdDepleted = 0,
                    PressureSourceProgress01 = 0f,
                    StageState = RunDirectorStageStateId.Idle,
                    StageStateElapsedSec = 0f,
                    GameplayElapsedSec = 0f,
                    LastHitLossValue = 0,
                    HitFlashRemainingSec = 0f,
                    TotalCollectValue = 0,
                    TotalCleanupValue = 0,
                    TotalHitValue = 0,
                    LastUpdatedFrame = 0u,
                });
            }

            using var combatEventChannelQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<CombatEventChannelSingletonTag>(),
                ComponentType.ReadOnly<CombatEventMetricsComponent>(),
                ComponentType.ReadWrite<CombatEventBufferElement>());
            if (combatEventChannelQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(
                    typeof(CombatEventChannelSingletonTag),
                    typeof(CombatEventMetricsComponent));
                em.SetComponentData(e, default(CombatEventMetricsComponent));
                var channel = em.AddBuffer<CombatEventBufferElement>(e);
                channel.EnsureCapacity(64);
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!HasSingleton<BulletRenderTraceConfigComponent>(em))
            {
                var e = em.CreateEntity(typeof(BulletRenderTraceConfigComponent));
                em.SetComponentData(e, new BulletRenderTraceConfigComponent
                {
                    // Default OFF. Turn ON only when diagnosing render/order issues.
                    EnableInvariantLog = 0,
                    MaxLogsPerFrame = 8,
                    MaxEntitiesToScanPerFrame = 4096,
                });
            }

            if (!HasSingleton<BulletRenderTraceMetricsComponent>(em))
            {
                var e = em.CreateEntity(typeof(BulletRenderTraceMetricsComponent));
                em.SetComponentData(e, default(BulletRenderTraceMetricsComponent));
            }
#endif

            if (!HasSingleton<StressSwitchStateComponent>(em))
            {
                var e = em.CreateEntity(typeof(StressSwitchStateComponent));
                em.SetComponentData(e, new StressSwitchStateComponent
                {
                    RequestExecute = 0,
                    Mode = (byte)StressSwitchModeId.None,
                    BurstCount = 100000,
                    SustainFrames = 300,
                    SustainPerFrame = 2000,
                    PreferredBulletTypeKey = -1,
                    RemainingFrames = 0,
                });
            }
        }

        private static bool HasSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return !query.IsEmptyIgnoreFilter;
        }

        private static void EnsureSecondarySpawnRuntimeSingleton(EntityManager em)
        {
            Entity singletonEntity;
            using (var query = em.CreateEntityQuery(ComponentType.ReadOnly<BulletSecondarySpawnChannelSingletonTag>()))
            {
                singletonEntity = query.IsEmptyIgnoreFilter
                    ? em.CreateEntity(typeof(BulletSecondarySpawnChannelSingletonTag))
                    : query.GetSingletonEntity();
            }

            if (!em.HasBuffer<BulletSecondarySpawnRequestBuffer>(singletonEntity))
            {
                var buffer = em.AddBuffer<BulletSecondarySpawnRequestBuffer>(singletonEntity);
                buffer.EnsureCapacity(32);
            }

            if (!em.HasComponent<SecondarySpawnPolicyComponent>(singletonEntity))
            {
                em.AddComponentData(singletonEntity, new SecondarySpawnPolicyComponent
                {
                    BudgetPerFrame = 256,
                    MaxPendingCount = 8192,
                    MaxPendingAgeFrames = 120,
                });
            }

            if (!em.HasComponent<SecondarySpawnBacklogMetricsComponent>(singletonEntity))
                em.AddComponentData(singletonEntity, default(SecondarySpawnBacklogMetricsComponent));
        }

        private static void ApplyDefinitionBehaviorComponents(EntityManager em, Entity bullet, in BulletPoolDefinitionBuffer def)
        {
            switch (def.MovementFamily)
            {
                case BulletMovementFamilyId.DampedLinear:
                    SetOrAddComponent(em, bullet, new BulletDampedMotionComponent
                    {
                        DampingPerSec = def.DampedLinear.DampingPerSec,
                        StopSpeedThreshold = def.DampedLinear.StopSpeedThreshold,
                    });
                    RemoveComponentIfPresent<BulletHomingLiteMotionComponent>(em, bullet);
                    break;

                case BulletMovementFamilyId.HomingLite:
                    SetOrAddComponent(em, bullet, new BulletHomingLiteMotionComponent
                    {
                        TurnRateDegPerSec = def.HomingLite.TurnRateDegPerSec,
                        MaxAcquireDistance = def.HomingLite.MaxAcquireDistance,
                        MinRetargetDistance = def.HomingLite.MinRetargetDistance,
                    });
                    RemoveComponentIfPresent<BulletDampedMotionComponent>(em, bullet);
                    break;

                default:
                    RemoveComponentIfPresent<BulletDampedMotionComponent>(em, bullet);
                    RemoveComponentIfPresent<BulletHomingLiteMotionComponent>(em, bullet);
                    break;
            }

            ApplyReactionComponent(
                em,
                bullet,
                def.OnMotionCompletedExplode,
                static reaction => new BulletOnMotionCompletedExplodeReactionComponent
                {
                    SecondaryBulletTypeKey = reaction.SecondaryBulletTypeKey,
                    SpawnCount = reaction.SpawnCount,
                    Shape = reaction.Shape,
                    SpreadAngleDeg = reaction.SpreadAngleDeg,
                    SpawnRadius = reaction.SpawnRadius,
                });

            ApplyReactionComponent(
                em,
                bullet,
                def.OnCollectedSpawnSecondary,
                static reaction => new BulletOnCollectedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = reaction.SecondaryBulletTypeKey,
                    SpawnCount = reaction.SpawnCount,
                    Shape = reaction.Shape,
                    SpreadAngleDeg = reaction.SpreadAngleDeg,
                    SpawnRadius = reaction.SpawnRadius,
                });
        }

        private static void ApplyReactionComponent<TComponent>(
            EntityManager em,
            Entity bullet,
            in BulletSecondarySpawnReactionRuntimeDefinition reaction,
            global::System.Func<BulletSecondarySpawnReactionRuntimeDefinition, TComponent> create)
            where TComponent : unmanaged, IComponentData
        {
            if (reaction.SecondaryBulletTypeKey < 0 || reaction.SpawnCount <= 0)
            {
                RemoveComponentIfPresent<TComponent>(em, bullet);
                return;
            }

            var component = create(reaction);
            SetOrAddComponent(em, bullet, component);
        }

        private static void SetOrAddComponent<T>(EntityManager em, Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.SetComponentData(entity, value);
            else
                em.AddComponentData(entity, value);
        }

        private static void RemoveComponentIfPresent<T>(EntityManager em, Entity entity)
            where T : unmanaged, IComponentData
        {
            if (em.HasComponent<T>(entity))
                em.RemoveComponent<T>(entity);
        }
    }

    // ----------------------------------------------------------------------
    // Pool Owner: Spawn (Begin)
    // ----------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateBefore(typeof(SecondarySpawnExecutionSystem))]
    [UpdateBefore(typeof(SpawnRequestRoundRobinExecutionSystem))]
    public partial struct BulletFieldAreaUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
            state.RequireForUpdate<Shape2DComponent>();
            state.RequireForUpdate<SourceShapeDerivedComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var sourceRegionCellsLookup = SystemAPI.GetBufferLookup<SourceRegionCellIndexBuffer>(true);
            foreach (var (shapeRW, derivedRW, entity) in SystemAPI.Query<RefRW<Shape2DComponent>, RefRW<SourceShapeDerivedComponent>>().WithAll<BulletFieldAreaComponent>().WithEntityAccess())
            {
                var shape = Shape2DUtility.Normalize(in shapeRW.ValueRO);
                shapeRW.ValueRW = shape;
                if (sourceRegionCellsLookup.HasBuffer(entity) && sourceRegionCellsLookup[entity].Length > 0)
                    continue;

                derivedRW.ValueRW = new SourceShapeDerivedComponent
                {
                    ComputedArea = Shape2DUtility.ComputeArea(in shape),
                    HalfExtents = Shape2DUtility.ComputeHalfExtents(in shape),
                };
            }
        }
    }

    // ----------------------------------------------------------------------
    // Execution End: Despawn + Return to Pool (Owner)
    // ----------------------------------------------------------------------

    /// <summary>
    /// 디스폰 실행 단일 책임.
    /// - BulletDespawnRequestTag(enabled) 또는 만료(시뮬레이션에서 request로 전환)된 탄을 비활성화
    /// - 렌더 off + request reset + free-list enqueue
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(BulletLifecycleReactionExecutionSystem))]
    public partial struct BulletDespawnExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            // FreeByKey는 ECS 의존성 추적 밖이므로 fence로 시퀀싱
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);
            uint frame = FrameSequenceUtility.GetCurrentFrame(SystemAPI.GetSingleton<BulletFrameCounterComponent>());

            var renderParts = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            var renderLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            var lifeCycleLookup = SystemAPI.GetComponentLookup<BulletLifecycleTraceComponent>(false);
            var sourceActiveCounts = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            renderParts.Update(ref state);
            renderLookup.Update(ref state);
            lifeCycleLookup.Update(ref state);
            sourceActiveCounts.Update(ref state);

            var countDeltaQueue = new NativeQueue<SourceActiveCountDelta>(Allocator.TempJob);

            var job = new DespawnAndReturnJob
            {
                FreeByKey = BulletFieldShared.FreeByKey.AsParallelWriter(),
                RenderPartsLookup = renderParts,
                RenderLookup = renderLookup,
                LifeCycleLookup = lifeCycleLookup,
                CurrentFrame = frame,
                CountDeltaWriter = countDeltaQueue.AsParallelWriter(),
            };

            var despawnHandle = job.ScheduleParallel(deps);
            var applyCountJob = new ApplySourceActiveCountDeltaJob
            {
                CountDeltas = countDeltaQueue,
                SourceActiveCountLookup = sourceActiveCounts,
            };

            var applyHandle = applyCountJob.Schedule(despawnHandle);
            state.Dependency = countDeltaQueue.Dispose(applyHandle);
            BulletFieldShared.PoolFence = state.Dependency;
        }

        [BurstCompile]
        private partial struct DespawnAndReturnJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter FreeByKey;
            [ReadOnly] public BufferLookup<EntityRenderElementBuffer> RenderPartsLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<MaterialMeshInfo> RenderLookup;
            [NativeDisableParallelForRestriction] public ComponentLookup<BulletLifecycleTraceComponent> LifeCycleLookup;
            public uint CurrentFrame;
            public NativeQueue<SourceActiveCountDelta>.ParallelWriter CountDeltaWriter;

            private void Execute(
                Entity e,
                ref BulletLifetimeComponent life,
                in BulletTypeKeyComponent typeKey,
                in BulletSourceRefComponent sourceRef,
                EnabledRefRW<BulletActiveTag> active,
                EnabledRefRW<BulletDespawnRequestTag> request)
            {
                if (!request.ValueRO)
                    return;

                // 이미 비활성인 경우(이중 요청) 방어
                if (active.ValueRO)
                {
                    active.ValueRW = false;

                    // 렌더 off: RenderParts(자식 렌더 엔티티) MaterialMeshInfo disable
                    bool toggled = false;
                    if (RenderPartsLookup.TryGetBuffer(e, out var parts))
                    {
                        for (int p = 0; p < parts.Length; p++)
                        {
                            var pe = parts[p].Value;
                            if (!RenderLookup.HasComponent(pe))
                                continue;

                            RenderLookup.SetComponentEnabled(pe, false);
                            toggled = true;
                        }
                    }

                    if (!toggled && RenderLookup.HasComponent(e))
                    {
                        // (fallback) 루트에 렌더가 있는 단일 프리팹 대응
                        RenderLookup.SetComponentEnabled(e, false);
                    }

                    FreeByKey.Add(typeKey.Value, e);
                    if (LifeCycleLookup.HasComponent(e))
                    {
                        var trace = LifeCycleLookup[e];
                        trace.LastDespawnFrame = CurrentFrame;
                        LifeCycleLookup[e] = trace;
                    }
                    if (sourceRef.Value != Entity.Null)
                    {
                        CountDeltaWriter.Enqueue(new SourceActiveCountDelta
                        {
                            Source = sourceRef.Value,
                            BulletTypeKey = typeKey.Value,
                            Delta = -1
                        });
                    }
                }

                request.ValueRW = false;
                life.Value = 0f;
            }
        }

        [BurstCompile]
        private struct ApplySourceActiveCountDeltaJob : IJob
        {
            public NativeQueue<SourceActiveCountDelta> CountDeltas;
            public BufferLookup<SourceActiveBulletCountBuffer> SourceActiveCountLookup;

            public void Execute()
            {
                while (CountDeltas.TryDequeue(out var item))
                {
                    if (item.Source == Entity.Null || !SourceActiveCountLookup.TryGetBuffer(item.Source, out var buffer))
                        continue;

                    for (int i = 0; i < buffer.Length; i++)
                    {
                        var entry = buffer[i];
                        if (entry.BulletTypeKey != item.BulletTypeKey)
                            continue;

                        entry.ActiveCount = math.max(0, entry.ActiveCount + item.Delta);
                        buffer[i] = entry;
                        break;
                    }
                }
            }
        }

        private struct SourceActiveCountDelta
        {
            public Entity Source;
            public int BulletTypeKey;
            public int Delta;
        }
    }
}
