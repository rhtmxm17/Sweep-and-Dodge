using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [BurstCompile]
    public partial struct BulletFieldSystem : ISystem, ISystemStartStop
    {
        private NativeParallelMultiHashMap<int, Entity> _cellMap;
        private NativeArray<Entity> _pool;
        private NativeQueue<Entity> _freeList;

        private float _spawnAcc;
        private uint _spawnSequence; // Random.CreateFromIndex 기반 스폰 시퀀스

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletVisualPrefabComponent>();
        }

        public void OnStartRunning(ref SystemState state)
        {
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

            // 위에서 생성했을 수 있으므로, 여기서 확정적으로 다시 읽는다.
            var cfgSingleton = SystemAPI.GetSingleton<BulletFieldConfigComponent>();

            _cellMap = new NativeParallelMultiHashMap<int, Entity>(cfgSingleton.PoolSize, Allocator.Persistent);
            _pool = new NativeArray<Entity>(cfgSingleton.PoolSize, Allocator.Persistent);
            _freeList = new NativeQueue<Entity>(Allocator.Persistent);

            // Entity Prefab Instantiate로 풀 구성
            var visualPrefab = SystemAPI.GetSingleton<BulletVisualPrefabComponent>().Value;
            em.Instantiate(visualPrefab, _pool);

            // 풀 초기화(비활성 + 기본값 세팅)
            for (int i = 0; i < _pool.Length; i++)
            {
                var b = _pool[i];

                // 시뮬레이션 off
                em.SetComponentEnabled<BulletActiveTag>(b, false);

                // 렌더 off (프리펩이 Renderable로 베이크되면 MaterialMeshInfo가 존재)
                em.SetComponentEnabled<MaterialMeshInfo>(b, false);

                // 기본 데이터
                // (BulletRadiusComponent는 프리펩 Authoring 기본값 유지)
                em.SetComponentData(b, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                em.SetComponentData(b, new BulletVelocityComponent { Value = float2.zero });
                em.SetComponentData(b, new BulletLifetimeComponent { Value = 0f });
                em.SetComponentData(b, new BulletKindComponent { Value = BulletKindId.Trash });

                // Free-list에 반납(초기에는 전부 비활성)
                _freeList.Enqueue(b);
            }

            _spawnAcc = 0f;
            _spawnSequence = 1;
        }

        public void OnStopRunning(ref SystemState state)
        {
            // 스케줄된 잡이 남아있을 수 있으므로 Dispose 전 완료
            state.Dependency.Complete();

            if (_cellMap.IsCreated) _cellMap.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
            if (_freeList.IsCreated) _freeList.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            // 임시 조치: BulletMoveJob에서 LocalTransform 타입을 사용하기 전에 캐시해둠
            // TODO: 탄환 제거 행동의 System 분리시 필요없어짐
            var playerTx = SystemAPI.GetComponent<LocalTransform>(playerEntity);
            var playerPos = playerTx.Position;

            // 0) 이번 프레임 스폰 개수 산출(메인)
            _spawnAcc += cfg.SpawnRate * dt;
            int spawnCount = (int)_spawnAcc;
            _spawnAcc -= spawnCount;
            uint spawnSeqStart = _spawnSequence;
            _spawnSequence += (uint)math.max(0, spawnCount);

            // 1) Spawn (Job: free-list pop + 데이터 세팅 + enable)
            var txLookupSpawn = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
            var velLookupSpawn = SystemAPI.GetComponentLookup<BulletVelocityComponent>(isReadOnly: false);
            var lifeLookupSpawn = SystemAPI.GetComponentLookup<BulletLifetimeComponent>(isReadOnly: false);
            var kindLookupSpawn = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: false);
            var activeLookupSpawn = SystemAPI.GetComponentLookup<BulletActiveTag>(isReadOnly: false);
            var renderLookupSpawn = SystemAPI.GetComponentLookup<MaterialMeshInfo>(isReadOnly: false);

            txLookupSpawn.Update(ref state);
            velLookupSpawn.Update(ref state);
            lifeLookupSpawn.Update(ref state);
            kindLookupSpawn.Update(ref state);
            activeLookupSpawn.Update(ref state);
            renderLookupSpawn.Update(ref state);

            var spawnHandle = new SpawnFromFreeListJob
            {
                SpawnCount = spawnCount,
                SpawnSeed = spawnSeqStart,

                PosMin = -20f,
                PosMax = 20f,
                Speed = 6.5f,
                Lifetime = cfg.BulletLifetime,
                Kind = BulletKindId.Trash,

                FreeList = _freeList,

                TxLookup = txLookupSpawn,
                VelLookup = velLookupSpawn,
                LifeLookup = lifeLookupSpawn,
                KindLookup = kindLookupSpawn,
                ActiveLookup = activeLookupSpawn,
                RenderLookup = renderLookupSpawn,
            }.Schedule(state.Dependency);

            // 2) Move + Lifetime (활성 탄만) + 디스폰 시 free-list 반납
            var moveHandle = new BulletMoveJob
            {
                DeltaTime = dt,
                FreeList = _freeList.AsParallelWriter()
            }.ScheduleParallel(spawnHandle);

            // 3) SpatialHash Build (활성 탄만)
            // - 메인 스레드에서 _cellMap.Clear()를 호출하면 이전 프레임 잡과 경쟁할 수 있으므로, Clear도 Job으로 처리
            var clearHandle = new ClearCellMapJob
            {
                CellMap = _cellMap
            }.Schedule(spawnHandle);

            var buildDeps = JobHandle.CombineDependencies(moveHandle, clearHandle);
            var buildHandle = new BuildSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = _cellMap.AsParallelWriter()
            }.ScheduleParallel(buildDeps);

            // 4) Vacuum 상태 갱신(플레이어 단일임. 메인에서 진행)
            var vacuumRW = SystemAPI.GetComponentRW<VacuumBurstComponent>(playerEntity);
            UpdateVacuumState(ref vacuumRW.ValueRW, dt);

            // 5) Vacuum 적용
            if (vacuumRW.ValueRO.IsActive != 0)
            {

                var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
                var velLookup = SystemAPI.GetComponentLookup<BulletVelocityComponent>(isReadOnly: false);
                var kindLookup = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: true);
                var activeLookup = SystemAPI.GetComponentLookup<BulletActiveTag>(isReadOnly: false);
                var renderLookup = SystemAPI.GetComponentLookup<MaterialMeshInfo>(isReadOnly: false);

                var scoreEntity = SystemAPI.GetSingletonEntity<BulletFieldConfigComponent>();
                var scoreLookup = SystemAPI.GetComponentLookup<ScoreComponent>(isReadOnly: false);

                txLookup.Update(ref state);
                velLookup.Update(ref state);
                kindLookup.Update(ref state);

                activeLookup.Update(ref state);
                renderLookup.Update(ref state);
                scoreLookup.Update(ref state);

                var vacuumHandle = new VacuumJob
                {
                    DeltaTime = dt,

                    PlayerPos = playerPos,
                    Range = vacuumRW.ValueRO.Range,
                    Strength = vacuumRW.ValueRO.Strength,
                    CollectRadius = vacuumRW.ValueRO.CollectRadius,
                    InvCellSize = cfg.InvCellSize,

                    CellMap = _cellMap,

                    TxLookup = txLookup,
                    VelLookup = velLookup,
                    KindLookup = kindLookup,

                    ActiveLookup = activeLookup,
                    RenderLookup = renderLookup,
                    FreeList = _freeList,

                    ScoreEntity = scoreEntity,
                    ScoreLookup = scoreLookup,
                }.Schedule(buildHandle);

                state.Dependency = vacuumHandle;
                return;
            }

            // Vacuum 비활성인 프레임
            state.Dependency = buildHandle;
        }

        private static void UpdateVacuumState(ref VacuumBurstComponent v, float dt)
        {
            if (v.CooldownTimer > 0f)
                v.CooldownTimer = math.max(0f, v.CooldownTimer - dt);

            if (v.IsActive != 0)
            {
                v.ActiveTimer = math.max(0f, v.ActiveTimer - dt);
                if (v.ActiveTimer <= 0f)
                {
                    v.IsActive = 0;
                    v.CooldownTimer = v.Cooldown;
                }
                return;
            }

            if (v.ActivateRequested != 0 && v.CooldownTimer <= 0f)
            {
                v.ActivateRequested = 0;
                v.IsActive = 1;
                v.ActiveTimer = v.ActiveTime;
            }
            else
            {
                // 선입력 버림(쿨타임 중 요청은 폐기)
                v.ActivateRequested = 0;
            }
        }

        // ---------------- Jobs ----------------

        [BurstCompile]
        public struct SpawnFromFreeListJob : IJob
        {
            public int SpawnCount;
            public uint SpawnSeed;

            public float PosMin;
            public float PosMax;
            public float Speed;
            public float Lifetime;
            public BulletKindId Kind;

            // NOTE: NativeQueue는 단일 소비자(pop)에 안전. 이 Job은 단일(IJob)로만 사용.
            public NativeQueue<Entity> FreeList;

            public ComponentLookup<LocalTransform> TxLookup;
            public ComponentLookup<BulletVelocityComponent> VelLookup;
            public ComponentLookup<BulletLifetimeComponent> LifeLookup;
            public ComponentLookup<BulletKindComponent> KindLookup;
            public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<MaterialMeshInfo> RenderLookup;

            public void Execute()
            {
                if (SpawnCount <= 0)
                    return;

                // 프레임별로 다른 시퀀스를 갖도록 CreateFromIndex를 사용
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

                    ActiveLookup.SetComponentEnabled(bullet, true);
                    RenderLookup.SetComponentEnabled(bullet, true);
                }
            }
        }

        [BurstCompile]
        public struct ClearCellMapJob : IJob
        {
            public NativeParallelMultiHashMap<int, Entity> CellMap;

            public void Execute()
            {
                CellMap.Clear();
            }
        }

        [BurstCompile]
        public partial struct BulletMoveJob : IJobEntity
        {
            public float DeltaTime;
            public NativeQueue<Entity>.ParallelWriter FreeList;

            private void Execute(Entity e,
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                in BulletVelocityComponent vel,
                EnabledRefRW<BulletActiveTag> active,
                EnabledRefRW<MaterialMeshInfo> render)
            {
                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    // 디스폰: 시뮬레이션 off + 렌더 off + free-list 반납
                    active.ValueRW = false;
                    render.ValueRW = false;
                    FreeList.Enqueue(e);
                }
            }
        }

        [BurstCompile]
        public partial struct BuildSpatialHashJob : IJobEntity
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
        public struct VacuumJob : IJob
        {
            public float DeltaTime;

            public float3 PlayerPos;
            public float Range;
            public float Strength;
            public float CollectRadius;
            public float InvCellSize;

            [ReadOnly] public NativeParallelMultiHashMap<int, Entity> CellMap;

            public ComponentLookup<LocalTransform> TxLookup;
            public ComponentLookup<BulletVelocityComponent> VelLookup;
            [ReadOnly] public ComponentLookup<BulletKindComponent> KindLookup;

            public ComponentLookup<BulletActiveTag> ActiveLookup;
            public ComponentLookup<MaterialMeshInfo> RenderLookup;

            public NativeQueue<Entity> FreeList;

            public Entity ScoreEntity;
            public ComponentLookup<ScoreComponent> ScoreLookup;

            public void Execute()
            {
                float rangeSq = Range * Range;
                float collectSq = CollectRadius * CollectRadius;

                int2 center = SpatialHashUtility.ToCell(PlayerPos, InvCellSize);
                int cellRadius = (int)math.ceil(Range * InvCellSize);

                long collected = 0;

                for (int dy = -cellRadius; dy <= cellRadius; dy++)
                    for (int dx = -cellRadius; dx <= cellRadius; dx++)
                    {
                        int2 c = center + new int2(dx, dy);
                        int key = SpatialHashUtility.Hash(c);

                        if (!CellMap.TryGetFirstValue(key, out var bullet, out var it))
                            continue;

                        do
                        {
                            if (!TxLookup.HasComponent(bullet)) continue;
                            if (!VelLookup.HasComponent(bullet)) continue;
                            if (!KindLookup.HasComponent(bullet)) continue;

                            if (KindLookup[bullet].Value != BulletKindId.Trash)
                                continue;

                            var tx = TxLookup[bullet];
                            float3 d3 = PlayerPos - tx.Position;
                            float distSq = d3.x * d3.x + d3.z * d3.z;

                            if (distSq > rangeSq)
                                continue;

                            if (distSq <= collectSq)
                            {
                                // 수거: 시뮬레이션 off + 렌더 off + free-list 반납
                                ActiveLookup.SetComponentEnabled(bullet, false);
                                RenderLookup.SetComponentEnabled(bullet, false);
                                FreeList.Enqueue(bullet);
                                collected++;
                                continue;
                            }

                            float invLen = math.rsqrt(math.max(distSq, 1e-6f));
                            float2 dir = new float2(d3.x, d3.z) * invLen;

                            var v = VelLookup[bullet];
                            v.Value += dir * Strength * DeltaTime;
                            VelLookup[bullet] = v;

                        } while (CellMap.TryGetNextValue(out bullet, ref it));
                    }

                if (collected > 0)
                {
                    var score = ScoreLookup[ScoreEntity];
                    score.Value += collected;
                    ScoreLookup[ScoreEntity] = score;
                }
            }
        }
    }
}
