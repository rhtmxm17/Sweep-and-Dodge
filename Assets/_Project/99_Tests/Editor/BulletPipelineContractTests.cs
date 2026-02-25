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
            AssertUpdateInGroup(typeof(BulletFramePipelineGroup), typeof(SimulationSystemGroup));
            AssertUpdateInGroup(typeof(BulletExecutionBeginGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletSimulationGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletRequestGroup), typeof(BulletFramePipelineGroup));
            AssertUpdateInGroup(typeof(BulletExecutionEndGroup), typeof(BulletFramePipelineGroup));

            AssertUpdateBefore(typeof(BulletExecutionBeginGroup), typeof(BulletSimulationGroup));
            AssertUpdateAfter(typeof(BulletSimulationGroup), typeof(BulletExecutionBeginGroup));
            AssertUpdateAfter(typeof(BulletRequestGroup), typeof(BulletSimulationGroup));
            AssertUpdateAfter(typeof(BulletExecutionEndGroup), typeof(BulletRequestGroup));
        }

        [Test]
        public void FrameCounterAdvanceSystem_IsExecutionBeginOrderFirst()
        {
            var attr = GetSingleAttribute<UpdateInGroupAttribute>(typeof(BulletFrameCounterAdvanceSystem));
            Assert.That(attr.GroupType, Is.EqualTo(typeof(BulletExecutionBeginGroup)));
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
        public void RequestSubSequence_StaysInContractOrder()
        {
            AssertUpdateBefore(typeof(PlayerCleanupActionSelectSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateAfter(typeof(SourcePollutionUpdateSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateBefore(typeof(SourcePollutionUpdateSystem), typeof(PlayerHazardCollisionRequestSystem));
            AssertUpdateAfter(typeof(PlayerHazardCollisionRequestSystem), typeof(BulletVacuumRequestSystem));
            AssertUpdateAfter(typeof(PlayerCarryBinDepositRequestSystem), typeof(PlayerHazardCollisionRequestSystem));
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
