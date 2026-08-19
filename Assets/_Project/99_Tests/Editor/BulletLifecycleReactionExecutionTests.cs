using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

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
        [TestCase(BulletLifecycleReasonId.PlayerHit)]
        public void ReactionOwner_NonTriggerReasons_DoNotAppendDiscreteRequest(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 7u);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, motionTargetProfileRefId: 200, cleanupTargetProfileRefId: 300, delaySec: 0f),
                CreateTargetRegistryEntry(200, bulletTypeKey: 44),
                CreateTargetRegistryEntry(300, bulletTypeKey: 45));
            var bullet = CreatePendingBullet(
                em,
                reason,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(3f, 5f),
                    DirectionXZ = math.normalizesafe(new float2(2f, 1f)),
                },
                active: true,
                despawnRequested: true,
                typeKey: 7,
                sourceRef: Entity.Null,
                addTransform: false,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel).Length, Is.EqualTo(0));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(reason));
        }

        [TestCase(BulletLifecycleReasonId.MotionCompleted)]
        [TestCase(BulletLifecycleReasonId.VacuumCollected)]
        [TestCase(BulletLifecycleReasonId.CarryFullRemoved)]
        public void ReactionOwner_TriggerReasonWithoutProfileReference_DoesNotAppendDiscreteRequest(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}_NoProfile");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 9u);
            var discreteChannel = CreateDiscreteChannel(em);
            CreatePendingBullet(
                em,
                reason,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(1f, 2f),
                    DirectionXZ = new float2(0f, 1f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel).Length, Is.EqualTo(0));
        }

        [Test]
        public void ReactionOwner_MotionCompletedWithProfileTrigger_AppendsDiscreteRequest()
        {
            using var world = new World("BulletLifecycleReaction_MotionCompletedProfileTrigger");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            SetFixedTickRuntime(em, 1f / 60f);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, motionTargetProfileRefId: 200, cleanupTargetProfileRefId: 0, delaySec: 0.10f),
                CreateTargetRegistryEntry(200, bulletTypeKey: 44));
            var source = em.CreateEntity();
            var bullet = CreatePendingBullet(
                em,
                BulletLifecycleReasonId.MotionCompleted,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(4f, 6f),
                    DirectionXZ = new float2(0f, 2f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: source,
                addTransform: true,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel);
            Assert.That(requests.Length, Is.EqualTo(1));
            AssertTriggeredRequest(requests[0], source, bullet, sourceProfileRefId: 100, targetProfileRefId: 200, bulletTypeKey: 44);
            Assert.That(requests[0].AnchorPosition, Is.EqualTo(new float3(4f, 0f, 6f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(requests[0].BaseAngleDeg, Is.EqualTo(100f).Within(1e-5f));
            Assert.That(requests[0].ReadyFrame, Is.EqualTo(20u));
        }

        [TestCase(BulletLifecycleReasonId.VacuumCollected)]
        [TestCase(BulletLifecycleReasonId.CarryFullRemoved)]
        public void ReactionOwner_CleanupRemovedWithProfileTrigger_AppendsDiscreteRequest(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}_CleanupProfileTrigger");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            SetFixedTickRuntime(em, 1f / 60f);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, motionTargetProfileRefId: 0, cleanupTargetProfileRefId: 300, delaySec: 0.10f),
                CreateTargetRegistryEntry(300, bulletTypeKey: 55));
            var source = em.CreateEntity();
            var bullet = CreatePendingBullet(
                em,
                reason,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(2f, 3f),
                    DirectionXZ = new float2(2f, 0f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 11,
                sourceRef: source,
                addTransform: true,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel);
            Assert.That(requests.Length, Is.EqualTo(1));
            AssertTriggeredRequest(requests[0], source, bullet, sourceProfileRefId: 100, targetProfileRefId: 300, bulletTypeKey: 55);
            Assert.That(requests[0].AnchorPosition, Is.EqualTo(new float3(2f, 0f, 3f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(requests[0].BaseAngleDeg, Is.EqualTo(10f).Within(1e-5f));
            Assert.That(requests[0].ReadyFrame, Is.EqualTo(20u));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(reason));
        }

        [TestCase(BulletLifecycleReasonId.MotionCompleted)]
        [TestCase(BulletLifecycleReasonId.VacuumCollected)]
        public void ReactionOwner_ProfileTriggerWithMissingTarget_DoesNotAppendDiscreteRequest(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}_MissingTarget");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, motionTargetProfileRefId: 200, cleanupTargetProfileRefId: 300, delaySec: 0f));
            CreatePendingBullet(
                em,
                reason,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(4f, 6f),
                    DirectionXZ = new float2(0f, 2f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel).Length, Is.EqualTo(0));
        }

        private static void AssertTriggeredRequest(
            in DiscreteEmitRequestBuffer request,
            Entity source,
            Entity bullet,
            int sourceProfileRefId,
            int targetProfileRefId,
            int bulletTypeKey)
        {
            Assert.That(request.ProducerKind, Is.EqualTo(DiscreteEmitProducerKind.TriggeredEmission));
            Assert.That(request.SourceEntity, Is.EqualTo(source));
            Assert.That(request.ProducerEntity, Is.EqualTo(bullet));
            Assert.That(request.CauserEntity, Is.EqualTo(bullet));
            Assert.That(request.EmissionId, Is.EqualTo(sourceProfileRefId));
            Assert.That(request.ProfileRefId, Is.EqualTo(targetProfileRefId));
            Assert.That(request.BulletTypeKey, Is.EqualTo(bulletTypeKey));
            Assert.That(request.RemainingRepeats, Is.EqualTo(1));
            Assert.That(request.OldestFrame, Is.EqualTo(14u));
        }

        private static void SetExecutionEndPrerequisites(EntityManager em, uint frame)
        {
            SetSingleton(em, new BulletFieldConfigComponent
            {
                PoolSize = 64,
                InvCellSize = 1f,
            });
            SetSingleton(em, new BulletFrameCounterComponent
            {
                Value = frame,
            });
            em.CreateEntity(typeof(PlayerTag));
        }

        private static void SetFixedTickRuntime(EntityManager em, float deltaTime)
        {
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = deltaTime,
                LogicDeltaTime = deltaTime,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
        }

        private static Entity CreateDiscreteChannel(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(DiscreteEmitChannelSingletonTag),
                typeof(DiscreteEmitPolicyComponent),
                typeof(DiscreteEmitBacklogMetricsComponent));
            em.SetComponentData(entity, new DiscreteEmitPolicyComponent
            {
                BudgetPerFrame = 8,
                MaxPendingCount = 32,
                MaxPendingAgeFrames = 120,
            });
            em.SetComponentData(entity, default(DiscreteEmitBacklogMetricsComponent));
            em.AddBuffer<DiscreteEmitRequestBuffer>(entity);
            return entity;
        }

        private static Entity CreateRegistry(EntityManager em, params EmissionProfileRuntimeRegistryBuffer[] entries)
        {
            var entity = em.CreateEntity(typeof(EmissionProfileRuntimeRegistryTag));
            var registry = em.AddBuffer<EmissionProfileRuntimeRegistryBuffer>(entity);
            for (int i = 0; i < entries.Length; i++)
                registry.Add(entries[i]);
            return entity;
        }

        private static EmissionProfileRuntimeRegistryBuffer CreateSourceRegistryEntry(
            int profileRefId,
            int motionTargetProfileRefId,
            int cleanupTargetProfileRefId,
            float delaySec)
        {
            return new EmissionProfileRuntimeRegistryBuffer
            {
                ProfileRefId = profileRefId,
                BulletTypeKey = 9,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                HasMotionCompletedTrigger = motionTargetProfileRefId != 0 ? (byte)1 : (byte)0,
                MotionCompletedTargetProfileRefId = motionTargetProfileRefId,
                MotionCompletedOriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition,
                MotionCompletedForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection,
                MotionCompletedSourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity,
                MotionCompletedCauserEntity = EmissionTriggerCauserBindingId.CompletedBullet,
                MotionCompletedDelaySec = delaySec,
                HasCleanupRemovedTrigger = cleanupTargetProfileRefId != 0 ? (byte)1 : (byte)0,
                CleanupRemovedTargetProfileRefId = cleanupTargetProfileRefId,
                CleanupRemovedOriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition,
                CleanupRemovedForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection,
                CleanupRemovedSourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity,
                CleanupRemovedCauserEntity = EmissionTriggerCauserBindingId.CompletedBullet,
                CleanupRemovedDelaySec = delaySec,
            };
        }

        private static EmissionProfileRuntimeRegistryBuffer CreateTargetRegistryEntry(int profileRefId, int bulletTypeKey)
        {
            return new EmissionProfileRuntimeRegistryBuffer
            {
                ProfileRefId = profileRefId,
                BulletTypeKey = bulletTypeKey,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 10f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
            };
        }

        private static Entity CreatePendingBullet(
            EntityManager em,
            BulletLifecycleReasonId reason,
            BulletLifecycleContactComponent contact,
            bool active,
            bool despawnRequested,
            int typeKey,
            Entity sourceRef,
            bool addTransform,
            int profileRefId = 0)
        {
            var entity = em.CreateEntity(
                typeof(BulletLifetimeComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletEmissionProfileRefComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            if (addTransform)
                em.AddComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 4f });
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = sourceRef });
            em.SetComponentData(entity, new BulletEmissionProfileRefComponent { ProfileRefId = profileRefId });
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
