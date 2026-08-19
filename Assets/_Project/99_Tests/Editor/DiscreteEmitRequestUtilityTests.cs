using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DiscreteEmitRequestUtilityTests
    {
        [Test]
        public void DiscreteEmitRuntimeBootstrap_CreatesSingletonBufferPolicyAndMetrics()
        {
            using var world = new World("DiscreteEmit_Bootstrap");
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

            var channelEntity = em.CreateEntityQuery(ComponentType.ReadOnly<DiscreteEmitChannelSingletonTag>())
                .GetSingletonEntity();

            Assert.That(em.HasBuffer<DiscreteEmitRequestBuffer>(channelEntity), Is.True);
            Assert.That(em.HasComponent<DiscreteEmitPolicyComponent>(channelEntity), Is.True);
            Assert.That(em.HasComponent<DiscreteEmitBacklogMetricsComponent>(channelEntity), Is.True);

            var policy = em.GetComponentData<DiscreteEmitPolicyComponent>(channelEntity);
            Assert.That(policy.BudgetPerFrame, Is.EqualTo(256));
            Assert.That(policy.MaxPendingCount, Is.EqualTo(8192));
            Assert.That(policy.MaxPendingAgeFrames, Is.EqualTo(120u));
            Assert.That(policy.WaveClipEventBudgetPerFrame, Is.EqualTo(0));
            Assert.That(policy.HazardActorBudgetPerFrame, Is.EqualTo(0));
            Assert.That(policy.TriggeredEmissionBudgetPerFrame, Is.EqualTo(0));
            Assert.That(policy.WaveClipEventMaxPendingCount, Is.EqualTo(0));
            Assert.That(policy.HazardActorMaxPendingCount, Is.EqualTo(0));
            Assert.That(policy.TriggeredEmissionMaxPendingCount, Is.EqualTo(0));
        }

        [Test]
        public void CreateDiscreteEmitRequest_InitializesMutableStateAndPreservesResolvedFields()
        {
            var sourceEntity = new Entity { Index = 101, Version = 1 };
            var producerEntity = new Entity { Index = 202, Version = 1 };
            var anchorEntity = new Entity { Index = 303, Version = 1 };
            var causerEntity = new Entity { Index = 404, Version = 1 };
            var seed = new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.HazardActor,
                SourceEntity = sourceEntity,
                ProducerEntity = producerEntity,
                CauserEntity = causerEntity,
                EmissionId = 77,
                ProfileRefId = 88,
                BulletTypeKey = 9,
                HasSpeedOverride = 1,
                SpeedOverride = 6.5f,
                HasLifetimeOverride = 1,
                LifetimeOverride = 9.25f,
                HasMovementOverride = 1,
                MovementFamily = BulletMovementFamilyId.DampedLinear,
                DampedLinear = new BulletDampedLinearDefinition
                {
                    DampingPerSec = 4f,
                    StopSpeedThreshold = 0.2f,
                },
                AnchorMode = DiscreteEmitAnchorMode.FixedWorld,
                AnchorEntity = anchorEntity,
                AnchorPosition = new float3(1f, 2f, 3f),
                AnchorLocalOffset = new float3(4f, 5f, 6f),
                PositionPatternMode = WavePositionPatternModeId.PointSet,
                SpawnOffset = new float2(7f, 8f),
                LineStart = new float2(-1f, 0f),
                LineEnd = new float2(1f, 0f),
                SampleSpacing = 0.5f,
                PointSetCount = 4,
                Point0 = new float2(0f, 1f),
                Point1 = new float2(1f, 2f),
                Point2 = new float2(2f, 3f),
                Point3 = new float2(3f, 4f),
                AimMode = WaveAimModeId.PlayerPosition,
                AimSnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                BaseAngleDeg = 30f,
                AimAngleOffsetDeg = 10f,
                LineNormalSide = WaveLineNormalSideId.Right,
                LineNormalAngleOffsetDeg = 15f,
                SpiralStepDeg = 22.5f,
                ShotPatternMode = WaveShotPatternModeId.NWay,
                ShotCount = 5,
                NWayAngleSpacingDeg = 18f,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                EventShotIntervalSec = 0.25f,
                RepeatCount = 3,
                Priority = 12,
                ReadyFrame = 60u,
            };

            var request = DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, 55u);

            Assert.That(request.ProducerKind, Is.EqualTo(DiscreteEmitProducerKind.HazardActor));
            Assert.That(request.SourceEntity, Is.EqualTo(sourceEntity));
            Assert.That(request.ProducerEntity, Is.EqualTo(producerEntity));
            Assert.That(request.CauserEntity, Is.EqualTo(causerEntity));
            Assert.That(request.EmissionId, Is.EqualTo(77));
            Assert.That(request.ProfileRefId, Is.EqualTo(88));
            Assert.That(request.BulletTypeKey, Is.EqualTo(9));
            Assert.That(request.HasSpeedOverride, Is.EqualTo(1));
            Assert.That(request.SpeedOverride, Is.EqualTo(6.5f));
            Assert.That(request.HasLifetimeOverride, Is.EqualTo(1));
            Assert.That(request.LifetimeOverride, Is.EqualTo(9.25f));
            Assert.That(request.HasMovementOverride, Is.EqualTo(1));
            Assert.That(request.MovementFamily, Is.EqualTo(BulletMovementFamilyId.DampedLinear));
            Assert.That(request.DampedLinear.DampingPerSec, Is.EqualTo(4f));
            Assert.That(request.DampedLinear.StopSpeedThreshold, Is.EqualTo(0.2f));
            Assert.That(request.AnchorMode, Is.EqualTo(DiscreteEmitAnchorMode.FixedWorld));
            Assert.That(request.AnchorEntity, Is.EqualTo(anchorEntity));
            Assert.That(request.AnchorPosition, Is.EqualTo(new float3(1f, 2f, 3f)));
            Assert.That(request.AnchorLocalOffset, Is.EqualTo(new float3(4f, 5f, 6f)));
            Assert.That(request.PositionPatternMode, Is.EqualTo(WavePositionPatternModeId.PointSet));
            Assert.That(request.ShotPatternMode, Is.EqualTo(WaveShotPatternModeId.NWay));
            Assert.That(request.ShotCount, Is.EqualTo(5));
            Assert.That(request.RemainingRepeats, Is.EqualTo(3));
            Assert.That(request.RepeatSequence, Is.EqualTo(0u));
            Assert.That(request.EventAimInitialized, Is.EqualTo(0));
            Assert.That(request.EventAimTargetPosition, Is.EqualTo(float3.zero));
            Assert.That(request.EventShotElapsedSec, Is.EqualTo(0f));
            Assert.That(request.Priority, Is.EqualTo(12));
            Assert.That(request.OldestFrame, Is.EqualTo(55u));
            Assert.That(request.ReadyFrame, Is.EqualTo(60u));
        }

        [Test]
        public void CreateDiscreteEmitRequest_ClampsRepeatCountAndKeepsAnchorMode()
        {
            var anchorEntity = new Entity { Index = 404, Version = 1 };
            var seed = new DiscreteEmitRequestSeed
            {
                ProducerKind = DiscreteEmitProducerKind.WaveClipEvent,
                SourceEntity = Entity.Null,
                ProducerEntity = Entity.Null,
                EmissionId = 1,
                BulletTypeKey = 2,
                AnchorMode = DiscreteEmitAnchorMode.SourceRelative,
                AnchorEntity = anchorEntity,
                AnchorPosition = new float3(10f, 0f, 20f),
                AnchorLocalOffset = new float3(1f, 0f, -1f),
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 0,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = -1f,
                RepeatCount = 0,
                PointSetCount = 9,
            };

            var request = DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, 99u);

            Assert.That(request.AnchorMode, Is.EqualTo(DiscreteEmitAnchorMode.SourceRelative));
            Assert.That(request.AnchorEntity, Is.EqualTo(anchorEntity));
            Assert.That(request.AnchorLocalOffset, Is.EqualTo(new float3(1f, 0f, -1f)));
            Assert.That(request.RemainingRepeats, Is.EqualTo(1));
            Assert.That(request.ShotCount, Is.EqualTo(1));
            Assert.That(request.PointSetCount, Is.EqualTo(4));
            Assert.That(request.EventShotIntervalSec, Is.EqualTo(0f));
            Assert.That(request.ReadyFrame, Is.EqualTo(99u));
        }
    }
}
