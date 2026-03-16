using System;
using System.Linq;
using NUnit.Framework;
using Unity.Entities;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletPipelineContractTests
    {
        [Test]
        public void PipelineGroups_AreNestedUnderRootInFixedOrder()
        {
            AssertUpdateInGroup(typeof(StageTopologyPrepareGroup), typeof(SimulationSystemGroup));
            AssertUpdateInGroup(typeof(FixedTickRootGroup), typeof(SimulationSystemGroup));
            AssertUpdateInGroup(typeof(PlayerFixedStepGroup), typeof(FixedTickRootGroup));
            AssertUpdateInGroup(typeof(BulletFramePipelineGroup), typeof(FixedTickRootGroup));
            AssertUpdateInGroup(typeof(BulletExecutionBeginGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletSimulationGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletRequestGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletExecutionEndGroup), typeof(BulletFramePipelineGroup));

            AssertUpdateBefore(typeof(StageTopologyPrepareGroup), typeof(FixedTickRootGroup));
            AssertUpdateAfter(typeof(PlayerFixedStepGroup), typeof(FixedTickTimeResolveSystem));
            AssertUpdateBefore(typeof(PlayerFixedStepGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateBefore(typeof(BulletExecutionBeginGroup), typeof(BulletSimulationGroup));
            AssertUpdateAfter(typeof(BulletSimulationGroup), typeof(BulletExecutionBeginGroup));
            AssertUpdateAfter(typeof(BulletRequestGroup), typeof(BulletSimulationGroup));
            AssertUpdateAfter(typeof(BulletExecutionEndGroup), typeof(BulletRequestGroup));
        }

        [Test]
        public void StageTopologyPrepareSystems_StayInPrepareContractOrder()
        {
            var bootstrapAttr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(StageTopologyBootstrapSystem));
            Assert.That(bootstrapAttr.GroupType, Is.EqualTo(typeof(StageTopologyPrepareGroup)));
            Assert.That(bootstrapAttr.OrderFirst, Is.True);

            AssertUpdateInGroup(typeof(StageTopologyApplyPrepareSystem), typeof(StageTopologyPrepareGroup));
            AssertUpdateAfter(typeof(StageTopologyApplyPrepareSystem), typeof(StageTopologyBootstrapSystem));
        }

        [Test]
        public void FrameCounterAdvanceSystem_IsExecutionBeginOrderFirst()
        {
            var attr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(BulletFrameCounterAdvanceSystem));
            Assert.That(attr.GroupType, Is.EqualTo(typeof(BulletExecutionBeginGroup)));
            Assert.That(attr.OrderFirst, Is.True);
        }

        [Test]
        public void FixedTickBootstrapSystem_IsFixedTickRootOrderFirst()
        {
            var attr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(FixedTickBootstrapSystem));
            Assert.That(attr.GroupType, Is.EqualTo(typeof(FixedTickRootGroup)));
            Assert.That(attr.OrderFirst, Is.True);
        }

        [Test]
        public void RequestFencePublishSystem_IsRequestOrderLast()
        {
            var attr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(BulletRequestFencePublishSystem));
            Assert.That(attr.GroupType, Is.EqualTo(typeof(BulletRequestGroup)));
            Assert.That(attr.OrderLast, Is.True);
        }

        [Test]
        public void PlayerFixedStepSubSequence_StaysInContractOrder()
        {
            var applyAttr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(ReplayTickInputApplySystem));
            Assert.That(applyAttr.GroupType, Is.EqualTo(typeof(PlayerFixedStepGroup)));
            Assert.That(applyAttr.OrderFirst, Is.True);

            var recordAttr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(ReplayTickRecordSystem));
            Assert.That(recordAttr.GroupType, Is.EqualTo(typeof(PlayerFixedStepGroup)));
            Assert.That(recordAttr.OrderLast, Is.True);

            AssertUpdateInGroup(typeof(PlayerPreviousPositionCaptureSystem), typeof(PlayerFixedStepGroup));
            AssertUpdateInGroup(typeof(PlayerIntentMovementSystem), typeof(PlayerFixedStepGroup));
            AssertUpdateInGroup(typeof(PlayerObstacleBlockSystem), typeof(PlayerFixedStepGroup));
            AssertUpdateInGroup(typeof(PlayerIntentConsumeSystem), typeof(PlayerFixedStepGroup));
            AssertUpdateAfter(typeof(PlayerPreviousPositionCaptureSystem), typeof(ReplayTickInputApplySystem));
            AssertUpdateBefore(typeof(PlayerPreviousPositionCaptureSystem), typeof(PlayerIntentMovementSystem));
            AssertUpdateAfter(typeof(PlayerIntentMovementSystem), typeof(ReplayTickInputApplySystem));
            AssertUpdateAfter(typeof(PlayerObstacleBlockSystem), typeof(PlayerIntentMovementSystem));
            AssertUpdateBefore(typeof(PlayerObstacleBlockSystem), typeof(PlayerIntentConsumeSystem));
            AssertUpdateBefore(typeof(PlayerIntentMovementSystem), typeof(PlayerIntentConsumeSystem));
            AssertUpdateAfter(typeof(PlayerIntentConsumeSystem), typeof(PlayerIntentMovementSystem));
            AssertUpdateBefore(typeof(PlayerIntentConsumeSystem), typeof(ReplayTickRecordSystem));
        }

        [Test]
        public void RequestSubSequence_StaysInContractOrder()
        {
            AssertUpdateBefore(typeof(PlayerCleanupActionSelectSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateAfter(typeof(SourcePollutionUpdateSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateAfter(typeof(BulletObstacleHitRequestSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateBefore(typeof(BulletObstacleHitRequestSystem), typeof(PlayerHazardCollisionRequestSystem));
            AssertUpdateBefore(typeof(SourcePollutionUpdateSystem), typeof(PlayerHazardCollisionRequestSystem));
            AssertUpdateAfter(typeof(PlayerHazardCollisionRequestSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateAfter(typeof(PlayerCarryBinDepositRequestSystem), typeof(PlayerHazardCollisionRequestSystem));
            AssertUpdateAfter(typeof(RunDirectorStageGateUpdateSystem), typeof(PlayerCarryBinDepositRequestSystem));
            AssertUpdateBefore(typeof(RunDirectorStageGateUpdateSystem), typeof(RunDirectorStageTransitionSystem));
            AssertUpdateAfter(typeof(RunDirectorStageTransitionSystem), typeof(RunDirectorStageGateUpdateSystem));
            AssertUpdateBefore(typeof(RunDirectorStageTransitionSystem), typeof(RunProgressDirectorSystem));
            AssertUpdateAfter(typeof(RunProgressDirectorSystem), typeof(PlayerCarryBinDepositRequestSystem));
            AssertUpdateBefore(typeof(RunProgressDirectorSystem), typeof(SourceClipRequestBuildSystem));
            AssertUpdateAfter(typeof(SourceClipRequestBuildSystem), typeof(PlayerCarryBinDepositRequestSystem));
            AssertUpdateBefore(typeof(SourceClipRequestBuildSystem), typeof(BulletRequestFencePublishSystem));
        }

        [Test]
        public void ExecutionBeginSpawnSubSequence_StaysInContractOrder()
        {
            AssertUpdateBefore(typeof(BulletFieldAreaUpdateSystem), typeof(SpawnRequestRoundRobinExecutionSystem));
            AssertUpdateAfter(typeof(SpawnRequestRoundRobinExecutionSystem), typeof(BulletFieldAreaUpdateSystem));
            AssertUpdateAfter(typeof(SpawnBacklogWarningSystem), typeof(SpawnRequestRoundRobinExecutionSystem));
        }

        [Test]
        public void ExecutionEndFeedbackSubSequence_StaysInContractOrder()
        {
            AssertUpdateAfter(typeof(PlayerCarryBinDepositExecutionSystem), typeof(PlayerHazardCollisionExecutionSystem));
            AssertUpdateAfter(typeof(PlayerHazardRiskResolveSystem), typeof(PlayerCarryBinDepositExecutionSystem));
            AssertUpdateBefore(typeof(PlayerHazardRiskResolveSystem), typeof(BulletDespawnExecutionSystem));
            AssertUpdateAfter(typeof(CombatEventChannelConsumeSystem), typeof(PlayerCarryBinDepositExecutionSystem));
            AssertUpdateAfter(typeof(PlayerHudSnapshotCollectSystem), typeof(CombatEventChannelConsumeSystem));
            AssertUpdateBefore(typeof(PlayerHudSnapshotCollectSystem), typeof(PlayerUiFeedbackConsumeSystem));
            AssertUpdateBefore(typeof(CombatEventChannelConsumeSystem), typeof(PlayerUiFeedbackConsumeSystem));
        }

        [Test]
        public void FrameSequenceUtility_ExposesCounterBasedAccessor()
        {
            var method = typeof(FrameSequenceUtility).GetMethod(nameof(FrameSequenceUtility.GetCurrentFrame));
            Assert.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(uint)));
        }

        private static void AssertUpdateInGroup(Type systemType, Type expectedGroupType)
        {
            var attr = GetSingleAttribute<UpdateInGroupAttribute>(systemType);
            Assert.That(attr.GroupType, Is.EqualTo(expectedGroupType), $"{systemType.Name} UpdateInGroup mismatch");
        }

        private static void AssertUpdateAfter(Type systemType, Type expectedSystemType)
        {
            var attrs = systemType.GetCustomAttributes(typeof(UpdateAfterAttribute), inherit: false)
                .Cast<UpdateAfterAttribute>()
                .ToArray();
            Assert.That(attrs.Any(a => a.SystemType == expectedSystemType), Is.True, $"{systemType.Name} missing UpdateAfter({expectedSystemType.Name})");
        }

        private static void AssertUpdateBefore(Type systemType, Type expectedSystemType)
        {
            var attrs = systemType.GetCustomAttributes(typeof(UpdateBeforeAttribute), inherit: false)
                .Cast<UpdateBeforeAttribute>()
                .ToArray();
            Assert.That(attrs.Any(a => a.SystemType == expectedSystemType), Is.True, $"{systemType.Name} missing UpdateBefore({expectedSystemType.Name})");
        }

        private static T GetSingleAttribute<T>(Type systemType) where T : Attribute
        {
            var attrs = systemType.GetCustomAttributes(typeof(T), inherit: false).Cast<T>().ToArray();
            Assert.That(attrs.Length, Is.EqualTo(1), $"{systemType.Name} should have exactly one {typeof(T).Name}");
            return attrs[0];
        }
    }
}
