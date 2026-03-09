using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageTopologyApplyPrepareSystemTests
    {
        [Test]
        public void StageTopologyBootstrap_CreatesTopologySingletons_WithoutBulletBootstrap()
        {
            using var world = new World("StageTopologyBootstrapWorld");
            var em = world.EntityManager;

            world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);

            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyStateComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyLifecycleStateComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageCatalogRuntimeComponent>()).IsEmptyIgnoreFilter, Is.False);
        }

        [Test]
        public void StageTopologyApply_InstantiatesOwnedSourceAndDeposit_WhenNoPrebakedEntitiesExist()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_A");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = CreateStageCatalog(createdAssets, stageId: 1, sourceStableIds: new uint[] { 1001u, 1002u }, depositStableIds: new uint[] { 2001u });
                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, new StageTopologyRequestComponent
                {
                    RequestedStageId = 1,
                    ApplyRequested = 1,
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(1));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(1));

                using var sourceQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologySourceTag>(),
                        ComponentType.ReadOnly<SourceStableIdComponent>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var depositQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologyDepositTag>(),
                        ComponentType.ReadOnly<DepositStableIdComponent>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var sourceEntities = sourceQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                using var depositEntities = depositQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

                Assert.That(sourceEntities.Length, Is.EqualTo(2));
                Assert.That(depositEntities.Length, Is.EqualTo(1));

                var sourceA = FindByStableId<SourceStableIdComponent>(em, sourceEntities, 1001u, c => c.Value);
                var sourceB = FindByStableId<SourceStableIdComponent>(em, sourceEntities, 1002u, c => c.Value);
                var deposit = FindByStableId<DepositStableIdComponent>(em, depositEntities, 2001u, c => c.Value);
                var lifecycle = em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em));

                Assert.That(em.IsEnabled(sourceA), Is.True);
                Assert.That(em.IsEnabled(sourceB), Is.True);
                Assert.That(em.IsEnabled(deposit), Is.True);
                Assert.That(lifecycle.CurrentAppliedVersion, Is.EqualTo(1u));
                AssertTopologyOwned(em, sourceA, StageTopologyKind.Source, 1u);
                AssertTopologyOwned(em, sourceB, StageTopologyKind.Source, 1u);
                AssertTopologyOwned(em, deposit, StageTopologyKind.Deposit, 1u);
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(sourceA).Length, Is.GreaterThan(0));
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(sourceB).Length, Is.GreaterThan(0));
                Assert.That(em.GetComponentData<DepositPointComponent>(deposit).Radius, Is.GreaterThan(0f));
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_ReusesDisabledOwnedEntities_AcrossStageSwitches()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_B");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    CreateEntry(createdAssets, 1, new uint[] { 1001u, 1002u }, new uint[] { 2001u }),
                    CreateEntry(createdAssets, 2, new uint[] { 1101u }, new uint[] { 2101u }),
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, default(StageTopologyRequestComponent));

                ApplyStage(world, 1);
                using var initialSourceQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologySourceTag>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var initialSources = initialSourceQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                using var initialDepositQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologyDepositTag>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var initialDeposits = initialDepositQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                var sourceEntityA = initialSources[0];
                var sourceEntityB = initialSources[1];
                var depositEntity = initialDeposits[0];
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 2, expectedDepositTotal: 1, expectedDepositEnabled: 1);
                Assert.That(em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em)).CurrentAppliedVersion, Is.EqualTo(1u));

                ApplyStage(world, 2);
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 1, expectedDepositTotal: 1, expectedDepositEnabled: 1);
                Assert.That(em.Exists(sourceEntityA), Is.True);
                Assert.That(em.Exists(sourceEntityB), Is.True);
                Assert.That(em.Exists(depositEntity), Is.True);
                Assert.That(em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em)).CurrentAppliedVersion, Is.EqualTo(2u));

                ApplyStage(world, 1);
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 2, expectedDepositTotal: 1, expectedDepositEnabled: 1);
                Assert.That(em.Exists(sourceEntityA), Is.True);
                Assert.That(em.Exists(sourceEntityB), Is.True);
                Assert.That(em.Exists(depositEntity), Is.True);
                Assert.That(em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em)).CurrentAppliedVersion, Is.EqualTo(3u));
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_InstantiatesOwnedObstacle_WhenLayoutContainsObstacle()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_ObstacleCreate");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = CreateStageCatalog(createdAssets, stageId: 1, sourceStableIds: new uint[] { 1001u }, depositStableIds: new uint[] { 2001u }, obstacleStableIds: new uint[] { 3001u });
                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                    ObstacleTemplate = CreateObstacleTemplate(em),
                });
                SetSingleton(em, new StageTopologyRequestComponent
                {
                    RequestedStageId = 1,
                    ApplyRequested = 1,
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.Ready, Is.EqualTo(1));
                Assert.That(IsEnabledObstacleStableIdPresent(em, 3001u), Is.True);

                using var obstacleQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                        ComponentType.ReadOnly<ObstacleStableIdComponent>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var obstacles = obstacleQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Assert.That(obstacles.Length, Is.EqualTo(1));

                var obstacle = FindByStableId<ObstacleStableIdComponent>(em, obstacles, 3001u, c => c.Value);
                var geometry = em.GetComponentData<ObstacleGeometryComponent>(obstacle);
                var mask = em.GetComponentData<ObstacleCollisionMaskComponent>(obstacle);

                Assert.That(geometry.Shape, Is.EqualTo(ObstacleShape.Box));
                Assert.That(geometry.Size.x, Is.EqualTo(3f).Within(0.01f));
                Assert.That(geometry.Size.y, Is.EqualTo(2f).Within(0.01f));
                Assert.That(mask.Value, Is.EqualTo(ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet));
                AssertTopologyOwned(em, obstacle, StageTopologyKind.Obstacle, 1u);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_MissingObstacleTemplate_KeepsPreviouslyAppliedTopology_AndMarksNewSelectionNotReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_ObstacleTemplateMissing");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    CreateEntry(createdAssets, 1, new uint[] { 1001u }, new uint[] { 2001u }, new uint[] { 3001u }),
                    CreateEntry(createdAssets, 2, new uint[] { 1101u }, new uint[] { 2101u }, new uint[] { 3101u }),
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                    ObstacleTemplate = CreateObstacleTemplate(em),
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, default(StageTopologyRequestComponent));

                ApplyStage(world, 1);
                Assert.That(IsEnabledObstacleStableIdPresent(em, 3001u), Is.True);

                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                    ObstacleTemplate = Entity.Null,
                });

                LogAssert.Expect(LogType.Warning, "[StageTopologyApply] Obstacle template prefab is missing. stageId=2");
                ApplyStage(world, 2);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                var lifecycleState = em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em));
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(2));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(0));
                Assert.That(lifecycleState.CurrentAppliedVersion, Is.EqualTo(1u));
                Assert.That(IsEnabledObstacleStableIdPresent(em, 3001u), Is.True);
                Assert.That(IsEnabledObstacleStableIdPresent(em, 3101u), Is.False);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_InvalidObstacleItem_WarnsAndSkipsObstacleButKeepsReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_InvalidObstacle");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var layout = CreateLayout(createdAssets, 1, new uint[] { 1001u }, new uint[] { 2001u }, new uint[] { 3001u });
                layout.Obstacles[0] = new StageObstacleLayoutData
                {
                    StableId = 3001u,
                    Active = true,
                    Position = new Vector3(2f, 0f, 1f),
                    EulerRotation = Vector3.zero,
                    Shape = ObstacleShape.Circle,
                    Radius = 0f,
                    Size = Vector2.zero,
                    CollisionMask = ObstacleCollisionMask.BlockBullet,
                };

                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_01",
                        Definition = CreateDefinition(createdAssets, 1, new uint[] { 1001u }),
                        Layout = layout,
                    },
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                    ObstacleTemplate = CreateObstacleTemplate(em),
                });
                SetSingleton(em, new StageTopologyRequestComponent
                {
                    RequestedStageId = 1,
                    ApplyRequested = 1,
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                LogAssert.Expect(LogType.Warning, "[StageTopologyApply] Obstacle item has invalid shape parameters and will be skipped. stageId=1, stableId=3001, shape=Circle");
                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.Ready, Is.EqualTo(1));
                Assert.That(IsEnabledObstacleStableIdPresent(em, 3001u), Is.False);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_MissingDefinitionBinding_DisablesOnlyMissingSource_AndKeepsReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_C");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var definition = CreateDefinition(createdAssets, 1, new uint[] { 1001u });
                var layout = CreateLayout(createdAssets, 1, new uint[] { 1001u, 1002u }, new uint[] { 2001u });
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_01",
                        Definition = definition,
                        Layout = layout,
                    },
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, new StageTopologyRequestComponent
                {
                    RequestedStageId = 1,
                    ApplyRequested = 1,
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.Ready, Is.EqualTo(1));

                using var sourceQuery = em.CreateEntityQuery(new EntityQueryDesc
                {
                    All = new[]
                    {
                        ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                        ComponentType.ReadOnly<StageTopologySourceTag>(),
                        ComponentType.ReadOnly<SourceStableIdComponent>(),
                    },
                    Options = EntityQueryOptions.IncludeDisabledEntities,
                });
                using var sourceEntities = sourceQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                Assert.That(sourceEntities.Length, Is.EqualTo(2));

                var bound = FindByStableId<SourceStableIdComponent>(em, sourceEntities, 1001u, c => c.Value);
                var missing = FindByStableId<SourceStableIdComponent>(em, sourceEntities, 1002u, c => c.Value);
                Assert.That(em.IsEnabled(bound), Is.True);
                Assert.That(em.IsEnabled(missing), Is.False);
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(missing).Length, Is.EqualTo(0));
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [TestCase(RunDirectorStageStateId.Running)]
        [TestCase(RunDirectorStageStateId.ClearReady)]
        public void StageTopologyApply_IgnoresRequest_OutsideBoundary_AndKeepsCurrentTopology(RunDirectorStageStateId blockedState)
        {
            using var world = CreateDefaultTestWorld($"StageTopologyWorld_Blocked_{blockedState}");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    CreateEntry(createdAssets, 1, new uint[] { 1001u, 1002u }, new uint[] { 2001u }),
                    CreateEntry(createdAssets, 2, new uint[] { 1101u }, new uint[] { 2101u }),
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, default(StageTopologyRequestComponent));

                ApplyStage(world, 1);
                Assert.That(IsEnabledSourceStableIdPresent(em, 1001u), Is.True);
                Assert.That(IsEnabledSourceStableIdPresent(em, 1101u), Is.False);

                SetStageState(em, blockedState);
                LogAssert.Expect(LogType.Warning, $"[StageTopologyApply] Ignored topology apply outside stage boundary. stageId=2, stageState={blockedState}");
                ApplyStage(world, 2);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(1));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(1));
                Assert.That(IsEnabledSourceStableIdPresent(em, 1001u), Is.True);
                Assert.That(IsEnabledSourceStableIdPresent(em, 1101u), Is.False);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_InfrastructureFailure_KeepsPreviouslyAppliedTopology_AndMarksNewSelectionNotReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_FailureKeep");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
                createdAssets.Add(catalog);
                catalog.Entries = new[]
                {
                    CreateEntry(createdAssets, 1, new uint[] { 1001u }, new uint[] { 2001u }),
                    CreateEntry(createdAssets, 2, new uint[] { 1101u }, new uint[] { 2101u }),
                };

                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, default(StageTopologyRequestComponent));

                ApplyStage(world, 1);
                Assert.That(IsEnabledSourceStableIdPresent(em, 1001u), Is.True);
                Assert.That(IsEnabledDepositStableIdPresent(em, 2001u), Is.True);

                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = Entity.Null,
                    DepositTemplate = CreateDepositTemplate(em),
                });

                LogAssert.Expect(LogType.Warning, "[StageTopologyApply] Source template prefab is missing. stageId=2");
                ApplyStage(world, 2);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                var lifecycleState = em.GetComponentData<StageTopologyLifecycleStateComponent>(GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(em));
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(2));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(0));
                Assert.That(lifecycleState.CurrentAppliedVersion, Is.EqualTo(1u));
                Assert.That(IsEnabledSourceStableIdPresent(em, 1001u), Is.True);
                Assert.That(IsEnabledDepositStableIdPresent(em, 2001u), Is.True);
                Assert.That(IsEnabledSourceStableIdPresent(em, 1101u), Is.False);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void StageTopologyApply_DuplicateActiveRuntimeStableId_DisablesDuplicatePoolEntries_AndKeepsReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_DuplicateRuntime");
            var em = world.EntityManager;
            var createdAssets = new List<ScriptableObject>();

            try
            {
                var catalog = CreateStageCatalog(createdAssets, stageId: 1, sourceStableIds: new uint[] { 1001u }, depositStableIds: new uint[] { 2001u });
                SetManagedSingleton(em, new StageCatalogRuntimeComponent { Catalog = catalog });
                SetSingleton(em, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                    DepositTemplate = CreateDepositTemplate(em),
                });
                SetSingleton(em, new StageTopologyRequestComponent
                {
                    RequestedStageId = 1,
                    ApplyRequested = 1,
                });
                SetSingleton(em, default(StageTopologyStateComponent));
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                var duplicateA = CreateSourceTemplate(em);
                EnsureOwnedSource(em, duplicateA, 1001u);
                em.SetEnabled(duplicateA, true);

                var duplicateB = CreateSourceTemplate(em);
                EnsureOwnedSource(em, duplicateB, 1001u);
                em.SetEnabled(duplicateB, true);

                LogAssert.Expect(LogType.Warning, "[StageTopologyApply] Duplicate active runtime source stableId detected. stageId=1, duplicateCount=1");
                world.SetTime(new TimeData(1d / 60d, 1f / 60f));
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(GetOrCreateSingletonEntity<StageTopologyStateComponent>(em));
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(1));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(1));
                Assert.That(em.IsEnabled(duplicateA), Is.False);
                Assert.That(em.IsEnabled(duplicateB), Is.False);
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(duplicateA).Length, Is.EqualTo(0));
                Assert.That(em.GetBuffer<SourceClipPatternBuffer>(duplicateB).Length, Is.EqualTo(0));
                Assert.That(IsEnabledDepositStableIdPresent(em, 2001u), Is.True);
            }
            finally
            {
                DestroyAll(createdAssets);
            }
        }

        [Test]
        public void RunDirectorStageTransition_IdleStart_IsBlockedUntilTopologyReady()
        {
            using var world = CreateDefaultTestWorld("StageTopologyWorld_D");
            var em = world.EntityManager;
            var transitionSystem = world.GetOrCreateSystem<RunDirectorStageTransitionSystem>();

            var stageStateEntity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(stageStateEntity, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });

            var topologyStateEntity = GetOrCreateSingletonEntity<StageTopologyStateComponent>(em);
            em.SetComponentData(topologyStateEntity, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 0,
                Ready = 0,
            });

            var gateEntity = GetOrCreateSingletonEntity<RunDirectorStageGateComponent>(em);
            em.SetComponentData(gateEntity, new RunDirectorStageGateComponent
            {
                IntroPresentationDone = 1,
                ClearPresentationDone = 1,
                MinIdleDurationElapsed = 1,
                AutoAdvanceTimeoutElapsed = 0,
            });

            var requestEntity = GetOrCreateSingletonEntity<RunDirectorStageRequestComponent>(em);
            em.SetComponentData(requestEntity, new RunDirectorStageRequestComponent
            {
                StageStartRequested = 1,
                ConfirmPressed = 0,
            });

            GetOrCreateSingletonEntity<RunDirectorStageSignalComponent>(em);
            GetOrCreateSingletonEntity<BulletFrameCounterComponent>(em);
            em.CreateEntity(typeof(SourceRunDirectorStateComponent));

            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            transitionSystem.Update(world.Unmanaged);
            Assert.That(em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity).State, Is.EqualTo(RunDirectorStageStateId.Idle));

            em.SetComponentData(topologyStateEntity, new StageTopologyStateComponent
            {
                SelectedStageId = 1,
                AppliedStageId = 1,
                Ready = 1,
            });
            transitionSystem.Update(world.Unmanaged);

            var stageState = em.GetComponentData<RunDirectorStageStateComponent>(stageStateEntity);
            var request = em.GetComponentData<RunDirectorStageRequestComponent>(requestEntity);
            Assert.That(stageState.State, Is.EqualTo(RunDirectorStageStateId.Running));
            Assert.That(request.StageStartRequested, Is.EqualTo(0));
        }

        private static void ApplyStage(World world, int stageId)
        {
            var em = world.EntityManager;
            var requestEntity = GetOrCreateSingletonEntity<StageTopologyRequestComponent>(em);
            em.SetComponentData(requestEntity, new StageTopologyRequestComponent
            {
                RequestedStageId = stageId,
                ApplyRequested = 1,
            });
            world.SetTime(new TimeData(1d / 60d, 1f / 60f));
            world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);
        }

        private static void SetStageState(EntityManager em, RunDirectorStageStateId state)
        {
            var entity = GetOrCreateSingletonEntity<RunDirectorStageStateComponent>(em);
            em.SetComponentData(entity, new RunDirectorStageStateComponent
            {
                State = state,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });
        }

        private static bool IsEnabledSourceStableIdPresent(EntityManager em, uint stableId)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologySourceTag>(),
                    ComponentType.ReadOnly<SourceStableIdComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<SourceStableIdComponent>(entities[i]).Value != stableId)
                    continue;
                if (em.IsEnabled(entities[i]))
                    return true;
            }

            return false;
        }

        private static bool IsEnabledDepositStableIdPresent(EntityManager em, uint stableId)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologyDepositTag>(),
                    ComponentType.ReadOnly<DepositStableIdComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<DepositStableIdComponent>(entities[i]).Value != stableId)
                    continue;
                if (em.IsEnabled(entities[i]))
                    return true;
            }

            return false;
        }

        private static bool IsEnabledObstacleStableIdPresent(EntityManager em, uint stableId)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologyObstacleTag>(),
                    ComponentType.ReadOnly<ObstacleStableIdComponent>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                if (em.GetComponentData<ObstacleStableIdComponent>(entities[i]).Value != stableId)
                    continue;
                if (em.IsEnabled(entities[i]))
                    return true;
            }

            return false;
        }

        private static void EnsureOwnedSource(EntityManager em, Entity entity, uint stableId)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            if (!em.HasComponent<StageTopologyOwnedComponent>(entity))
            {
                em.AddComponentData(entity, new StageTopologyOwnedComponent
                {
                    Kind = StageTopologyKind.Source,
                    LastAppliedVersion = 0u,
                });
            }
            if (!em.HasComponent<StageTopologySourceTag>(entity))
                em.AddComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyDepositTag>(entity))
                em.RemoveComponent<StageTopologyDepositTag>(entity);
            em.SetComponentData(entity, new SourceStableIdComponent { Value = stableId });
        }

        private static void AssertTopologyOwned(EntityManager em, Entity entity, StageTopologyKind expectedKind, uint expectedVersion)
        {
            Assert.That(em.HasComponent<StageTopologyOwnedComponent>(entity), Is.True);
            var owned = em.GetComponentData<StageTopologyOwnedComponent>(entity);
            Assert.That(owned.Kind, Is.EqualTo(expectedKind));
            Assert.That(owned.LastAppliedVersion, Is.EqualTo(expectedVersion));
        }

        private static void AssertOwnedCounts(EntityManager em, int expectedSourceTotal, int expectedSourceEnabled, int expectedDepositTotal, int expectedDepositEnabled)
        {
            using var sourceQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologySourceTag>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var depositQuery = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[]
                {
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologyDepositTag>(),
                },
                Options = EntityQueryOptions.IncludeDisabledEntities,
            });
            using var sourceEntities = sourceQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            using var depositEntities = depositQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            int sourceEnabled = 0;
            for (int i = 0; i < sourceEntities.Length; i++)
            {
                if (em.IsEnabled(sourceEntities[i]))
                    sourceEnabled++;
            }

            int depositEnabled = 0;
            for (int i = 0; i < depositEntities.Length; i++)
            {
                if (em.IsEnabled(depositEntities[i]))
                    depositEnabled++;
            }

            Assert.That(sourceEntities.Length, Is.EqualTo(expectedSourceTotal));
            Assert.That(sourceEnabled, Is.EqualTo(expectedSourceEnabled));
            Assert.That(depositEntities.Length, Is.EqualTo(expectedDepositTotal));
            Assert.That(depositEnabled, Is.EqualTo(expectedDepositEnabled));
        }

        private static World CreateDefaultTestWorld(string worldName)
        {
            var world = new World(worldName);
            var systems = DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default);
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(world, systems);
            var lifecycleEntity = GetOrCreateSingletonEntity<StageTopologyLifecycleStateComponent>(world.EntityManager);
            world.EntityManager.SetComponentData(lifecycleEntity, default(StageTopologyLifecycleStateComponent));
            return world;
        }

        private static Entity GetOrCreateSingletonEntity<T>(EntityManager em)
            where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            if (query.IsEmptyIgnoreFilter)
                return em.CreateEntity(typeof(T));
            if (query.CalculateEntityCount() == 1)
                return query.GetSingletonEntity();
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            return entities[0];
        }

        private static void SetSingleton<T>(EntityManager em, T value)
            where T : unmanaged, IComponentData
        {
            var entity = GetOrCreateSingletonEntity<T>(em);
            em.SetComponentData(entity, value);
        }

        private static void SetManagedSingleton<T>(EntityManager em, T value)
            where T : class
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadWrite<T>());
            Entity entity;
            if (query.IsEmptyIgnoreFilter)
            {
                entity = em.CreateEntity();
                em.AddComponentObject(entity, value);
                return;
            }

            entity = query.CalculateEntityCount() == 1
                ? query.GetSingletonEntity()
                : query.ToEntityArray(Unity.Collections.Allocator.Temp)[0];
            em.RemoveComponent<T>(entity);
            em.AddComponentObject(entity, value);
        }

        private static Entity CreateSourceTemplate(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(SourcePollutionConfigComponent),
                typeof(SourcePollutionGridComponent),
                typeof(SourceSustainRuntimeComponent),
                typeof(SourceEventRuntimeComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new SourceStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceSpawnRuntimeComponent { SpawnSequence = 1u });
            em.SetComponentData(entity, new SourceAnchorComponent { Position = float3.zero });
            em.SetComponentData(entity, new BulletFieldAreaComponent
            {
                Shape = BulletFieldShapeId.Circle,
                Radius = 8f,
                Size = new float2(12f, 8f),
                ComputedArea = SourceRuntimeApplyUtility.ComputeArea(BulletFieldShapeId.Circle, 8f, new Vector2(12f, 8f)),
            });
            em.SetComponentData(entity, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.08f,
                DropPerCollect = 0.12f,
                TopKSampleCount = 6,
            });
            em.SetComponentData(entity, new SourcePollutionGridComponent
            {
                CellSize = 2f,
                InvCellSize = 0.5f,
                HalfExtents = new float2(8f, 8f),
                Cols = 1,
                Rows = 1,
            });
            em.SetComponentData(entity, new SourceSustainRuntimeComponent { ActiveState = SourceStateId.Normal });
            em.SetComponentData(entity, new SourceEventRuntimeComponent
            {
                IsPlaying = 0,
                ActiveEventClipId = 0,
                TriggerState = SourceStateId.Normal,
                ElapsedSec = 0f,
                SelectionSequence = 1u,
            });
            em.SetComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Baseline,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 1u,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            em.AddBuffer<SourceSpawnRequestBuffer>(entity).Clear();
            em.AddBuffer<SourceClipPatternBuffer>(entity).Clear();
            em.AddBuffer<SourceSustainSlotCandidateBuffer>(entity).Clear();
            em.AddBuffer<SourceSustainRuntimeLaneBuffer>(entity).Clear();
            em.AddBuffer<SourceEventQueueBuffer>(entity).Clear();
            em.AddBuffer<SourceActiveBulletCountBuffer>(entity).Clear();
            var pressureInputs = em.AddBuffer<SourceDirectorPressureInputBuffer>(entity);
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);
            em.AddBuffer<SourcePollutionCellBuffer>(entity).Clear();
            em.AddBuffer<SourcePollutionDropRequestBuffer>(entity).Clear();
            em.AddBuffer<SourcePollutionValidCellIndexBuffer>(entity).Clear();
            return entity;
        }

        private static Entity CreateDepositTemplate(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(DepositStableIdComponent),
                typeof(DepositPointComponent),
                typeof(LocalTransform));
            em.SetComponentData(entity, new DepositStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new DepositPointComponent { Radius = 1.2f });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            return entity;
        }

        private static Entity CreateObstacleTemplate(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(ObstacleStableIdComponent),
                typeof(ObstacleCollisionMaskComponent),
                typeof(ObstacleGeometryComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new ObstacleStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new ObstacleCollisionMaskComponent
            {
                Value = ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet,
            });
            em.SetComponentData(entity, new ObstacleGeometryComponent
            {
                Shape = ObstacleShape.Box,
                Radius = 1f,
                Size = new float2(2f, 2f),
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
            return entity;
        }

        private static StageCatalogSO CreateStageCatalog(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds, uint[] obstacleStableIds = null)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            createdAssets.Add(catalog);
            catalog.Entries = new[] { CreateEntry(createdAssets, stageId, sourceStableIds, depositStableIds, obstacleStableIds) };
            return catalog;
        }

        private static StageCatalogEntry CreateEntry(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds, uint[] obstacleStableIds = null)
        {
            return new StageCatalogEntry
            {
                Enabled = true,
                EntryKey = $"stage_{stageId:00}",
                Definition = CreateDefinition(createdAssets, stageId, sourceStableIds),
                Layout = CreateLayout(createdAssets, stageId, sourceStableIds, depositStableIds, obstacleStableIds),
            };
        }

        private static StageDefinitionSO CreateDefinition(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = stageId;
            definition.DisplayName = $"Stage {stageId}";
            definition.StageTimeLimitSec = 90f;
            createdAssets.Add(definition);

            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
#if UNITY_EDITOR
            bullet.Editor_SetDefinitionId(stageId * 100 + 1);
#endif
            createdAssets.Add(bullet);

            var sustain = ScriptableObject.CreateInstance<WaveClipSO>();
            sustain.ClipId = stageId * 1000 + 1;
            sustain.Phase = SourceWavePhaseId.Sustain;
            sustain.Lane = SourceSpawnLaneId.Hazard;
            sustain.DurationSec = 1f;
            sustain.Segments = new[]
            {
                new WaveClipSO.ClipSegment
                {
                    StartSec = 0f,
                    EndSec = 1f,
                    Entries = new[]
                    {
                        new WaveClipSO.SpawnEntry
                        {
                            Payload = new WaveClipSO.SpawnPayloadProfile
                            {
                                Bullet = bullet,
                            },
                            Emission = new WaveClipSO.SpawnEmissionProfile
                            {
                                EmissionMode = SourceSpawnEmissionModeId.RateField,
                                SpawnMode = SourceSpawnModeId.FixedDensity,
                                RatePerSecPerArea = 20f,
                                BurstShotsPerEvent = 1,
                            },
                            Sampling = new WaveClipSO.SpawnSamplingProfile
                            {
                                SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                SpawnSampleBudget = 8,
                            },
                            Direction = new WaveClipSO.SpawnDirectionProfile
                            {
                                DirectionMode = SourceSpawnDirectionModeId.Fixed,
                                BaseAngleDeg = 0f,
                                NWayCount = 1,
                            },
                        },
                    },
                },
            };
            createdAssets.Add(sustain);

            definition.SourceBindings = new StageSourceBinding[sourceStableIds.Length];
            for (int i = 0; i < sourceStableIds.Length; i++)
            {
                definition.SourceBindings[i] = new StageSourceBinding
                {
                    SourceStableId = sourceStableIds[i],
                    InitialSourceState = SourceStateId.Normal,
                    ThresholdWeakened = 10,
                    ThresholdDepleted = 20,
                    SustainSlots = new[]
                    {
                        new SustainSlotBinding
                        {
                            State = SourceStateId.Normal,
                            Lane = SourceSpawnLaneId.Hazard,
                            Clips = new[] { sustain },
                            Weights = new[] { 1f },
                        },
                    },
                    EventSlots = new EventSlotBinding[0],
                };
            }

            return definition;
        }

        private static StageLayoutSO CreateLayout(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds, uint[] obstacleStableIds = null)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = stageId;
            createdAssets.Add(layout);

            layout.Sources = new StageSourceLayoutData[sourceStableIds.Length];
            for (int i = 0; i < sourceStableIds.Length; i++)
            {
                layout.Sources[i] = new StageSourceLayoutData
                {
                    StableId = sourceStableIds[i],
                    Active = true,
                    Position = new Vector3(i * 3f, 0f, stageId * 2f),
                    YawDeg = i * 30f,
                    FieldShape = i % 2 == 0 ? BulletFieldShapeId.Circle : BulletFieldShapeId.Rectangle,
                    FieldRadius = 6f + i,
                    FieldSize = new Vector2(8f + i, 6f + i),
                };
            }

            layout.Deposits = new StageDepositLayoutData[depositStableIds.Length];
            for (int i = 0; i < depositStableIds.Length; i++)
            {
                layout.Deposits[i] = new StageDepositLayoutData
                {
                    StableId = depositStableIds[i],
                    Active = true,
                    Position = new Vector3(stageId * 5f, 0f, i * 4f),
                    Radius = 3f + i,
                };
            }

            obstacleStableIds ??= System.Array.Empty<uint>();
            layout.Obstacles = new StageObstacleLayoutData[obstacleStableIds.Length];
            for (int i = 0; i < obstacleStableIds.Length; i++)
            {
                layout.Obstacles[i] = new StageObstacleLayoutData
                {
                    StableId = obstacleStableIds[i],
                    Active = true,
                    Position = new Vector3(stageId * 2f, 0f, -4f + (i * 3f)),
                    EulerRotation = new Vector3(0f, i * 15f, 0f),
                    Shape = ObstacleShape.Box,
                    Radius = 0f,
                    Size = new Vector2(3f + i, 2f + i),
                    CollisionMask = ObstacleCollisionMask.BlockPlayer | ObstacleCollisionMask.BlockBullet,
                };
            }

            return layout;
        }

        private static Entity FindByStableId<T>(EntityManager em, Unity.Collections.NativeArray<Entity> entities, uint stableId, System.Func<T, uint> accessor)
            where T : unmanaged, IComponentData
        {
            for (int i = 0; i < entities.Length; i++)
            {
                if (accessor(em.GetComponentData<T>(entities[i])) == stableId)
                    return entities[i];
            }

            Assert.Fail($"StableId {stableId} not found.");
            return Entity.Null;
        }

        private static void DestroyAll(List<ScriptableObject> createdAssets)
        {
            for (int i = createdAssets.Count - 1; i >= 0; i--)
            {
                if (createdAssets[i] != null)
                    Object.DestroyImmediate(createdAssets[i]);
            }

            createdAssets.Clear();
        }
    }
}





