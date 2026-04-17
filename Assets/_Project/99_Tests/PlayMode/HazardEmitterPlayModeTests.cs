using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterPlayModeTests : PlayModeTestBase
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
                    Object.DestroyImmediate(Root);
                if (Telegraph != null)
                    Object.DestroyImmediate(Telegraph);
                if (Emission != null)
                    Object.DestroyImmediate(Emission);
                if (Bullet != null)
                    Object.DestroyImmediate(Bullet);
            }
        }

        [UnityTest]
        public IEnumerator PlayMode_AlwaysCycleHazardEmitter_AppendsAndConsumesDiscreteEmit()
        {
            ForceDisposeSharedContainersIfNeeded();
            InitializeSharedContainers();

            try
            {
                using var world = new World("PlayMode_HazardEmitter_AlwaysCycle");
                var em = world.EntityManager;

                SetSingleton(em, new BulletFrameCounterComponent { Value = 1u });
                SetSingleton(em, new FixedTickStepRuntimeComponent
                {
                    FrameDeltaTime = 1f / 60f,
                    LogicDeltaTime = 1f / 60f,
                    LogicStepCount = 1,
                    HasStep = 1,
                    UsingFixedTick = 0,
                });
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                });
                SetSingleton(em, new StageTopologyStateComponent
                {
                    SelectedStageId = 1,
                    AppliedStageId = 1,
                    Ready = 1,
                });

                var channel = em.CreateEntity(
                    typeof(DiscreteEmitChannelSingletonTag),
                    typeof(DiscreteEmitPolicyComponent),
                    typeof(DiscreteEmitBacklogMetricsComponent));
                em.SetComponentData(channel, new DiscreteEmitPolicyComponent
                {
                    BudgetPerFrame = 8,
                    MaxPendingCount = 32,
                    MaxPendingAgeFrames = 120u,
                });
                em.SetComponentData(channel, default(DiscreteEmitBacklogMetricsComponent));
                em.AddBuffer<DiscreteEmitRequestBuffer>(channel);

                var source = em.CreateEntity();
                var activeCounts = em.AddBuffer<SourceActiveBulletCountBuffer>(source);
                activeCounts.Add(new SourceActiveBulletCountBuffer
                {
                    BulletTypeKey = 801,
                    ActiveCount = 0,
                });

                var emitter = em.CreateEntity(
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
                    typeof(HazardEmitterSelectedPatternRuntimeComponent),
                    typeof(HazardEmitterCycleSignalComponent),
                    typeof(HazardEmitterCoordinatorStateComponent));
                em.SetComponentData(emitter, LocalTransform.FromPosition(new float3(2f, 0f, 1f)));
                em.SetComponentData(emitter, new LocalToWorld { Value = float4x4.Translate(new float3(2f, 0f, 1f)) });
                var actor = CreateActor(em, source, actorId: 11);
                em.SetComponentData(emitter, new HazardEmitterComponent
                {
                    EmitterId = 11,
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
                    LocalOffset = float3.zero,
                    TelegraphProfileRefId = 1,
                    EmissionProfileRefId = 1,
                });
                em.SetComponentData(emitter, new HazardEmitterAppliedConfigComponent
                {
                    IsEnabled = 1,
                    IsSuppressed = 0,
                    LocalOffset = float3.zero,
                    TelegraphProfileRefId = 1,
                    EmissionProfileRefId = 1,
                });
                em.SetComponentData(emitter, new HazardEmitterTelegraphProfileBaselineComponent
                {
                    ProfileId = 1,
                    TelegraphDurationSec = 0f,
                });
                em.SetComponentData(emitter, new HazardEmitterTelegraphProfileComponent
                {
                    ProfileId = 1,
                    TelegraphDurationSec = 0f,
                });
                em.SetComponentData(emitter, new HazardEmitterEmissionProfileBaselineComponent
                {
                    ProfileId = 1,
                    BulletTypeKey = 801,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Fixed,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                    BaseAngleDeg = 0f,
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
                    ProfileId = 1,
                    BulletTypeKey = 801,
                    PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                    AimMode = WaveAimModeId.Fixed,
                    AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                    BaseAngleDeg = 0f,
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
                em.SetComponentData(emitter, new HazardEmitterSelectedPatternRuntimeComponent
                {
                    AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId,
                });
                em.SetComponentData(emitter, new HazardEmitterCycleSignalComponent
                {
                    CompletedVersion = 0u,
                });
                em.SetComponentData(emitter, new HazardEmitterCoordinatorStateComponent
                {
                    ActivationAllowed = 0,
                    SuppressionReasonMask = 0u,
                    LastPlayerDistanceSq = float.MaxValue,
                });
                em.AddBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                em.AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
                var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitter);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitter);
                var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitter);
                HazardEmitterPatternSetCompatibilityUtility.ReseedSingleCompatibilitySlot(
                    ref patternSlots,
                    ref executionSlots,
                    telegraphProfileRefId: 1,
                    telegraphDurationSec: 0f,
                    in emission);
                em.GetBuffer<HazardActorEmitterRefBuffer>(actor).Add(new HazardActorEmitterRefBuffer
                {
                    EmitterEntity = emitter,
                    EmitterId = 11,
                });
                em.GetBuffer<HazardActorPhaseSelectorCandidateBuffer>(actor).Add(new HazardActorPhaseSelectorCandidateBuffer
                {
                    PhaseId = 1,
                    OrderIndex = 0,
                    EmitterId = 11,
                    PatternSlotId = HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId,
                });

                var pooledBullet = CreatePooledBullet(em, 801, 4f, 6f);
                BulletFieldShared.FreeByKey.Add(801, pooledBullet);

                world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                var selectedPattern = em.GetComponentData<HazardEmitterSelectedPatternRuntimeComponent>(emitter);
                var runtime = em.GetComponentData<HazardEmitterRuntimeStateComponent>(emitter);
                Assert.That(selectedPattern.AppliedPatternSlotId, Is.EqualTo(HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId));
                Assert.That(runtime.LifecycleState, Is.EqualTo(HazardEmitterLifecycleStateId.Dormant));
                Assert.That(runtime.StateElapsedSec, Is.EqualTo(0f));
                Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));

                world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardEmitterEmitBuildSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<DiscreteEmitExecutionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.IsComponentEnabled<BulletActiveTag>(pooledBullet), Is.True);
                Assert.That(em.GetBuffer<SourceActiveBulletCountBuffer>(source)[0].ActiveCount, Is.EqualTo(1));
                Assert.That(em.GetBuffer<DiscreteEmitRequestBuffer>(channel).Length, Is.EqualTo(0));
            }
            finally
            {
                ForceDisposeSharedContainersIfNeeded();
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayMode_PlacementDeliveredHazardActor_RemainsHiddenWithoutOrchestration()
        {
            using var archetype = CreateHazardActorArchetype("placement_actor_playmode", actorId: 31, emitterId: 41);
            var stageCatalog = CreatePlacementStageCatalog(archetype.Root);

            try
            {
                using var world = new World("PlayMode_HazardPlacement_Hidden");
                var em = world.EntityManager;

                world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
                var prefabCatalogEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).GetSingletonEntity();
                em.SetComponentData(prefabCatalogEntity, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                });

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForPlacement(em, source, placementInstanceId: 101);
                var emitter = em.GetBuffer<HazardActorEmitterRefBuffer>(actor)[0].EmitterEntity;

                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));

                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                });
                world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);

                Assert.That(coordinator.ActivationAllowed, Is.EqualTo(0));
                Assert.That((coordinator.SuppressionReasonMask & (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceHidden) != 0u, Is.True);
                Assert.That(selector.TargetEmitterId, Is.EqualTo(-1));
                Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(-1));
            }
            finally
            {
                Object.DestroyImmediate(stageCatalog);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayMode_PlacementDeliveredHazardActor_OnStageStartSpawnAndPhaseSet_SerializesAcrossFrames()
        {
            using var archetype = CreateHazardActorArchetype("placement_actor_orchestrated", actorId: 32, emitterId: 42);
            ConfigureTwoPhaseArchetype(archetype, emitterId: 42, activationDurationSec: 0.05f, retireDurationSec: 0f, transitionLeadInSec: 0.05f);
            var stageCatalog = CreatePlacementStageCatalog(
                archetype.Root,
                new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceId = 101,
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 2,
                        TargetPlacementInstanceId = 101,
                        ActionType = HazardActorOrchestrationActionId.PhaseSet,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                        TargetPhaseId = 2,
                    },
                });

            try
            {
                using var world = new World("PlayMode_HazardPlacement_Orchestration");
                var em = world.EntityManager;

                world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, new FixedTickStepRuntimeComponent
                {
                    FrameDeltaTime = 0.05f,
                    LogicDeltaTime = 0.05f,
                    LogicStepCount = 1,
                    HasStep = 1,
                    UsingFixedTick = 0,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
                var prefabCatalogEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).GetSingletonEntity();
                em.SetComponentData(prefabCatalogEntity, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                });

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForPlacement(em, source, placementInstanceId: 101);
                var emitter = em.GetBuffer<HazardActorEmitterRefBuffer>(actor)[0].EmitterEntity;
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                });

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPhaseTransitionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                var ruleStates = em.GetBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(source);
                Assert.That(ruleStates[0].HasFired, Is.EqualTo(1));
                Assert.That(ruleStates[1].HasFired, Is.EqualTo(0));
                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Activating));

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPhaseTransitionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                ruleStates = em.GetBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(source);
                Assert.That(ruleStates[1].HasFired, Is.EqualTo(0));
                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPhaseTransitionSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                var transitionRuntime = em.GetComponentData<HazardActorPhaseTransitionRuntimeComponent>(actor);
                var transitionSignal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actor);
                ruleStates = em.GetBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(source);
                Assert.That(ruleStates[1].HasFired, Is.EqualTo(1));
                Assert.That(transitionRuntime.State, Is.EqualTo(HazardActorPhaseTransitionStateId.Preparing));
                Assert.That(transitionSignal.Cue, Is.EqualTo(HazardActorPhaseTransitionSignalCueId.PreparingStarted));
                Assert.That(transitionSignal.PendingToPhaseId, Is.EqualTo(2));

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPhaseTransitionSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardEmitterCoordinatorSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                var phaseState = em.GetComponentData<HazardActorBehaviorPhaseStateComponent>(actor);
                var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
                transitionSignal = em.GetComponentData<HazardActorPhaseTransitionSignalComponent>(actor);
                Assert.That(phaseState.CurrentPhaseId, Is.EqualTo(2));
                Assert.That(transitionSignal.Cue, Is.EqualTo(HazardActorPhaseTransitionSignalCueId.PhaseCommitted));
                Assert.That(selector.TargetEmitterId, Is.EqualTo(42));
                Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(1));
                Assert.That(selector.LastResolvedPhaseVersion, Is.EqualTo(phaseState.PhaseVersion));

                var coordinator = em.GetComponentData<HazardEmitterCoordinatorStateComponent>(emitter);
                Assert.That(coordinator.ActivationAllowed, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(stageCatalog);
            }

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayMode_PlacementDeliveredHazardActor_RetireRequest_DrivesRetiringToHidden()
        {
            using var archetype = CreateHazardActorArchetype("placement_actor_retire", actorId: 33, emitterId: 43);
            ConfigureTwoPhaseArchetype(archetype, emitterId: 43, activationDurationSec: 0f, retireDurationSec: 0.05f, transitionLeadInSec: 0f);
            var stageCatalog = CreatePlacementStageCatalog(
                archetype.Root,
                new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceId = 101,
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 2,
                        TargetPlacementInstanceId = 101,
                        ActionType = HazardActorOrchestrationActionId.Retire,
                        TriggerType = HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove,
                        TriggerThresholdNormalized = 0.5f,
                    },
                });
            stageCatalog.Entries[0].Definition.SourceBindings[0].ThresholdDepleted = 4;

            try
            {
                using var world = new World("PlayMode_HazardPlacement_Retire");
                var em = world.EntityManager;

                world.GetOrCreateSystem<StageTopologyBootstrapSystem>().Update(world.Unmanaged);
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Idle,
                });
                SetSingleton(em, new FixedTickStepRuntimeComponent
                {
                    FrameDeltaTime = 0.05f,
                    LogicDeltaTime = 0.05f,
                    LogicStepCount = 1,
                    HasStep = 1,
                    UsingFixedTick = 0,
                });

                var requestEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyRequestComponent>()).GetSingletonEntity();
                var prefabCatalogEntity = em.CreateEntityQuery(ComponentType.ReadOnly<StageTopologyPrefabCatalogComponent>()).GetSingletonEntity();
                em.SetComponentData(prefabCatalogEntity, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = CreateSourceTemplate(em),
                });

                PublishCatalogAndRequest(em, requestEntity, stageCatalog, stageId: 1);
                world.GetOrCreateSystem<StageTopologyApplyPrepareSystem>().Update(world.Unmanaged);

                var source = FindAppliedSource(em);
                var actor = FindActorForPlacement(em, source, placementInstanceId: 101);
                SetSingleton(em, new RunDirectorStageStateComponent
                {
                    State = RunDirectorStageStateId.Running,
                });

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Active));

                var sourceSpawn = em.GetComponentData<SourceSpawnComponent>(source);
                sourceSpawn.CollectedCount = 2;
                em.SetComponentData(source, sourceSpawn);

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Retiring));

                world.GetOrCreateSystem<HazardActorOrchestrationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<HazardActorPresenceSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();

                Assert.That(em.GetComponentData<HazardActorRuntimeStateComponent>(actor).PresenceState, Is.EqualTo(HazardActorPresenceStateId.Hidden));
            }
            finally
            {
                Object.DestroyImmediate(stageCatalog);
            }

            yield break;
        }

        private static Entity CreateActor(EntityManager em, Entity source, int actorId)
        {
            var entity = em.CreateEntity(
                typeof(HazardActorComponent),
                typeof(HazardActorAppliedConfigBaselineComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorPresencePolicyComponent),
                typeof(HazardActorRuntimeBaselineComponent),
                typeof(HazardActorBehaviorPhaseBaselineComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorBehaviorPhaseStateComponent),
                typeof(HazardActorPatternSelectorStateComponent),
                typeof(HazardActorPhaseTransitionRuntimeComponent),
                typeof(HazardActorPhaseTransitionSignalComponent),
                typeof(HazardActorPresencePresentationSignalComponent));
            em.SetComponentData(entity, new HazardActorComponent
            {
                ActorId = actorId,
                SourceEntity = source,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigBaselineComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorAppliedConfigComponent
            {
                IsEnabled = 1,
                IsSuppressed = 0,
            });
            em.SetComponentData(entity, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                ActivationDurationSec = 0f,
                RetireTrigger = HazardActorPresenceTriggerMode.None,
                RetireDurationSec = 0f,
            });
            em.SetComponentData(entity, new HazardActorRuntimeBaselineComponent
            {
                InitialPresenceState = HazardActorPresenceStateId.Active,
            });
            em.SetComponentData(entity, new HazardActorBehaviorPhaseBaselineComponent
            {
                InitialPhaseId = 1,
            });
            em.SetComponentData(entity, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Active,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(entity, new HazardActorBehaviorPhaseStateComponent
            {
                CurrentPhaseId = 1,
                PreviousPhaseId = 1,
                PhaseVersion = 0u,
            });
            em.SetComponentData(entity, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
                CurrentCandidateOrder = -1,
                LastResolvedPhaseVersion = 0u,
                LastConsumedCycleVersion = 0u,
            });
            em.SetComponentData(entity, new HazardActorPhaseTransitionRuntimeComponent
            {
                State = HazardActorPhaseTransitionStateId.Idle,
                PendingFromPhaseId = -1,
                PendingToPhaseId = -1,
                ElapsedSec = 0f,
                DurationSec = 0f,
                TransitionVersion = 0u,
            });
            em.SetComponentData(entity, new HazardActorPhaseTransitionSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPhaseTransitionSignalCueId.None,
                Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold,
                PreviousPhaseId = 1,
                CurrentPhaseId = 1,
                PendingToPhaseId = -1,
            });
            em.SetComponentData(entity, new HazardActorPresencePresentationSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPresencePresentationCueId.None,
            });
            em.AddBuffer<HazardActorEmitterRefBuffer>(entity);
            var policies = em.AddBuffer<HazardActorPhaseSelectorPolicyBuffer>(entity);
            policies.Add(new HazardActorPhaseSelectorPolicyBuffer
            {
                PhaseId = 1,
                SelectionMode = HazardActorSelectionModeId.OrderedPriority,
            });
            em.AddBuffer<HazardActorPhaseSelectorCandidateBuffer>(entity);
            em.AddBuffer<HazardActorPhaseProgressTransitionBuffer>(entity);
            return entity;
        }

        private static Entity CreatePooledBullet(EntityManager em, int bulletTypeKey, float speed, float lifetime)
        {
            var entity = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(BulletVelocityComponent),
                typeof(BulletLifetimeComponent),
                typeof(BulletSpeedComponent),
                typeof(BulletLifetimeMaxComponent),
                typeof(BulletLifecycleRequestComponent),
                typeof(BulletLifecycleContactComponent),
                typeof(BulletTypeKeyComponent),
                typeof(BulletSourceRefComponent),
                typeof(BulletLifecycleTraceComponent),
                typeof(BulletActiveTag),
                typeof(BulletDespawnRequestTag));

            em.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(entity, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(entity, new BulletVelocityComponent { Value = float2.zero });
            em.SetComponentData(entity, new BulletLifetimeComponent { Value = 0f });
            em.SetComponentData(entity, new BulletSpeedComponent { Value = speed });
            em.SetComponentData(entity, new BulletLifetimeMaxComponent { Value = lifetime });
            em.SetComponentData(entity, default(BulletLifecycleRequestComponent));
            em.SetComponentData(entity, default(BulletLifecycleContactComponent));
            em.SetComponentData(entity, new BulletTypeKeyComponent { Value = bulletTypeKey });
            em.SetComponentData(entity, new BulletSourceRefComponent { Value = Entity.Null });
            em.SetComponentData(entity, default(BulletLifecycleTraceComponent));
            em.SetComponentEnabled<BulletActiveTag>(entity, false);
            em.SetComponentEnabled<BulletDespawnRequestTag>(entity, false);
            return entity;
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
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            Entity entity = query.IsEmptyIgnoreFilter ? em.CreateEntity(typeof(T)) : query.GetSingletonEntity();
            em.SetComponentData(entity, value);
        }

        private static StageCatalogSO CreatePlacementStageCatalog(
            GameObject archetypePrefab,
            HazardActorOrchestrationRuleBinding[] orchestrationRules = null)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = 1;
            layout.SchemaVersion = 2;
            layout.Grid = new StageGridSpec
            {
                Width = 1,
                Height = 1,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            layout.Cells = new[]
            {
                new StageCellLayoutData { SourceRegionId = 1001u }
            };
            layout.SourceRegions = new[]
            {
                new StageSourceRegionLayoutData
                {
                    StableId = 1001u,
                    Active = true,
                    AnchorCell = Vector2Int.zero,
                    AnchorOffset = Vector2.zero,
                }
            };
            layout.DepositRegions = System.Array.Empty<StageDepositRegionLayoutData>();
            layout.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = Vector2Int.zero,
                AnchorOffset = Vector2.zero,
                YawDeg = 0f,
            };
            layout.Presentations = System.Array.Empty<StagePresentationLayoutData>();

            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = 1;
            definition.SourceBindings = new[]
            {
                new StageSourceBinding
                {
                    SourceStableId = 1001u,
                    InitialSourceState = SourceStateId.Normal,
                    ThresholdWeakened = 0,
                    ThresholdDepleted = 0,
                    SustainSlots = System.Array.Empty<SustainSlotBinding>(),
                    EventSlots = System.Array.Empty<EventSlotBinding>(),
                    HazardActorPlacements = new[]
                    {
                        new HazardActorPlacementBinding
                        {
                            PlacementInstanceId = 101,
                            ActorArchetypePrefab = archetypePrefab,
                            LocalOffset = new Vector3(2f, 0f, 0f),
                        }
                    },
                    HazardActorOrchestrationRules = orchestrationRules ?? System.Array.Empty<HazardActorOrchestrationRuleBinding>(),
                }
            };

            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            catalog.Entries = new[]
            {
                new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "playmode_placement",
                    Definition = definition,
                    Layout = layout,
                }
            };
            return catalog;
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

        private static Entity FindAppliedSource(EntityManager em)
        {
            using var sourceQuery = em.CreateEntityQuery(
                ComponentType.ReadOnly<StageTopologyOwnedTag>(),
                ComponentType.ReadOnly<StageTopologySourceTag>(),
                ComponentType.ReadOnly<SourceStableIdComponent>());
            using var sources = sourceQuery.ToEntityArray(Allocator.Temp);
            Assert.That(sources.Length, Is.EqualTo(1));
            return sources[0];
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

        private static void ConfigureTwoPhaseArchetype(
            HazardActorArchetypeFixture fixture,
            int emitterId,
            float activationDurationSec,
            float retireDurationSec,
            float transitionLeadInSec)
        {
            var actor = fixture.Root.GetComponent<HazardActorAuthoring>();
            actor.InitialPhaseId = 1;
            actor.ActivationTrigger = HazardActorPresenceTriggerMode.None;
            actor.RetireTrigger = HazardActorPresenceTriggerMode.None;
            actor.ActivationDurationSec = activationDurationSec;
            actor.RetireDurationSec = retireDurationSec;
            actor.PhaseSelectorPolicies = new[]
            {
                new HazardActorPhaseSelectorPolicyAuthoring
                {
                    PhaseId = 1,
                    SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                    Candidates = new[]
                    {
                        new HazardActorPhaseSelectorCandidateAuthoring { EmitterId = emitterId, PatternSlotId = 1 },
                    },
                },
                new HazardActorPhaseSelectorPolicyAuthoring
                {
                    PhaseId = 2,
                    SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                    Candidates = new[]
                    {
                        new HazardActorPhaseSelectorCandidateAuthoring { EmitterId = emitterId, PatternSlotId = 1 },
                    },
                },
            };
            actor.PhaseProgressTransitions = new[]
            {
                new HazardActorPhaseProgressTransitionAuthoring
                {
                    FromPhaseId = 1,
                    ToPhaseId = 2,
                    ProgressThresholdNormalized = 0.5f,
                    TransitionLeadInSec = transitionLeadInSec,
                }
            };
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
            fixture.Bullet.Editor_SetDefinitionId(9000 + emitterId);
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
