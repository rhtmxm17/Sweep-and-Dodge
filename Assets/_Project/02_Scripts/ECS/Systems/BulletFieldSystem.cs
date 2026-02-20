// Bullet의 생성, 이동, 수명 관리, 제거를 담당하는 ECS 시스템
// - 업데이트 그룹 파이프라인 구성
//   BulletFramePipelineGroup   : 탄막 파이프라인 루트 그룹
//   BulletExecutionBeginGroup  : 풀 Dequeue(스폰 실행)
//   BulletSimulationGroup      : Move/Lifetime + SpatialHash(Build) 단일 소유(Write)
//   BulletRequestGroup         : 제거 행동(예: 탄환 흡입)
//                                - 외부에서 정의될 요청 시스템의 위치
//                                - SpatialHash ReadOnly 조회로 디스폰 요청 태그 enable
//   BulletExecutionEndGroup    : 디스폰 실행 + 풀 Enqueue(반납 실행)
// - FreeByKey(키 기반 풀) 접근은 Begin/End "Owner 영역"으로만 제한(다른 시스템은 요청만 남김)
// - SpatialHash writer는 Simulation으로 고정, Request는 ReadOnly 소비
//   - CellMap: 전체 활성 탄
//   - HazardCellMap: 위험탄(BulletHazardTag enabled) 전용
// - 메인 스레드에서 LocalTransform 직접 읽기 금지(타입 충돌 방지): Request는 Job 스케줄로 처리
// - 풀링 시스템에서 렌더 토글 처리:
//   - 풀 초기화/스폰/디스폰에서 EntityRenderElementBuffer(렌더 파츠)를 통해 MaterialMeshInfo 토글
//   - 디스폰 병렬 잡에서 렌더 파츠 OFF는 ECB.ParallelWriter로 기록(교차 엔티티 쓰기 제약 회피)
//   - SpawnFromPoolJob에 BufferLookup<EntityRenderElementBuffer> 추가(렌더 파츠 ON)
//   - Bootstrap에서 렌더 파츠 OFF 처리

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
    // Pipeline Groups
    // ----------------------------------------------------------------------

    // 탄막 필드 파이프라인 그룹들.
    // - 루트 그룹(BulletFramePipelineGroup) 아래에서 순서를 강제한다.

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BulletFramePipelineGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateBefore(typeof(BulletSimulationGroup))]
    public partial class BulletExecutionBeginGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletExecutionBeginGroup))]
    public partial class BulletSimulationGroup : ComponentSystemGroup { }

    /// <summary>
    /// Bullet에 대한 외부 요청 시스템들이 위치할 그룹
    /// </summary>
    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletSimulationGroup))]
    public partial class BulletRequestGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(BulletFramePipelineGroup))]
    [UpdateAfter(typeof(BulletRequestGroup))]
    public partial class BulletExecutionEndGroup : ComponentSystemGroup { }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup), OrderFirst = true)]
    public partial struct BulletFrameCounterAdvanceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var em = state.EntityManager;
            var frameQuery = SystemAPI.QueryBuilder().WithAll<BulletFrameCounterComponent>().Build();
            if (frameQuery.IsEmptyIgnoreFilter)
            {
                var e = em.CreateEntity(typeof(BulletFrameCounterComponent));
                em.SetComponentData(e, new BulletFrameCounterComponent { Value = 0 });
            }

            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFrameCounterComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var counter = SystemAPI.GetSingletonRW<BulletFrameCounterComponent>();
            counter.ValueRW.Value += 1;
        }
    }

    // ----------------------------------------------------------------------
    // Shared (Pool + SpatialHash)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Burst/Job에서 사용 가능한 정적 공유 저장소.
    /// - FreeByKey(키 기반 풀): Begin/End Owner 영역에서만 접근
    /// - CellMap(SpatialHash): 전체 활성 탄 (Simulation Write / Request ReadOnly)
    /// - HazardCellMap(SpatialHash): 위험탄 전용 (Simulation Write / Request ReadOnly)
    ///
    /// 주의: SharedStatic은 프로세스 전역이므로, 멀티 월드 사용 시 관리 전략이 추가로 필요할 수 있다.
    /// (현재 스코프에서는 단일 월드 전제로 사용)
    /// </summary>
    public static class BulletFieldShared
    {
        private struct FlagsKey { }
        private struct FreeByKeyKey { }
        private struct PoolFenceKey { }
        private struct CellMapKey { }
        private struct HazardCellMapKey { }
        private struct CellMapFenceKey { }

        private static readonly SharedStatic<byte> _flags = SharedStatic<byte>.GetOrCreate<FlagsKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _freeByKey = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<FreeByKeyKey>();
        private static readonly SharedStatic<JobHandle> _poolFence = SharedStatic<JobHandle>.GetOrCreate<PoolFenceKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _cellMap = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<CellMapKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _hazardCellMap = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<HazardCellMapKey>();
        private static readonly SharedStatic<JobHandle> _cellMapFence = SharedStatic<JobHandle>.GetOrCreate<CellMapFenceKey>();

        public static bool IsInitialized => _flags.Data != 0;

        public static ref NativeParallelMultiHashMap<int, Entity> FreeByKey => ref _freeByKey.Data;
        public static ref JobHandle PoolFence => ref _poolFence.Data;

        public static ref NativeParallelMultiHashMap<int, Entity> CellMap => ref _cellMap.Data;
        public static ref NativeParallelMultiHashMap<int, Entity> HazardCellMap => ref _hazardCellMap.Data;
        public static ref JobHandle CellMapFence => ref _cellMapFence.Data;

        public static void MarkInitialized() => _flags.Data = 1;
        public static void MarkUninitialized() => _flags.Data = 0;
    }

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
                    MaxActiveTarget = 100_000,
                    CellSize = 1.6f,
                    InvCellSize = 1f / 1.6f,
                    BulletLifetime = 4.0f,
                };

                em.SetComponentData(configEntity, cfg);
                em.SetComponentData(configEntity, new MetaScrapComponent { Value = 0 });
            }

            var poolRegistryEntity = SystemAPI.GetSingletonEntity<BulletPoolRegistryTag>();
            var poolDefs = SystemAPI.GetBuffer<BulletPoolDefinitionBuffer>(poolRegistryEntity);

            int totalPoolSize = 0;
            for (int i = 0; i < poolDefs.Length; i++)
                totalPoolSize += math.max(0, poolDefs[i].PoolSize);

            var cfgSingleton = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            int poolCapacity = math.max(math.max(1, totalPoolSize), cfgSingleton.PoolSize);

            BulletFieldShared.FreeByKey = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.PoolFence = default;

            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.HazardCellMap = new NativeParallelMultiHashMap<int, Entity>(poolCapacity, Allocator.Persistent);
            BulletFieldShared.CellMapFence = default;

            // Key-Pool 구성: 타입 정의별로 프리팹 인스턴스를 생성해 FreeByKey로 반납
            for (int i = 0; i < poolDefs.Length; i++)
            {
                var def = poolDefs[i];
                if (def.Prefab == Entity.Null || def.PoolSize <= 0)
                    continue;

                using var pool = new NativeArray<Entity>(def.PoolSize, Allocator.Temp);
                em.Instantiate(def.Prefab, pool);

                for (int p = 0; p < pool.Length; p++)
                {
                    var b = pool[p];

                    if (em.HasComponent<BulletActiveTag>(b))
                        em.SetComponentEnabled<BulletActiveTag>(b, false);
                    if (em.HasComponent<BulletDespawnRequestTag>(b))
                        em.SetComponentEnabled<BulletDespawnRequestTag>(b, false);

                    if (em.HasBuffer<EntityRenderElementBuffer>(b))
                    {
                        var parts = em.GetBuffer<EntityRenderElementBuffer>(b);
                        for (int k = 0; k < parts.Length; k++)
                        {
                            var pe = parts[k].Value;
                            if (em.HasComponent<MaterialMeshInfo>(pe))
                                em.SetComponentEnabled<MaterialMeshInfo>(pe, false);
                        }
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

                    BulletFieldShared.FreeByKey.Add(def.TypeKey, b);
                }
            }

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
    }

    // ----------------------------------------------------------------------
    // Pool Owner: Spawn (Begin)
    // ----------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    [UpdateBefore(typeof(BulletSpawnFromPoolSystem))]
    public partial struct BulletFieldAreaUpdateSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new UpdateFieldAreaJob().ScheduleParallel(state.Dependency);
        }

        [BurstCompile]
        private partial struct UpdateFieldAreaJob : IJobEntity
        {
            private void Execute(ref BulletFieldAreaComponent area)
            {
                if (area.Shape == BulletFieldShapeId.Rectangle)
                {
                    area.Size = math.max(0f, area.Size);
                    area.ComputedArea = area.Size.x * area.Size.y;
                    return;
                }

                area.Radius = math.max(0f, area.Radius);
                area.ComputedArea = math.PI * area.Radius * area.Radius;
            }
        }
    }

    /// <summary>
    /// 동프레임 반영을 위한 스폰 실행 단계.
    /// - FreeByKey에서 TypeKey 기준으로 스폰 엔티티 확보
    /// - 데이터 세팅 + BulletActiveTag/MaterialMeshInfo enable
    /// - BulletDespawnRequestTag reset(disable)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    [UpdateAfter(typeof(BulletPoolOwnerBootstrapSystem))]
    public partial struct BulletSpawnFromPoolSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<SourceSpawnComponent>();
            state.RequireForUpdate<BulletFieldAreaComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            float deltaTime = SystemAPI.Time.DeltaTime;

            var tx = SystemAPI.GetComponentLookup<LocalTransform>(false);
            var vel = SystemAPI.GetComponentLookup<BulletVelocityComponent>(false);
            var life = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(false);
            var speed = SystemAPI.GetComponentLookup<BulletSpeedComponent>(true);
            var lifeMax = SystemAPI.GetComponentLookup<BulletLifetimeMaxComponent>(true);
            var typeKey = SystemAPI.GetComponentLookup<BulletTypeKeyComponent>(false);
            var sourceRef = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(false);
            var active = SystemAPI.GetComponentLookup<BulletActiveTag>(false);
            var request = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            var render = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            var renderParts = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            var sourcePollutionConfig = SystemAPI.GetComponentLookup<SourcePollutionConfigComponent>(true);
            var sourcePollutionGrid = SystemAPI.GetComponentLookup<SourcePollutionGridComponent>(true);
            var sourcePollutionCells = SystemAPI.GetBufferLookup<SourcePollutionCellBuffer>(true);
            var sourcePollutionValidCellIndices = SystemAPI.GetBufferLookup<SourcePollutionValidCellIndexBuffer>(true);

            tx.Update(ref state);
            vel.Update(ref state);
            life.Update(ref state);
            speed.Update(ref state);
            lifeMax.Update(ref state);
            typeKey.Update(ref state);
            sourceRef.Update(ref state);
            active.Update(ref state);
            request.Update(ref state);
            render.Update(ref state);
            renderParts.Update(ref state);
            sourcePollutionConfig.Update(ref state);
            sourcePollutionGrid.Update(ref state);
            sourcePollutionCells.Update(ref state);
            sourcePollutionValidCellIndices.Update(ref state);

            // FreeByKey는 ECS 의존성 추적 밖이므로, 전용 fence로 접근 순서를 강제
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);

            var job = new SpawnFromSourcesJob
            {
                DeltaTime = deltaTime,

                FreeByKey = BulletFieldShared.FreeByKey,

                TxLookup = tx,
                VelLookup = vel,
                LifeLookup = life,
                SpeedLookup = speed,
                LifeMaxLookup = lifeMax,
                TypeKeyLookup = typeKey,
                SourceRefLookup = sourceRef,
                ActiveLookup = active,
                RequestLookup = request,
                RenderPartsLookup = renderParts,
                RenderLookup = render,
                PollutionConfigLookup = sourcePollutionConfig,
                PollutionGridLookup = sourcePollutionGrid,
                PollutionCellsLookup = sourcePollutionCells,
                PollutionValidCellIndicesLookup = sourcePollutionValidCellIndices,
            };

            state.Dependency = job.Schedule(deps);
            BulletFieldShared.PoolFence = state.Dependency;
        }

        [BurstCompile]
        private partial struct SpawnFromSourcesJob : IJobEntity
        {
            public float DeltaTime;

            public NativeParallelMultiHashMap<int, Entity> FreeByKey;

            public ComponentLookup<LocalTransform> TxLookup;
            public ComponentLookup<BulletVelocityComponent> VelLookup;
            public ComponentLookup<BulletLifetimeComponent> LifeLookup;
            [ReadOnly] public ComponentLookup<BulletSpeedComponent> SpeedLookup;
            [ReadOnly] public ComponentLookup<BulletLifetimeMaxComponent> LifeMaxLookup;
            public ComponentLookup<BulletTypeKeyComponent> TypeKeyLookup;
            public ComponentLookup<BulletSourceRefComponent> SourceRefLookup;
            public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;
            [ReadOnly] public BufferLookup<EntityRenderElementBuffer> RenderPartsLookup;
            public ComponentLookup<MaterialMeshInfo> RenderLookup;
            [ReadOnly] public ComponentLookup<SourcePollutionConfigComponent> PollutionConfigLookup;
            [ReadOnly] public ComponentLookup<SourcePollutionGridComponent> PollutionGridLookup;
            [ReadOnly] public BufferLookup<SourcePollutionCellBuffer> PollutionCellsLookup;
            [ReadOnly] public BufferLookup<SourcePollutionValidCellIndexBuffer> PollutionValidCellIndicesLookup;

            private void Execute(
                Entity sourceEntity,
                in SourceAnchorComponent sourceAnchor,
                ref SourceSpawnComponent source,
                in BulletFieldAreaComponent fieldArea,
                ref SourceSpawnRuntimeComponent runtime,
                DynamicBuffer<SourceSpawnPatternBuffer> patterns,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts)
            {
                if (source.State == SourceStateId.Depleted)
                    return;

                if (patterns.Length <= 0)
                    return;

                uint seed = math.max(1u, runtime.SpawnSequence);
                var random = Unity.Mathematics.Random.CreateFromIndex(seed ^ (uint)sourceEntity.Index);
                float area = math.max(0f, fieldArea.ComputedArea);
                float3 center = sourceAnchor.Position;

                int spawnedTotal = 0;
                for (int i = 0; i < patterns.Length; i++)
                {
                    var pattern = patterns[i];
                    if (pattern.State != source.State)
                        continue;

                    int spawnCount = ResolveSpawnCount(ref pattern, activeCounts, area);
                    patterns[i] = pattern;
                    if (spawnCount <= 0)
                        continue;

                    for (int s = 0; s < spawnCount; s++)
                    {
                        if (!TryDequeueByKey(pattern.BulletTypeKey, out var e))
                            break;

                        float3 pos = SampleSpawnPosition(ref random, sourceEntity, center, fieldArea);

                        float angle = random.NextFloat(0f, math.PI * 2f);
                        float2 dir = new float2(math.cos(angle), math.sin(angle));
                        var rot = quaternion.LookRotationSafe(new float3(dir.x, 0f, dir.y), math.up());
                        float bulletSpeed = SpeedLookup.HasComponent(e) ? math.max(0f, SpeedLookup[e].Value) : 0f;
                        float bulletLifetime = LifeMaxLookup.HasComponent(e) ? math.max(0f, LifeMaxLookup[e].Value) : 0f;

                        if (TxLookup.HasComponent(e))
                            TxLookup[e] = LocalTransform.FromPositionRotationScale(pos, rot, 1f);
                        if (VelLookup.HasComponent(e))
                            VelLookup[e] = new BulletVelocityComponent { Value = dir * bulletSpeed };
                        if (LifeLookup.HasComponent(e))
                            LifeLookup[e] = new BulletLifetimeComponent { Value = bulletLifetime };
                        if (TypeKeyLookup.HasComponent(e))
                            TypeKeyLookup[e] = new BulletTypeKeyComponent { Value = pattern.BulletTypeKey };
                        if (SourceRefLookup.HasComponent(e))
                            SourceRefLookup[e] = new BulletSourceRefComponent { Value = sourceEntity };

                        if (RequestLookup.HasComponent(e))
                            RequestLookup.SetComponentEnabled(e, false);

                        if (ActiveLookup.HasComponent(e))
                            ActiveLookup.SetComponentEnabled(e, true);

                        if (RenderPartsLookup.HasBuffer(e))
                        {
                            var parts = RenderPartsLookup[e];
                            for (int p = 0; p < parts.Length; p++)
                            {
                                var pe = parts[p].Value;
                                if (RenderLookup.HasComponent(pe))
                                    RenderLookup.SetComponentEnabled(pe, true);
                            }
                        }
                        else if (RenderLookup.HasComponent(e))
                        {
                            RenderLookup.SetComponentEnabled(e, true);
                        }

                        IncrementActiveCount(ref activeCounts, pattern.BulletTypeKey);
                        spawnedTotal++;
                    }
                }

                runtime.SpawnSequence = seed + (uint)math.max(1, spawnedTotal);
            }

            private int ResolveSpawnCount(ref SourceSpawnPatternBuffer pattern, DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, float area)
            {
                float density = math.max(0f, pattern.SpawnDensityPerSecPerArea);
                float rate = density * area;

                if (rate <= 0f)
                {
                    pattern.SpawnAccumulator = 0f;
                    return 0;
                }

                pattern.SpawnAccumulator += rate * DeltaTime;
                int spawnCount = (int)pattern.SpawnAccumulator;
                pattern.SpawnAccumulator -= spawnCount;

                if (spawnCount <= 0)
                    return 0;

                if (pattern.SpawnMode != SourceSpawnModeId.CapAndMaxDensity)
                    return spawnCount;

                int active = GetActiveCount(activeCounts, pattern.BulletTypeKey);
                int maxActive = (int)math.floor(math.max(0f, pattern.MaxActiveDensityPerArea) * area);
                int room = math.max(0, maxActive - active);
                return math.min(spawnCount, room);
            }

            private float3 SampleSpawnPosition(
                ref Unity.Mathematics.Random random,
                Entity sourceEntity,
                float3 center,
                in BulletFieldAreaComponent fieldArea)
            {
                if (TrySampleSpawnPositionFromPollution(
                        ref random,
                        sourceEntity,
                        center,
                        out var pollutionPosition))
                {
                    return pollutionPosition;
                }

                return SampleSpawnPositionUniform(ref random, center, fieldArea);
            }

            private bool TrySampleSpawnPositionFromPollution(
                ref Unity.Mathematics.Random random,
                Entity sourceEntity,
                float3 center,
                out float3 position)
            {
                position = center;
                if (!PollutionConfigLookup.HasComponent(sourceEntity))
                    return false;
                if (!PollutionGridLookup.HasComponent(sourceEntity))
                    return false;
                if (!PollutionCellsLookup.HasBuffer(sourceEntity))
                    return false;
                if (!PollutionValidCellIndicesLookup.HasBuffer(sourceEntity))
                    return false;

                var config = PollutionConfigLookup[sourceEntity];
                if (config.SamplingMode != SourcePollutionSamplingModeId.TopK)
                    return false;

                var grid = PollutionGridLookup[sourceEntity];
                int cols = math.max(1, grid.Cols);
                int rows = math.max(1, grid.Rows);
                var cells = PollutionCellsLookup[sourceEntity];
                var validCellIndices = PollutionValidCellIndicesLookup[sourceEntity];
                int validCount = validCellIndices.Length;
                if (validCount <= 0)
                    return false;

                int topK = math.clamp(config.TopKSampleCount, 1, validCount);
                int bestCellIndex = -1;
                float bestWeight = -1f;

                for (int i = 0; i < topK; i++)
                {
                    int listIndex = random.NextInt(0, validCount);
                    int candidate = validCellIndices[listIndex].Value;
                    float weight = GetValidCellWeight(cells, candidate);
                    if (weight < 0f)
                        continue;

                    if (bestCellIndex < 0)
                    {
                        bestWeight = weight;
                        bestCellIndex = candidate;
                        continue;
                    }

                    if (weight <= bestWeight)
                        continue;

                    bestWeight = weight;
                    bestCellIndex = candidate;
                }

                if (bestCellIndex < 0)
                    return false;

                position = SampleInsidePollutionCell(ref random, bestCellIndex, center, cols, rows, in grid);
                return true;
            }

            private static float GetValidCellWeight(DynamicBuffer<SourcePollutionCellBuffer> cells, int cellIndex)
            {
                if ((uint)cellIndex >= (uint)cells.Length)
                    return -1f;

                var cell = cells[cellIndex];
                if (cell.IsValid == 0)
                    return -1f;

                return math.max(0f, cell.Value);
            }

            private static float3 SampleInsidePollutionCell(
                ref Unity.Mathematics.Random random,
                int cellIndex,
                float3 center,
                int cols,
                int rows,
                in SourcePollutionGridComponent grid)
            {
                int safeCols = math.max(1, cols);
                int safeRows = math.max(1, rows);
                int clampedIndex = math.clamp(cellIndex, 0, safeCols * safeRows - 1);
                int cellX = clampedIndex % safeCols;
                int cellY = math.clamp(clampedIndex / safeCols, 0, safeRows - 1);

                float cellSize = math.max(0.001f, grid.CellSize);
                float2 half = math.max(0f, grid.HalfExtents);

                float localX = -half.x + (cellX + random.NextFloat(0f, 1f)) * cellSize;
                float localZ = -half.y + (cellY + random.NextFloat(0f, 1f)) * cellSize;
                localX = math.clamp(localX, -half.x, half.x);
                localZ = math.clamp(localZ, -half.y, half.y);
                return new float3(center.x + localX, center.y, center.z + localZ);
            }

            private static float3 SampleSpawnPositionUniform(
                ref Unity.Mathematics.Random random,
                float3 center,
                in BulletFieldAreaComponent fieldArea)
            {
                if (fieldArea.Shape == BulletFieldShapeId.Rectangle)
                {
                    float2 half = math.max(0f, fieldArea.Size) * 0.5f;
                    float2 offsetRect = new float2(
                        random.NextFloat(-half.x, half.x),
                        random.NextFloat(-half.y, half.y));
                    return new float3(center.x + offsetRect.x, center.y, center.z + offsetRect.y);
                }

                float radius = math.max(0f, fieldArea.Radius);
                float angle = random.NextFloat(0f, math.PI * 2f);
                float dist = math.sqrt(random.NextFloat(0f, 1f)) * radius;
                float2 offsetCircle = new float2(math.cos(angle), math.sin(angle)) * dist;
                return new float3(center.x + offsetCircle.x, center.y, center.z + offsetCircle.y);
            }

            private bool TryDequeueByKey(int key, out Entity e)
            {
                if (!FreeByKey.TryGetFirstValue(key, out e, out var it))
                    return false;

                FreeByKey.Remove(key, e);
                return true;
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

            private static void IncrementActiveCount(ref DynamicBuffer<SourceActiveBulletCountBuffer> activeCounts, int typeKey)
            {
                for (int i = 0; i < activeCounts.Length; i++)
                {
                    var item = activeCounts[i];
                    if (item.BulletTypeKey != typeKey)
                        continue;

                    item.ActiveCount++;
                    activeCounts[i] = item;
                    return;
                }

                activeCounts.Add(new SourceActiveBulletCountBuffer
                {
                    BulletTypeKey = typeKey,
                    ActiveCount = 1
                });
            }
        }
    }

    // ----------------------------------------------------------------------
    // Simulation: Move/Lifetime + SpatialHash Build (Owner)
    // ----------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletSimulationGroup))]
    public partial struct BulletSimulationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            float dt = SystemAPI.Time.DeltaTime;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            var requestLookup = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            requestLookup.Update(ref state);

            // 1) Move + Lifetime (활성 탄만). 만료 시 디스폰 요청 태그 enable
            state.Dependency = new BulletMoveAndLifetimeJob
            {
                DeltaTime = dt,
                RequestLookup = requestLookup
            }.ScheduleParallel(state.Dependency);

            // 2) SpatialHash Build
            // - CellMap: 전체 활성 탄
            // - HazardCellMap: 위험탄(BulletHazardTag enabled)만
            // - SharedStatic이므로 이전 프레임/요청 시스템의 read가 끝난 뒤 Clear/Build해야 한다.
            var cellMapDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);

            var clearHandle = new ClearCellMapJob
            {
                CellMap = BulletFieldShared.CellMap
            }.Schedule(cellMapDeps);
            var clearHazardHandle = new ClearCellMapJob
            {
                CellMap = BulletFieldShared.HazardCellMap
            }.Schedule(cellMapDeps);

            var clearDeps = JobHandle.CombineDependencies(clearHandle, clearHazardHandle);
            var buildDeps = JobHandle.CombineDependencies(state.Dependency, clearDeps);
            var buildHandle = new BuildSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = BulletFieldShared.CellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);
            var buildHazardHandle = new BuildHazardSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = BulletFieldShared.HazardCellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);

            state.Dependency = JobHandle.CombineDependencies(buildHandle, buildHazardHandle);
            BulletFieldShared.CellMapFence = state.Dependency; // RequestGroup이 이 fence에 의존하도록
        }

        [BurstCompile]
        private partial struct BulletMoveAndLifetimeJob : IJobEntity
        {
            public float DeltaTime;
            // 주의: Enableable 토글을 위해 Lookup을 병렬 Job에서 사용.
            // 동일 엔티티에만 접근하므로 안전하지만, 교차 엔티티 write가 섞이면 레이스 위험이 있음.
            [NativeDisableParallelForRestriction] public ComponentLookup<BulletDespawnRequestTag> RequestLookup;

            private void Execute(
                Entity e,
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                in BulletVelocityComponent vel,
                in BulletActiveTag _)
            {
                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    if (RequestLookup.HasComponent(e))
                        RequestLookup.SetComponentEnabled(e, true);
                }
            }
        }

        [BurstCompile]
        private struct ClearCellMapJob : IJob
        {
            public NativeParallelMultiHashMap<int, Entity> CellMap;
            public void Execute() => CellMap.Clear();
        }

        [BurstCompile]
        private partial struct BuildSpatialHashJob : IJobEntity
        {
            public float InvCellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Writer;

            private void Execute(Entity e, in LocalTransform tx, in BulletActiveTag _)
            {
                var cell = SpatialHashUtility.ToCell(tx.Position, InvCellSize);
                Writer.Add(SpatialHashUtility.Hash(cell), e);
            }
        }

        [BurstCompile]
        private partial struct BuildHazardSpatialHashJob : IJobEntity
        {
            public float InvCellSize;
            public NativeParallelMultiHashMap<int, Entity>.ParallelWriter Writer;

            private void Execute(Entity e, in LocalTransform tx, in BulletActiveTag _, in BulletHazardTag __)
            {
                var cell = SpatialHashUtility.ToCell(tx.Position, InvCellSize);
                Writer.Add(SpatialHashUtility.Hash(cell), e);
            }
        }
    }

    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup), OrderLast = true)]
    public partial struct BulletRequestFencePublishSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            BulletFieldShared.CellMapFence = JobHandle.CombineDependencies(
                BulletFieldShared.CellMapFence,
                state.Dependency);
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
    public partial struct BulletDespawnExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
            state.RequireForUpdate<PlayerTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            // FreeByKey는 ECS 의존성 추적 밖이므로 fence로 시퀀싱
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.PoolFence);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var renderParts = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            var sourceActiveCounts = SystemAPI.GetBufferLookup<SourceActiveBulletCountBuffer>(false);
            renderParts.Update(ref state);
            sourceActiveCounts.Update(ref state);

            var countDeltaQueue = new NativeQueue<SourceActiveCountDelta>(Allocator.TempJob);

            var job = new DespawnAndReturnJob
            {
                FreeByKey = BulletFieldShared.FreeByKey.AsParallelWriter(),
                Ecb = ecb,
                RenderPartsLookup = renderParts,
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
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public BufferLookup<EntityRenderElementBuffer> RenderPartsLookup;
            public NativeQueue<SourceActiveCountDelta>.ParallelWriter CountDeltaWriter;

            private void Execute(
                [EntityIndexInQuery] int sortKey,
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
                    if (RenderPartsLookup.TryGetBuffer(e, out var parts))
                    {
                        for (int p = 0; p < parts.Length; p++)
                        {
                            var pe = parts[p].Value;
                            Ecb.SetComponentEnabled<MaterialMeshInfo>(sortKey, pe, false);
                        }
                    }
                    else
                    {
                        // (fallback) 루트에 렌더가 있는 단일 프리팹 대응
                        Ecb.SetComponentEnabled<MaterialMeshInfo>(sortKey, e, false);
                    }

                    FreeByKey.Add(typeKey.Value, e);
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
