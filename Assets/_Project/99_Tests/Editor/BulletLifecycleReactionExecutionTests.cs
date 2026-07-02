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
        [TestCase(BulletLifecycleReasonId.VacuumCollected)]
        [TestCase(BulletLifecycleReasonId.CarryFullRemoved)]
        [TestCase(BulletLifecycleReasonId.PlayerHit)]
        public void ReactionOwner_NonMotionCompletedReasons_LeaveLifecycleRequestUntouched(BulletLifecycleReasonId reason)
        {
            using var world = new World($"BulletLifecycleReaction_{reason}");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 7u);
            CreateSecondaryChannel(em);
            var contact = new BulletLifecycleContactComponent
            {
                PositionXZ = new float2(3f, 5f),
                DirectionXZ = math.normalizesafe(new float2(2f, 1f)),
            };
            var bullet = CreatePendingBullet(
                em,
                reason,
                contact,
                active: true,
                despawnRequested: true,
                typeKey: 7,
                sourceRef: Entity.Null,
                addTransform: false,
                explodeReaction: null,
                collectReaction: null);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(reason));
            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(
                em.CreateEntityQuery(ComponentType.ReadOnly<BulletSecondarySpawnChannelSingletonTag>()).GetSingletonEntity()).Length, Is.EqualTo(0));
        }

        [Test]
        public void ReactionOwner_MotionCompletedWithoutReaction_DoesNotAppendSecondarySpawn()
        {
            using var world = new World("BulletLifecycleReaction_MotionCompletedNoReaction");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 9u);
            var channelEntity = CreateSecondaryChannel(em);
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
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true,
                explodeReaction: null,
                collectReaction: null);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Length, Is.EqualTo(0));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
        }

        [TestCase(-1, 2)]
        [TestCase(5, 0)]
        public void ReactionOwner_InvalidExplodeConfig_DoesNotAppendSecondarySpawn(int secondaryBulletTypeKey, int spawnCount)
        {
            using var world = new World("BulletLifecycleReaction_InvalidExplode");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 9u);
            var channelEntity = CreateSecondaryChannel(em);
            CreatePendingBullet(
                em,
                BulletLifecycleReasonId.MotionCompleted,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(1f, 2f),
                    DirectionXZ = new float2(0f, 1f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true,
                explodeReaction: new BulletOnMotionCompletedExplodeReactionComponent
                {
                    SecondaryBulletTypeKey = secondaryBulletTypeKey,
                    SpawnCount = spawnCount,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    SpreadAngleDeg = 45f,
                    SpawnRadius = 1f,
                    SpawnDelaySec = 0f,
                },
                collectReaction: null);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Length, Is.EqualTo(0));
        }

        [Test]
        public void ReactionOwner_VacuumCollectedWithoutReaction_DoesNotAppendSecondarySpawn()
        {
            using var world = new World("BulletLifecycleReaction_VacuumCollectedNoReaction");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 10u);
            var channelEntity = CreateSecondaryChannel(em);
            var bullet = CreatePendingBullet(
                em,
                BulletLifecycleReasonId.VacuumCollected,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(1f, 2f),
                    DirectionXZ = new float2(0f, 1f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true,
                explodeReaction: null,
                collectReaction: null);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Length, Is.EqualTo(0));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
        }

        [TestCase(-1, 2)]
        [TestCase(5, 0)]
        public void ReactionOwner_InvalidCollectConfig_DoesNotAppendSecondarySpawn(int secondaryBulletTypeKey, int spawnCount)
        {
            using var world = new World("BulletLifecycleReaction_InvalidCollect");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 10u);
            var channelEntity = CreateSecondaryChannel(em);
            CreatePendingBullet(
                em,
                BulletLifecycleReasonId.VacuumCollected,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(1f, 2f),
                    DirectionXZ = new float2(0f, 1f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 9,
                sourceRef: Entity.Null,
                addTransform: true,
                explodeReaction: null,
                collectReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = secondaryBulletTypeKey,
                    SpawnCount = spawnCount,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    SpreadAngleDeg = 30f,
                    SpawnRadius = 0.5f,
                    SpawnDelaySec = 0f,
                });

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity).Length, Is.EqualTo(0));
        }

        [Test]
        public void ReactionOwner_MotionCompletedWithExplodeReaction_AppendsSecondarySpawnRequest_AndKeepsSourcePending()
        {
            using var world = new World("BulletLifecycleReaction_MotionCompletedAppend");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 12u);
            var channelEntity = CreateSecondaryChannel(em);
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
                explodeReaction: new BulletOnMotionCompletedExplodeReactionComponent
                {
                    SecondaryBulletTypeKey = 21,
                    SpawnCount = 3,
                    Shape = BulletSecondarySpawnShapeId.ForwardSpread,
                    SpreadAngleDeg = 90f,
                    SpawnRadius = 1.5f,
                    SpawnDelaySec = 0f,
                },
                collectReaction: null);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            Assert.That(requests.Length, Is.EqualTo(1));

            var request = requests[0];
            Assert.That(request.BulletTypeKey, Is.EqualTo(21));
            Assert.That(request.Count, Is.EqualTo(3));
            Assert.That(request.SourceEntity, Is.EqualTo(source));
            Assert.That(request.CauserEntity, Is.EqualTo(bullet));
            Assert.That(request.OriginPosition, Is.EqualTo(new float3(4f, 0f, 6f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(request.BaseDirection, Is.EqualTo(new float2(0f, 1f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(request.SpreadAngleDeg, Is.EqualTo(90f));
            Assert.That(request.SpawnRadius, Is.EqualTo(1.5f));
            Assert.That(request.Shape, Is.EqualTo(BulletSecondarySpawnShapeId.ForwardSpread));
            Assert.That(request.OldestFrame, Is.EqualTo(12u));
            Assert.That(request.ReadyFrame, Is.EqualTo(13u));
            Assert.That(request.Sequence, Is.EqualTo(0u));

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(BulletLifecycleReasonId.MotionCompleted));
            Assert.That(em.GetComponentData<BulletLifecycleContactComponent>(bullet).PositionXZ, Is.EqualTo(new float2(4f, 6f)).Using(Float2Comparer.Within(1e-5f)));
        }

        [Test]
        public void ReactionOwner_MotionCompletedWithProfileTrigger_AppendsDiscreteRequest_AndSuppressesLegacyExplode()
        {
            using var world = new World("BulletLifecycleReaction_ProfileTrigger");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            SetFixedTickRuntime(em, 1f / 60f);
            var secondaryChannel = CreateSecondaryChannel(em);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, targetProfileRefId: 200, delaySec: 0.10f), CreateTargetRegistryEntry(200, bulletTypeKey: 44));
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
                explodeReaction: new BulletOnMotionCompletedExplodeReactionComponent
                {
                    SecondaryBulletTypeKey = 21,
                    SpawnCount = 3,
                    Shape = BulletSecondarySpawnShapeId.ForwardSpread,
                    SpreadAngleDeg = 90f,
                    SpawnRadius = 1.5f,
                    SpawnDelaySec = 0f,
                },
                collectReaction: null,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(secondaryChannel).Length, Is.EqualTo(0));

            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel);
            Assert.That(requests.Length, Is.EqualTo(1));
            var request = requests[0];
            Assert.That(request.ProducerKind, Is.EqualTo(DiscreteEmitProducerKind.TriggeredEmission));
            Assert.That(request.SourceEntity, Is.EqualTo(source));
            Assert.That(request.ProducerEntity, Is.EqualTo(bullet));
            Assert.That(request.CauserEntity, Is.EqualTo(bullet));
            Assert.That(request.EmissionId, Is.EqualTo(100));
            Assert.That(request.ProfileRefId, Is.EqualTo(200));
            Assert.That(request.BulletTypeKey, Is.EqualTo(44));
            Assert.That(request.AnchorPosition, Is.EqualTo(new float3(4f, 0f, 6f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(request.BaseAngleDeg, Is.EqualTo(100f).Within(1e-5f));
            Assert.That(request.ReadyFrame, Is.EqualTo(20u));
        }

        [Test]
        public void ReactionOwner_MotionCompletedWithMissingTriggerTarget_DoesNotAppendProfileOrLegacyWithoutLegacyReaction()
        {
            using var world = new World("BulletLifecycleReaction_ProfileTriggerMissingTarget");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            var secondaryChannel = CreateSecondaryChannel(em);
            var discreteChannel = CreateDiscreteChannel(em);
            CreateRegistry(em, CreateSourceRegistryEntry(100, targetProfileRefId: 200, delaySec: 0f));
            CreatePendingBullet(
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
                sourceRef: Entity.Null,
                addTransform: true,
                explodeReaction: null,
                collectReaction: null,
                profileRefId: 100);

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannel).Length, Is.EqualTo(0));
            Assert.That(em.GetBuffer<BulletSecondarySpawnRequestBuffer>(secondaryChannel).Length, Is.EqualTo(0));
        }

        [Test]
        public void ReactionOwner_VacuumCollectedWithCleanupRemovedReaction_AppendsSecondarySpawnRequest_AndKeepsSourcePending()
        {
            using var world = new World("BulletLifecycleReaction_VacuumCollectedAppend");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            var channelEntity = CreateSecondaryChannel(em);
            var source = em.CreateEntity();
            var bullet = CreatePendingBullet(
                em,
                BulletLifecycleReasonId.VacuumCollected,
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
                explodeReaction: null,
                collectReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = 33,
                    SpawnCount = 2,
                    Shape = BulletSecondarySpawnShapeId.SingleForward,
                    SpreadAngleDeg = 15f,
                    SpawnRadius = 0.25f,
                    SpawnDelaySec = 0f,
                });

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            Assert.That(requests.Length, Is.EqualTo(1));

            var request = requests[0];
            Assert.That(request.BulletTypeKey, Is.EqualTo(33));
            Assert.That(request.Count, Is.EqualTo(2));
            Assert.That(request.SourceEntity, Is.EqualTo(source));
            Assert.That(request.CauserEntity, Is.EqualTo(bullet));
            Assert.That(request.OriginPosition, Is.EqualTo(new float3(2f, 0f, 3f)).Using(Float3Comparer.Within(1e-5f)));
            Assert.That(request.BaseDirection, Is.EqualTo(new float2(1f, 0f)).Using(Float2Comparer.Within(1e-5f)));
            Assert.That(request.SpreadAngleDeg, Is.EqualTo(15f));
            Assert.That(request.SpawnRadius, Is.EqualTo(0.25f));
            Assert.That(request.Shape, Is.EqualTo(BulletSecondarySpawnShapeId.SingleForward));
            Assert.That(request.OldestFrame, Is.EqualTo(14u));
            Assert.That(request.ReadyFrame, Is.EqualTo(15u));
            Assert.That(request.Sequence, Is.EqualTo(0u));

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
            Assert.That(em.GetComponentData<BulletLifecycleContactComponent>(bullet).PositionXZ, Is.EqualTo(new float2(2f, 3f)).Using(Float2Comparer.Within(1e-5f)));
        }

        [Test]
        public void ReactionOwner_CarryFullRemovedWithCleanupRemovedReaction_AppendsSecondarySpawnRequest_AndKeepsSourcePending()
        {
            using var world = new World("BulletLifecycleReaction_CarryFullRemovedAppend");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            var channelEntity = CreateSecondaryChannel(em);
            var source = em.CreateEntity();
            var bullet = CreatePendingBullet(
                em,
                BulletLifecycleReasonId.CarryFullRemoved,
                new BulletLifecycleContactComponent
                {
                    PositionXZ = new float2(2f, 3f),
                    DirectionXZ = new float2(1f, 0f),
                },
                active: true,
                despawnRequested: true,
                typeKey: 11,
                sourceRef: source,
                addTransform: true,
                explodeReaction: null,
                collectReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = 33,
                    SpawnCount = 2,
                    Shape = BulletSecondarySpawnShapeId.SingleForward,
                    SpreadAngleDeg = 15f,
                    SpawnRadius = 0.25f,
                    SpawnDelaySec = 0f,
                });

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].BulletTypeKey, Is.EqualTo(33));
            Assert.That(requests[0].Count, Is.EqualTo(2));
            Assert.That(requests[0].SourceEntity, Is.EqualTo(source));
            Assert.That(requests[0].CauserEntity, Is.EqualTo(bullet));
            Assert.That(requests[0].ReadyFrame, Is.EqualTo(15u));
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            Assert.That(em.IsComponentEnabled<BulletActiveTag>(bullet), Is.True);
            Assert.That(em.GetComponentData<BulletLifecycleRequestComponent>(bullet).Reason, Is.EqualTo(BulletLifecycleReasonId.CarryFullRemoved));
        }

        [Test]
        public void ReactionOwner_DelayedCleanupRemovedReaction_ComputesReadyFrameFromLogicDelta()
        {
            using var world = new World("BulletLifecycleReaction_DelayedReadyFrame");
            var em = world.EntityManager;

            SetExecutionEndPrerequisites(em, frame: 14u);
            SetFixedTickRuntime(em, 1f / 60f);
            var channelEntity = CreateSecondaryChannel(em);
            var source = em.CreateEntity();
            CreatePendingBullet(
                em,
                BulletLifecycleReasonId.VacuumCollected,
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
                explodeReaction: null,
                collectReaction: new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = 33,
                    SpawnCount = 2,
                    Shape = BulletSecondarySpawnShapeId.SingleForward,
                    SpreadAngleDeg = 15f,
                    SpawnRadius = 0.25f,
                    SpawnDelaySec = 0.10f,
                });

            world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            var requests = em.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].OldestFrame, Is.EqualTo(14u));
            Assert.That(requests[0].ReadyFrame, Is.EqualTo(20u));
        }

        [Test]
        public void MotionCompletedExplode_EndToEnd_AppendsDespawnsAndSpawnsSecondaryBullets()
        {
            using var world = new World("BulletLifecycleReaction_EndToEnd");
            var em = world.EntityManager;

            SetFrameAndSimulationPrerequisites(em, frame: 1u);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.None });
            CreateSecondaryChannel(em);

            var source = em.CreateEntity();
            em.AddBuffer<SourceActiveBulletCountBuffer>(source);

            var sourceBullet = CreateSimulationBullet(
                em,
                position: new float3(1f, 0f, 2f),
                velocity: new float2(0.5f, 0f),
                radius: 0.05f,
                lifetime: 5f,
                typeKey: 9,
                sourceRef: source,
                dampedMotion: new BulletDampedMotionComponent
                {
                    DampingPerSec = 100f,
                    StopSpeedThreshold = 0.1f,
                },
                explodeReaction: new BulletOnMotionCompletedExplodeReactionComponent
                {
                    SecondaryBulletTypeKey = 21,
                    SpawnCount = 3,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    SpreadAngleDeg = 0f,
                    SpawnRadius = 1f,
                    SpawnDelaySec = 0f,
                });

            var secondaryBullets = new NativeArray<Entity>(3, Allocator.Temp);
            try
            {
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    secondaryBullets[i] = CreatePooledSecondaryBullet(em, typeKey: 21, speed: 2f, lifetime: 6f);
                    BulletFieldShared.FreeByKey.Add(21, secondaryBullets[i]);
                }

                world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletLifecycleReactionExecutionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<BulletDespawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(sourceBullet), Is.False);
                Assert.That(em.IsComponentEnabled<BulletActiveTag>(sourceBullet), Is.False);

                AdvanceFrame(em);
                world.GetOrCreateSystem<SecondarySpawnExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                int activeSecondaryCount = 0;
                for (int i = 0; i < secondaryBullets.Length; i++)
                {
                    if (!em.IsComponentEnabled<BulletActiveTag>(secondaryBullets[i]))
                        continue;

                    activeSecondaryCount++;
                    Assert.That(em.GetComponentData<BulletSourceRefComponent>(secondaryBullets[i]).Value, Is.EqualTo(source));
                }

                Assert.That(activeSecondaryCount, Is.EqualTo(3));

                var activeCounts = em.GetBuffer<SourceActiveBulletCountBuffer>(source);
                Assert.That(activeCounts.Length, Is.EqualTo(1));
                Assert.That(activeCounts[0].BulletTypeKey, Is.EqualTo(21));
                Assert.That(activeCounts[0].ActiveCount, Is.EqualTo(3));
            }
            finally
            {
                secondaryBullets.Dispose();
            }
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

        private static void SetFrameAndSimulationPrerequisites(EntityManager em, uint frame)
        {
            SetExecutionEndPrerequisites(em, frame);
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f,
                LogicDeltaTime = 1f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
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

            var cells = em.AddBuffer<StageRuntimeGridCellBufferElement>(entity);
            for (int i = 0; i < flags.Length; i++)
            {
                cells.Add(new StageRuntimeGridCellBufferElement
                {
                    MovementFlags = flags[i],
                    DepositRegionId = 0u,
                });
            }
        }

        private static Entity CreateSecondaryChannel(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(BulletSecondarySpawnChannelSingletonTag),
                typeof(SecondarySpawnPolicyComponent),
                typeof(SecondarySpawnBacklogMetricsComponent));
            em.SetComponentData(entity, new SecondarySpawnPolicyComponent
            {
                BudgetPerFrame = 8,
                MaxPendingCount = 32,
                MaxPendingAgeFrames = 120,
            });
            em.SetComponentData(entity, default(SecondarySpawnBacklogMetricsComponent));
            em.AddBuffer<BulletSecondarySpawnRequestBuffer>(entity);
            return entity;
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

        private static EmissionProfileRuntimeRegistryBuffer CreateSourceRegistryEntry(int profileRefId, int targetProfileRefId, float delaySec)
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
                HasMotionCompletedTrigger = 1,
                MotionCompletedTargetProfileRefId = targetProfileRefId,
                MotionCompletedOriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition,
                MotionCompletedForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection,
                MotionCompletedSourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity,
                MotionCompletedCauserEntity = EmissionTriggerCauserBindingId.CompletedBullet,
                MotionCompletedDelaySec = delaySec,
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

        private static void AdvanceFrame(EntityManager em)
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<BulletFrameCounterComponent>());
            var entity = query.GetSingletonEntity();
            var counter = em.GetComponentData<BulletFrameCounterComponent>(entity);
            counter.Value += 1;
            em.SetComponentData(entity, counter);
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
            BulletOnMotionCompletedExplodeReactionComponent? explodeReaction,
            BulletOnCleanupRemovedSpawnSecondaryReactionComponent? collectReaction,
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
            if (explodeReaction.HasValue)
                em.AddComponentData(entity, explodeReaction.Value);
            if (collectReaction.HasValue)
                em.AddComponentData(entity, collectReaction.Value);

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

        private static Entity CreateSimulationBullet(
            EntityManager em,
            float3 position,
            float2 velocity,
            float radius,
            float lifetime,
            int typeKey,
            Entity sourceRef,
            BulletDampedMotionComponent dampedMotion,
            BulletOnMotionCompletedExplodeReactionComponent explodeReaction)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletDampedMotionComponent),
                typeof(BulletOnMotionCompletedExplodeReactionComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new BulletVelocityComponent { Value = velocity });
            em.SetComponentData(entity, new BulletRadiusComponent { Value = radius });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = typeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = sourceRef });
            em.SetComponentData(entity, new BulletLifecycleTraceComponent
            {
                LastSpawnFrame = 0u,
                LastDespawnFrame = 0u,
            });
            em.SetComponentData(entity, dampedMotion);
            em.SetComponentData(entity, explodeReaction);
            em.SetComponentEnabled<BulletActiveTag>(entity, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
        }

        private static Entity CreatePooledSecondaryBullet(EntityManager em, int typeKey, float speed, float lifetime)
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
