using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class SecondarySpawnExecutionTests
    {
        [SetUp]
        public void SetUp()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [TearDown]
        public void TearDown()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void Bootstrap_CreatesSecondarySpawnSingletons()
        {
            using var world = new World("SecondarySpawn_Bootstrap");
            var em = world.EntityManager;

            var configEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(MetaScrapComponent));
            em.SetComponentData(configEntity, new BulletFieldConfigComponent
            {
                PoolSize = 16,
                InvCellSize = 1f,
            });
            em.SetComponentData(configEntity, new MetaScrapComponent { Value = 0 });

            var registry = em.CreateEntity(typeof(BulletPoolRegistryTag));
            em.AddBuffer<BulletPoolDefinitionBuffer>(registry);
            em.CreateEntity(typeof(PlayerTag));

            world.GetOrCreateSystem<BulletPoolOwnerBootstrapSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var channelEntity = em.CreateEntityQuery(ComponentType.ReadOnly<BulletSecondarySpawnChannelSingletonTag>())
                .GetSingletonEntity();
            Assert.That(em.HasComponent<SecondarySpawnPolicyComponent>(channelEntity), Is.True);
            Assert.That(em.HasComponent<SecondarySpawnBacklogMetricsComponent>(channelEntity), Is.True);
            Assert.That(em.HasBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity), Is.True);
        }

        [Test]
        public void SecondarySpawnExecution_NoRequests_LeavesMetricsAtZero()
        {
            using var world = new World("SecondarySpawn_NoRequests");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 10u);
            CreateSecondaryChannel(em, budgetPerFrame: 4, maxPendingCount: 16, maxPendingAgeFrames: 30);

            world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<SecondarySpawnBacklogMetricsComponent>())
                .GetSingleton<SecondarySpawnBacklogMetricsComponent>();
            Assert.That(metrics.PendingCount, Is.EqualTo(0));
            Assert.That(metrics.LastFrameBudgetUsed, Is.EqualTo(0));
            Assert.That(metrics.DeferredByBudget, Is.EqualTo(0));
            Assert.That(metrics.DeferredByPool, Is.EqualTo(0));
        }

        [Test]
        public void SecondarySpawnExecution_SingleForward_SpawnsAndResetsLifecycleState()
        {
            using var world = new World("SecondarySpawn_SingleForward");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 11u);
            var channelEntity = CreateSecondaryChannel(em, budgetPerFrame: 4, maxPendingCount: 16, maxPendingAgeFrames: 30);
            var source = CreateSourceWithActiveCountBuffer(em, 17);
            var pooledBullet = CreatePooledBullet(em, 17, 5f, 7f);
            BulletFieldShared.FreeByKey.Add(17, pooledBullet);

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            requests.Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = 17,
                Count = 1,
                Priority = 10,
                SourceEntity = source,
                CauserEntity = Entity.Null,
                OriginPosition = new float3(3f, 0f, 4f),
                BaseDirection = new float2(0f, 1f),
                SpreadAngleDeg = 0f,
                SpawnRadius = 0.5f,
                Shape = BulletSecondarySpawnShapeId.SingleForward,
                OldestFrame = 11u,
                Sequence = 0u,
            });

            world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(pooledBullet), Is.False);
            Assert.That(em.GetComponentData<BulletSourceRefComponent>(pooledBullet).Value, Is.EqualTo(source));
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(pooledBullet).Reason, Is.EqualTo(BulletLifecycleReasonId.None));
            Assert.That(em.GetComponentData<BulletLifecycleContactComponent>(pooledBullet).PositionXZ, Is.EqualTo(float2.zero));
            Assert.That(em.GetComponentData<BulletVelocityComponent>(pooledBullet).Value, Is.EqualTo(new float2(0f, 5f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<BulletLifetimeComponent>(pooledBullet).Value, Is.EqualTo(7f));

            var transform = em.GetComponentData<LocalTransform>(pooledBullet);
            Assert.That(transform.Position, Is.EqualTo(new float3(3f, 0f, 4.5f)).Using(Float3Comparer.Within(1e-5f)));

            var activeCounts = em.GetBuffer<SourceActiveBulletCountBuffer>(source);
            Assert.That(activeCounts[0].ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void SecondarySpawnExecution_ForwardSpread_AndPointBurst_ProduceExpectedDistribution()
        {
            using var world = new World("SecondarySpawn_Shapes");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 12u);
            var channelEntity = CreateSecondaryChannel(em, budgetPerFrame: 8, maxPendingCount: 16, maxPendingAgeFrames: 30);
            var bullets = new NativeArray<Entity>(6, Allocator.Temp);
            try
            {
                for (int i = 0; i < bullets.Length; i++)
                {
                    bullets[i] = CreatePooledBullet(em, 21, 4f, 6f);
                    BulletFieldShared.FreeByKey.Add(21, bullets[i]);
                }

                var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
                requests.Add(new BulletSecondarySpawnRequestBuffer
                {
                    BulletTypeKey = 21,
                    Count = 3,
                    Priority = 0,
                    SourceEntity = Entity.Null,
                    CauserEntity = Entity.Null,
                    OriginPosition = new float3(0f, 0f, 0f),
                    BaseDirection = new float2(1f, 0f),
                    SpreadAngleDeg = 60f,
                    SpawnRadius = 1f,
                    Shape = BulletSecondarySpawnShapeId.ForwardSpread,
                    OldestFrame = 12u,
                    Sequence = 0u,
                });
                requests.Add(new BulletSecondarySpawnRequestBuffer
                {
                    BulletTypeKey = 21,
                    Count = 3,
                    Priority = 0,
                    SourceEntity = Entity.Null,
                    CauserEntity = Entity.Null,
                    OriginPosition = new float3(10f, 0f, 0f),
                    BaseDirection = new float2(0f, 1f),
                    SpreadAngleDeg = 0f,
                    SpawnRadius = 2f,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    OldestFrame = 12u,
                    Sequence = 0u,
                });

                world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(GetActiveBulletCount(em, bullets), Is.EqualTo(6));

                var forwardDirs = new NativeList<float2>(Allocator.Temp);
                var burstPositions = new NativeList<float3>(Allocator.Temp);
                try
                {
                    for (int i = 0; i < bullets.Length; i++)
                    {
                        var position = em.GetComponentData<LocalTransform>(bullets[i]).Position;
                        if (position.x < 5f)
                            forwardDirs.Add(em.GetComponentData<BulletVelocityComponent>(bullets[i]).Value / 4f);
                        else
                            burstPositions.Add(position);
                    }

                    Assert.That(forwardDirs.Length, Is.EqualTo(3));
                    Assert.That(burstPositions.Length, Is.EqualTo(3));

                    bool hasCenter = false;
                    float minY = float.MaxValue;
                    float maxY = float.MinValue;
                    for (int i = 0; i < forwardDirs.Length; i++)
                    {
                        minY = math.min(minY, forwardDirs[i].y);
                        maxY = math.max(maxY, forwardDirs[i].y);
                        if (math.abs(forwardDirs[i].y) <= 1e-5f)
                            hasCenter = true;
                    }

                    Assert.That(hasCenter, Is.True);
                    Assert.That(minY, Is.LessThan(-0.4f));
                    Assert.That(maxY, Is.GreaterThan(0.4f));

                    for (int i = 0; i < burstPositions.Length; i++)
                    {
                        float2 delta = new float2(burstPositions[i].x - 10f, burstPositions[i].z);
                        Assert.That(math.length(delta), Is.EqualTo(2f).Within(1e-4f));
                    }
                }
                finally
                {
                    forwardDirs.Dispose();
                    burstPositions.Dispose();
                }
            }
            finally
            {
                bullets.Dispose();
            }
        }

        [Test]
        public void SecondarySpawnExecution_BudgetLimit_DefersRemainingRequests()
        {
            using var world = new World("SecondarySpawn_Budget");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 13u);
            var channelEntity = CreateSecondaryChannel(em, budgetPerFrame: 2, maxPendingCount: 16, maxPendingAgeFrames: 30);
            for (int i = 0; i < 4; i++)
            {
                var bullet = CreatePooledBullet(em, 33, 2f, 5f);
                BulletFieldShared.FreeByKey.Add(33, bullet);
            }

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            requests.Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = 33,
                Count = 4,
                Priority = 0,
                SourceEntity = Entity.Null,
                CauserEntity = Entity.Null,
                OriginPosition = float3.zero,
                BaseDirection = new float2(1f, 0f),
                SpreadAngleDeg = 0f,
                SpawnRadius = 0f,
                Shape = BulletSecondarySpawnShapeId.SingleForward,
                OldestFrame = 13u,
                Sequence = 0u,
            });

            world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<SecondarySpawnBacklogMetricsComponent>())
                .GetSingleton<SecondarySpawnBacklogMetricsComponent>();
            var remaining = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);

            Assert.That(metrics.LastFrameBudgetUsed, Is.EqualTo(2));
            Assert.That(metrics.PendingCount, Is.EqualTo(2));
            Assert.That(metrics.DeferredByBudget, Is.EqualTo(2));
            Assert.That(remaining.Length, Is.EqualTo(1));
            Assert.That(remaining[0].Count, Is.EqualTo(2));
            Assert.That(remaining[0].Sequence, Is.EqualTo(2u));
        }

        [Test]
        public void SecondarySpawnExecution_PrunesExpiredRequests()
        {
            using var world = new World("SecondarySpawn_AgePrune");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 30u);
            var channelEntity = CreateSecondaryChannel(em, budgetPerFrame: 4, maxPendingCount: 16, maxPendingAgeFrames: 3);

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            requests.Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = 41,
                Count = 3,
                Priority = 0,
                SourceEntity = Entity.Null,
                CauserEntity = Entity.Null,
                OriginPosition = float3.zero,
                BaseDirection = new float2(1f, 0f),
                SpreadAngleDeg = 0f,
                SpawnRadius = 0f,
                Shape = BulletSecondarySpawnShapeId.SingleForward,
                OldestFrame = 20u,
                Sequence = 0u,
            });

            world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var metrics = em.CreateEntityQuery(ComponentType.ReadOnly<SecondarySpawnBacklogMetricsComponent>())
                .GetSingleton<SecondarySpawnBacklogMetricsComponent>();
            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Length, Is.EqualTo(0));
            Assert.That(metrics.ExpiredByAge, Is.EqualTo(3));
            Assert.That(metrics.LastFrameExpiredByAge, Is.EqualTo(3));
            Assert.That(metrics.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void SecondarySpawnExecution_DoesNotTouchSourceSpawnBacklog()
        {
            using var world = new World("SecondarySpawn_SourceIsolation");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 15u);
            var channelEntity = CreateSecondaryChannel(em, budgetPerFrame: 4, maxPendingCount: 16, maxPendingAgeFrames: 30);
            var sourceMetricsEntity = em.CreateEntity(typeof(SpawnBacklogMetricsComponent));
            em.SetComponentData(sourceMetricsEntity, default(SpawnBacklogMetricsComponent));

            var source = em.CreateEntity(typeof(SourceSpawnComponent));
            em.AddBuffer<SourceSpawnRequestBuffer>(source);
            var sourceRequests = em.GetBuffer<SourceSpawnRequestBuffer>(source);
            sourceRequests.Add(new SourceSpawnRequestBuffer
            {
                DirectiveId = 99,
                BulletTypeKey = 55,
                Count = 2,
                OldestFrame = 15u,
            });

            var pooledBullet = CreatePooledBullet(em, 55, 3f, 4f);
            BulletFieldShared.FreeByKey.Add(55, pooledBullet);
            em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = 55,
                Count = 1,
                Priority = 0,
                SourceEntity = Entity.Null,
                CauserEntity = Entity.Null,
                OriginPosition = float3.zero,
                BaseDirection = new float2(1f, 0f),
                SpreadAngleDeg = 0f,
                SpawnRadius = 0f,
                Shape = BulletSecondarySpawnShapeId.SingleForward,
                OldestFrame = 15u,
                Sequence = 0u,
            });

            world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(source).Length, Is.EqualTo(1));
            Assert.That(em.GetBuffer<SourceSpawnRequestBuffer>(source)[0].Count, Is.EqualTo(2));
            Assert.That(em.GetComponentData<SpawnBacklogMetricsComponent>(sourceMetricsEntity).PendingCount, Is.EqualTo(0));
        }

        private static void CreateFrameCounter(EntityManager em, uint frame)
        {
            var entity = em.CreateEntity(typeof(BulletFrameCounterComponent));
            em.SetComponentData(entity, new BulletFrameCounterComponent { Value = frame });
        }

        private static Entity CreateSecondaryChannel(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var entity = em.CreateEntity(
                typeof(BulletSecondarySpawnChannelSingletonTag),
                typeof(SecondarySpawnPolicyComponent),
                typeof(SecondarySpawnBacklogMetricsComponent));
            em.SetComponentData(entity, new SecondarySpawnPolicyComponent
            {
                BudgetPerFrame = budgetPerFrame,
                MaxPendingCount = maxPendingCount,
                MaxPendingAgeFrames = maxPendingAgeFrames,
            });
            em.SetComponentData(entity, default(SecondarySpawnBacklogMetricsComponent));
            em.AddBuffer<BulletSecondarySpawnRequestBuffer>(entity);
            return entity;
        }

        private static Entity CreateSourceWithActiveCountBuffer(EntityManager em, int typeKey)
        {
            var source = em.CreateEntity();
            var activeCounts = em.AddBuffer<SourceActiveBulletCountBuffer>(source);
            activeCounts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = typeKey,
                ActiveCount = 0,
            });
            return source;
        }

        private static Entity CreatePooledBullet(EntityManager em, int typeKey, float speed, float lifetime)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletSpeedComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.PlayerHit,
                Priority = BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.PlayerHit),
                RelatedEntity = entity,
                Frame = 99u,
            });
            em.SetComponentData(entity, new BulletLifecycleContactComponent
            {
                PositionXZ = new float2(9f, 9f),
                DirectionXZ = new float2(1f, 1f),
            });
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = entity });
            em.SetComponentData(entity, new BulletLifecycleTraceComponent { LastSpawnFrame = 0u, LastDespawnFrame = 0u });
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, true);
            return entity;
        }

        private static int GetActiveBulletCount(EntityManager em, NativeArray<Entity> bullets)
        {
            int count = 0;
            for (int i = 0; i < bullets.Length; i++)
            {
                if (em.IsComponentEnabled<BulletActiveTag>(bullets[i]))
                    count++;
            }

            return count;
        }

        private static void InitializeSharedContainers(int capacity = 128)
        {
            BulletFieldShared.FreeByKey = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.CellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.HazardCellMap = new NativeParallelMultiHashMap<int, Entity>(capacity, Allocator.Persistent);
            BulletFieldShared.PoolFence = default;
            BulletFieldShared.CellMapFence = default;
            BulletFieldShared.MarkInitialized();
        }

        private static void ForceDisposeSharedContainersIfNeeded()
        {
            if (!BulletFieldShared.IsInitialized)
                return;

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

        private sealed class Float2Comparer : System.Collections.Generic.IEqualityComparer<float2>
        {
            private readonly float _tolerance;

            private Float2Comparer(float tolerance)
            {
                _tolerance = tolerance;
            }

            public static Float2Comparer Within(float tolerance) => new(tolerance);

            public bool Equals(float2 x, float2 y) => math.all(math.abs(x - y) <= _tolerance);

            public int GetHashCode(float2 obj) => obj.GetHashCode();
        }

        private sealed class Float3Comparer : System.Collections.Generic.IEqualityComparer<float3>
        {
            private readonly float _tolerance;

            private Float3Comparer(float tolerance)
            {
                _tolerance = tolerance;
            }

            public static Float3Comparer Within(float tolerance) => new(tolerance);

            public bool Equals(float3 x, float3 y) => math.all(math.abs(x - y) <= _tolerance);

            public int GetHashCode(float3 obj) => obj.GetHashCode();
        }
    }
}
