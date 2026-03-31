using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletDampedMotionTests
    {
        [SetUp]
        public void SetUp()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();
        }

        [TearDown]
        public void TearDown()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void BulletSimulation_DampedBullet_ReducesVelocityMagnitudeAfterOneFrame()
        {
            using var world = new World("BulletSimulation_DampedVelocityDecay");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(2f, 0f),
                radius: 0.05f,
                lifetime: 5f,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 1f,
                    StopSpeedThreshold = 0.1f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            float expectedSpeed = 2f * math.exp(-1f);
            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value.x, Is.EqualTo(expectedSpeed).Within(1e-5f));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        [Test]
        public void BulletSimulation_LinearAndDampedBullets_CanUpdateInSameFrame()
        {
            using var world = new World("BulletSimulation_LinearAndDampedMixed");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });

            var linearBullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                radius: 0.05f,
                lifetime: 5f);
            var dampedBullet = CreateBullet(
                em,
                new float3(0f, 0f, 1f),
                new float2(2f, 0f),
                radius: 0.05f,
                lifetime: 5f,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 1f,
                    StopSpeedThreshold = 0.1f,
                });

            Assert.DoesNotThrow(() =>
            {
                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
            });

            Assert.That(em.GetComponentData<BulletVelocityComponent>(linearBullet).Value, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<BulletVelocityComponent>(dampedBullet).Value.x, Is.LessThan(2f));
        }

        [Test]
        public void BulletSimulation_DampedBullet_EmitsMotionCompleted_AndClampsVelocityToZero()
        {
            using var world = new World("BulletSimulation_MotionCompleted");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            var bullet = CreateBullet(
                em,
                new float3(1f, 0f, 2f),
                new float2(0.5f, 0f),
                radius: 0.05f,
                lifetime: 5f,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 100f,
                    StopSpeedThreshold = 0.1f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value, Is.EqualTo(float2.zero));

            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.MotionCompleted));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.MotionCompleted)));

            var contact = em.GetComponentData<BulletLifecycleContactComponent>(bullet);
            Assert.That(contact.PositionXZ, Is.EqualTo(new float2(1.5f, 2f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(contact.DirectionXZ, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
        }

        [Test]
        public void BulletSimulation_DampedMotionCompleted_DoesNotOverrideHigherPriorityRequest()
        {
            using var world = new World("BulletSimulation_DampedMotionPriority");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(0.5f, 0f),
                radius: 0.05f,
                lifetime: 5f,
                despawnRequested: true,
                existingReason: BulletLifecycleReasonId.PlayerHit,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 100f,
                    StopSpeedThreshold = 0.1f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.PlayerHit));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.PlayerHit)));
        }

        [Test]
        public void BulletSimulation_LinearBullet_RemainsOnLinearPathWithoutMotionCompleted()
        {
            using var world = new World("BulletSimulation_LinearRegression");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            var bullet = CreateBullet(
                em,
                new float3(0.25f, 0f, 0.5f),
                new float2(1f, 0f),
                radius: 0.05f,
                lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var tx = em.GetComponentData<LocalTransform>(bullet);
            Assert.That(tx.Position, Is.EqualTo(new float3(1.25f, 0f, 0.5f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        private static void SetSimulationPrerequisites(EntityManager em)
        {
            SetSingleton(em, new BulletFieldConfigComponent
            {
                PoolSize = 64,
                InvCellSize = 1f,
            });
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f,
                LogicDeltaTime = 1f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            em.CreateEntity(typeof(PlayerTag));
        }

        private static void SetGameplayReadySingletons(EntityManager em)
        {
            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 1,
                Ready = 1,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
            });
        }

        private static void SetRuntimeGrid(EntityManager em, StageCellMovementFlags[] flags, int width = 1, int height = 1)
        {
            var entity = em.CreateEntity(typeof(StageRuntimeGridComponent));
            em.SetComponentData(entity, new StageRuntimeGridComponent
            {
                StageId = 1,
                Width = width,
                Height = height,
                CellSize = 1f,
                OriginX = 0f,
                OriginZ = 0f,
                Ready = 1,
            });

            var buffer = em.AddBuffer<StageRuntimeGridCellBufferElement>(entity);
            for (int i = 0; i < flags.Length; i++)
            {
                buffer.Add(new StageRuntimeGridCellBufferElement
                {
                    MovementFlags = flags[i],
                    DepositRegionId = 0u,
                });
            }
        }

        private static Entity CreateBullet(
            EntityManager em,
            float3 position,
            float2 velocity,
            float radius,
            float lifetime,
            bool despawnRequested = false,
            BulletLifecycleReasonId existingReason = BulletLifecycleReasonId.None,
            BulletDampedMotionComponent? dampedMotion = null)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));
            if (dampedMotion.HasValue)
                em.AddComponentData(entity, dampedMotion.Value);

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new BulletVelocityComponent { Value = velocity });
            em.SetComponentData(entity, new BulletRadiusComponent { Value = radius });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = lifetime });
            byte priority = despawnRequested
                ? BulletLifecycleRequestUtility.ResolvePriority(existingReason)
                : (byte)0;
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = despawnRequested ? existingReason : BulletLifecycleReasonId.None,
                Priority = priority,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentEnabled<BulletActiveTag>(entity, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, despawnRequested);
            return entity;
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

        private static void SetSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }

        private sealed class Float2Comparer : IEqualityComparer<float2>
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

        private sealed class Float3Comparer : IEqualityComparer<float3>
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
