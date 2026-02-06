using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    /*
     * - 파이프라인 구성
     *   BulletExecutionBeginGroup  : 풀 Dequeue(스폰 실행)
     *   BulletSimulationGroup      : Move/Lifetime + SpatialHash 소유(Build + Write)
     *   BulletRequestGroup         : Vacuum/Bomb/... 등 디스폰 요청 생성 전용 (SpatialHash ReadOnly)
     *   BulletExecutionGroup       : 요청/만료 기반 디스폰 실행(비활성 + 풀 반납)
     *   
     * - LocalTransform 타입 충돌 방지: 메인 스레드에서 LocalTransform을 직접 읽지 않고 Job으로 스케줄
     * 
     */

    // ---------------------------------------------------------------------
    // 파이프라인 그룹
    // ---------------------------------------------------------------------

    /// <summary>
    /// 탄막 필드 파이프라인 그룹들.
    /// - 별도의 루트 그룹을 두지 않고, SimulationSystemGroup에서 순서를 강제한다.
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(BulletSimulationGroup))]
    public partial class BulletExecutionBeginGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class BulletSimulationGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletSimulationGroup))]
    public partial class BulletRequestGroup : ComponentSystemGroup { }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletRequestGroup))]
    public partial class BulletExecutionGroup : ComponentSystemGroup { }

    // ---------------------------------------------------------------------
    // Shared
    // ---------------------------------------------------------------------

    internal static class BulletPoolShared
    {
        public static NativeQueue<Entity> FreeList;
        public static NativeArray<Entity> Pool;

        // NativeQueue는 Entities dependency tracking 밖의 컨테이너이므로,
        // 시스템 간 동시 접근(Dequeue/Enqueue)을 방지하기 위한 수동 시퀀싱 핸들.
        public static JobHandle FreeListAccessHandle;

        public static bool IsCreated;

        public static void Dispose()
        {
            FreeListAccessHandle.Complete();

            if (FreeList.IsCreated) FreeList.Dispose();
            if (Pool.IsCreated) Pool.Dispose();

            IsCreated = false;
            FreeListAccessHandle = default;
        }
    }

    // ---------------------------------------------------------------------
    // Bootstrap: 풀 생성 + 초기화
    // ---------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletSimulationGroup))]
    public partial struct BulletPoolBootstrapSystem : ISystem, ISystemStartStop
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletVisualPrefabComponent>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            if (BulletPoolShared.IsCreated)
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
                    SpawnRate = 25_000f,
                };

                em.SetComponentData(configEntity, cfg);
                em.SetComponentData(configEntity, new ScoreComponent { Value = 0 });
            }

            var cfgSingleton = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            BulletPoolShared.FreeList = new NativeQueue<Entity>(Allocator.Persistent);
            BulletPoolShared.Pool = new NativeArray<Entity>(cfgSingleton.PoolSize, Allocator.Persistent);

            // Entity Prefab Instantiate로 풀 구성
            var visualPrefab = SystemAPI.GetSingleton<BulletVisualPrefabComponent>().Value;
            em.Instantiate(visualPrefab, BulletPoolShared.Pool);

            // 풀 초기화(비활성 + 기본값 세팅)
            for (int i = 0; i < BulletPoolShared.Pool.Length; i++)
            {
                var b = BulletPoolShared.Pool[i];

                // 시뮬레이션 off
                if (em.HasComponent<BulletActiveTag>(b))
                    em.SetComponentEnabled<BulletActiveTag>(b, false);

                // 렌더 off
                if (em.HasComponent<MaterialMeshInfo>(b))
                    em.SetComponentEnabled<MaterialMeshInfo>(b, false);

                // 디스폰 요청 off
                if (em.HasComponent<BulletDespawnRequestTag>(b))
                    em.SetComponentEnabled<BulletDespawnRequestTag>(b, false);

                // 기본 데이터
                em.SetComponentData(b, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                em.SetComponentData(b, new BulletVelocityComponent { Value = float2.zero });
                em.SetComponentData(b, new BulletLifetimeComponent { Value = 0f });
                em.SetComponentData(b, new BulletKindComponent { Value = BulletKindId.Trash });

                BulletPoolShared.FreeList.Enqueue(b);
            }

            BulletPoolShared.FreeListAccessHandle = default;
            BulletPoolShared.IsCreated = true;
        }

        public void OnStopRunning(ref SystemState state)
        {
            // 시스템/잡 종료 전 안전하게 정리
            state.Dependency.Complete();
            BulletPoolShared.Dispose();
        }
    }

    // ---------------------------------------------------------------------
    // Simulation: 스폰 + 이동 + 수명관리
    // ---------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletSimulationGroup))]
    [UpdateAfter(typeof(BulletPoolBootstrapSystem))]
    public partial struct BulletSimulationSystem : ISystem
    {
        private float _spawnAcc;
        private uint _spawnSequence;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            // 이번 프레임 스폰 개수 산출(메인)
            _spawnAcc += cfg.SpawnRate * dt;
            int spawnCount = (int)_spawnAcc;
            _spawnAcc -= spawnCount;

            uint spawnSeqStart = _spawnSequence;
            _spawnSequence += (uint)math.max(0, spawnCount);

            // Spawn: NativeQueue 접근은 수동 핸들로 시퀀싱
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletPoolShared.FreeListAccessHandle);

            var txLookupSpawn = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
            var velLookupSpawn = SystemAPI.GetComponentLookup<BulletVelocityComponent>(isReadOnly: false);
            var lifeLookupSpawn = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(isReadOnly: false);
            var kindLookupSpawn = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: false);
            var activeLookupSpawn = SystemAPI.GetComponentLookup<BulletActiveTag>(isReadOnly: false);
            var renderLookupSpawn = SystemAPI.GetComponentLookup<MaterialMeshInfo>(isReadOnly: false);
            var requestLookupSpawn = SystemAPI.GetComponentLookup<BulletDespawnRequestTag>(isReadOnly: false);

            txLookupSpawn.Update(ref state);
            velLookupSpawn.Update(ref state);
            lifeLookupSpawn.Update(ref state);
            kindLookupSpawn.Update(ref state);
            activeLookupSpawn.Update(ref state);
            renderLookupSpawn.Update(ref state);
            requestLookupSpawn.Update(ref state);

            var spawnHandle = new SpawnFromFreeListJob
            {
                SpawnCount = spawnCount,
                SpawnSeed = spawnSeqStart,

                PosMin = -20f,
                PosMax = 20f,
                Speed = 6.5f,
                Lifetime = cfg.BulletLifetime,
                Kind = BulletKindId.Trash,

                FreeList = BulletPoolShared.FreeList,

                TxLookup = txLookupSpawn,
                VelLookup = velLookupSpawn,
                LifeLookup = lifeLookupSpawn,
                KindLookup = kindLookupSpawn,
                ActiveLookup = activeLookupSpawn,
                RenderLookup = renderLookupSpawn,
                RequestLookup = requestLookupSpawn,
            }.Schedule(deps);

            BulletPoolShared.FreeListAccessHandle = spawnHandle;

            // Move + Lifetime 감소 + 만료 시 디스폰 태그 활성화(실제 디스폰은 Execution 단계)
            var moveHandle = new BulletMoveAndExpireRequestJob
            {
                DeltaTime = dt
            }.ScheduleParallel(spawnHandle);

            state.Dependency = moveHandle;
        }

        // ---------------- Jobs ----------------

        [BurstCompile]
        private struct SpawnFromFreeListJob : IJob
        {
            public int SpawnCount;
            public uint SpawnSeed;

            public float PosMin;
            public float PosMax;
            public float Speed;
            public float Lifetime;
            public BulletKindId Kind;

            // 단일 소비자(Dequeue) Job
            public NativeQueue<Entity> FreeList;

            public ComponentLookup<LocalTransform> TxLookup;
            public ComponentLookup<BulletVelocityComponent> VelLookup;
            public ComponentLookup<BulletLifetimeComponent> LifeLookup;
            public ComponentLookup<BulletKindComponent> KindLookup;
            public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<MaterialMeshInfo> RenderLookup;
            public ComponentLookup<BulletDespawnRequestTag> RequestLookup;

            public void Execute()
            {
                if (SpawnCount <= 0)
                    return;

                var rand = Random.CreateFromIndex(math.max(1u, SpawnSeed));

                for (int i = 0; i < SpawnCount; i++)
                {
                    if (!FreeList.TryDequeue(out var bullet))
                        break;

                    float3 pos = new float3(
                        rand.NextFloat(PosMin, PosMax),
                        0f,
                        rand.NextFloat(PosMin, PosMax));

                    float angle = rand.NextFloat(0f, math.PI * 2f);
                    float2 dir = new float2(math.cos(angle), math.sin(angle));
                    var rot = quaternion.LookRotationSafe(new float3(dir.x, 0f, dir.y), math.up());

                    TxLookup[bullet] = LocalTransform.FromPositionRotationScale(pos, rot, 1f);
                    VelLookup[bullet] = new BulletVelocityComponent { Value = dir * Speed };
                    LifeLookup[bullet] = new BulletLifetimeComponent { Value = Lifetime };
                    KindLookup[bullet] = new BulletKindComponent { Value = Kind };

                    // 요청 리셋(재스폰 즉시 제거 버그 방지)
                    if (RequestLookup.HasComponent(bullet))
                        RequestLookup.SetComponentEnabled(bullet, false);

                    if (ActiveLookup.HasComponent(bullet))
                        ActiveLookup.SetComponentEnabled(bullet, true);

                    if (RenderLookup.HasComponent(bullet))
                        RenderLookup.SetComponentEnabled(bullet, true);
                }
            }
        }

        [BurstCompile]
        private partial struct BulletMoveAndExpireRequestJob : IJobEntity
        {
            public float DeltaTime;

            private void Execute(
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                in BulletVelocityComponent vel,
                EnabledRefRO<BulletActiveTag> active,
                EnabledRefRW<BulletDespawnRequestTag> despawnRequest)
            {
                if (!active.ValueRO)
                    return;

                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    // 만료는 요청 태그만 남기고, 실제 비활성/풀 반납은 Execution 단계에서 수행
                    despawnRequest.ValueRW = true;
                }
            }
        }
    }

    // ---------------------------------------------------------------------
    // Execution: 디스폰 태그 실행
    // ---------------------------------------------------------------------

    [BurstCompile]
    [UpdateInGroup(typeof(BulletExecutionGroup))]
    public partial struct BulletDespawnExecutionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletFieldConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!BulletPoolShared.IsCreated)
                return;

            // NativeQueue 접근은 수동 핸들로 시퀀싱
            var deps = JobHandle.CombineDependencies(state.Dependency, BulletPoolShared.FreeListAccessHandle);

            var handle = new DespawnExecutionJob
            {
                FreeList = BulletPoolShared.FreeList.AsParallelWriter()
            }.ScheduleParallel(deps);

            BulletPoolShared.FreeListAccessHandle = handle;
            state.Dependency = handle;
        }

        [BurstCompile]
        private partial struct DespawnExecutionJob : IJobEntity
        {
            public NativeQueue<Entity>.ParallelWriter FreeList;

            private void Execute(
                Entity e,
                ref BulletLifetimeComponent life,
                EnabledRefRW<BulletActiveTag> active,
                EnabledRefRW<MaterialMeshInfo> render,
                EnabledRefRW<BulletDespawnRequestTag> request)
            {
                // 요청이 없으면 스킵(만료 요청은 Simulation 단계에서 이미 request=true로 전환)
                if (!request.ValueRO)
                    return;

                // 중복 반납 방지: active true→false 전이에서만 enqueue
                if (active.ValueRO)
                {
                    active.ValueRW = false;
                    render.ValueRW = false;
                    FreeList.Enqueue(e);
                }

                // 요청 리셋
                request.ValueRW = false;
                life.Value = 0f;
            }
        }
    }
}
