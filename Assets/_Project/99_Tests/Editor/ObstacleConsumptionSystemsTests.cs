using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class ObstacleConsumptionSystemsTests
    {
        [Test]
        public void ObstacleGeometryUtility_ContainsPointXZ_WorksForCircleAndRotatedBox()
        {
            var circleTx = LocalTransform.FromPositionRotationScale(new float3(1f, 0f, 2f), quaternion.identity, 1f);
            var circle = new ObstacleGeometryComponent
            {
                Shape = ObstacleShape.Circle,
                Radius = 2f,
                Size = float2.zero,
            };
            Assert.That(ObstacleGeometryUtility.ContainsPointXZ(new float2(2f, 2f), in circleTx, in circle), Is.True);
            Assert.That(ObstacleGeometryUtility.ContainsPointXZ(new float2(4.1f, 2f), in circleTx, in circle), Is.False);

            var boxTx = LocalTransform.FromPositionRotationScale(
                new float3(0f, 0f, 0f),
                quaternion.RotateY(math.radians(90f)),
                1f);
            var box = new ObstacleGeometryComponent
            {
                Shape = ObstacleShape.Box,
                Radius = 0f,
                Size = new float2(4f, 2f),
            };
            Assert.That(ObstacleGeometryUtility.ContainsPointXZ(new float2(0.5f, -1.5f), in boxTx, in box), Is.True);
            Assert.That(ObstacleGeometryUtility.ContainsPointXZ(new float2(1.6f, 0f), in boxTx, in box), Is.False);
        }

        [Test]
        public void ObstacleGeometryUtility_OverlapsCircleXZ_WorksForCircleAndRotatedBox()
        {
            var circleTx = LocalTransform.FromPositionRotationScale(new float3(0f, 0f, 0f), quaternion.identity, 1f);
            var circle = new ObstacleGeometryComponent
            {
                Shape = ObstacleShape.Circle,
                Radius = 1f,
                Size = float2.zero,
            };
            Assert.That(ObstacleGeometryUtility.OverlapsCircleXZ(new float2(1.2f, 0f), 0.3f, in circleTx, in circle), Is.True);
            Assert.That(ObstacleGeometryUtility.OverlapsCircleXZ(new float2(1.5f, 0f), 0.2f, in circleTx, in circle), Is.False);

            var boxTx = LocalTransform.FromPositionRotationScale(
                new float3(0f, 0f, 0f),
                quaternion.RotateY(math.radians(45f)),
                1f);
            var box = new ObstacleGeometryComponent
            {
                Shape = ObstacleShape.Box,
                Radius = 0f,
                Size = new float2(2f, 2f),
            };
            Assert.That(ObstacleGeometryUtility.OverlapsCircleXZ(new float2(0.9f, 0f), 0.3f, in boxTx, in box), Is.True);
            Assert.That(ObstacleGeometryUtility.OverlapsCircleXZ(new float2(2f, 0f), 0.2f, in boxTx, in box), Is.False);
        }

        [Test]
        public void BulletObstacleHit_EnableDespawn_WhenPointInsideBlockBulletObstacle()
        {
            using var world = new World("BulletObstacleHit_Enable");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            var obstacle = CreateObstacle(em, new float3(0f, 0f, 0f), ObstacleShape.Box, radius: 0f, size: new float2(2f, 2f), ObstacleCollisionMask.BlockBullet);
            var bullet = CreateBullet(em, new float3(0.4f, 0f, 0.4f));

            world.GetOrCreateSystem<BulletObstacleHitRequestSystem>().Update(world.Unmanaged);

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.True);
        }

        [Test]
        public void BulletObstacleHit_IgnoresBlockPlayerOnlyObstacle_AndTopologyNotReady()
        {
            using var world = new World("BulletObstacleHit_NoOp");
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

            CreateObstacle(em, new float3(0f, 0f, 0f), ObstacleShape.Box, radius: 0f, size: new float2(2f, 2f), ObstacleCollisionMask.BlockPlayer);
            var bullet = CreateBullet(em, new float3(0.2f, 0f, 0.2f));

            world.GetOrCreateSystem<BulletObstacleHitRequestSystem>().Update(world.Unmanaged);

            Assert.That(em.IsComponentEnabled<BulletDespawnRequestTag>(bullet), Is.False);
        }

        [Test]
        public void PlayerObstacleBlock_AppliesXAxisSlide_WhenFullMoveBlocked()
        {
            using var world = new World("PlayerObstacle_XSlide");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            CreateObstacle(em, new float3(0.45f, 0f, 0.8f), ObstacleShape.Circle, radius: 0.45f, size: float2.zero, ObstacleCollisionMask.BlockPlayer);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.25f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_AppliesZAxisSlide_WhenFullMoveBlocked()
        {
            using var world = new World("PlayerObstacle_ZSlide");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            CreateObstacle(em, new float3(0.8f, 0f, 0.45f), ObstacleShape.Circle, radius: 0.45f, size: float2.zero, ObstacleCollisionMask.BlockPlayer);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.25f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_RollsBack_WhenBothSlideCandidatesBlocked()
        {
            using var world = new World("PlayerObstacle_Rollback");
            var em = world.EntityManager;

            SetGameplayReadySingletons(em);
            CreateObstacle(em, new float3(0.5f, 0f, 0.5f), ObstacleShape.Circle, radius: 0.6f, size: float2.zero, ObstacleCollisionMask.BlockPlayer);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(1f, 0f, 1f), radius: 0.25f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(tx.Position.z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void PlayerObstacleBlock_NoOp_WhenTopologyNotReady_OrObstacleDoesNotBlockPlayer()
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

            CreateObstacle(em, new float3(0f, 0f, 0f), ObstacleShape.Box, radius: 0f, size: new float2(2f, 2f), ObstacleCollisionMask.BlockBullet);
            var player = CreatePlayer(em, prev: new float3(0f, 0f, 0f), current: new float3(0.5f, 0f, 0.5f), radius: 0.25f);

            world.GetOrCreateSystem<PlayerObstacleBlockSystem>().Update(world.Unmanaged);

            var tx = em.GetComponentData<LocalTransform>(player);
            Assert.That(tx.Position.x, Is.EqualTo(0.5f).Within(0.001f));
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

        private static Entity CreateObstacle(
            EntityManager em,
            float3 position,
            ObstacleShape shape,
            float radius,
            float2 size,
            ObstacleCollisionMask mask)
        {
            var entity = em.CreateEntity(
                typeof(StageTopologyObstacleTag),
                typeof(ObstacleStableIdComponent),
                typeof(ObstacleCollisionMaskComponent),
                typeof(ObstacleGeometryComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new ObstacleStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new ObstacleCollisionMaskComponent { Value = mask });
            em.SetComponentData(entity, new ObstacleGeometryComponent
            {
                Shape = shape,
                Radius = radius,
                Size = size,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }

        private static Entity CreateBullet(EntityManager em, float3 position)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            em.SetComponentEnabled<BulletActiveTag>(entity, true);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
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
