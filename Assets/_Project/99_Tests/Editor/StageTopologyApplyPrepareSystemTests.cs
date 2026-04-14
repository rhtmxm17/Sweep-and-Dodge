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
        public void StageTopologyApply_AppliesHazardActorAndEmitterOverrides_Deterministically()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_HazardEmitterOverride", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = new[]
                {
                    new HazardActorBinding
                    {
                        ActorId = 7,
                        EnabledMode = HazardActorEnabledOverrideMode.ForceDisabled,
                        StartSuppressedMode = HazardActorSuppressionOverrideMode.ForceSuppressed,
                        Emitters = new[]
                        {
                            new HazardEmitterBinding
                            {
                                EmitterId = 41,
                                OverrideLocalOffset = true,
                                LocalOffset = new UnityEngine.Vector3(2f, 0f, -1f),
                                TelegraphProfileOverride = CreateTelegraphProfile(55, 0.75f),
                                EmissionProfileOverride = CreateEmissionProfile(66, 902, 30f, 2f),
                            }
                        },
                    }
                };
                var binding = stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors[0].Emitters[0];

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                Entity source = FindAppliedSource(em);
                var actor = FindActorForSource(em, source);
                var emitter = FindEmitterForActor(em, actor);

                var actorApplied = em.GetComponentData<HazardActorAppliedConfigComponent>(actor);
                var actorRuntime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
                var actorPhase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actor);
                var actorTransitionRuntime = em.GetComponentData<HazardActorPhaseTransitionRuntimeComponent>(actor);
                var actorTransitionSignal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actor);
                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);

                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
                var telegraph = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitter);
                var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
                var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
                var selectedPattern = em.GetComponentData<HazardEmitterSelectedPatternRuntimeComponent>(emitter);
                var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
                var cycleSignal = em.GetComponentData<HazardEmitterCycleSignalComponent>(emitter);
                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);

                Assert.That(em.IsEnabled(actor), Is.True);
                Assert.That(em.IsEnabled(emitter), Is.True);
                Assert.That(actorApplied.IsEnabled, Is.EqualTo(0));
                Assert.That(actorApplied.IsSuppressed, Is.EqualTo(1));
                Assert.That(actorRuntime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
                Assert.That(actorRuntime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(actorPhase.CurrentPhaseId, Is.EqualTo(1));
                Assert.That(actorPhase.PreviousPhaseId, Is.EqualTo(1));
                Assert.That(actorPhase.PhaseVersion, Is.EqualTo(0u));
                Assert.That(actorTransitionRuntime.State, Is.EqualTo(HazardActorPhaseTransitionStateId.Idle));
                Assert.That(actorTransitionRuntime.PendingFromPhaseId, Is.EqualTo(-1));
                Assert.That(actorTransitionRuntime.PendingToPhaseId, Is.EqualTo(-1));
                Assert.That(actorTransitionRuntime.ElapsedSec, Is.EqualTo(0f));
                Assert.That(actorTransitionRuntime.DurationSec, Is.EqualTo(0f));
                Assert.That(actorTransitionRuntime.TransitionVersion, Is.EqualTo(0u));
                Assert.That(actorTransitionSignal.Version, Is.EqualTo(0u));
                Assert.That(actorTransitionSignal.Cue, Is.EqualTo(HazardActorPhaseTransitionSignalCueId.None));
                Assert.That(actorTransitionSignal.PreviousPhaseId, Is.EqualTo(1));
                Assert.That(actorTransitionSignal.CurrentPhaseId, Is.EqualTo(1));
                Assert.That(actorTransitionSignal.PendingToPhaseId, Is.EqualTo(-1));
                Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
                Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
                Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
                Assert.That(selector.SelectionSequence, Is.EqualTo(0u));
                Assert.That(selector.CurrentCandidateOrder, Is.EqualTo(-1));
                Assert.That(selector.LastResolvedPhaseVersion, Is.EqualTo(0u));
                Assert.That(selector.LastConsumedCycleVersion, Is.EqualTo(0u));
                Assert.That(appliedConfig.IsEnabled, Is.EqualTo(1));
                Assert.That(appliedConfig.IsSuppressed, Is.EqualTo(0));
                Assert.That(appliedConfig.LocalOffset.x, Is.EqualTo(2f).Within(0.001f));
                Assert.That(appliedConfig.LocalOffset.z, Is.EqualTo(-1f).Within(0.001f));
                Assert.That(appliedConfig.TelegraphProfileRefId, Is.EqualTo(binding.TelegraphProfileOverride.GetInstanceID()));
                Assert.That(appliedConfig.EmissionProfileRefId, Is.EqualTo(binding.EmissionProfileOverride.GetInstanceID()));
                Assert.That(patternSlots.Length, Is.EqualTo(1));
                Assert.That(patternSlots[0].PatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
                Assert.That(patternSlots[0].TelegraphProfileRefId, Is.EqualTo(appliedConfig.TelegraphProfileRefId));
                Assert.That(patternSlots[0].EmissionProfileRefId, Is.EqualTo(appliedConfig.EmissionProfileRefId));
                Assert.That(patternSlots[0].BaseWeight, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityBaseWeight));
                Assert.That(patternSlots[0].AvailabilityFlags, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityAvailabilityFlags));
                Assert.That(executionSlots.Length, Is.EqualTo(1));
                Assert.That(executionSlots[0].PatternSlotId, Is.EqualTo(patternSlots[0].PatternSlotId));
                Assert.That(executionSlots[0].TelegraphProfileRefId, Is.EqualTo(appliedConfig.TelegraphProfileRefId));
                Assert.That(executionSlots[0].EmissionProfileRefId, Is.EqualTo(appliedConfig.EmissionProfileRefId));
                Assert.That(telegraph.ProfileId, Is.EqualTo(binding.TelegraphProfileOverride.GetInstanceID()));
                Assert.That(telegraph.TelegraphDurationSec, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(emission.ProfileId, Is.EqualTo(binding.EmissionProfileOverride.GetInstanceID()));
                Assert.That(emission.BulletTypeKey, Is.EqualTo(902));
                Assert.That(emission.BaseAngleDeg, Is.EqualTo(30f).Within(0.001f));
                Assert.That(emission.CooldownSec, Is.EqualTo(2f).Within(0.001f));
                Assert.That(selectedPattern.AppliedPatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId));
                Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
                Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(cycleSignal.CompletedVersion, Is.EqualTo(0u));
                Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
                Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
                Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(float.MaxValue));
            }
            finally
            {
                var binding = stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors[0].Emitters[0];
                UnityEngine.Object.DestroyImmediate(binding.TelegraphProfileOverride);
                UnityEngine.Object.DestroyImmediate(binding.EmissionProfileOverride.Bullet);
                UnityEngine.Object.DestroyImmediate(binding.EmissionProfileOverride);
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_OmittedActor_DisablesActorAndEmitterDeterministically()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_OmittedActorRoster", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = System.Array.Empty<HazardActorBinding>();

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForSource(em, source);
                var emitter = FindEmitterForActor(em, actor);
                var actorApplied = em.GetComponentData<HazardActorAppliedConfigComponent>(actor);
                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
                var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);

                Assert.That(em.IsEnabled(source), Is.True);
                Assert.That(em.IsEnabled(actor), Is.False);
                Assert.That(em.IsEnabled(emitter), Is.False);
                Assert.That(actorApplied.IsEnabled, Is.EqualTo(0));
                Assert.That(actorApplied.IsSuppressed, Is.EqualTo(1));
                Assert.That(appliedConfig.IsEnabled, Is.EqualTo(0));
                Assert.That(appliedConfig.IsSuppressed, Is.EqualTo(1));
                Assert.That(patternSlots.Length, Is.EqualTo(1));
                Assert.That(patternSlots[0].PatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
                Assert.That(patternSlots[0].TelegraphProfileRefId, Is.EqualTo(appliedConfig.TelegraphProfileRefId));
                Assert.That(patternSlots[0].EmissionProfileRefId, Is.EqualTo(appliedConfig.EmissionProfileRefId));
                Assert.That(executionSlots.Length, Is.EqualTo(1));
                Assert.That(executionSlots[0].PatternSlotId, Is.EqualTo(patternSlots[0].PatternSlotId));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_EmptyEmitterRoster_DisablesEmitterDeterministically()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_EmptyEmitterRoster", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = new[]
                {
                    new HazardActorBinding
                    {
                        ActorId = 7,
                        EnabledMode = HazardActorEnabledOverrideMode.ForceEnabled,
                        StartSuppressedMode = HazardActorSuppressionOverrideMode.ForceUnsuppressed,
                        Emitters = System.Array.Empty<HazardEmitterBinding>(),
                    }
                };

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForSource(em, source);
                var emitter = FindEmitterForActor(em, actor);
                var actorApplied = em.GetComponentData<HazardActorAppliedConfigComponent>(actor);
                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
                var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
                var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);

                Assert.That(em.IsEnabled(actor), Is.True);
                Assert.That(actorApplied.IsEnabled, Is.EqualTo(1));
                Assert.That(actorApplied.IsSuppressed, Is.EqualTo(0));
                Assert.That(em.IsEnabled(emitter), Is.False);
                Assert.That(appliedConfig.IsEnabled, Is.EqualTo(0));
                Assert.That(appliedConfig.IsSuppressed, Is.EqualTo(1));
                Assert.That(patternSlots.Length, Is.EqualTo(1));
                Assert.That(patternSlots[0].PatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
                Assert.That(patternSlots[0].TelegraphProfileRefId, Is.EqualTo(appliedConfig.TelegraphProfileRefId));
                Assert.That(patternSlots[0].EmissionProfileRefId, Is.EqualTo(appliedConfig.EmissionProfileRefId));
                Assert.That(executionSlots.Length, Is.EqualTo(1));
                Assert.That(executionSlots[0].PatternSlotId, Is.EqualTo(patternSlots[0].PatternSlotId));
                Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
                Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
                Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
                Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(float.MaxValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_MultiSlotEmitterProfileOverride_IsRejectedAndKeepsAuthoredSlots()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_MultiSlotEmitterOverrideRejected", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = new[]
                {
                    new HazardActorBinding
                    {
                        ActorId = 7,
                        EnabledMode = HazardActorEnabledOverrideMode.Inherit,
                        StartSuppressedMode = HazardActorSuppressionOverrideMode.Inherit,
                        Emitters = new[]
                        {
                            new HazardEmitterBinding
                            {
                                EmitterId = 41,
                                TelegraphProfileOverride = CreateTelegraphProfile(155, 1.25f),
                                EmissionProfileOverride = CreateEmissionProfile(166, 1902, 60f, 3f),
                            }
                        },
                    }
                };

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                Entity source = FindAppliedSource(em);
                var actor = FindActorForSource(em, source);
                var emitter = FindEmitterForActor(em, actor);
                ReplaceEmitterPatternSlots(
                    em,
                    emitter,
                    CreateExecutionSlot(1, 11, 0.1f, 1101, 10f, 1f),
                    CreateExecutionSlot(2, 22, 0.2f, 2202, 20f, 2f));

                LogAssert.Expect(
                    UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex(@"\[StageTopologyApply\] Multi-slot HazardEmitter cannot use emitter-level Telegraph/Emission profile overrides\..*"));

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
                var telegraph = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitter);
                var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
                var metadataSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);

                Assert.That(metadataSlots.Length, Is.EqualTo(2));
                Assert.That(executionSlots.Length, Is.EqualTo(2));
                Assert.That(metadataSlots[0].PatternSlotId, Is.EqualTo(1));
                Assert.That(metadataSlots[0].TelegraphProfileRefId, Is.EqualTo(11));
                Assert.That(metadataSlots[0].EmissionProfileRefId, Is.EqualTo(1101));
                Assert.That(metadataSlots[1].PatternSlotId, Is.EqualTo(2));
                Assert.That(metadataSlots[1].TelegraphProfileRefId, Is.EqualTo(22));
                Assert.That(metadataSlots[1].EmissionProfileRefId, Is.EqualTo(2202));
                Assert.That(executionSlots[0].PatternSlotId, Is.EqualTo(1));
                Assert.That(executionSlots[0].TelegraphProfileRefId, Is.EqualTo(11));
                Assert.That(executionSlots[0].EmissionProfileRefId, Is.EqualTo(1101));
                Assert.That(executionSlots[1].PatternSlotId, Is.EqualTo(2));
                Assert.That(executionSlots[1].TelegraphProfileRefId, Is.EqualTo(22));
                Assert.That(executionSlots[1].EmissionProfileRefId, Is.EqualTo(2202));
                Assert.That(appliedConfig.TelegraphProfileRefId, Is.Not.EqualTo(stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors[0].Emitters[0].TelegraphProfileOverride.GetInstanceID()));
                Assert.That(appliedConfig.EmissionProfileRefId, Is.Not.EqualTo(stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors[0].Emitters[0].EmissionProfileOverride.GetInstanceID()));
                Assert.That(telegraph.ProfileId, Is.EqualTo(10));
                Assert.That(telegraph.TelegraphDurationSec, Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(emission.ProfileId, Is.EqualTo(20));
                Assert.That(emission.BulletTypeKey, Is.EqualTo(901));
                Assert.That(emission.BaseAngleDeg, Is.EqualTo(15f).Within(0.001f));
                Assert.That(emission.CooldownSec, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                LogAssert.NoUnexpectedReceived();
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_LayoutOnly_DisablesAllHazardActorsAndEmitters()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_LayoutOnlyHazards", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog.Entries[0].Definition);
                stageCatalog.Entries[0].Definition = null;

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForSource(em, source);
                var emitter = FindEmitterForActor(em, actor);
                var actorApplied = em.GetComponentData<HazardActorAppliedConfigComponent>(actor);
                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);

                Assert.That(em.IsEnabled(source), Is.True);
                Assert.That(em.IsEnabled(actor), Is.False);
                Assert.That(em.IsEnabled(emitter), Is.False);
                Assert.That(actorApplied.IsEnabled, Is.EqualTo(0));
                Assert.That(actorApplied.IsSuppressed, Is.EqualTo(1));
                Assert.That(appliedConfig.IsEnabled, Is.EqualTo(0));
                Assert.That(appliedConfig.IsSuppressed, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_MissingSourceBinding_DisablesHazardActorAndEmitterDeterministically()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_HazardEmitterDisable", out var em, out var requestEntity, out _);
            var stageCatalog = CreateStageCatalog(includeSourceLayout: true);

            try
            {
                stageCatalog.Entries[0].Definition.SourceBindings = System.Array.Empty<StageSourceBinding>();
                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

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
                using var sources = sourceQuery.ToEntityArray(Allocator.Temp);
                Assert.That(sources.Length, Is.EqualTo(1));

                var source = sources[0];
                Assert.That(em.IsEnabled(source), Is.False);

                var actor = FindActorForSource(em, source);
                var actorApplied = em.GetComponentData<HazardActorAppliedConfigComponent>(actor);
                var actorRuntime = em.GetComponentData<HazardActorRuntimeStateComponent>(actor);
                var actorPhase = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actor);
                var actorTransitionRuntime = em.GetComponentData<HazardActorPhaseTransitionRuntimeComponent>(actor);
                var actorTransitionSignal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actor);
                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
                var emitter = FindEmitterForActor(em, actor);
                var appliedConfig = em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitter);
                var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
                var cycleSignal = em.GetComponentData<HazardEmitterCycleSignalComponent>(emitter);
                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);

                Assert.That(em.IsEnabled(actor), Is.False);
                Assert.That(em.IsEnabled(emitter), Is.False);
                Assert.That(actorApplied.IsEnabled, Is.EqualTo(0));
                Assert.That(actorApplied.IsSuppressed, Is.EqualTo(1));
                Assert.That(actorRuntime.PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
                Assert.That(actorRuntime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(actorPhase.CurrentPhaseId, Is.EqualTo(1));
                Assert.That(actorPhase.PreviousPhaseId, Is.EqualTo(1));
                Assert.That(actorPhase.PhaseVersion, Is.EqualTo(0u));
                Assert.That(actorTransitionRuntime.State, Is.EqualTo(HazardActorPhaseTransitionStateId.Idle));
                Assert.That(actorTransitionRuntime.PendingFromPhaseId, Is.EqualTo(-1));
                Assert.That(actorTransitionRuntime.PendingToPhaseId, Is.EqualTo(-1));
                Assert.That(actorTransitionRuntime.ElapsedSec, Is.EqualTo(0f));
                Assert.That(actorTransitionRuntime.DurationSec, Is.EqualTo(0f));
                Assert.That(actorTransitionRuntime.TransitionVersion, Is.EqualTo(0u));
                Assert.That(actorTransitionSignal.Version, Is.EqualTo(0u));
                Assert.That(actorTransitionSignal.Cue, Is.EqualTo(HazardActorPhaseTransitionSignalCueId.None));
                Assert.That(actorTransitionSignal.PreviousPhaseId, Is.EqualTo(1));
                Assert.That(actorTransitionSignal.CurrentPhaseId, Is.EqualTo(1));
                Assert.That(actorTransitionSignal.PendingToPhaseId, Is.EqualTo(-1));
                Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
                Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
                Assert.That(selector.LastPatternSlotId, Is.EqualTo(-1));
                Assert.That(selector.SelectionSequence, Is.EqualTo(0u));
                Assert.That(selector.CurrentCandidateOrder, Is.EqualTo(-1));
                Assert.That(selector.LastResolvedPhaseVersion, Is.EqualTo(0u));
                Assert.That(selector.LastConsumedCycleVersion, Is.EqualTo(0u));
                Assert.That(appliedConfig.IsEnabled, Is.EqualTo(0));
                Assert.That(appliedConfig.IsSuppressed, Is.EqualTo(1));
                Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
                Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(cycleSignal.CompletedVersion, Is.EqualTo(0u));
                Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
                Assert.That(coordinator.SuppressionReasonMask, Is.EqualTo(0u));
                Assert.That(coordinator.LastPlayerDistanceSq, Is.EqualTo(float.MaxValue));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_PlacementDelivery_CreatesPlacementResolveSeam_AndForcesHiddenPresence()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_PlacementSingle", out var em, out var requestEntity, out _, includeLegacyHazardHierarchy: false);
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
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = System.Array.Empty<HazardActorBinding>();

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
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(stageCatalog);
            }
        }

        [Test]
        public void StageTopologyApply_PlacementDelivery_AllowsMultipleInstancesFromSameArchetype()
        {
            using var world = CreatePreparedWorld("StageTopologyApply_PlacementMulti", out var em, out var requestEntity, out _, includeLegacyHazardHierarchy: false);
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
                stageCatalog.Entries[0].Definition.SourceBindings[0].HazardActors = System.Array.Empty<HazardActorBinding>();

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

        private static World CreatePreparedWorld(string name, out EntityManager em, out Entity requestEntity, out Entity topologyStateEntity, bool includeLegacyHazardHierarchy = true)
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
                SourceTemplate = CreateSourceTemplate(em, includeLegacyHazardHierarchy),
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

        private static HazardEmitterTelegraphProfileSO CreateTelegraphProfile(int id, float telegraphDurationSec)
        {
            var profile = UnityEngine.ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            profile.name = $"hetp_test_{id}";
            profile.TelegraphDurationSec = telegraphDurationSec;
            return profile;
        }

        private static HazardEmitterEmissionProfileSO CreateEmissionProfile(int id, int bulletTypeKey, float baseAngleDeg, float cooldownSec)
        {
            var bullet = UnityEngine.ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(bulletTypeKey);

            var profile = UnityEngine.ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            profile.name = $"heep_test_{id}";
            profile.Bullet = bullet;
            profile.PositionPattern = new SinglePointPositionPatternAuthoring();
            profile.Aim = new FixedAimAuthoring { BaseAngleDeg = baseAngleDeg };
            profile.ShotPattern = new SingleShotPatternAuthoring();
            profile.EventRepeatCount = 1;
            profile.EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
            profile.EventShotIntervalSec = 0f;
            profile.CooldownSec = cooldownSec;
            return profile;
        }

        private static void ReplaceEmitterPatternSlots(
            EntityManager em,
            Entity emitter,
            params HazardEmitterPatternExecutionSlotBuffer[] executionSlots)
        {
            var metadataSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
            var runtimeExecutionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
            metadataSlots.Clear();
            runtimeExecutionSlots.Clear();

            for (int i = 0; i < executionSlots.Length; i++)
            {
                metadataSlots.Add(new HazardEmitterPatternSlotBuffer
                {
                    PatternSlotId = executionSlots[i].PatternSlotId,
                    TelegraphProfileRefId = executionSlots[i].TelegraphProfileRefId,
                    EmissionProfileRefId = executionSlots[i].EmissionProfileRefId,
                    BaseWeight = 1f,
                    AvailabilityFlags = 0u,
                });
                runtimeExecutionSlots.Add(executionSlots[i]);
            }
        }

        private static HazardEmitterPatternExecutionSlotBuffer CreateExecutionSlot(
            int patternSlotId,
            int telegraphProfileRefId,
            float telegraphDurationSec,
            int bulletTypeKey,
            float baseAngleDeg,
            float cooldownSec)
        {
            return new HazardEmitterPatternExecutionSlotBuffer
            {
                PatternSlotId = patternSlotId,
                TelegraphProfileRefId = telegraphProfileRefId,
                EmissionProfileRefId = bulletTypeKey,
                TelegraphDurationSec = telegraphDurationSec,
                BulletTypeKey = bulletTypeKey,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                SpawnOffset = float2.zero,
                LineStart = float2.zero,
                LineEnd = float2.zero,
                SampleSpacing = 0f,
                PointSetCount = 0,
                Point0 = float2.zero,
                Point1 = float2.zero,
                Point2 = float2.zero,
                Point3 = float2.zero,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = baseAngleDeg,
                AimAngleOffsetDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                LineNormalAngleOffsetDeg = 0f,
                SpiralStepDeg = 0f,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                NWayAngleSpacingDeg = 0f,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = cooldownSec,
            };
        }

        private static void SetSingleton<T>(EntityManager em, T value) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            var entity = query.GetSingletonEntity();
            em.SetComponentData(entity, value);
        }

        private static Entity CreateSourceTemplate(EntityManager em, bool includeLegacyHazardHierarchy = true)
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
            em.AddBuffer<SourceHazardActorRefBuffer>(entity);

            em.GetBuffer<LinkedEntityGroup>(entity).Add(new LinkedEntityGroup { Value = entity });

            if (!includeLegacyHazardHierarchy)
                return entity;

            var actor = em.CreateEntity(
                typeof(Prefab),
                typeof(HazardActorComponent),
                typeof(HazardActorAppliedConfigBaselineComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorRuntimeBaselineComponent),
                typeof(HazardActorBehaviorPhaseBaselineComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorBehaviorPhaseStateComponent),
                typeof(HazardActorPatternSelectorStateComponent),
                typeof(HazardActorPhaseTransitionRuntimeComponent),
                typeof(HazardActorPhaseTransitionSignalComponent),
                typeof(HazardActorPresencePresentationSignalComponent));
            em.SetComponentData(actor, new HazardActorComponent
            {
                ActorId = 7,
                SourceEntity = entity,
            });
            em.SetComponentData(actor, new HazardActorAppliedConfigBaselineComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(actor, new HazardActorRuntimeBaselineComponent
            {
                InitialPresenceState = HazardActorPresenceStateId.Hidden,
            });
            em.SetComponentData(actor, new HazardActorBehaviorPhaseBaselineComponent
            {
                InitialPhaseId = 1,
            });
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorBehaviorPhaseStateComponent
            {
                CurrentPhaseId = 1,
                PreviousPhaseId = 1,
                PhaseVersion = 0u,
            });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
                CurrentCandidateOrder = -1,
                LastResolvedPhaseVersion = 0u,
                LastConsumedCycleVersion = 0u,
            });
            em.SetComponentData(actor, new HazardActorPhaseTransitionRuntimeComponent
            {
                State = HazardActorPhaseTransitionStateId.Idle,
                PendingFromPhaseId = -1,
                PendingToPhaseId = -1,
                ElapsedSec = 0f,
                DurationSec = 0f,
                TransitionVersion = 0u,
            });
            em.SetComponentData(actor, new HazardActorPhaseTransitionSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPhaseTransitionSignalCueId.None,
                Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold,
                PreviousPhaseId = 1,
                CurrentPhaseId = 1,
                PendingToPhaseId = -1,
            });
            em.SetComponentData(actor, new HazardActorPresencePresentationSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPresencePresentationCueId.None,
            });
            em.AddBuffer<HazardActorEmitterRefBuffer>(actor);
            var actorPolicies = em.AddBuffer<HazardActorPhaseSelectorPolicyBuffer>(actor);
            actorPolicies.Add(new HazardActorPhaseSelectorPolicyBuffer
            {
                PhaseId = 1,
                SelectionMode = HazardActorSelectionModeId.OrderedPriority,
            });
            em.AddBuffer<HazardActorPhaseSelectorCandidateBuffer>(actor);
            em.AddBuffer<HazardActorPhaseProgressTransitionBuffer>(actor);
            em.GetBuffer<SourceHazardActorRefBuffer>(entity).Add(new SourceHazardActorRefBuffer
            {
                ActorEntity = actor,
                ActorId = 7,
            });
            em.GetBuffer<LinkedEntityGroup>(entity).Add(new LinkedEntityGroup { Value = actor });

            var emitter = em.CreateEntity(
                typeof(Prefab),
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(HazardEmitterComponent),
                typeof(HazardEmitterAppliedConfigBaselineComponent),
                typeof(HazardEmitterAppliedConfigComponent),
                typeof(HazardEmitterTelegraphProfileBaselineComponent),
                typeof(HazardEmitterTelegraphProfileComponent),
                typeof(HazardEmitterEmissionProfileBaselineComponent),
                typeof(HazardEmitterEmissionProfileComponent),
                typeof(HazardEmitterRuntimeStateComponent),
                typeof(HazardEmitterCycleSignalComponent));
            em.SetComponentData(emitter, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(emitter, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(emitter, new HazardEmitterComponent
            {
                EmitterId = 41,
                ActorEntity = actor,
                ActivationPolicy = HazardEmitterActivationPolicyId.AlwaysCycle,
                InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                AnchorKind = HazardEmitterAnchorKindId.ObjectBound,
                Mobility = HazardEmitterMobilityId.Static,
            });
            em.SetComponentData(emitter, new HazardEmitterAppliedConfigBaselineComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
                LocalOffset = new float3(0.5f, 0f, 0f),
                TelegraphProfileRefId = 10,
                EmissionProfileRefId = 20,
            });
            em.SetComponentData(emitter, new HazardEmitterAppliedConfigComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
                LocalOffset = new float3(0.5f, 0f, 0f),
                TelegraphProfileRefId = 10,
                EmissionProfileRefId = 20,
            });
            em.SetComponentData(emitter, new HazardEmitterTelegraphProfileBaselineComponent
            {
                ProfileId = 10,
                TelegraphDurationSec = 0.25f,
            });
            em.SetComponentData(emitter, new HazardEmitterTelegraphProfileComponent
            {
                ProfileId = 10,
                TelegraphDurationSec = 0.25f,
            });
            em.SetComponentData(emitter, new HazardEmitterEmissionProfileBaselineComponent
            {
                ProfileId = 20,
                BulletTypeKey = 901,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 15f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = 1f,
            });
            em.SetComponentData(emitter, new HazardEmitterEmissionProfileComponent
            {
                ProfileId = 20,
                BulletTypeKey = 901,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 15f,
                LineNormalSide = WaveLineNormalSideId.Left,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventShotIntervalSec = 0f,
                EventRepeatCount = 1,
                CooldownSec = 1f,
            });
            em.SetComponentData(emitter, new HazardEmitterRuntimeStateComponent
            {
                LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                StateElapsedSec = 0f,
            });
            em.AddComponentData(emitter, new HazardEmitterSelectedPatternRuntimeComponent
            {
                AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId,
            });
            em.AddComponentData(emitter, new HazardEmitterCycleSignalComponent
            {
                CompletedVersion = 0u,
            });
            em.AddBuffer<HazardEmitterPatternSlotBuffer>(emitter);
            em.AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
            var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
            var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
            HazardEmitterPatternSetCompatibilityUtility.ReseedSingleCompatibilitySlot(
                ref patternSlots,
                ref executionSlots,
                telegraphProfileRefId: 10,
                telegraphDurationSec: 0.5f,
                in emission);

            em.GetBuffer<HazardActorEmitterRefBuffer>(actor).Add(new HazardActorEmitterRefBuffer
            {
                EmitterEntity = emitter,
                EmitterId = 41,
            });
            em.GetBuffer<HazardActorPhaseSelectorCandidateBuffer>(actor).Add(new HazardActorPhaseSelectorCandidateBuffer
            {
                PhaseId = 1,
                OrderIndex = 0,
                EmitterId = 41,
                PatternSlotId = HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId,
            });
            em.GetBuffer<LinkedEntityGroup>(entity).Add(new LinkedEntityGroup { Value = emitter });
            return entity;
        }

        private static Entity FindActorForSource(EntityManager em, Entity source)
        {
            var actorRefs = em.GetBuffer<SourceHazardActorRefBuffer>(source);
            Assert.That(actorRefs.Length, Is.GreaterThanOrEqualTo(1));
            return actorRefs[0].ActorEntity;
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

        private static Entity FindEmitterForActor(EntityManager em, Entity actor)
        {
            var emitterRefs = em.GetBuffer<HazardActorEmitterRefBuffer>(actor);
            Assert.That(emitterRefs.Length, Is.GreaterThanOrEqualTo(1));
            return emitterRefs[0].EmitterEntity;
        }

        private static HazardActorArchetypeFixture CreateHazardActorArchetype(string name, int actorId, int emitterId)
        {
            var fixture = new HazardActorArchetypeFixture();
            fixture.Root = new GameObject(name);
            var actor = fixture.Root.AddComponent<HazardActorAuthoring>();
            actor.ActorId = actorId;

            var emitterGo = new GameObject("emitter");
            emitterGo.transform.SetParent(fixture.Root.transform);
            var emitter = emitterGo.AddComponent<HazardEmitterAuthoring>();
            emitter.EmitterId = emitterId;

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

            emitter.Slots = new[]
            {
                new HazardEmitterPatternSlotAuthoring
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
