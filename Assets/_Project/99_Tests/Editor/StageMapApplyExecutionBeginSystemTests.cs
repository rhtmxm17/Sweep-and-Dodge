using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageMapApplyExecutionBeginSystemTests
    {
        [Test]
        public void StageMapApply_RequestApplied_MapsAndDisablesByStableId()
        {
            using var world = CreateDefaultTestWorld("StageMapApplyWorld_A", out var simGroup);
            var em = world.EntityManager;

            var sourceMappedActive = CreateSource(em, stableId: 1001u, position: new float3(-4f, 0f, -2f), shape: BulletFieldShapeId.Circle, radius: 3f, size: new float2(3f, 3f), state: SourceStateId.Weakened);
            var sourceMappedInactive = CreateSource(em, stableId: 1002u, position: new float3(9f, 0f, 9f), shape: BulletFieldShapeId.Circle, radius: 2f, size: new float2(2f, 2f), state: SourceStateId.Normal);
            var sourceUnmapped = CreateSource(em, stableId: 9001u, position: new float3(12f, 0f, 12f), shape: BulletFieldShapeId.Circle, radius: 1f, size: new float2(1f, 1f), state: SourceStateId.Normal);

            var depositMappedActive = CreateDeposit(em, stableId: 2001u, position: new float3(-8f, 0f, 0f), radius: 10f);
            var depositMappedInactive = CreateDeposit(em, stableId: 2002u, position: new float3(5f, 0f, 5f), radius: 2f);
            var depositUnmapped = CreateDeposit(em, stableId: 9002u, position: new float3(7f, 0f, 7f), radius: 3f);

            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            try
            {
                catalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 1,
                        Sources = new[]
                        {
                            new StageSourceLayoutData
                            {
                                StableId = 1001,
                                Active = true,
                                Position = new Vector3(1f, 0f, 2f),
                                YawDeg = 90f,
                                FieldShape = BulletFieldShapeId.Rectangle,
                                FieldRadius = 0f,
                                FieldSize = new Vector2(12f, 8f),
                            },
                            new StageSourceLayoutData
                            {
                                StableId = 1002,
                                Active = false,
                                Position = new Vector3(3f, 0f, 4f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Circle,
                                FieldRadius = 6f,
                                FieldSize = new Vector2(0f, 0f),
                            },
                        },
                        Deposits = new[]
                        {
                            new StageDepositLayoutData
                            {
                                StableId = 2001,
                                Active = true,
                                Position = new Vector3(0f, 0f, 10f),
                                Radius = 5f,
                            },
                            new StageDepositLayoutData
                            {
                                StableId = 2002,
                                Active = false,
                                Position = new Vector3(2f, 0f, 2f),
                                Radius = 1f,
                            },
                        },
                    }
                };

                var runtimeEntity = em.CreateEntity();
                em.AddComponentObject(runtimeEntity, new StageMapCatalogRuntimeComponent
                {
                    Catalog = catalog,
                });

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    RequestedStageId = 1,
                    StageMapApplyRequested = 1,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.StageMapApplyRequested, Is.EqualTo(0));
                Assert.That(request.RequestedStageId, Is.EqualTo(1));

                var source1 = em.GetComponentData<SourceSpawnComponent>(sourceMappedActive);
                var anchor1 = em.GetComponentData<SourceAnchorComponent>(sourceMappedActive);
                var area1 = em.GetComponentData<BulletFieldAreaComponent>(sourceMappedActive);
                var tx1 = em.GetComponentData<LocalTransform>(sourceMappedActive);
                Assert.That(source1.State, Is.EqualTo(SourceStateId.Normal));
                Assert.That(source1.CollectedCount, Is.EqualTo(0));
                Assert.That(anchor1.Position.x, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(anchor1.Position.z, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(tx1.Position.x, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(tx1.Position.z, Is.EqualTo(2f).Within(1e-4f));
                Assert.That(area1.Shape, Is.EqualTo(BulletFieldShapeId.Rectangle));
                Assert.That(area1.Size.x, Is.EqualTo(12f).Within(1e-4f));
                Assert.That(area1.Size.y, Is.EqualTo(8f).Within(1e-4f));

                var source2 = em.GetComponentData<SourceSpawnComponent>(sourceMappedInactive);
                Assert.That(source2.State, Is.EqualTo(SourceStateId.Depleted));

                var source3 = em.GetComponentData<SourceSpawnComponent>(sourceUnmapped);
                Assert.That(source3.State, Is.EqualTo(SourceStateId.Depleted));

                var deposit1 = em.GetComponentData<DepositPointComponent>(depositMappedActive);
                var deposit1Tx = em.GetComponentData<LocalTransform>(depositMappedActive);
                Assert.That(deposit1.Radius, Is.EqualTo(5f).Within(1e-4f));
                Assert.That(deposit1Tx.Position.z, Is.EqualTo(10f).Within(1e-4f));

                var deposit2 = em.GetComponentData<DepositPointComponent>(depositMappedInactive);
                var deposit2Tx = em.GetComponentData<LocalTransform>(depositMappedInactive);
                Assert.That(deposit2.Radius, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(deposit2Tx.Position.y, Is.LessThan(-9999f));

                var deposit3 = em.GetComponentData<DepositPointComponent>(depositUnmapped);
                var deposit3Tx = em.GetComponentData<LocalTransform>(depositUnmapped);
                Assert.That(deposit3.Radius, Is.EqualTo(0f).Within(1e-4f));
                Assert.That(deposit3Tx.Position.y, Is.LessThan(-9999f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void StageMapApply_MissingStage_ConsumesRequestAndKeepsLayout()
        {
            using var world = CreateDefaultTestWorld("StageMapApplyWorld_B", out var simGroup);
            var em = world.EntityManager;

            var source = CreateSource(em, stableId: 1001u, position: new float3(7f, 0f, 8f), shape: BulletFieldShapeId.Circle, radius: 2f, size: new float2(2f, 2f), state: SourceStateId.Normal);

            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            try
            {
                catalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 1,
                        Sources = new[]
                        {
                            new StageSourceLayoutData
                            {
                                StableId = 1001,
                                Active = true,
                                Position = new Vector3(1f, 0f, 1f),
                                YawDeg = 0f,
                                FieldShape = BulletFieldShapeId.Rectangle,
                                FieldRadius = 0f,
                                FieldSize = new Vector2(5f, 4f),
                            },
                        },
                    }
                };

                var runtimeEntity = em.CreateEntity();
                em.AddComponentObject(runtimeEntity, new StageMapCatalogRuntimeComponent
                {
                    Catalog = catalog,
                });

                var requestEntity = em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
                {
                    RequestedStageId = 999,
                    StageMapApplyRequested = 1,
                });

                var beforeSource = em.GetComponentData<SourceSpawnComponent>(source);
                var beforeAnchor = em.GetComponentData<SourceAnchorComponent>(source);
                var beforeArea = em.GetComponentData<BulletFieldAreaComponent>(source);

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                simGroup.Update();

                var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
                Assert.That(request.StageMapApplyRequested, Is.EqualTo(0));
                Assert.That(request.RequestedStageId, Is.EqualTo(999));

                var afterSource = em.GetComponentData<SourceSpawnComponent>(source);
                var afterAnchor = em.GetComponentData<SourceAnchorComponent>(source);
                var afterArea = em.GetComponentData<BulletFieldAreaComponent>(source);
                Assert.That(afterSource.State, Is.EqualTo(beforeSource.State));
                Assert.That(afterAnchor.Position.x, Is.EqualTo(beforeAnchor.Position.x).Within(1e-4f));
                Assert.That(afterAnchor.Position.z, Is.EqualTo(beforeAnchor.Position.z).Within(1e-4f));
                Assert.That(afterArea.Shape, Is.EqualTo(beforeArea.Shape));
                Assert.That(afterArea.Radius, Is.EqualTo(beforeArea.Radius).Within(1e-4f));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
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

        private static Entity CreateSource(
            EntityManager em,
            uint stableId,
            float3 position,
            BulletFieldShapeId shape,
            float radius,
            float2 size,
            SourceStateId state)
        {
            var entity = em.CreateEntity(
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new SourceStableIdComponent
            {
                Value = stableId,
            });
            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 100,
                ThresholdDepleted = 200,
                CollectedCount = 42,
                State = state,
            });
            em.SetComponentData(entity, new SourceAnchorComponent
            {
                Position = position,
            });
            em.SetComponentData(entity, new BulletFieldAreaComponent
            {
                Shape = shape,
                Radius = radius,
                Size = size,
                ComputedArea = 0f,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));

            return entity;
        }

        private static Entity CreateDeposit(EntityManager em, uint stableId, float3 position, float radius)
        {
            var entity = em.CreateEntity(
                typeof(DepositStableIdComponent),
                typeof(DepositPointComponent),
                typeof(LocalTransform));
            em.SetComponentData(entity, new DepositStableIdComponent
            {
                Value = stableId,
            });
            em.SetComponentData(entity, new DepositPointComponent
            {
                Radius = radius,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(position, quaternion.identity, 1f));
            return entity;
        }
    }
}
