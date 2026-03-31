using NUnit.Framework;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletHomingLiteMotionTests
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
        public void BulletSimulation_HomingLite_TurnsTowardPlayerWithinAngularCap_AndPreservesConfiguredSpeed()
        {
            using var world = new World("BulletSimulation_HomingLiteTurn");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 10f), quaternion.identity, 1f));

            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 3f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 45f,
                    MaxAcquireDistance = 20f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            float2 expectedDirection = math.normalize(new float2(1f, 1f));
            var velocity = em.GetComponentData<BulletVelocityComponent>(bullet).Value;
            Assert.That(velocity, Is.EqualTo(expectedDirection * 3f).Using(Float2Comparer.Within(1e-5f)));

            var tx = em.GetComponentData<LocalTransform>(bullet);
            Assert.That(tx.Position, Is.EqualTo(new float3(expectedDirection.x * 3f, 0f, expectedDirection.y * 3f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        [Test]
        public void BulletSimulation_HomingLite_FallsBackToStraight_WhenPlayerTooClose()
        {
            using var world = new World("BulletSimulation_HomingLiteTooClose");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(0.1f, 0f, 0f), quaternion.identity, 1f));

            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 180f,
                    MaxAcquireDistance = 10f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<LocalTransform>(bullet).Position, Is.EqualTo(new float3(1f, 0f, 0f)).Using(Float3Comparer.Within(1e-5f)));
        }

        [Test]
        public void BulletSimulation_HomingLite_FallsBackToStraight_WhenPlayerTooFar()
        {
            using var world = new World("BulletSimulation_HomingLiteTooFar");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(100f, 0f, 0f), quaternion.identity, 1f));

            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 180f,
                    MaxAcquireDistance = 10f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<LocalTransform>(bullet).Position, Is.EqualTo(new float3(1f, 0f, 0f)).Using(Float3Comparer.Within(1e-5f)));
        }

        [Test]
        public void BulletSimulation_HomingLite_FallsBackToStraight_WhenPlayerHasNoTransform()
        {
            using var world = new World("BulletSimulation_HomingLiteMissingPlayerTransform");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });

            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 180f,
                    MaxAcquireDistance = 10f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetComponentData<BulletVelocityComponent>(bullet).Value, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(em.GetComponentData<LocalTransform>(bullet).Position, Is.EqualTo(new float3(1f, 0f, 0f)).Using(Float3Comparer.Within(1e-5f)));
        }

        [Test]
        public void BulletSimulation_HomingLite_EmitsLifetimeExpired_WhenLifetimeRunsOut()
        {
            using var world = new World("BulletSimulation_HomingLiteLifetimeExpired");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 10f), quaternion.identity, 1f));

            var bullet = CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 0.25f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 45f,
                    MaxAcquireDistance = 20f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.LifetimeExpired));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.LifetimeExpired)));
        }

        [Test]
        public void BulletSimulation_HomingLite_EmitsStageBlocked_WhenEnteringBlockedCell()
        {
            using var world = new World("BulletSimulation_HomingLiteStageBlocked");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockBullet,
            }, width: 2, height: 1);
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(100f, 0f, 0f), quaternion.identity, 1f));

            var bullet = CreateBullet(
                em,
                new float3(0.25f, 0f, 0.5f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 90f,
                    MaxAcquireDistance = 10f,
                    MinRetargetDistance = 0.25f,
                });

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(BulletLifecycleReasonId.StageBlocked));
        }

        [Test]
        public void BulletSimulation_LinearDampedAndHomingBullets_CanUpdateInSameFrame()
        {
            using var world = new World("BulletSimulation_AllMovementFamilies");
            var em = world.EntityManager;

            var player = SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            em.AddComponentData(player, LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 10f), quaternion.identity, 1f));

            CreateBullet(
                em,
                new float3(0f, 0f, 0f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f);
            CreateBullet(
                em,
                new float3(0f, 0f, 1f),
                new float2(2f, 0f),
                speed: 2f,
                radius: 0.05f,
                lifetime: 5f,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 1f,
                    StopSpeedThreshold = 0.1f,
                });
            CreateBullet(
                em,
                new float3(0f, 0f, 2f),
                new float2(1f, 0f),
                speed: 1f,
                radius: 0.05f,
                lifetime: 5f,
                homingMotion: new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 45f,
                    MaxAcquireDistance = 20f,
                    MinRetargetDistance = 0.25f,
                });

            Assert.DoesNotThrow(() =>
            {
                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
            });
        }

        private static Entity SetSimulationPrerequisites(EntityManager em)
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
            return em.CreateEntity(typeof(PlayerTag));
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
            float speed,
            float radius,
            float lifetime,
            bool despawnRequested = false,
            BulletLifecycleReasonId existingReason = BulletLifecycleReasonId.None,
            BulletDampedMotionComponent? dampedMotion = null,
            BulletHomingLiteMotionComponent? homingMotion = null)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletSpeedComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));
            if (dampedMotion.HasValue)
                em.AddComponentData(entity, dampedMotion.Value);
            if (homingMotion.HasValue)
                em.AddComponentData(entity, homingMotion.Value);

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new BulletVelocityComponent { Value = velocity });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
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
