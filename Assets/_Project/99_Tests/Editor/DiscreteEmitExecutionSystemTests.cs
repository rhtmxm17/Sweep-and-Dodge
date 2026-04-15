using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DiscreteEmitExecutionSystemTests
    {
        [SetUp]
        public void SetUp() => ForceDisposeSharedContainersIfNeeded();

        [TearDown]
        public void TearDown() => ForceDisposeSharedContainersIfNeeded();

        [Test]
        public void DiscreteEmitExecution_SingleItemConsume_CompletesAndUpdatesMetrics()
        {
            using var world = new World("DiscreteEmit_SingleConsume");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 10u);
            CreateFixedTickRuntime(em, 1f / 60f);
            var channel = CreateDiscreteChannel(em, 4, 16, 60u);
            var source = CreateSourceWithActiveCountBuffer(em, 17);
            var pooledBullet = CreatePooledBullet(em, 17, 5f, 7f);
            BulletFieldShared.FreeByKey.Add(17, pooledBullet);

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
            requests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = source,
                ProducerEntity = source,
                EmissionId = 1,
                BulletTypeKey = 17,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = source,
                AnchorPosition = new float3(2f, 0f, 3f),
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 90f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 1,
            }, 10u));

            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(1));

            var metrics = em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel);
            Assert.That(metrics.PendingCount, Is.EqualTo(0));
            Assert.That(metrics.LastFrameBudgetUsed, Is.EqualTo(1));
        }

        [Test]
        public void DiscreteEmitExecution_TimedSchedule_FirstRepeatImmediate_ThenWaitsForInterval()
        {
            using var world = new World("DiscreteEmit_Timed");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 11u);
            CreateFixedTickRuntime(em, 0.5f);
            var channel = CreateDiscreteChannel(em, 4, 16, 60u);
            var source = CreateSourceWithActiveCountBuffer(em, 21);
            var bulletA = CreatePooledBullet(em, 21, 4f, 5f);
            var bulletB = CreatePooledBullet(em, 21, 4f, 5f);
            BulletFieldShared.FreeByKey.Add(21, bulletA);
            BulletFieldShared.FreeByKey.Add(21, bulletB);

            em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = source,
                ProducerEntity = source,
                EmissionId = 2,
                BulletTypeKey = 21,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = source,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                EventShotIntervalSec = 1f,
                RepeatCount = 2,
            }, 11u));

            var system = world.GetOrCreateSystem<DiscreteEmitExecutionSystem>();
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].RemainingRepeats, Is.EqualTo(1));
            Assert.That(requests[0].RepeatSequence, Is.EqualTo(1u));

            CreateFrameCounter(em, 12u);
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].RemainingRepeats, Is.EqualTo(1));

            CreateFrameCounter(em, 13u);
            system.Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
        }

        [Test]
        public void DiscreteEmitExecution_PriorityDesc_WinsWhenBudgetAllowsSingleRepeat()
        {
            using var world = new World("DiscreteEmit_Priority");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 20u);
            CreateFixedTickRuntime(em, 1f / 60f);
            var channel = CreateDiscreteChannel(em, 1, 16, 60u);
            var lowSource = CreateSourceWithActiveCountBuffer(em, 31);
            var highSource = CreateSourceWithActiveCountBuffer(em, 31);
            BulletFieldShared.FreeByKey.Add(31, CreatePooledBullet(em, 31, 2f, 4f));

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
            requests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = lowSource,
                ProducerEntity = lowSource,
                EmissionId = 3,
                BulletTypeKey = 31,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = lowSource,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 1,
                Priority = 1,
            }, 10u));
            requests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = highSource,
                ProducerEntity = highSource,
                EmissionId = 4,
                BulletTypeKey = 31,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = highSource,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 1,
                Priority = 5,
            }, 9u));

            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(highSource)[0].ActiveCount, Is.EqualTo(1));
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(lowSource)[0].ActiveCount, Is.EqualTo(0));
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(1));
        }

        [Test]
        public void DiscreteEmitExecution_NWayRepeat_IsAtomicForBudgetAndPoolDefers()
        {
            using var world = new World("DiscreteEmit_Atomic");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 30u);
            CreateFixedTickRuntime(em, 1f / 60f);
            var channel = CreateDiscreteChannel(em, 2, 16, 60u);
            var source = CreateSourceWithActiveCountBuffer(em, 41);
            for (int i = 0; i < 3; i++)
                BulletFieldShared.FreeByKey.Add(41, CreatePooledBullet(em, 41, 2f, 4f));

            em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = source,
                ProducerEntity = source,
                EmissionId = 5,
                BulletTypeKey = 41,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = source,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.NWay,
                ShotCount = 3,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 1,
            }, 30u));

            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var metrics = em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel);
            Assert.That(metrics.DeferredByBudget, Is.EqualTo(3));
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(0));

            em.SetComponentData(channel, new DiscreteEmitPolicyComponent { BudgetPerFrame = 4, MaxPendingCount = 16, MaxPendingAgeFrames = 60u });
            CreateFrameCounter(em, 31u);
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();
            for (int i = 0; i < 2; i++)
                BulletFieldShared.FreeByKey.Add(41, CreatePooledBullet(em, 41, 2f, 4f));

            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            metrics = em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel);
            Assert.That(metrics.DeferredByPool, Is.EqualTo(3));
            Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void DiscreteEmitExecution_OverflowTailDrop_UsesBulletEquivalent()
        {
            using var world = new World("DiscreteEmit_Overflow");
            var em = world.EntityManager;

            InitializeSharedContainers();
            CreateFrameCounter(em, 40u);
            CreateFixedTickRuntime(em, 1f / 60f);
            var channel = CreateDiscreteChannel(em, 0, 3, 60u);

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);
            requests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = Entity.Null,
                ProducerEntity = Entity.Null,
                EmissionId = 6,
                BulletTypeKey = 51,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 3,
            }, 40u));
            requests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = Entity.Null,
                ProducerEntity = Entity.Null,
                EmissionId = 7,
                BulletTypeKey = 52,
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorPosition = float3.zero,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                RepeatCount = 3,
            }, 40u));

            world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var metrics = em.GetComponentData<DiscreteEmitBacklogMetricsComponent>(channel);
            Assert.That(metrics.LastFrameDroppedByCapacity, Is.EqualTo(3));
            Assert.That(metrics.PendingCount, Is.EqualTo(3));
            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(1));
        }

        private static void CreateFrameCounter(EntityManager em, uint frame)
        {
            var entity = GetOrCreateSingletonEntity<BulletFrameCounterComponent>(em);
            em.SetComponentData(entity, new BulletFrameCounterComponent { Value = frame });
        }

        private static void CreateFixedTickRuntime(EntityManager em, float deltaTime)
        {
            var entity = GetOrCreateSingletonEntity<FixedTickStepRuntimeComponent>(em);
            em.SetComponentData(entity, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
        }

        private static Entity CreateDiscreteChannel(EntityManager em, int budgetPerFrame, int maxPendingCount, uint maxPendingAgeFrames)
        {
            var entity = GetOrCreateSingletonEntity<DiscreteEmitChannelSingletonTag>(em);
            if (!em.HasBuffer<DiscreteEmitRequestBuffer>(entity))
                em.AddBuffer<DiscreteEmitRequestBuffer>(entity);
            if (!em.HasComponent<DiscreteEmitPolicyComponent>(entity))
                em.AddComponentData(entity, new DiscreteEmitPolicyComponent());
            if (!em.HasComponent<DiscreteEmitBacklogMetricsComponent>(entity))
                em.AddComponentData(entity, default(DiscreteEmitBacklogMetricsComponent));

            em.SetComponentData(entity, new DiscreteEmitPolicyComponent
            {
                BudgetPerFrame = budgetPerFrame,
                MaxPendingCount = maxPendingCount,
                MaxPendingAgeFrames = maxPendingAgeFrames,
            });
            em.SetComponentData(entity, default(DiscreteEmitBacklogMetricsComponent));
            return entity;
        }

        private static Entity CreateSourceWithActiveCountBuffer(EntityManager em, int bulletTypeKey)
        {
            var entity = em.CreateEntity();
            var counts = em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            counts.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = bulletTypeKey,
                ActiveCount = 0,
            });
            return entity;
        }

        private static Entity CreatePooledBullet(EntityManager em, int bulletTypeKey, float speed, float lifetime)
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

            em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, default(BulletLifecycleRequestComponent));
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = bulletTypeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, default(BulletLifecycleTraceComponent));
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            return query.GetSingletonEntity();
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
    }
}
