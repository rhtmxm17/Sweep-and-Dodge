using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class BulletDefinitionSchemaTests
    {
        [SetUp]
        public void SetUp()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [TearDown]
        public void TearDown()
        {
            ForceDisposeSharedContainersIfNeeded();
        }

        [Test]
        public void PoolBootstrap_AppliesMovementMetadata_FromDefinitionBuffer()
        {
            using var world = new World("BulletDefinitionSchema_Apply");
            var em = world.EntityManager;

            CreateConfigAndPlayer(em);
            var prefab = CreateBulletPrefab(em, includeLegacyBehaviorComponents: false);
            var registry = em.CreateEntity(typeof(BulletPoolRegistryTag));
            var defs = em.AddBuffer<BulletPoolDefinitionBuffer>(registry);
            defs.Add(new BulletPoolDefinitionBuffer
            {
                TypeKey = 501,
                Prefab = prefab,
                PoolSize = 1,
                CaptureRule = BulletCaptureRuleId.StandardCollectible,
                Speed = 2f,
                Lifetime = 5f,
                Radius = 0.2f,
                ScoreValue = 3,
                MovementFamily = BulletMovementFamilyId.DampedLinear,
                DampedLinear = new BulletDampedLinearDefinition
                {
                    DampingPerSec = 4f,
                    StopSpeedThreshold = 0.25f,
                },
                HomingLite = default,
            });

            world.GetOrCreateSystem<BulletPoolOwnerBootstrapSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(BulletFieldShared.FreeByKey.TryGetFirstValue(501, out var bullet, out var iterator), Is.True);
            Assert.That(em.HasComponent<BulletDampedMotionComponent>(bullet), Is.True);
            Assert.That(em.HasComponent<BulletHomingLiteMotionComponent>(bullet), Is.False);

            var damped = em.GetComponentData<BulletDampedMotionComponent>(bullet);
            Assert.That(damped.DampingPerSec, Is.EqualTo(4f));
            Assert.That(damped.StopSpeedThreshold, Is.EqualTo(0.25f));
        }

        [Test]
        public void PoolBootstrap_RemovesLegacyOptionalBehaviorComponents_WhenDefinitionDoesNotUseThem()
        {
            using var world = new World("BulletDefinitionSchema_RemoveLegacy");
            var em = world.EntityManager;

            CreateConfigAndPlayer(em);
            var prefab = CreateBulletPrefab(em, includeLegacyBehaviorComponents: true);
            var registry = em.CreateEntity(typeof(BulletPoolRegistryTag));
            var defs = em.AddBuffer<BulletPoolDefinitionBuffer>(registry);
            defs.Add(new BulletPoolDefinitionBuffer
            {
                TypeKey = 502,
                Prefab = prefab,
                PoolSize = 1,
                CaptureRule = BulletCaptureRuleId.StandardCollectible,
                Speed = 1f,
                Lifetime = 3f,
                Radius = 0.1f,
                ScoreValue = 1,
                MovementFamily = BulletMovementFamilyId.Linear,
                DampedLinear = default,
                HomingLite = default,
            });

            world.GetOrCreateSystem<BulletPoolOwnerBootstrapSystem>().Update(world.Unmanaged);
            em.CompleteAllTrackedJobs();

            Assert.That(BulletFieldShared.FreeByKey.TryGetFirstValue(502, out var bullet, out var iterator), Is.True);
            Assert.That(em.HasComponent<BulletDampedMotionComponent>(bullet), Is.False);
            Assert.That(em.HasComponent<BulletHomingLiteMotionComponent>(bullet), Is.False);
        }

        private static void CreateConfigAndPlayer(EntityManager em)
        {
            var configEntity = em.CreateEntity(typeof(BulletFieldConfigComponent), typeof(MetaScrapComponent));
            em.SetComponentData(configEntity, new BulletFieldConfigComponent
            {
                PoolSize = 16,
                InvCellSize = 1f,
            });
            em.SetComponentData(configEntity, new MetaScrapComponent { Value = 0 });
            em.CreateEntity(typeof(PlayerTag));
        }

        private static Entity CreateBulletPrefab(EntityManager em, bool includeLegacyBehaviorComponents)
        {
            var prefab = em.CreateEntity();
            em.AddComponent<Prefab>(prefab);
            em.AddComponent<LocalTransform>(prefab);
            em.AddComponent<BulletVelocityComponent>(prefab);
            em.AddComponent<BulletSpeedComponent>(prefab);
            em.AddComponent<BulletLifetimeComponent>(prefab);
            em.AddComponent<BulletLifetimeMaxComponent>(prefab);
            em.AddComponent<BulletRadiusComponent>(prefab);
            em.AddComponent<BulletScoreValueComponent>(prefab);
            em.AddComponent<BulletTypeKeyComponent>(prefab);
            em.AddComponent<BulletCaptureRuleComponent>(prefab);
            em.AddComponent<BulletLifecycleRequestComponent>(prefab);
            em.AddComponent<BulletLifecycleContactComponent>(prefab);
            em.AddComponent<BulletSourceRefComponent>(prefab);
            em.AddComponent<BulletLifecycleTraceComponent>(prefab);
            em.AddComponent<BulletActiveTag>(prefab);
            em.AddComponent<BulletDespawnRequestTag>(prefab);
            em.AddComponent<BulletHazardTag>(prefab);
            em.AddBuffer<EntityRenderElementBuffer>(prefab);

            em.SetComponentData(prefab, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            em.SetComponentData(prefab, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(prefab, new BulletSpeedComponent { Value = 0f });
            em.SetComponentData(prefab, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(prefab, new BulletLifetimeMaxComponent { Value = 0f });
            em.SetComponentData(prefab, new BulletRadiusComponent { Value = 0f });
            em.SetComponentData(prefab, new BulletScoreValueComponent { Value = 0 });
            em.SetComponentData(prefab, new BulletTypeKeyComponent { Value = 0 });
            em.SetComponentData(prefab, new BulletCaptureRuleComponent { Value = BulletCaptureRuleId.StandardCollectible });
            em.SetComponentData(prefab, new BulletLifecycleRequestComponent
            {
                Reason = BulletLifecycleReasonId.None,
                Priority = 0,
                RelatedEntity = Entity.Null,
                Frame = 0u,
            });
            em.SetComponentData(prefab, default(BulletLifecycleContactComponent));
            em.SetComponentData(prefab, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(prefab, new BulletLifecycleTraceComponent { LastSpawnFrame = 0u, LastDespawnFrame = 0u });

            if (includeLegacyBehaviorComponents)
            {
                em.AddComponentData(prefab, new BulletDampedMotionComponent
                {
                    DampingPerSec = 1f,
                    StopSpeedThreshold = 0.1f,
                });
                em.AddComponentData(prefab, new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = 90f,
                    MaxAcquireDistance = 10f,
                    MinRetargetDistance = 0.25f,
                });
            }

            return prefab;
        }

        private static void ForceDisposeSharedContainersIfNeeded()
        {
            if (!BulletFieldShared.IsInitialized)
                return;

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
    }
}
