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
    public class StageTopologyApplyExecutionBeginSystemTests
    {
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
                world.GetOrCreateSystem<StageTopologyApplyExecutionBeginSystem>().Update(world.Unmanaged);

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

                Assert.That(em.IsEnabled(sourceA), Is.True);
                Assert.That(em.IsEnabled(sourceB), Is.True);
                Assert.That(em.IsEnabled(deposit), Is.True);
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
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 2, expectedDepositTotal: 1, expectedDepositEnabled: 1);

                ApplyStage(world, 2);
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 1, expectedDepositTotal: 1, expectedDepositEnabled: 1);

                ApplyStage(world, 1);
                AssertOwnedCounts(em, expectedSourceTotal: 2, expectedSourceEnabled: 2, expectedDepositTotal: 1, expectedDepositEnabled: 1);
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
                world.GetOrCreateSystem<StageTopologyApplyExecutionBeginSystem>().Update(world.Unmanaged);

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
                Assert.That(topologyState.SelectedStageId, Is.EqualTo(2));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(0));
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
                world.GetOrCreateSystem<StageTopologyApplyExecutionBeginSystem>().Update(world.Unmanaged);

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
            world.GetOrCreateSystem<StageTopologyApplyExecutionBeginSystem>().Update(world.Unmanaged);
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

        private static void EnsureOwnedSource(EntityManager em, Entity entity, uint stableId)
        {
            if (!em.HasComponent<StageTopologyOwnedTag>(entity))
                em.AddComponent<StageTopologyOwnedTag>(entity);
            if (!em.HasComponent<StageTopologySourceTag>(entity))
                em.AddComponent<StageTopologySourceTag>(entity);
            if (em.HasComponent<StageTopologyDepositTag>(entity))
                em.RemoveComponent<StageTopologyDepositTag>(entity);
            em.SetComponentData(entity, new SourceStableIdComponent { Value = stableId });
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

        private static StageCatalogSO CreateStageCatalog(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            createdAssets.Add(catalog);
            catalog.Entries = new[] { CreateEntry(createdAssets, stageId, sourceStableIds, depositStableIds) };
            return catalog;
        }

        private static StageCatalogEntry CreateEntry(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds)
        {
            return new StageCatalogEntry
            {
                Enabled = true,
                EntryKey = $"stage_{stageId:00}",
                Definition = CreateDefinition(createdAssets, stageId, sourceStableIds),
                Layout = CreateLayout(createdAssets, stageId, sourceStableIds, depositStableIds),
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

        private static StageLayoutSO CreateLayout(List<ScriptableObject> createdAssets, int stageId, uint[] sourceStableIds, uint[] depositStableIds)
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





