using NUnit.Framework;
using Unity.Core;
using Unity.Entities;

namespace SweepNDodge.DotsBullets.Tests
{
    public class PlayerHudSnapshotTests
    {
        [Test]
        public void PlayerHudSnapshotCollect_CapturesCarrySourceStageAndHit()
        {
            using var world = CreateDefaultTestWorld("PlayerHudSnapshotWorld_A", out var simGroup);
            var em = world.EntityManager;

            CreatePlayer(em, load: 47, capacity: 300);
            CreateSnapshotSingleton(em);
            CreateStageStateSingleton(em, RunDirectorStageStateId.Running, 3.5f);
            CreateCombatMetricsSingleton(em, hitCount: 1, hitValue: 12);

            CreateSource(em, stableId: 9u, state: SourceStateId.Normal, directorState: RunDirectorSourceStateId.Pressure, collected: 4, thresholdDepleted: 80);
            CreateSource(em, stableId: 3u, state: SourceStateId.Normal, directorState: RunDirectorSourceStateId.Pressure, collected: 9, thresholdDepleted: 0);
            CreateSource(em, stableId: 11u, state: SourceStateId.Depleted, directorState: RunDirectorSourceStateId.Finish, collected: 40, thresholdDepleted: 40);

            TickWorld(world, simGroup, 1f / 60f);

            var snapshot = GetSingleton<PlayerHudSnapshotComponent>(em);
            Assert.That(snapshot.CarryLoad, Is.EqualTo(47));
            Assert.That(snapshot.CarryCapacity, Is.EqualTo(300));
            Assert.That(snapshot.TotalSourceCount, Is.EqualTo(3));
            Assert.That(snapshot.DepletedSourceCount, Is.EqualTo(1));
            Assert.That(snapshot.PressureSourceStableId, Is.EqualTo(3u), "Tie-break must choose the smallest StableId.");
            Assert.That(snapshot.PressureSourceCollected, Is.EqualTo(9));
            Assert.That(snapshot.PressureSourceThresholdDepleted, Is.EqualTo(0));
            Assert.That(snapshot.PressureSourceProgress01, Is.EqualTo(1f).Within(1e-6f), "Threshold 0 must use denominator max(1, threshold).");
            Assert.That(snapshot.StageState, Is.EqualTo(RunDirectorStageStateId.Running));
            Assert.That(snapshot.LastHitLossValue, Is.EqualTo(12));
            Assert.That(snapshot.HitFlashRemainingSec, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(snapshot.LastUpdatedFrame, Is.GreaterThan(0u));
        }

        [Test]
        public void PlayerHudSnapshotCollect_DecaysHitFlash_WhenNoNewHit()
        {
            using var world = CreateDefaultTestWorld("PlayerHudSnapshotWorld_B", out var simGroup);
            var em = world.EntityManager;

            CreatePlayer(em, load: 10, capacity: 100);
            CreateSnapshotSingleton(em);
            CreateStageStateSingleton(em, RunDirectorStageStateId.Running, 0f);
            CreateCombatMetricsSingleton(em, hitCount: 1, hitValue: 7);
            CreateSource(em, stableId: 1u, state: SourceStateId.Normal, directorState: RunDirectorSourceStateId.Pressure, collected: 1, thresholdDepleted: 10);

            TickWorld(world, simGroup, 1f / 60f);
            var snapshotAfterHit = GetSingleton<PlayerHudSnapshotComponent>(em);
            Assert.That(snapshotAfterHit.HitFlashRemainingSec, Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(snapshotAfterHit.LastHitLossValue, Is.EqualTo(7));

            var metricsEntity = GetSingletonEntity<CombatEventMetricsComponent>(em);
            em.SetComponentData(metricsEntity, new CombatEventMetricsComponent
            {
                LastFrameHitCount = 0,
                LastFrameHitValue = 0,
            });

            for (int i = 0; i < 45; i++)
                TickWorld(world, simGroup, 1f / 60f);

            var snapshotAfterDecay = GetSingleton<PlayerHudSnapshotComponent>(em);
            Assert.That(snapshotAfterDecay.HitFlashRemainingSec, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(snapshotAfterDecay.LastHitLossValue, Is.EqualTo(7));
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

        private static void TickWorld(World world, ComponentSystemGroup simGroup, float deltaTime)
        {
            double elapsed = world.Time.ElapsedTime + deltaTime;
            world.SetTime(new TimeData(elapsed, deltaTime));
            simGroup.Update();
        }

        private static void CreatePlayer(EntityManager em, int load, int capacity)
        {
            var player = em.CreateEntity(typeof(PlayerTag), typeof(PlayerCarryBinComponent));
            em.SetComponentData(player, new PlayerCarryBinComponent
            {
                Load = load,
                Capacity = capacity,
            });
        }

        private static void CreateSnapshotSingleton(EntityManager em)
        {
            var entity = em.CreateEntity(typeof(PlayerHudSnapshotComponent));
            em.SetComponentData(entity, default(PlayerHudSnapshotComponent));
        }

        private static void CreateStageStateSingleton(EntityManager em, RunDirectorStageStateId state, float elapsedSec)
        {
            var entity = em.CreateEntity(typeof(RunDirectorStageStateComponent));
            em.SetComponentData(entity, new RunDirectorStageStateComponent
            {
                State = state,
                StateElapsedSec = elapsedSec,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });
        }

        private static void CreateCombatMetricsSingleton(EntityManager em, int hitCount, int hitValue)
        {
            var entity = em.CreateEntity(typeof(CombatEventMetricsComponent));
            em.SetComponentData(entity, new CombatEventMetricsComponent
            {
                LastFrameHitCount = hitCount,
                LastFrameHitValue = hitValue,
            });
        }

        private static void CreateSource(
            EntityManager em,
            uint stableId,
            SourceStateId state,
            RunDirectorSourceStateId directorState,
            int collected,
            int thresholdDepleted)
        {
            var entity = em.CreateEntity(
                typeof(SourceSpawnComponent),
                typeof(SourceStableIdComponent),
                typeof(SourceRunDirectorStateComponent));

            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 0,
                ThresholdDepleted = thresholdDepleted,
                CollectedCount = collected,
                State = state,
            });
            em.SetComponentData(entity, new SourceStableIdComponent
            {
                Value = stableId,
            });
            em.SetComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = directorState,
                SelectedClipState = state,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 0u,
            });
        }

        private static T GetSingleton<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingleton<T>();
        }

        private static Entity GetSingletonEntity<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.GetSingletonEntity();
        }
    }
}
