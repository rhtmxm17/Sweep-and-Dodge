using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageTopologyApplyPrepareSystemTests
    {
        [Test]
        public void StageTopologyBootstrap_CreatesTopologySingletons_WithoutBulletBootstrap()
        {
            using var world = new World("StageTopologyBootstrap");
            world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);

            var em = world.EntityManager;
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyStateComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyLifecycleStateComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>()).IsEmptyIgnoreFilter, Is.False);
            Assert.That(em.CreateEntityQuery(ComponentType.ReadOnly<StagePlayerStartRuntimeComponent>()).IsEmptyIgnoreFilter, Is.False);
        }

        [Test]
        public void StageSessionResetPrepare_ResetsDepositContextToRegionZero()
        {
            using var world = new World("StageSessionResetPrepare");
            var em = world.EntityManager;
            world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);

            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerCarryBinComponent),
                typeof(PlayerCarryBinDepositRequestTag),
                typeof(PlayerCarryBinDepositContextComponent));
            em.SetComponentEnabled<PlayerCarryBinDepositRequestTag>(player, true);
            em.SetComponentData(player, new PlayerCarryBinComponent { Load = 5, Capacity = 10 });
            em.SetComponentData(player, new PlayerCarryBinDepositContextComponent { DepositRegionId = 44u });

            var topologyRequestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
            em.SetComponentData(topologyRequestEntity, new StageTopologyRequestComponent
            {
                ApplyRequested = 1,
                RequestedStageId = 1,
            });

            world.GetOrCreateSystem<StageSessionResetPrepareSystem>().Update(world.Unmanaged);

            Assert.That(em.IsComponentEnabled<PlayerCarryBinDepositRequestTag>(player), Is.False);
            Assert.That(em.GetComponentData<PlayerCarryBinDepositContextComponent>(player).DepositRegionId, Is.EqualTo(0u));
        }

        [Test]
        public void StageSessionResetPrepare_ResetsPlayerStageEntryTransientFields()
        {
            using var world = new World("StageSessionResetPrepare_PlayerEntry");
            var em = world.EntityManager;
            world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);

            var player = em.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerInputIntentComponent),
                typeof(PlayerResolvedInputSnapshotComponent),
                typeof(PlayerGoSyncComponent),
                typeof(VacuumRuntimeStateComponent),
                typeof(PlayerCleanupSweepRuntimeStateComponent),
                typeof(PlayerStageEntryApplyStateComponent));
            em.SetComponentData(player, new PlayerInputIntentComponent
            {
                MoveAxis = new float2(1f, 0f),
                AimWorldXZ = new float2(2f, 3f),
                HasAimWorldPoint = 1,
                VacuumRequested = 1,
                CleanupActionRequested = 1,
                RequestedCleanupActionSlot = 2,
                Sequence = 9u,
            });
            em.SetComponentData(player, new PlayerResolvedInputSnapshotComponent
            {
                MoveAxis = new float2(1f, 1f),
                AimWorldXZ = new float2(4f, 5f),
                HasAimWorldPoint = 1,
                VacuumRequested = 1,
                CleanupActionRequested = 1,
                RequestedCleanupActionSlot = 2,
                Sequence = 10u,
            });
            em.SetComponentData(player, new PlayerGoSyncComponent
            {
                Position = new float3(5f, 0f, 6f),
                Rotation = quaternion.identity,
                SyncRotation = 1,
                VacuumRequested = 1,
                CleanupActionRequested = 1,
                RequestedCleanupActionSlot = 2,
            });
            em.SetComponentData(player, new VacuumRuntimeStateComponent
            {
                CaptureActiveTimer = 0.3f,
                CaptureCooldownTimer = 0.2f,
                ActiveTimer = 0.4f,
                CooldownTimer = 0.1f,
                IsActive = 1,
                ActivateRequested = 1,
            });
            em.SetComponentData(player, new PlayerCleanupSweepRuntimeStateComponent
            {
                NextSweepDirectionSign = -1,
                ActiveSweepDirectionSign = 1,
                LockedFacingXZ = new float2(1f, 0f),
                HasLockedFacing = 1,
                ActivationFrame = 9u,
            });
            em.SetComponentData(player, new PlayerStageEntryApplyStateComponent
            {
                LastAppliedVersion = 9u,
            });

            var topologyRequestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
            em.SetComponentData(topologyRequestEntity, new StageTopologyRequestComponent
            {
                ApplyRequested = 1,
                RequestedStageId = 1,
            });

            world.GetOrCreateSystem<StageSessionResetPrepareSystem>().Update(world.Unmanaged);

            var intent = em.GetComponentData<PlayerInputIntentComponent>(player);
            var snapshot = em.GetComponentData<PlayerResolvedInputSnapshotComponent>(player);
            var sync = em.GetComponentData<PlayerGoSyncComponent>(player);
            var vacuum = em.GetComponentData<VacuumRuntimeStateComponent>(player);
            var sweepRuntime = em.GetComponentData<PlayerCleanupSweepRuntimeStateComponent>(player);
            var applyState = em.GetComponentData<PlayerStageEntryApplyStateComponent>(player);

            Assert.That(intent.VacuumRequested, Is.EqualTo(0));
            Assert.That(intent.CleanupActionRequested, Is.EqualTo(0));
            Assert.That(intent.RequestedCleanupActionSlot, Is.EqualTo(0));
            Assert.That(intent.Sequence, Is.EqualTo(0u));
            Assert.That(snapshot.VacuumRequested, Is.EqualTo(0));
            Assert.That(snapshot.CleanupActionRequested, Is.EqualTo(0));
            Assert.That(snapshot.RequestedCleanupActionSlot, Is.EqualTo(0));
            Assert.That(snapshot.Sequence, Is.EqualTo(0u));
            Assert.That(sync.VacuumRequested, Is.EqualTo(0));
            Assert.That(sync.CleanupActionRequested, Is.EqualTo(0));
            Assert.That(sync.RequestedCleanupActionSlot, Is.EqualTo(0));
            Assert.That(vacuum.IsActive, Is.EqualTo(0));
            Assert.That(vacuum.ActivateRequested, Is.EqualTo(0));
            Assert.That(vacuum.ActiveTimer, Is.EqualTo(0f));
            Assert.That(vacuum.CaptureActiveTimer, Is.EqualTo(0f));
            Assert.That(sweepRuntime.NextSweepDirectionSign, Is.EqualTo(1));
            Assert.That(sweepRuntime.ActiveSweepDirectionSign, Is.EqualTo(0));
            Assert.That(sweepRuntime.LockedFacingXZ, Is.EqualTo(float2.zero));
            Assert.That(sweepRuntime.HasLockedFacing, Is.EqualTo(0));
            Assert.That(sweepRuntime.ActivationFrame, Is.EqualTo(0u));
            Assert.That(applyState.LastAppliedVersion, Is.EqualTo(0u));
        }

        [Test]
        public void StageTopologyApply_PublishesRuntimeGridCache_AndDoesNotCreateDepositOrObstacleEntities()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_GridCache", out var em, out var requestEntity, out var topologyStateEntity);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: false);

            try
            {
                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var gridEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>()).GetSingletonEntity();
                var grid = em.GetComponentData<StageRuntimeGridComponent>(gridEntity);
                var cells = em.GetBuffer<StageRuntimeGridCellBufferElement>(gridEntity);
                var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);

                Assert.That(grid.Ready, Is.EqualTo(1));
                Assert.That(grid.StageId, Is.EqualTo(1));
                Assert.That(grid.Width, Is.EqualTo(2));
                Assert.That(grid.Height, Is.EqualTo(2));
                Assert.That(cells.Length, Is.EqualTo(4));
                Assert.That(cells[1].MovementFlags, Is.EqualTo(StageCellMovementFlags.BlockPlayer));
                Assert.That(cells[3].DepositRegionId, Is.EqualTo(2001u));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(1));
                Assert.That(topologyState.Ready, Is.EqualTo(1));

                var playerStartEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StagePlayerStartRuntimeComponent>()).GetSingletonEntity();
                var playerStart = em.GetComponentData<StagePlayerStartRuntimeComponent>(playerStartEntity);
                Assert.That(playerStart.Ready, Is.EqualTo(1));
                Assert.That(playerStart.StageId, Is.EqualTo(1));
                Assert.That(playerStart.PositionX, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(playerStart.PositionZ, Is.EqualTo(1.5f).Within(0.001f));
                Assert.That(playerStart.YawDeg, Is.EqualTo(45f).Within(0.001f));

            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_ReconcilesSourceTopologyFromRegionCells_AndKeepsGridReady()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_SourceOnly", out var em, out var requestEntity, out var topologyStateEntity);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
                Assert.That(topologyState.Ready, Is.EqualTo(1));

                using var sourceQuery = em.CreateEntityQuery(
                    ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                    ComponentType.ReadOnly<StageTopologySourceTag>(),
                    ComponentType.ReadOnly<SourceStableIdComponent>(),
                    ComponentType.ReadOnly<SourceSpawnComponent>());
                using var entities = sourceQuery.ToEntityArray(Allocator.Temp);
                Assert.That(entities.Length, Is.EqualTo(1));
                Assert.That(em.GetComponentData<SourceStableIdComponent>(entities[0]).Value, Is.EqualTo(1001u));
                Assert.That(em.GetBuffer<SourceRegionCellIndexBuffer>(entities[0]).Length, Is.EqualTo(2));
                Assert.That(em.GetComponentData<SourceAnchorComponent>(entities[0]).Position.x, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(em.GetComponentData<SourceAnchorComponent>(entities[0]).Position.z, Is.EqualTo(0.5f).Within(0.001f));

                var gridEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageRuntimeGridComponent>()).GetSingletonEntity();
                Assert.That(em.GetComponentData<StageRuntimeGridComponent>(gridEntity).Ready, Is.EqualTo(1));
                var gridCells = em.GetBuffer<StageRuntimeGridCellBufferElement>(gridEntity);
                Assert.That(gridCells[0].SourceRegionId, Is.EqualTo(1001u));
                Assert.That(gridCells[2].SourceRegionId, Is.EqualTo(1001u));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_InvalidPlayerStart_KeepsTopologyNotReady()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_InvalidPlayerStart", out var em, out var requestEntity, out var topologyStateEntity);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: false);

            try
            {
                stageCatalog.Entries[0].Layout.PlayerStart = new StagePlayerStartLayoutData
                {
                    Active = true,
                    AnchorCell = new UnityEngine.Vector2Int(1, 0),
                    AnchorOffset = UnityEngine.Vector2.zero,
                    YawDeg = 15f,
                };
                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var topologyState = em.GetComponentData<StageTopologyStateComponent>(topologyStateEntity);
                var playerStartEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StagePlayerStartRuntimeComponent>()).GetSingletonEntity();
                var playerStart = em.GetComponentData<StagePlayerStartRuntimeComponent>(playerStartEntity);

                Assert.That(topologyState.Ready, Is.EqualTo(0));
                Assert.That(topologyState.AppliedStageId, Is.EqualTo(0));
                Assert.That(playerStart.Ready, Is.EqualTo(0));
                Assert.That(playerStart.AppliedVersion, Is.EqualTo(1u));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        private static World CreatePreparedWorld(string name, out EntityManager em, out Entity requestEntity, out Entity topologyStateEntity)
        {
            var world = new World(name);
            em = world.EntityManager;

            world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Idle,
                StateElapsedSec = 0f,
                EnteredFrame = 0u,
                LastTransitionReason = RunDirectorStageTransitionReasonId.None,
            });

            requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
            topologyStateEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyStateComponent>()).GetSingletonEntity();
            var prefabCatalogEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).GetSingletonEntity();
            em.SetComponentData(prefabCatalogEntity, new StageTopologyPrefabCatalogComponent
            {
                SourceTemplate = CreateSourceTemplate(em),
            });

            return world;
        }

        private static void PublishCatalogAndRequest(EntityManager em, Entity requestEntity, StageCatalogSO stageCatalog, int stageId)
        {
            using var runtimeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<StageCatalogRuntimeComponent>());
            var runtimeEntity = runtimeQuery.GetSingletonEntity();
            em.GetComponentObject<StageCatalogRuntimeComponent>(runtimeEntity).Catalog = stageCatalog;

            em.SetComponentData(requestEntity, new StageTopologyRequestComponent
            {
                ApplyRequested = 1,
                RequestedStageId = stageId,
            });
        }

        private static StageCatalogSO CreateStageCatalog(bool includeSourceLayout)
        {
            var layout = UnityEngine.ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = 1;
            layout.SchemaVersion = 2;
            layout.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = new UnityEngine.Vector3(0f, 0f, 0f),
            };
            layout.Cells = new[]
            {
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, DepositRegionId = 0u, SourceRegionId = includeSourceLayout ? 1001u : 0u },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockPlayer, DepositRegionId = 0u, SourceRegionId = 0u },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, DepositRegionId = 0u, SourceRegionId = includeSourceLayout ? 1001u : 0u },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockBullet, DepositRegionId = 2001u, SourceRegionId = 0u },
            };
            layout.SourceRegions = includeSourceLayout
                ? new[]
                {
                    new StageSourceRegionLayoutData
                    {
                        StableId = 1001u,
                        Active = true,
                        AnchorCell = new UnityEngine.Vector2Int(0, 0),
                        AnchorOffset = new UnityEngine.Vector2(0.25f, 0f),
                    }
                }
                : System.Array.Empty<StageSourceRegionLayoutData>();
            layout.DepositRegions = new[]
            {
                new StageDepositRegionLayoutData
                {
                    StableId = 2001u,
                    Active = true,
                    AnchorCell = new UnityEngine.Vector2Int(1, 1),
                    AnchorOffset = UnityEngine.Vector2.zero,
                }
            };
            layout.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = new UnityEngine.Vector2Int(0, 1),
                AnchorOffset = UnityEngine.Vector2.zero,
                YawDeg = 45f,
            };
            layout.Presentations = System.Array.Empty<StagePresentationLayoutData>();

            var definition = UnityEngine.ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = 1;
            definition.SourceBindings = includeSourceLayout
                ? new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1001u,
                        ThresholdWeakened = 2,
                        ThresholdDepleted = 4,
                        InitialSourceState = SourceStateId.Normal,
                    }
                }
                : System.Array.Empty<StageSourceBinding>();

            var catalog = UnityEngine.ScriptableObject.CreateInstance<StageCatalogSO>();
            catalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    Definition = definition,
                    Layout = layout,
                }
            };
            return catalog;
        }

        private static void SetSingleton<T>(EntityManager em, T value) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            var entity = query.GetSingletonEntity();
            em.SetComponentData(entity, value);
        }

        private static Entity CreateSourceTemplate(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(Prefab),
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(Shape2DComponent),
                typeof(SourceShapeDerivedComponent),
                typeof(SourcePollutionConfigComponent),
                typeof(SourcePollutionGridComponent),
                typeof(SourceSustainRuntimeComponent),
                typeof(SourceEventRuntimeComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new SourceStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 2,
                ThresholdDepleted = 4,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceSpawnRuntimeComponent { SpawnSequence = 1u });
            em.SetComponentData(entity, new SourceAnchorComponent { Position = float3.zero });
            em.SetComponentData(entity, new Shape2DComponent
            {
                Kind = Shape2DKind.Circle,
                Radius = 2f,
                Size = float2.zero,
            });
            em.SetComponentData(entity, new SourceShapeDerivedComponent
            {
                ComputedArea = math.PI * 4f,
                HalfExtents = new float2(2f, 2f),
            });
            em.SetComponentData(entity, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.1f,
                DropPerCollect = 0.1f,
                TopKSampleCount = 1,
            });
            em.SetComponentData(entity, new SourcePollutionGridComponent
            {
                CellSize = 1f,
                InvCellSize = 1f,
                HalfExtents = new float2(2f, 2f),
                OriginX = -2f,
                OriginZ = -2f,
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

            em.AddBuffer<SourceSpawnRequestBuffer>(entity);
            em.AddBuffer<SourceClipPatternBuffer>(entity);
            em.AddBuffer<SourceSustainSlotCandidateBuffer>(entity);
            em.AddBuffer<SourceSustainRuntimeLaneBuffer>(entity);
            em.AddBuffer<SourceEventQueueBuffer>(entity);
            em.AddBuffer<SourceActiveBulletCountBuffer>(entity);
            em.AddBuffer<SourceDirectorPressureInputBuffer>(entity);
            em.AddBuffer<SourcePollutionCellBuffer>(entity);
            em.AddBuffer<SourcePollutionDropRequestBuffer>(entity);
            em.AddBuffer<SourcePollutionValidCellIndexBuffer>(entity);
            em.AddBuffer<SourceRegionCellIndexBuffer>(entity);
            return entity;
        }
    }
}
