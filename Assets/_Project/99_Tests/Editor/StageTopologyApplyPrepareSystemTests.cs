using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageTopologyApplyPrepareSystemTests
    {
        private sealed class HazardActorArchetypeFixture : System.IDisposable
        {
            public GameObject Root;
            public HazardEmitterTelegraphProfileSO Telegraph;
            public HazardEmitterEmissionProfileSO Emission;
            public BulletDefinitionSO Bullet;

            public void Dispose()
            {
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
                if (Telegraph != null)
                    UnityEngine.Object.DestroyImmediate(Telegraph);
                if (Emission != null)
                    UnityEngine.Object.DestroyImmediate(Emission);
                if (Bullet != null)
                    UnityEngine.Object.DestroyImmediate(Bullet);
            }
        }

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
        public void StageTopologyApply_PlacementDelivery_CreatesPlacementResolveSeam_AndForcesHiddenPresence()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_PlacementSingle", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);
            using var archetype = CreateHazardActorArchetype("placement_actor_single", actorId: 99, emitterId: 41);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActorPlacements = new[]
                {
                    new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = 101,
                        ActorArchetypePrefab = archetype.Root,
                        LocalOffset = new UnityEngine.Vector3(3f, 0f, -2f),
                    }
                };

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                Assert.That(em.HasBuffer<SourceHazardActorPlacementRefBuffer>(source), Is.True);
                var placementRefs = em.GetBuffer<SourceHazardActorPlacementRefBuffer>(source);
                Assert.That(placementRefs.Length, Is.EqualTo(1));
                Assert.That(placementRefs[0].PlacementInstanceId, Is.EqualTo(101));

                var actor = placementRefs[0].ActorEntity;
                Assert.That(em.Exists(actor), Is.True);
                Assert.That(em.GetComponentData<HazardActorComponent>(actor).ActorId, Is.EqualTo(101));
                Assert.That(em.HasComponent<HazardActorPlacementComponent>(actor), Is.True);
                var placement = em.GetComponentData<HazardActorPlacementComponent>(actor);
                Assert.That(placement.PlacementInstanceId, Is.EqualTo(101));
                Assert.That(placement.LocalOffset.x, Is.EqualTo(3f).Within(0.001f));
                Assert.That(placement.LocalOffset.z, Is.EqualTo(-2f).Within(0.001f));
                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));

                var actorRefs = em.GetBuffer<SourceHazardActorRefBuffer>(source);
                Assert.That(actorRefs.Length, Is.EqualTo(1));
                Assert.That(actorRefs[0].ActorEntity, Is.EqualTo(actor));
                Assert.That(actorRefs[0].ActorId, Is.EqualTo(101));

                var linkedGroup = em.GetBuffer<LinkedEntityGroup>(source);
                Assert.That(linkedGroup.Length, Is.EqualTo(1), "Runtime hazard actors must not be added to the SubScene source LinkedEntityGroup.");
                Assert.That(linkedGroup[0].Value, Is.EqualTo(source));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_PlacementDelivery_AllowsMultipleInstancesFromSameArchetype()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_PlacementMulti", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);
            using var archetype = CreateHazardActorArchetype("placement_actor_multi", actorId: 99, emitterId: 41);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActorPlacements = new[]
                {
                    new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = 201,
                        ActorArchetypePrefab = archetype.Root,
                        LocalOffset = new UnityEngine.Vector3(1f, 0f, 0f),
                    },
                    new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = 202,
                        ActorArchetypePrefab = archetype.Root,
                        LocalOffset = new UnityEngine.Vector3(-1f, 0f, 0f),
                    }
                };

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var placementRefs = em.GetBuffer<SourceHazardActorPlacementRefBuffer>(source);
                Assert.That(placementRefs.Length, Is.EqualTo(2));

                var actorA = FindActorForPlacement(em, source, 201);
                var actorB = FindActorForPlacement(em, source, 202);
                Assert.That(actorA, Is.Not.EqualTo(actorB));

                var placementA = em.GetComponentData<HazardActorPlacementComponent>(actorA);
                var placementB = em.GetComponentData<HazardActorPlacementComponent>(actorB);
                Assert.That(placementA.LocalOffset.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(placementB.LocalOffset.x, Is.EqualTo(-1f).Within(0.001f));
                Assert.That(em.GetComponentData<HazardActorComponent>(actorA).ActorId, Is.EqualTo(201));
                Assert.That(em.GetComponentData<HazardActorComponent>(actorB).ActorId, Is.EqualTo(202));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_PlacementDelivery_SeedsSourceOwnedOrchestrationBuffers()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_PlacementOrchestrationSeed", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);
            using var archetype = CreateHazardActorArchetype("placement_actor_orchestration", actorId: 99, emitterId: 41);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActorPlacements = new[]
                {
                    new HazardActorPlacementBinding
                    {
                        PlacementInstanceId = 301,
                        ActorArchetypePrefab = archetype.Root,
                        LocalOffset = UnityEngine.Vector3.zero,
                    }
                };
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActorOrchestrationRules = new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceId = 301,
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                };

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var rules = em.GetBuffer<SourceHazardActorOrchestrationRuleBuffer>(source);
                var states = em.GetBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(source);
                Assert.That(rules.Length, Is.EqualTo(1));
                Assert.That(states.Length, Is.EqualTo(1));
                Assert.That(rules[0].RuleId, Is.EqualTo(1));
                Assert.That(rules[0].TargetPlacementInstanceId, Is.EqualTo(301));
                Assert.That(rules[0].ActionType, Is.EqualTo(HazardActorOrchestrationActionId.Spawn));
                Assert.That(rules[0].TriggerType, Is.EqualTo(HazardActorOrchestrationTriggerId.OnStageStart));
                Assert.That(states[0].RuleId, Is.EqualTo(1));
                Assert.That(states[0].HasFired, Is.EqualTo(0));

                var actor = FindActorForPlacement(em, source, 301);
                var signal = em.GetComponentData<HazardActorOrchestrationRequestSignalComponent>(actor);
                var consumption = em.GetComponentData<HazardActorOrchestrationRequestConsumptionComponent>(actor);
                Assert.That(signal.Version, Is.EqualTo(0u));
                Assert.That(signal.ActionType, Is.EqualTo(HazardActorOrchestrationActionId.None));
                Assert.That(signal.TargetPhaseId, Is.EqualTo(-1));
                Assert.That(consumption.LastPresenceRequestVersion, Is.EqualTo(0u));
                Assert.That(consumption.LastPhaseRequestVersion, Is.EqualTo(0u));
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

        private static Entity FindAppliedSource(EntityManager em)
        {
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
            using var entities = sourceQuery.ToEntityArray(Allocator.Temp);
            Assert.That(entities.Length, Is.GreaterThanOrEqualTo(1));
            return entities[0];
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
                typeof(LinkedEntityGroup),
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
            em.AddBuffer<SourceHazardActorPlacementRefBuffer>(entity);
            em.AddBuffer<SourceHazardActorOrchestrationRuleBuffer>(entity);
            em.AddBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(entity);
            em.AddBuffer<SourceHazardActorRefBuffer>(entity);

            em.GetBuffer<LinkedEntityGroup>(entity).Add(new LinkedEntityGroup { Value = entity });
            return entity;
        }

        private static Entity FindActorForPlacement(EntityManager em, Entity source, int placementInstanceId)
        {
            var placementRefs = em.GetBuffer<SourceHazardActorPlacementRefBuffer>(source);
            for (int i = 0; i < placementRefs.Length; i++)
            {
                if (placementRefs[i].PlacementInstanceId == placementInstanceId)
                    return placementRefs[i].ActorEntity;
            }

            Assert.Fail($"Placement actor not found. placementInstanceId={placementInstanceId}");
            return Entity.Null;
        }

        private static HazardActorArchetypeFixture CreateHazardActorArchetype(string name, int actorId, int emitterId)
        {
            var fixture = new HazardActorArchetypeFixture();
            fixture.Root = new GameObject(name);
            var actor = fixture.Root.AddComponent<HazardActorAuthoring>();
            actor.ActorId = actorId;

            fixture.Telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            fixture.Bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            fixture.Bullet.Editor_SetDefinitionId(7000 + emitterId);
            fixture.Emission = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            fixture.Emission.Bullet = fixture.Bullet;
            fixture.Emission.PositionPattern = new SinglePointPositionPatternAuthoring();
            fixture.Emission.Aim = new FixedAimAuthoring();
            fixture.Emission.ShotPattern = new SingleShotPatternAuthoring();
            fixture.Emission.EventRepeatCount = 1;
            fixture.Emission.EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
            fixture.Emission.EventShotIntervalSec = 0f;
            fixture.Emission.CooldownSec = 1f;

            actor.PatternSlots = new[]
            {
                new HazardActorPatternSlotAuthoring
                {
                    PatternSlotId = 1,
                    TelegraphProfile = fixture.Telegraph,
                    EmissionProfile = fixture.Emission,
                    BaseWeight = 1f,
                    AvailabilityFlags = 0u,
                }
            };

            return fixture;
        }
    }
}
