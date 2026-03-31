using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletLifecycleReactionExecutionTests
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

        [TestCase(BulletLifecycleReasonId.LifetimeExpired)]
        [TestCase(BulletLifecycleReasonId.StageBlocked)]
        [TestCase(BulletLifecycleReasonId.VacuumCollected)]
        [TestCase(BulletLifecycleReasonId.CarryFullRemoved)]
        [TestCase(BulletLifecycleReasonId.PlayerHit)]
        [TestCase(BulletLifecycleReasonId.MotionCompleted)]
        public void ReactionOwner_LeavesPendingLifecycleRequestUntouched(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em);
            var contact = new BulletLifecycleContactComponent
            {
                PositionXZ = new float2(3f, 5f),
                DirectionXZ = math.normalizesafe(new float2(2f, 1f)),
            };
            var bullet = CreatePendingBullet(em, reason, contact, active: true, despawnRequested: true, typeKey: 7);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);

            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(reason));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(reason)));

            var actualContact = em.GetComponentData<BulletLifecycleContactComponent>(bullet);
            Assert.That(actualContact.PositionXZ, Is.EqualTo(contact.PositionXZ).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(actualContact.DirectionXZ, Is.EqualTo(contact.DirectionXZ).Using(Float2Comparer.Within(1e-5f)));
        }

        [Test]
        public void ReactionOwner_FollowedByDespawnOwner_KeepsTerminalFlowClosed()
        {
            using var world = new World("BulletLifecycleReaction_DespawnFlow");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em);
            var bullet = CreatePendingBullet(
                em,
                BulletLifecycleReasonId.MotionCompleted,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(1f, 2f),
                    DirectionXZ = new float2(0f, 1f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<BulletDespawnExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.False);
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
            Assert.That(em.GetComponentData<BulletLifetimeComponent>(bullet).Value, Is.EqualTo(0f));
            Assert.That(BulletFieldShared.FreeByKey.TryGetFirstValue(9, out var pooled, out var iterator), Is.True);
            Assert.That(pooled, Is.EqualTo(bullet));
        }

        private static void SetExecutionEndPrerequisites(EntityManager em)
        {
            SetSingleton(em, new BulletFieldConfigComponent
            {
                PoolSize = 64,
                InvCellSize = 1f,
            });
            SetSingleton(em, new BulletFrameCounterComponent
            {
                Value = 1u,
            });
            em.CreateEntity(typeof(PlayerTag));
        }

        private static Entity CreatePendingBullet(
            EntityManager em,
            BulletLifecycleReasonId reason,
            BulletLifecycleContactComponent contact,
            bool active,
            bool despawnRequested,
            int typeKey)
        {
            var entity = em.CreateEntity(
                typeof(BulletLifetimeComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 4f });
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = reason,
                Priority = BulletLifecycleRequestUtility.ResolvePriority(reason),
                RelatedEntity = Entity.Null,
                Frame = 1u,
            });
            em.SetComponentData(entity, contact);
            em.SetComponentEnabled<BulletActiveTag>(entity, active);
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
    }
}
