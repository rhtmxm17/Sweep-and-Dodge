// Bullet의 생성, 이동, 수명 관리, 제거를 담당하는 ECS 시스템
// - 업데이트 그룹 파이프라인 구성
//   BulletExecutionBeginGroup  : 풀 Dequeue(스폰 실행)
//   BulletSimulationGroup      : Move/Lifetime + SpatialHash(Build) 단일 소유(Write)
//   BulletRequestGroup         : 제거 행동(예: 탄환 흡입)
//                                - 외부에서 정의될 요청 시스템의 위치
//                                - SpatialHash ReadOnly 조회로 디스폰 요청 태그 enable
//   BulletExecutionEndGroup    : 디스폰 실행 + 풀 Enqueue(반납 실행)
// - FreeList(풀) 접근은 Begin/End "Owner 영역"으로만 제한(다른 시스템은 요청만 남김)
// - SpatialHash(CellMap) writer는 Simulation으로 고정, Request는 ReadOnly 소비
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
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    // ----------------------------------------------------------------------
    // Pipeline Groups
    // ----------------------------------------------------------------------

    // 탄막 필드 파이프라인 그룹들.
    // - 별도의 루트 그룹을 두지 않고, SimulationSystemGroup에서 순서를 강제한다.

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BulletSimulationGroup))]
    public partial class BulletExecutionBeginGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BulletSimulationGroup : ComponentSystemGroup { }

    /// <summary>
    /// Bullet에 대한 외부 요청 시스템들이 위치할 그룹
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletSimulationGroup))]
    public partial class BulletRequestGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletRequestGroup))]
    public partial class BulletExecutionEndGroup : ComponentSystemGroup { }

    // ----------------------------------------------------------------------
    // Shared (Pool + SpatialHash)
    // ----------------------------------------------------------------------

    /// <summary>
    /// Burst/Job에서 사용 가능한 정적 공유 저장소.
    /// - FreeList(풀): Begin/End Owner 영역에서만 접근
    /// - CellMap(SpatialHash): Simulation이 Write, Request가 ReadOnly
    ///
    /// 주의: SharedStatic은 프로세스 전역이므로, 멀티 월드 사용 시 관리 전략이 추가로 필요할 수 있다.
    /// (현재 스코프에서는 단일 월드 전제로 사용)
    /// </summary>
    public static class BulletFieldShared
    {
        private struct FlagsKey { }
        private struct FreeListKey { }
        private struct FreeListFenceKey { }
        private struct CellMapKey { }
        private struct CellMapFenceKey { }

        private static readonly SharedStatic<byte> _flags = SharedStatic<byte>.GetOrCreate<FlagsKey>();
        private static readonly SharedStatic<NativeQueue<Entity>> _freeList = SharedStatic<NativeQueue<Entity>>.GetOrCreate<FreeListKey>();
        private static readonly SharedStatic<JobHandle> _freeListFence = SharedStatic<JobHandle>.GetOrCreate<FreeListFenceKey>();
        private static readonly SharedStatic<NativeParallelMultiHashMap<int, Entity>> _cellMap = SharedStatic<NativeParallelMultiHashMap<int, Entity>>.GetOrCreate<CellMapKey>();
        private static readonly SharedStatic<JobHandle> _cellMapFence = SharedStatic<JobHandle>.GetOrCreate<CellMapFenceKey>();

        public static bool IsInitialized => _flags.Data != 0;

        public static ref NativeQueue<Entity> FreeList => ref _freeList.Data;
        public static ref JobHandle FreeListFence => ref _freeListFence.Data;

        public static ref NativeParallelMultiHashMap<int, Entity> CellMap => ref _cellMap.Data;
        public static ref JobHandle CellMapFence => ref _cellMapFence.Data;

        public static void MarkInitialized() => _flags.Data = 1;
        public static void MarkUninitialized() => _flags.Data = 0;
    }

    // ----------------------------------------------------------------------
    // Pool Owner: Bootstrap
    // ----------------------------------------------------------------------

    /// <summary>
    /// 풀/SpatialHash 컨테이너 초기화 및 해제.
    /// - FreeList, CellMap을 Persistent로 생성
    /// - BulletVisualPrefabComponent 기반으로 PoolSize 만큼 Instantiate 후 FreeList에 전부 반납
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionBeginGroup))]
    public partial struct BulletPoolOwnerBootstrapSystem : ISystem, ISystemStartStop
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletVisualPrefabComponent>();
            state.RequireForUpdate<PlayerTag>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            if (BulletFieldShared.IsInitialized)
                return;

            var em = state.EntityManager;

            // Config + Score 싱글톤이 없으면 기본값 생성(테스트 편의)
            if (!SystemAPI.TryGetSingleton<BulletFieldConfigComponent>(out _))
            {
                var configEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(ScoreComponent));

                var cfg = new BulletFieldConfigComponent
                {
                    PoolSize = 120_000,
                    MaxActiveTarget = 100_000,
                    CellSize = 1.6f,
                    InvCellSize = 1f / 1.6f,
                    BulletLifetime = 4.0f,
                };

                em.SetComponentData(configEntity, cfg);
                em.SetComponentData(configEntity, new ScoreComponent { Value = 0 });
            }

            var cfgSingleton = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            int poolSize = math.max(0, cfgSingleton.PoolSize);

            BulletFieldShared.FreeList = new NativeQueue<Entity>(Allocator.Persistent);
            BulletFieldShared.FreeListFence = default;

            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(poolSize, Allocator.Persistent);
            BulletFieldShared.CellMapFence = default;

            // Entity Prefab Instantiate로 풀 구성
            var visualPrefab = SystemAPI.GetSingleton<BulletVisualPrefabComponent>().Value;
            using var pool = new NativeArray<Entity>(poolSize, Allocator.Temp);
            em.Instantiate(visualPrefab, pool);

            // 풀 초기화(비활성 + 기본값 세팅) + FreeList 반납
            for (int i = 0; i < pool.Length; i++)
            {
                var b = pool[i];

                // 시뮬레이션 off
                if (em.HasComponent<BulletActiveTag>(b))
                    em.SetComponentEnabled<BulletActiveTag>(b, false);

                // 요청 off
                if (em.HasComponent<BulletDespawnRequestTag>(b))
                    em.SetComponentEnabled<BulletDespawnRequestTag>(b, false);

                // 렌더 off: 루트가 아닌 RenderParts(자식 렌더 엔티티)에 대해 토글
                if (em.HasBuffer<EntityRenderElementBuffer>(b))
                {
                    var parts = em.GetBuffer<EntityRenderElementBuffer>(b);
                    for (int p = 0; p < parts.Length; p++)
                    {
                        var pe = parts[p].Value;
                        if (em.HasComponent<MaterialMeshInfo>(pe))
                            em.SetComponentEnabled<MaterialMeshInfo>(pe, false);
                    }
                }
                // fallback) 루트에 렌더가 있는 단일 프리팹 대응
                else
                {
                    if (em.HasComponent<MaterialMeshInfo>(b))
                        em.SetComponentEnabled<MaterialMeshInfo>(b, false);
                }

                // 기본 데이터
                if (em.HasComponent<LocalTransform>(b))
                    em.SetComponentData(b, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                if (em.HasComponent<BulletVelocityComponent>(b))
                    em.SetComponentData(b, new BulletVelocityComponent { Value = float2.zero });
                if (em.HasComponent<BulletLifetimeComponent>(b))
                    em.SetComponentData(b, new BulletLifetimeComponent { Value = 0f });
                if (em.HasComponent<BulletKindComponent>(b))
                    em.SetComponentData(b, new BulletKindComponent { Value = BulletKindId.Trash });
                if (em.HasComponent<BulletSourceRefComponent>(b))
                    em.SetComponentData(b, new BulletSourceRefComponent { Value = Entity.Null });

                BulletFieldShared.FreeList.Enqueue(b);
            }

            BulletFieldShared.MarkInitialized();
        }

        public void OnStopRunning(ref SystemState state)
        {
            // SharedStatic 컨테이너 접근을 안전하게 마무리
            JobHandle.CombineDependencies(BulletFieldShared.FreeListFence, BulletFieldShared.CellMapFence).Complete();

            if (BulletFieldShared.CellMap.IsCreated)
                BulletFieldShared.CellMap.Dispose();
            if (BulletFieldShared.FreeList.IsCreated)
                BulletFieldShared.FreeList.Dispose();

            BulletFieldShared.FreeListFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkUninitialized();
        }
    }

    // ----------------------------------------------------------------------
    // Pool Owner: Spawn (Begin)
    // ----------------------------------------------------------------------

    /// <summary>
    /// 동프레임 반영을 위한 스폰 실행 단계.
    /// - FreeList.TryDequeue()로 스폰 엔티티 확보
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
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletFieldShared.IsInitialized)
                return;

            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            var tx = SystemAPI.GetComponentLookup<LocalTransform>(false);
            var vel = SystemAPI.GetComponentLookup<BulletVelocityComponent>(false);
            var life = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(false);
            var kind = SystemAPI.GetComponentLookup<BulletKindComponent>(false);
            var sourceRef = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(false);
            var active = SystemAPI.GetComponentLookup<BulletActiveTag>(false);
            var request = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(false);
            var render = SystemAPI.GetComponentLookup<MaterialMeshInfo>(false);
            var renderParts = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);

            tx.Update(ref state);
            vel.Update(ref state);
            life.Update(ref state);
            kind.Update(ref state);
            sourceRef.Update(ref state);
            active.Update(ref state);
            request.Update(ref state);
            render.Update(ref state);
            renderParts.Update(ref state);

            // FreeList는 ECS 의존성 추적 밖이므로, 전용 fence로 접근 순서를 강제
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.FreeListFence);

            var job = new SpawnFromSourcesJob
            {
                DeltaTime = deltaTime,
                Speed = 2.5f,
                Lifetime = cfg.BulletLifetime,

                FreeList = BulletFieldShared.FreeList,

                TxLookup = tx,
                VelLookup = vel,
                LifeLookup = life,
                KindLookup = kind,
                SourceRefLookup = sourceRef,
                ActiveLookup = active,
                RequestLookup = request,
                RenderPartsLookup = renderParts,
                RenderLookup = render,
            };

            state.Dependency = job.Schedule(deps);
            BulletFieldShared.FreeListFence = state.Dependency;
        }

        [BurstCompile]
        private partial struct SpawnFromSourcesJob : IJobEntity
        {
            public float DeltaTime;
            public float Speed;
            public float Lifetime;

            public NativeQueue<Entity> FreeList;

            public ComponentLookup<LocalTransform> TxLookup;
            public ComponentLookup<BulletVelocityComponent> VelLookup;
            public ComponentLookup<BulletLifetimeComponent> LifeLookup;
            public ComponentLookup<BulletKindComponent> KindLookup;
            public ComponentLookup<BulletSourceRefComponent> SourceRefLookup;
            public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;
            [ReadOnly] public BufferLookup<EntityRenderElementBuffer> RenderPartsLookup;
            public ComponentLookup<MaterialMeshInfo> RenderLookup;

            private void Execute(
                Entity sourceEntity,
                in SourceAnchorComponent sourceAnchor,
                ref SourceSpawnComponent source,
                ref SourceSpawnRuntimeComponent runtime)
            {
                float rate = source.SpawnRateNormal;
                if (source.State == SourceStateId.Weakened)
                    rate *= source.WeakenedMultiplier;
                else if (source.State == SourceStateId.Depleted)
                    rate = 0f;

                if (rate <= 0f)
                    return;

                runtime.SpawnAccumulator += rate * DeltaTime;
                int spawnCount = (int)runtime.SpawnAccumulator;
                runtime.SpawnAccumulator -= spawnCount;

                if (spawnCount <= 0)
                    return;

                uint seed = math.max(1u, runtime.SpawnSequence);
                runtime.SpawnSequence = seed + (uint)spawnCount;
                var random = Unity.Mathematics.Random.CreateFromIndex(seed);
                float radius = math.max(0f, source.Radius);
                float3 center = sourceAnchor.Position;

                for (int i = 0; i < spawnCount; i++)
                {
                    if (!FreeList.TryDequeue(out var e))
                        break;

                    float anglePos = random.NextFloat(0f, math.PI * 2f);
                    float dist = math.sqrt(random.NextFloat(0f, 1f)) * radius;
                    float2 offset = new float2(math.cos(anglePos), math.sin(anglePos)) * dist;
                    float3 pos = new float3(center.x + offset.x, center.y, center.z + offset.y);

                    float angle = random.NextFloat(0f, math.PI * 2f);
                    float2 dir = new float2(math.cos(angle), math.sin(angle));
                    var rot = quaternion.LookRotationSafe(new float3(dir.x, 0f, dir.y), math.up());

                    if (TxLookup.HasComponent(e))
                        TxLookup[e] = LocalTransform.FromPositionRotationScale(pos, rot, 1f);
                    if (VelLookup.HasComponent(e))
                        VelLookup[e] = new BulletVelocityComponent { Value = dir * Speed };
                    if (LifeLookup.HasComponent(e))
                        LifeLookup[e] = new BulletLifetimeComponent { Value = Lifetime };
                    if (KindLookup.HasComponent(e))
                    {
                        float hazardRatio = GetHazardRatio(source);
                        var kind = random.NextFloat(0f, 1f) < hazardRatio ? BulletKindId.Hazard : BulletKindId.Trash;
                        KindLookup[e] = new BulletKindComponent { Value = kind };
                    }
                    if (SourceRefLookup.HasComponent(e))
                        SourceRefLookup[e] = new BulletSourceRefComponent { Value = sourceEntity };

                    if (RequestLookup.HasComponent(e))
                        RequestLookup.SetComponentEnabled(e, false);

                    if (ActiveLookup.HasComponent(e))
                        ActiveLookup.SetComponentEnabled(e, true);
                    // 렌더 on: RenderParts(자식 렌더 엔티티) MaterialMeshInfo enable
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
                    // 초기화 시점에 fallback 이 적용된, 루트에 렌더가 있는 단일 프리팹 대응
                    else
                    {
                        if (RenderLookup.HasComponent(e))
                            RenderLookup.SetComponentEnabled(e, true);
                    }
                }
            }

            private static float GetHazardRatio(in SourceSpawnComponent source)
            {
                if (source.State == SourceStateId.Depleted)
                    return math.saturate(source.HazardRatioNearDepleted);
                if (source.State == SourceStateId.Weakened)
                    return math.saturate(source.HazardRatioWeakened);
                return math.saturate(source.HazardRatioNormal);
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

            // 2) SpatialHash Build (활성 탄만)
            // - CellMap은 SharedStatic이므로 이전 프레임/요청 시스템의 read가 끝난 뒤 Clear/Build해야 한다.
            var cellMapDeps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.CellMapFence);

            var clearHandle = new ClearCellMapJob
            {
                CellMap = BulletFieldShared.CellMap
            }.Schedule(cellMapDeps);

            var buildDeps = JobHandle.CombineDependencies(state.Dependency, clearHandle);
            var buildHandle = new BuildSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = BulletFieldShared.CellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);

            state.Dependency = buildHandle;
            BulletFieldShared.CellMapFence = buildHandle; // RequestGroup이 이 fence에 의존하도록
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

            // FreeList는 ECS 의존성 추적 밖이므로 fence로 시퀀싱
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletFieldShared.FreeListFence);

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter();

            var renderParts = SystemAPI.GetBufferLookup<EntityRenderElementBuffer>(true);
            renderParts.Update(ref state);

            var job = new DespawnAndReturnJob
            {
                FreeList = BulletFieldShared.FreeList.AsParallelWriter(),
                Ecb = ecb,
                RenderPartsLookup = renderParts,
            };

            state.Dependency = job.ScheduleParallel(deps);
            BulletFieldShared.FreeListFence = state.Dependency;
        }

        [BurstCompile]
        private partial struct DespawnAndReturnJob : IJobEntity
        {
            public NativeQueue<Entity>.ParallelWriter FreeList;
            public EntityCommandBuffer.ParallelWriter Ecb;
            [ReadOnly] public BufferLookup<EntityRenderElementBuffer> RenderPartsLookup;

            private void Execute(
                [EntityIndexInQuery] int sortKey,
                Entity e,
                ref BulletLifetimeComponent life,
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

                    FreeList.Enqueue(e);
                }

                request.ValueRW = false;
                life.Value = 0f;
            }
        }
    }
}
