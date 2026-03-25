using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletSimulationBlockTests
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
        public void BulletSimulation_EnableDespawn_WhenSweptPathEntersBlockedCell()
        {
            using var world = new World("BulletSimulation_BlockHit");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockBullet,
            }, width: 2, height: 1);
            var bullet = CreateBullet(em, new float3(0.25f, 0f, 0.5f), new float2(1f, 0f), radius: 0.05f, lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
        }

        [Test]
        public void BulletSimulation_IgnoresBlockPlayerOnlyCell()
        {
            using var world = new World("BulletSimulation_BlockPlayerOnly");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.BlockPlayer });
            var bullet = CreateBullet(em, new float3(0.2f, 0f, 0.2f), new float2(0.1f, 0f), radius: 0.05f, lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        [Test]
        public void BulletSimulation_DoesNotEvaluateBlock_WhenTopologyNotReadyAndStageIdle()
        {
            using var world = new World("BulletSimulation_Gated");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetSingleton(em, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 0,
                Ready = 0,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
            });
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.BlockBullet });
            var bullet = CreateBullet(em, new float3(0.2f, 0f, 0.2f), new float2(0.5f, 0f), radius: 0.05f, lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        [Test]
        public void BulletSimulation_EnableDespawn_WhenSweptPathCrossesMultipleCells()
        {
            using var world = new World("BulletSimulation_MultiCellSweep");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockBullet,
                StageCellMovementFlags.None,
            }, width: 4, height: 1);
            var bullet = CreateBullet(em, new float3(0.1f, 0f, 0.5f), new float2(3.2f, 0f), radius: 0.05f, lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
        }

        [Test]
        public void BulletSimulation_LeavesExistingDespawnRequestEnabled()
        {
            using var world = new World("BulletSimulation_RequestAlreadyEnabled");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.BlockBullet });
            var bullet = CreateBullet(em, new float3(0.2f, 0f, 0.2f), new float2(0.5f, 0f), radius: 0.05f, lifetime: 5f, despawnRequested: true);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
        }

        [Test]
        public void BulletSimulation_RadiusSweepCoversBlockedNeighborCell()
        {
            using var world = new World("BulletSimulation_RadiusCoverage");
            var em = world.EntityManager;

            SetSimulationPrerequisites(em);
            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockBullet,
            }, width: 1, height: 2);
            var bullet = CreateBullet(em, new float3(0.5f, 0f, 0.85f), new float2(0.2f, 0f), radius: 0.2f, lifetime: 5f);

            world.GetOrCreateSystem<BulletSimulationSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
        }

        private static void SetSimulationPrerequisites(EntityManager em)
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
            em.CreateEntity(typeof(PlayerTag));
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
            float radius,
            float lifetime,
            bool despawnRequested = false)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletVelocityComponent),
                typeof(BulletRadiusComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentData(entity, new BulletVelocityComponent { Value = velocity });
            em.SetComponentData(entity, new BulletRadiusComponent { Value = radius });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = lifetime });
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
    }
}
