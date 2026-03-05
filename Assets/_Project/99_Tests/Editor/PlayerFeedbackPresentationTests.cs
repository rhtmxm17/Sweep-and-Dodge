using NUnit.Framework;
using Unity.Core;
using Unity.Entities;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayerFeedbackPresentationTests
    {
        [Test]
        public void UiFeedbackConsume_AppliesPriorityAndCooldown_ThenClearsBuffer()
        {
            using var world = CreateDefaultTestWorld("PlayerFeedbackPresentationWorld_A", out var simGroup);
            var em = world.EntityManager;
            var player = CreatePlayerForFeedbackTests(em);
            var uiBuffer = em.GetBuffer<PlayerUiFeedbackEventBufferElement>(player);

            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                Reason = (byte)PlayerUiFeedbackReasonId.CarryBinFull,
                Value = 0,
                RelatedEntity = Entity.Null,
                Frame = 10u,
                Sequence = 0u,
            });
            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.VacuumStartBlocked,
                Reason = (byte)PlayerUiFeedbackReasonId.CarryBinFull,
                Value = 0,
                RelatedEntity = Entity.Null,
                Frame = 10u,
                Sequence = 1u,
            });
            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.HazardCaptured,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                Value = 0,
                RelatedEntity = Entity.Null,
                Frame = 10u,
                Sequence = 2u,
            });
            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                Value = 9,
                RelatedEntity = Entity.Null,
                Frame = 10u,
                Sequence = 3u,
            });

            TickWorld(world, simGroup, 1f / 60f);

            var snapshot = em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(player);
            Assert.That(snapshot.Version, Is.EqualTo(1u));
            Assert.That(snapshot.Type, Is.EqualTo(PlayerUiFeedbackEventType.PlayerHazardHit));
            Assert.That(snapshot.Value, Is.EqualTo(9));
            Assert.That(snapshot.RemainingSec, Is.GreaterThan(1.2f));
            Assert.That(uiBuffer.Length, Is.EqualTo(0));

            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                Value = 4,
                RelatedEntity = Entity.Null,
                Frame = 11u,
                Sequence = 0u,
            });
            TickWorld(world, simGroup, 1f / 60f);

            var cooldownSnapshot = em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(player);
            Assert.That(cooldownSnapshot.Version, Is.EqualTo(1u), "Hit cooldown should suppress immediate repeated hit feedback.");
            Assert.That(uiBuffer.Length, Is.EqualTo(0));

            for (int i = 0; i < 7; i++)
                TickWorld(world, simGroup, 1f / 60f);

            uiBuffer.Add(new PlayerUiFeedbackEventBufferElement
            {
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Reason = (byte)PlayerUiFeedbackReasonId.Default,
                Value = 5,
                RelatedEntity = Entity.Null,
                Frame = 20u,
                Sequence = 0u,
            });
            TickWorld(world, simGroup, 1f / 60f);

            var acceptedSnapshot = em.GetComponentData<PlayerUiFeedbackPresentationSnapshotComponent>(player);
            Assert.That(acceptedSnapshot.Version, Is.EqualTo(2u));
            Assert.That(acceptedSnapshot.Value, Is.EqualTo(5));
            Assert.That(uiBuffer.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImpulseConsume_MergesImpulseEvents_AndClearsBuffer()
        {
            using var world = CreateDefaultTestWorld("PlayerFeedbackPresentationWorld_B", out var simGroup);
            var em = world.EntityManager;
            var player = CreatePlayerForFeedbackTests(em);
            var impulseBuffer = em.GetBuffer<PlayerImpulseEventBufferElement>(player);

            impulseBuffer.Add(new PlayerImpulseEventBufferElement
            {
                Reason = (byte)PlayerImpulseReasonId.Default,
                DirX = 1f,
                DirZ = 0f,
                Magnitude = 1f,
                Frame = 33u,
                Sequence = 0u,
            });
            impulseBuffer.Add(new PlayerImpulseEventBufferElement
            {
                Reason = (byte)PlayerImpulseReasonId.Default,
                DirX = 0f,
                DirZ = 1f,
                Magnitude = 1f,
                Frame = 33u,
                Sequence = 1u,
            });

            TickWorld(world, simGroup, 1f / 60f);

            var snapshot = em.GetComponentData<PlayerImpulsePresentationSnapshotComponent>(player);
            Assert.That(snapshot.Version, Is.EqualTo(1u));
            Assert.That(snapshot.MergedEventCount, Is.EqualTo(2));
            Assert.That(snapshot.Magnitude, Is.EqualTo(1.4142135f).Within(1e-4f));
            Assert.That(snapshot.DirX, Is.EqualTo(0.7071067f).Within(1e-4f));
            Assert.That(snapshot.DirZ, Is.EqualTo(0.7071067f).Within(1e-4f));
            Assert.That(impulseBuffer.Length, Is.EqualTo(0));
        }

        [Test]
        public void ImpulseConsume_ClampsMergedMagnitude_DefensiveGuard()
        {
            using var world = CreateDefaultTestWorld("PlayerFeedbackPresentationWorld_C", out var simGroup);
            var em = world.EntityManager;
            var player = CreatePlayerForFeedbackTests(em);
            var impulseBuffer = em.GetBuffer<PlayerImpulseEventBufferElement>(player);

            impulseBuffer.Add(new PlayerImpulseEventBufferElement
            {
                Reason = (byte)PlayerImpulseReasonId.Default,
                DirX = 1f,
                DirZ = 0f,
                Magnitude = 1.2f,
                Frame = 77u,
                Sequence = 0u,
            });
            impulseBuffer.Add(new PlayerImpulseEventBufferElement
            {
                Reason = (byte)PlayerImpulseReasonId.Default,
                DirX = 1f,
                DirZ = 0f,
                Magnitude = 1.3f,
                Frame = 77u,
                Sequence = 1u,
            });

            TickWorld(world, simGroup, 1f / 60f);

            var snapshot = em.GetComponentData<PlayerImpulsePresentationSnapshotComponent>(player);
            Assert.That(snapshot.Version, Is.EqualTo(1u));
            Assert.That(snapshot.MergedEventCount, Is.EqualTo(2));
            Assert.That(snapshot.Magnitude, Is.EqualTo(1.5f).Within(1e-6f), "Merged magnitude must be clamped by defensive frame cap.");
            Assert.That(snapshot.DirX, Is.EqualTo(1f).Within(1e-6f));
            Assert.That(snapshot.DirZ, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(impulseBuffer.Length, Is.EqualTo(0));
        }

        private static World CreateDefaultTestWorld(string worldName, out SimulationSystemGroup simGroup)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            Assert.That(simGroup, Is.Not.Null, "SimulationSystemGroup must exist");
            return world;
        }

        private static Entity CreatePlayerForFeedbackTests(EntityManager em)
        {
            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerUiFeedbackPresentationSnapshotComponent),
                typeof(PlayerImpulsePresentationSnapshotComponent));
            em.AddBuffer<PlayerUiFeedbackEventBufferElement>(player);
            em.AddBuffer<PlayerImpulseEventBufferElement>(player);

            em.SetComponentData(player, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 0u,
                Type = PlayerUiFeedbackEventType.None,
                Reason = (byte)PlayerUiFeedbackReasonId.None,
                Value = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
                RemainingSec = 0f,
                ClockSec = 0f,
                NextAllowedVacuumBlockedSec = 0f,
                NextAllowedSourceStateChangedSec = 0f,
                NextAllowedHazardCapturedSec = 0f,
                NextAllowedHazardRemovedSec = 0f,
                NextAllowedHitSec = 0f,
            });
            em.SetComponentData(player, new PlayerImpulsePresentationSnapshotComponent
            {
                Version = 0u,
                Reason = (byte)PlayerImpulseReasonId.None,
                DirX = 0f,
                DirZ = 0f,
                Magnitude = 0f,
                Frame = 0u,
                MergedEventCount = 0,
            });

            return player;
        }

        private static void TickWorld(World world, ComponentSystemGroup simGroup, float deltaTime)
        {
            double elapsed = world.Time.ElapsedTime + deltaTime;
            world.SetTime(new TimeData(elapsed, deltaTime));
            simGroup.Update();
        }
    }
}
