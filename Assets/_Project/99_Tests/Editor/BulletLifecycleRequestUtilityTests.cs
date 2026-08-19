using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletLifecycleRequestUtilityTests
    {
        [TestCase(
            BulletLifecycleReasonId.StageBlocked,
            false,
            BulletLifecycleReasonId.PlayerHit,
            true,
            TestName = "CanPromote_DisabledRequest_IgnoresStalePriority")]
        [TestCase(
            BulletLifecycleReasonId.PlayerHit,
            true,
            BulletLifecycleReasonId.LifetimeExpired,
            true,
            TestName = "CanPromote_EnabledRequest_AcceptsHigherPriority")]
        [TestCase(
            BulletLifecycleReasonId.CarryFullRemoved,
            true,
            BulletLifecycleReasonId.VacuumCollected,
            false,
            TestName = "CanPromote_EnabledRequest_RejectsEqualPriority")]
        [TestCase(
            BulletLifecycleReasonId.LifetimeExpired,
            true,
            BulletLifecycleReasonId.PlayerHit,
            false,
            TestName = "CanPromote_EnabledRequest_RejectsLowerPriority")]
        public void CanPromoteLifecycleRequest_UsesEnableStateAndPriority(
            BulletLifecycleReasonId candidateReason,
            bool requestEnabled,
            BulletLifecycleReasonId currentReason,
            bool expected)
        {
            var currentRequest = new BulletLifecycleRequestComponent
            {
                Reason = currentReason,
                Priority = BulletLifecycleRequestUtility.ResolvePriority(currentReason),
            };

            Assert.That(
                BulletLifecycleRequestUtility.CanPromoteLifecycleRequest(
                    candidateReason,
                    requestEnabled,
                    in currentRequest),
                Is.EqualTo(expected));
        }

        [Test]
        public void TryPromoteLifecycleRequest_AcceptsEmptyRequest()
        {
            using var world = new World("BulletLifecycleRequestUtility_Empty");
            var em = world.EntityManager;
            var bullet = CreateBullet(em);

            bool accepted = BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.StageBlocked,
                Entity.Null,
                7u,
                new float2(1f, 2f),
                new float2(0f, 3f));

            Assert.That(accepted, Is.True);
            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.StageBlocked));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.StageBlocked)));
            Assert.That(request.Frame, Is.EqualTo(7u));
            var contact = em.GetComponentData<BulletLifecycleContactComponent>(bullet);
            Assert.That(contact.PositionXZ, Is.EqualTo(new float2(1f, 2f)));
            Assert.That(contact.DirectionXZ, Is.EqualTo(new float2(0f, 1f)));
        }

        [Test]
        public void TryPromoteLifecycleRequest_DoesNotOverrideLowerPriority()
        {
            using var world = new World("BulletLifecycleRequestUtility_LowerPriority");
            var em = world.EntityManager;
            var bullet = CreateBullet(em);

            Assert.That(BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.PlayerHit,
                Entity.Null,
                10u,
                new float2(0f, 0f),
                new float2(1f, 0f)), Is.True);

            bool accepted = BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.LifetimeExpired,
                Entity.Null,
                11u,
                new float2(3f, 4f),
                new float2(0f, 1f));

            Assert.That(accepted, Is.False);
            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.PlayerHit));
            Assert.That(request.Frame, Is.EqualTo(10u));
        }

        [Test]
        public void TryPromoteLifecycleRequest_OverridesHigherPriority()
        {
            using var world = new World("BulletLifecycleRequestUtility_HigherPriority");
            var em = world.EntityManager;
            var bullet = CreateBullet(em);

            Assert.That(BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.StageBlocked,
                Entity.Null,
                3u,
                new float2(1f, 1f),
                new float2(1f, 0f)), Is.True);

            bool accepted = BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.PlayerHit,
                Entity.Null,
                4u,
                new float2(5f, 6f),
                new float2(0f, 2f));

            Assert.That(accepted, Is.True);
            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.PlayerHit));
            Assert.That(request.Priority, Is.EqualTo(BulletLifecycleRequestUtility.ResolvePriority(BulletLifecycleReasonId.PlayerHit)));
            Assert.That(request.Frame, Is.EqualTo(4u));
            var contact = em.GetComponentData<BulletLifecycleContactComponent>(bullet);
            Assert.That(contact.PositionXZ, Is.EqualTo(new float2(5f, 6f)));
            Assert.That(contact.DirectionXZ, Is.EqualTo(new float2(0f, 1f)));
        }

        [Test]
        public void TryPromoteLifecycleRequest_LeavesEqualPriorityRequestUnchanged()
        {
            using var world = new World("BulletLifecycleRequestUtility_EqualPriority");
            var em = world.EntityManager;
            var bullet = CreateBullet(em);

            Assert.That(BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.VacuumCollected,
                Entity.Null,
                12u,
                new float2(2f, 3f),
                new float2(1f, 1f)), Is.True);

            bool accepted = BulletLifecycleRequestUtility.TryPromoteLifecycleRequest(
                em,
                bullet,
                BulletLifecycleReasonId.CarryFullRemoved,
                Entity.Null,
                13u,
                new float2(8f, 9f),
                new float2(-1f, 0f));

            Assert.That(accepted, Is.False);
            var request = em.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            Assert.That(request.Reason, Is.EqualTo(BulletLifecycleReasonId.VacuumCollected));
            Assert.That(request.Frame, Is.EqualTo(12u));
        }

        private static Entity CreateBullet(EntityManager em)
        {
            var bullet = em.CreateEntity(
                typeof(BulletDespawnRequestTag),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent));
            em.SetComponentEnabled<BulletDespawnRequestTag>(bullet, false);
            BulletLifecycleRequestUtility.ResetLifecycleRequestState(em, bullet);
            return bullet;
        }
    }
}
