using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class ObstacleConsumptionSystemsTests
    {
        [Test]
        public void PlayerObstacleBlock_AppliesZAxisSlide_WhenFullMoveBlocked()
        {
            using var world = new World("PlayerObstacle_XSlide");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None, StageCellMovementFlags.BlockPlayer,
                StageCellMovementFlags.None, StageCellMovementFlags.BlockPlayer,
            }, width: 2, height: 2);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.1f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_AppliesXAxisSlide_WhenFullMoveBlocked()
        {
            using var world = new World("PlayerObstacle_ZSlide");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None, StageCellMovementFlags.None,
                StageCellMovementFlags.BlockPlayer, StageCellMovementFlags.BlockPlayer,
            }, width: 2, height: 2);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.1f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_RollsBack_WhenBothSlideCandidatesBlocked()
        {
            using var world = new World("PlayerObstacle_Rollback");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None, StageCellMovementFlags.BlockPlayer,
                StageCellMovementFlags.BlockPlayer, StageCellMovementFlags.BlockPlayer,
            }, width: 2, height: 2);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.1f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_NoOp_WhenTopologyNotReady()
        {
            using var world = new World("PlayerObstacle_NoOp");
            var em = world.EntityManager;

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
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            SetRuntimeGrid(em, new[] { StageCellMovementFlags.BlockPlayer });
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(0.5f, 0f, 0.5f), radius: 0.1f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_RollsBack_WhenSweptPathCrossesBlockedCell()
        {
            using var world = new World("PlayerObstacle_SweptRollback");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockPlayer,
                StageCellMovementFlags.None,
                StageCellMovementFlags.None,
            }, width: 4, height: 1);
            var player = CreatePlayer(em, prev: new float3(0.1f, 0f, 0.5f), current: new float3(3.2f, 0f, 0.5f), radius: 0.05f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_RollsBack_WhenRadiusTouchesNeighborBlockedCell()
        {
            using var world = new World("PlayerObstacle_RadiusNeighbor");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockPlayer,
            }, width: 2, height: 1);
            var player = CreatePlayer(em, prev: new float3(0.2f, 0f, 0.5f), current: new float3(0.75f, 0f, 0.5f), radius: 0.3f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_IgnoresBlockBulletOnlyCell()
        {
            using var world = new World("PlayerObstacle_IgnoreBulletMask");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            SetRuntimeGrid(em, new[]
            {
                StageCellMovementFlags.None,
                StageCellMovementFlags.BlockBullet,
            }, width: 2, height: 1);
            var player = CreatePlayer(em, prev: new float3(0.1f, 0f, 0.5f), current: new float3(1.1f, 0f, 0.5f), radius: 0.05f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(1.1f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0.5f).Within(0.001f));
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
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                HasStep = 1,
                UsingFixedTick = 0,
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

        private static Entity CreatePlayer(EntityManager em, float3 prev, float3 current, float radius)
        {
            var entity = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerRadiusComponent),
                typeof(PlayerPreviousPositionComponent),
                typeof(LocalTransform),
                typeof(PlayerGoSyncComponent));
            em.SetComponentData(entity, new PlayerRadiusComponent { Value = radius });
            em.SetComponentData(entity, new PlayerPreviousPositionComponent { Position = prev });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(current, quaternion.identity, 1f));
            em.SetComponentData(entity, new PlayerGoSyncComponent
            {
                Position = current,
                Rotation = quaternion.identity,
                SyncRotation = 1,
            });
            return entity;
        }

        private static void SetSingleton<T>(EntityManager em, T value) where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }
    }
}
