using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    internal static class StageTopologyTemplateFactory
    {
        public static Entity CreateSourceTemplate(EntityManager em)
        {
            return CreateSourceTemplate(em, null);
        }

        public static Entity CreateSourceTemplate(EntityManager em, StageTopologyPrefabCatalogSO catalog)
        {
            return CreateDefaultSourceTemplate(em);
        }

        private static Entity CreateDefaultSourceTemplate(EntityManager em)
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
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceSpawnRuntimeComponent { SpawnSequence = 1u });
            em.SetComponentData(entity, new SourceAnchorComponent { Position = float3.zero });
            var sourceShape = new Shape2DComponent
            {
                Kind = Shape2DKind.Circle,
                Radius = 8f,
                Size = new float2(12f, 8f),
            };
            var sourceDerived = default(SourceShapeDerivedComponent);
            SourceRuntimeApplyUtility.RefreshSourceShapeDerived(in sourceShape, ref sourceDerived);
            em.SetComponentData(entity, sourceShape);
            em.SetComponentData(entity, sourceDerived);
            em.SetComponentData(entity, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.08f,
                DropPerCollect = 0.12f,
                TopKSampleCount = 6,
                ActiveRatioThreshold = 0.35f,
                RecoveryCooldownFrames = 45u,
                RecoveryWaveSeedCount = 2,
                RecoveryWaveClusterSize = 4,
                RecoveryWaveRestoreValue = 0.4f,
                RecoveryRecentCleanBiasFrames = 90u,
            });
            em.SetComponentData(entity, new SourcePollutionGridComponent
            {
                CellSize = 2f,
                InvCellSize = 0.5f,
                HalfExtents = sourceDerived.HalfExtents,
                OriginX = -sourceDerived.HalfExtents.x,
                OriginZ = -sourceDerived.HalfExtents.y,
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
            em.AddBuffer<SourceRegionCellIndexBuffer>(entity).Clear();
            em.AddBuffer<SourceHazardActorPlacementRefBuffer>(entity).Clear();
            em.AddBuffer<SourceHazardActorOrchestrationRuleBuffer>(entity).Clear();
            em.AddBuffer<SourceHazardActorOrchestrationRuleStateBuffer>(entity).Clear();
            em.AddBuffer<SourceHazardActorRefBuffer>(entity).Clear();
            var linkedEntities = em.AddBuffer<LinkedEntityGroup>(entity);
            linkedEntities.Add(entity);
            return entity;
        }

        public static bool TryAttachHazardPlacementArchetype(
            EntityManager em,
            Entity sourceEntity,
            int placementInstanceId,
            GameObject actorArchetypePrefab,
            float3 localOffset,
            float localYawDeg,
            out string error)
        {
            error = string.Empty;

            if (sourceEntity == Entity.Null || !em.Exists(sourceEntity))
            {
                error = "Source entity is invalid.";
                return false;
            }

            if (placementInstanceId < 1)
            {
                error = "PlacementInstanceId must be >= 1.";
                return false;
            }

            if (!TryResolveStandaloneActorAuthoring(actorArchetypePrefab, out var actorAuthoring, out error))
                return false;

            if (!HazardActorAuthoringValidationUtility.TryValidateStandalone(
                    actorAuthoring,
                    out var compatibilitySeed,
                    out var phaseTransitions,
                    out _,
                    out error))
            {
                return false;
            }

            if (!em.HasBuffer<SourceHazardActorRefBuffer>(sourceEntity))
                em.AddBuffer<SourceHazardActorRefBuffer>(sourceEntity);
            if (!em.HasBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity))
                em.AddBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity);
            if (!em.HasBuffer<LinkedEntityGroup>(sourceEntity))
            {
                var linked = em.AddBuffer<LinkedEntityGroup>(sourceEntity);
                linked.Add(sourceEntity);
            }

            return TryCreateStandaloneActorHierarchy(
                em,
                sourceEntity,
                placementInstanceId,
                localOffset,
                localYawDeg,
                actorAuthoring,
                compatibilitySeed,
                phaseTransitions,
                out error);
        }

        private static bool TryResolveStandaloneActorAuthoring(
            GameObject actorArchetypePrefab,
            out HazardActorAuthoring actorAuthoring,
            out string error)
        {
            actorAuthoring = null;
            error = string.Empty;

            if (actorArchetypePrefab == null)
            {
                error = "ActorArchetypePrefab is null.";
                return false;
            }

            var actors = actorArchetypePrefab.GetComponentsInChildren<HazardActorAuthoring>(true);
            if (actors == null || actors.Length <= 0)
            {
                error = $"ActorArchetypePrefab '{actorArchetypePrefab.name}' does not contain HazardActorAuthoring.";
                return false;
            }

            if (actors.Length > 1)
            {
                error = $"ActorArchetypePrefab '{actorArchetypePrefab.name}' must contain exactly one HazardActorAuthoring root.";
                return false;
            }

            actorAuthoring = actors[0];
            return actorAuthoring != null;
        }

        private static bool TryCreateStandaloneActorHierarchy(
            EntityManager em,
            Entity sourceEntity,
            int placementInstanceId,
            float3 localOffset,
            float localYawDeg,
            HazardActorAuthoring actorAuthoring,
            HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            HazardActorPhaseProgressTransitionBuffer[] phaseTransitions,
            out string error)
        {
            error = string.Empty;
            if (actorAuthoring == null)
            {
                error = "HazardActorAuthoring is null.";
                return false;
            }

            var createdEntities = new List<Entity>(8);

            var actorEntity = em.CreateEntity(
                typeof(HazardActorComponent),
                typeof(HazardActorPlacementComponent),
                typeof(HazardActorAppliedConfigBaselineComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorRuntimeBaselineComponent),
                typeof(HazardActorBehaviorPhaseBaselineComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorBehaviorPhaseStateComponent),
                typeof(HazardActorPatternSelectorStateComponent),
                typeof(HazardActorPhaseTransitionRuntimeComponent),
                typeof(HazardActorPhaseTransitionSignalComponent),
                typeof(HazardActorPresencePresentationSignalComponent),
                typeof(HazardActorOrchestrationRequestSignalComponent),
                typeof(HazardActorOrchestrationRequestConsumptionComponent));
            createdEntities.Add(actorEntity);

            byte actorEnabled = actorAuthoring.Enabled ? (byte)1 : (byte)0;
            byte actorSuppressed = actorAuthoring.StartSuppressed ? (byte)1 : (byte)0;

            em.SetComponentData(actorEntity, new HazardActorComponent
            {
                ActorId = placementInstanceId,
                SourceEntity = sourceEntity,
            });
            em.SetComponentData(actorEntity, new HazardActorPlacementComponent
            {
                PlacementInstanceId = placementInstanceId,
                LocalOffset = localOffset,
                LocalYawDeg = localYawDeg,
            });
            em.SetComponentData(actorEntity, new HazardActorAppliedConfigBaselineComponent
            {
                IsEnabled = actorEnabled,
                IsSuppressed = actorSuppressed,
            });
            em.SetComponentData(actorEntity, new HazardActorAppliedConfigComponent
            {
                IsEnabled = actorEnabled,
                IsSuppressed = actorSuppressed,
            });
            em.AddComponentData(actorEntity, new HazardActorPresencePolicyComponent
            {
                ActivationTrigger = actorAuthoring.ActivationTrigger,
                ActivationDurationSec = math.max(0f, actorAuthoring.ActivationDurationSec),
                RetireTrigger = actorAuthoring.RetireTrigger,
                RetireDurationSec = math.max(0f, actorAuthoring.RetireDurationSec),
            });
            em.SetComponentData(actorEntity, new HazardActorRuntimeBaselineComponent
            {
                InitialPresenceState = HazardActorPresenceStateId.Hidden,
            });
            em.SetComponentData(actorEntity, new HazardActorBehaviorPhaseBaselineComponent
            {
                InitialPhaseId = compatibilitySeed.InitialPhaseId,
            });
            em.SetComponentData(actorEntity, new HazardActorRuntimeStateComponent
            {
                PresenceState = HazardActorPresenceStateId.Hidden,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actorEntity, new HazardActorBehaviorPhaseStateComponent
            {
                CurrentPhaseId = compatibilitySeed.InitialPhaseId,
                PreviousPhaseId = compatibilitySeed.InitialPhaseId,
                PhaseVersion = 0u,
            });
            em.SetComponentData(actorEntity, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
                CurrentCandidateOrder = -1,
                LastResolvedPhaseVersion = 0u,
                LastConsumedCycleVersion = 0u,
            });
            em.SetComponentData(actorEntity, new HazardActorPhaseTransitionRuntimeComponent
            {
                State = HazardActorPhaseTransitionStateId.Idle,
                PendingFromPhaseId = -1,
                PendingToPhaseId = -1,
                ElapsedSec = 0f,
                DurationSec = 0f,
                TransitionVersion = 0u,
            });
            em.SetComponentData(actorEntity, new HazardActorPhaseTransitionSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPhaseTransitionSignalCueId.None,
                Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold,
                PreviousPhaseId = compatibilitySeed.InitialPhaseId,
                CurrentPhaseId = compatibilitySeed.InitialPhaseId,
                PendingToPhaseId = -1,
            });
            em.SetComponentData(actorEntity, new HazardActorPresencePresentationSignalComponent
            {
                Version = 0u,
                Cue = HazardActorPresencePresentationCueId.None,
            });
            em.SetComponentData(actorEntity, new HazardActorOrchestrationRequestSignalComponent
            {
                Version = 0u,
                ActionType = HazardActorOrchestrationActionId.None,
                TargetPhaseId = -1,
            });
            em.SetComponentData(actorEntity, new HazardActorOrchestrationRequestConsumptionComponent
            {
                LastPresenceRequestVersion = 0u,
                LastPhaseRequestVersion = 0u,
            });
            em.AddBuffer<HazardActorEmitterRefBuffer>(actorEntity).Clear();

            var selectorPolicies = em.AddBuffer<HazardActorPhaseSelectorPolicyBuffer>(actorEntity);
            selectorPolicies.Clear();
            for (int policyIndex = 0; policyIndex < compatibilitySeed.Policies.Length; policyIndex++)
                selectorPolicies.Add(compatibilitySeed.Policies[policyIndex]);

            var selectorCandidates = em.AddBuffer<HazardActorPhaseSelectorCandidateBuffer>(actorEntity);
            selectorCandidates.Clear();
            for (int candidateIndex = 0; candidateIndex < compatibilitySeed.Candidates.Length; candidateIndex++)
                selectorCandidates.Add(compatibilitySeed.Candidates[candidateIndex]);

            var transitionBuffer = em.AddBuffer<HazardActorPhaseProgressTransitionBuffer>(actorEntity);
            transitionBuffer.Clear();
            for (int transitionIndex = 0; transitionIndex < phaseTransitions.Length; transitionIndex++)
                transitionBuffer.Add(phaseTransitions[transitionIndex]);

            em.GetBuffer<SourceHazardActorRefBuffer>(sourceEntity).Add(new SourceHazardActorRefBuffer
            {
                ActorEntity = actorEntity,
                ActorId = placementInstanceId,
            });
            em.GetBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity).Add(new SourceHazardActorPlacementRefBuffer
            {
                PlacementInstanceId = placementInstanceId,
                ActorEntity = actorEntity,
            });
            em.GetBuffer<LinkedEntityGroup>(sourceEntity).Add(actorEntity);

            var yawRotation = quaternion.RotateY(math.radians(localYawDeg));
            var emitters = actorAuthoring.GetComponentsInChildren<HazardEmitterAuthoring>(true);
            for (int emitterIndex = 0; emitterIndex < emitters.Length; emitterIndex++)
            {
                var emitterAuthoring = emitters[emitterIndex];
                if (emitterAuthoring == null)
                    continue;

                var parentActor = emitterAuthoring.GetComponentInParent<HazardActorAuthoring>(true);
                if (parentActor != actorAuthoring)
                    continue;

                if (!HazardEmitterPatternSlotAuthoringUtility.TryResolveSlots(emitterAuthoring.Slots, out var resolvedSlots, out error))
                    goto Fail;

                byte emitterEnabled = emitterAuthoring.IsEnabled ? (byte)1 : (byte)0;
                byte emitterSuppressed = emitterAuthoring.StartSuppressed ? (byte)1 : (byte)0;
                int emitterId = math.max(1, emitterAuthoring.EmitterId);
                var firstSlot = resolvedSlots[0];
                var baselineTelegraph = HazardEmitterPatternSlotAuthoringUtility.CreateBaselineTelegraph(resolvedSlots);
                var baselineEmission = HazardEmitterPatternSlotAuthoringUtility.CreateBaselineEmission(resolvedSlots);
                baselineEmission.BaseAngleDeg += localYawDeg;

                var emitterEntity = em.CreateEntity(
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
                createdEntities.Add(emitterEntity);

                var baselineConfig = new HazardEmitterAppliedConfigBaselineComponent
                {
                    IsEnabled = emitterEnabled,
                    IsSuppressed = emitterSuppressed,
                    LocalOffset = math.rotate(yawRotation, (float3)emitterAuthoring.LocalOffset),
                    TelegraphProfileRefId = firstSlot.Metadata.TelegraphProfileRefId,
                    EmissionProfileRefId = firstSlot.Metadata.EmissionProfileRefId,
                };

                em.SetComponentData(emitterEntity, new HazardEmitterComponent
                {
                    EmitterId = emitterId,
                    ActorEntity = actorEntity,
                    ActivationPolicy = emitterAuthoring.ActivationPolicy,
                    InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    AnchorKind = emitterAuthoring.AnchorKind,
                    Mobility = emitterAuthoring.Mobility,
                });
                em.SetComponentData(emitterEntity, baselineConfig);
                em.SetComponentData(emitterEntity, new HazardEmitterAppliedConfigComponent
                {
                    IsEnabled = baselineConfig.IsEnabled,
                    IsSuppressed = baselineConfig.IsSuppressed,
                    LocalOffset = baselineConfig.LocalOffset,
                    TelegraphProfileRefId = baselineConfig.TelegraphProfileRefId,
                    EmissionProfileRefId = baselineConfig.EmissionProfileRefId,
                });
                em.SetComponentData(emitterEntity, baselineTelegraph);
                em.SetComponentData(emitterEntity, new HazardEmitterTelegraphProfileComponent
                {
                    ProfileId = baselineTelegraph.ProfileId,
                    TelegraphDurationSec = baselineTelegraph.TelegraphDurationSec,
                });
                em.SetComponentData(emitterEntity, baselineEmission);
                em.SetComponentData(emitterEntity, new HazardEmitterEmissionProfileComponent
                {
                    ProfileId = baselineEmission.ProfileId,
                    BulletTypeKey = baselineEmission.BulletTypeKey,
                    PositionPatternMode = baselineEmission.PositionPatternMode,
                    SpawnOffset = baselineEmission.SpawnOffset,
                    LineStart = baselineEmission.LineStart,
                    LineEnd = baselineEmission.LineEnd,
                    SampleSpacing = baselineEmission.SampleSpacing,
                    PointSetCount = baselineEmission.PointSetCount,
                    Point0 = baselineEmission.Point0,
                    Point1 = baselineEmission.Point1,
                    Point2 = baselineEmission.Point2,
                    Point3 = baselineEmission.Point3,
                    AimMode = baselineEmission.AimMode,
                    AimSnapshotTiming = baselineEmission.AimSnapshotTiming,
                    BaseAngleDeg = baselineEmission.BaseAngleDeg,
                    AimAngleOffsetDeg = baselineEmission.AimAngleOffsetDeg,
                    LineNormalSide = baselineEmission.LineNormalSide,
                    LineNormalAngleOffsetDeg = baselineEmission.LineNormalAngleOffsetDeg,
                    SpiralStepDeg = baselineEmission.SpiralStepDeg,
                    ShotPatternMode = baselineEmission.ShotPatternMode,
                    ShotCount = baselineEmission.ShotCount,
                    NWayAngleSpacingDeg = baselineEmission.NWayAngleSpacingDeg,
                    EventShotSchedule = baselineEmission.EventShotSchedule,
                    EventShotIntervalSec = baselineEmission.EventShotIntervalSec,
                    EventRepeatCount = baselineEmission.EventRepeatCount,
                    CooldownSec = baselineEmission.CooldownSec,
                });
                em.SetComponentData(emitterEntity, new HazardEmitterRuntimeStateComponent
                {
                    LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    StateElapsedSec = 0f,
                });
                em.SetComponentData(emitterEntity, new HazardEmitterSelectedPatternRuntimeComponent
                {
                    AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId,
                });
                em.SetComponentData(emitterEntity, new HazardEmitterCycleSignalComponent
                {
                    CompletedVersion = 0u,
                });
                em.SetComponentData(emitterEntity, new HazardEmitterCoordinatorStateComponent
                {
                    ActivationAllowed = 0,
                    SuppressionReasonMask = 0u,
                    LastPlayerDistanceSq = float.MaxValue,
                });
                em.AddBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
                em.AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
                var patternSlots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
                var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
                HazardEmitterPatternSlotAuthoringUtility.ApplyResolvedSlotsToBuffers(
                    resolvedSlots,
                    ref patternSlots,
                    ref executionSlots);

                em.GetBuffer<HazardActorEmitterRefBuffer>(actorEntity).Add(new HazardActorEmitterRefBuffer
                {
                    EmitterEntity = emitterEntity,
                    EmitterId = emitterId,
                });
                em.GetBuffer<LinkedEntityGroup>(sourceEntity).Add(emitterEntity);
            }

            return true;

        Fail:
            for (int i = 0; i < createdEntities.Count; i++)
            {
                if (em.Exists(createdEntities[i]))
                    em.DestroyEntity(createdEntities[i]);
            }

            if (em.Exists(sourceEntity))
            {
                if (em.HasBuffer<SourceHazardActorRefBuffer>(sourceEntity))
                {
                    var actorRefs = em.GetBuffer<SourceHazardActorRefBuffer>(sourceEntity);
                    for (int i = actorRefs.Length - 1; i >= 0; i--)
                    {
                        if (actorRefs[i].ActorId == placementInstanceId)
                            actorRefs.RemoveAt(i);
                    }
                }

                if (em.HasBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity))
                {
                    var placementRefs = em.GetBuffer<SourceHazardActorPlacementRefBuffer>(sourceEntity);
                    for (int i = placementRefs.Length - 1; i >= 0; i--)
                    {
                        if (placementRefs[i].PlacementInstanceId == placementInstanceId)
                            placementRefs.RemoveAt(i);
                    }
                }

                if (em.HasBuffer<LinkedEntityGroup>(sourceEntity))
                {
                    var linkedGroup = em.GetBuffer<LinkedEntityGroup>(sourceEntity);
                    for (int i = linkedGroup.Length - 1; i >= 0; i--)
                    {
                        if (!em.Exists(linkedGroup[i].Value))
                            linkedGroup.RemoveAt(i);
                    }

                    if (linkedGroup.Length == 0)
                        linkedGroup.Add(new LinkedEntityGroup { Value = sourceEntity });
                }
            }

            return false;
        }

    }
}
