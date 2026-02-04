using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace SweepnDodge.DotsBullets
{
    [BurstCompile]
    public partial struct BulletFieldSystem : ISystem, ISystemStartStop
    {
        private NativeParallelMultiHashMap<int, Entity> _cellMap;
        private NativeArray<Entity> _pool;
        private int _poolNext;
        private float _spawnAcc;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<BulletVisualPrefabComponent>();
        }

        public void OnStartRunning(ref SystemState state)
        {
            var em = state.EntityManager;

            // Config + Score 싱글톤이 없으면 기본값 생성(테스트 편의)
            if (!SystemAPI.TryGetSingleton<BulletFieldConfigComponent>(out var cfgSingleton))
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

            _cellMap = new NativeParallelMultiHashMap<int, Entity>(cfgSingleton.PoolSize, Allocator.Persistent);
            _pool = new NativeArray<Entity>(cfgSingleton.PoolSize, Allocator.Persistent);

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
            }
        }

        public void OnStopRunning(ref SystemState state)
        {
            if (_cellMap.IsCreated) _cellMap.Dispose();
            if (_pool.IsCreated) _pool.Dispose();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var cfg = SystemAPI.GetSingleton<BulletFieldConfigComponent>();
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            // 1) Spawn
            Spawn(ref state, cfg, dt);

            // 2) Move + Lifetime (활성 탄만)
            state.Dependency = new BulletMoveJob
            {
                DeltaTime = dt,
                ECB = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            // 3) SpatialHash Build (활성 탄만)
            _cellMap.Clear();
            state.Dependency = new BuildSpatialHashJob
            {
                InvCellSize = cfg.InvCellSize,
                Writer = _cellMap.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            // 4) Vacuum 상태 갱신(플레이어 단일임. 메인에서 진행)
            var vacuumRW = SystemAPI.GetComponentRW<VacuumBurstComponent>(playerEntity);
            UpdateVacuumState(ref vacuumRW.ValueRW, dt);

            // 5) Vacuum 적용
            if (vacuumRW.ValueRO.IsActive != 0)
            {
                var playerTx = SystemAPI.GetComponent<LocalTransform>(playerEntity);
                var playerPos = playerTx.Position;

                var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: false);
                var velLookup = SystemAPI.GetComponentLookup<BulletVelocityComponent>(isReadOnly: false);
                var kindLookup = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: true);

                txLookup.Update(ref state);
                velLookup.Update(ref state);
                kindLookup.Update(ref state);

                var collectedRef = new NativeReference<long>(Allocator.TempJob);
                collectedRef.Value = 0;

                state.Dependency = new VacuumJob
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

                    ECB = ecb,
                    Collected = collectedRef
                }.Schedule(state.Dependency);

                // Score 반영
                var configEntity = SystemAPI.GetSingletonEntity<BulletFieldConfigComponent>();
                var scoreLookup = SystemAPI.GetComponentLookup<ScoreComponent>(isReadOnly: false);
                scoreLookup.Update(ref state);

                state.Dependency = new ApplyCollectedToScoreJob
                {
                    ScoreEntity = configEntity,
                    ScoreLookup = scoreLookup,
                    Collected = collectedRef
                }.Schedule(state.Dependency);

                state.Dependency = collectedRef.Dispose(state.Dependency);
            }
        }

        private void Spawn(ref SystemState state, BulletFieldConfigComponent cfg, float dt)
        {
            _spawnAcc += cfg.SpawnRate * dt;
            int spawnCount = (int)_spawnAcc;
            _spawnAcc -= spawnCount;

            var em = state.EntityManager;

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryGetInactiveBullet(em, out var bullet))
                    break;

                // MVP 스폰: 임의 위치/속도 (패턴 시스템으로 교체 예정)
                float angle = (float)((_poolNext * 0.6180339887) % 1.0) * math.PI * 2f;
                float speed = 6.5f;

                var pos = new float3(math.cos(angle) * 18f, 0f, math.sin(angle) * 18f);

                em.SetComponentData(bullet, LocalTransform.FromPositionRotationScale(pos, quaternion.identity, 1f));
                em.SetComponentData(bullet, new BulletVelocityComponent
                {
                    Value = new float2(-math.cos(angle), -math.sin(angle)) * speed
                });
                em.SetComponentData(bullet, new BulletLifetimeComponent { Value = cfg.BulletLifetime });
                em.SetComponentData(bullet, new BulletKindComponent { Value = BulletKindId.Trash });

                // 시뮬레이션/렌더 on
                em.SetComponentEnabled<BulletActiveTag>(bullet, true);
                em.SetComponentEnabled<MaterialMeshInfo>(bullet, true);
            }
        }

        private bool TryGetInactiveBullet(EntityManager em, out Entity bullet)
        {
            int tries = 0;
            while (tries++ < _pool.Length)
            {
                var e = _pool[_poolNext];
                _poolNext = (_poolNext + 1) % _pool.Length;

                if (!em.IsComponentEnabled<BulletActiveTag>(e))
                {
                    bullet = e;
                    return true;
                }
            }

            bullet = Entity.Null;
            return false;
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
        public partial struct BulletMoveJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter ECB;

            private void Execute([EntityIndexInQuery] int sortKey, Entity e,
                ref LocalTransform tx,
                ref BulletLifetimeComponent life,
                in BulletVelocityComponent vel,
                in BulletActiveTag _)
            {
                tx.Position += new float3(vel.Value.x, 0f, vel.Value.y) * DeltaTime;

                life.Value -= DeltaTime;
                if (life.Value <= 0f)
                {
                    // 시뮬레이션 off + 렌더 off
                    ECB.SetComponentEnabled<BulletActiveTag>(sortKey, e, false);
                    ECB.SetComponentEnabled<MaterialMeshInfo>(sortKey, e, false);
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

            public EntityCommandBuffer ECB;
            public NativeReference<long> Collected;

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
                                // 수거: 시뮬레이션 off + 렌더 off
                                ECB.SetComponentEnabled<BulletActiveTag>(bullet, false);
                                ECB.SetComponentEnabled<MaterialMeshInfo>(bullet, false);
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

                Collected.Value += collected;
            }
        }

        [BurstCompile]
        public struct ApplyCollectedToScoreJob : IJob
        {
            public Entity ScoreEntity;
            public ComponentLookup<ScoreComponent> ScoreLookup;
            [ReadOnly] public NativeReference<long> Collected;

            public void Execute()
            {
                long add = Collected.Value;
                if (add <= 0) return;

                var score = ScoreLookup[ScoreEntity];
                score.Value += add;
                ScoreLookup[ScoreEntity] = score;
            }
        }
    }
}
