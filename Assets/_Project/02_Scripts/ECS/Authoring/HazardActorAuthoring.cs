using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [System.Serializable]
    public struct HazardActorPhaseSelectorCandidateAuthoring
    {
        [Min(1)] public int PatternSlotId;
    }

    [System.Serializable]
    public struct HazardActorPhaseSelectorPolicyAuthoring
    {
        [Min(1)] public int PhaseId;
        public HazardActorSelectionModeId SelectionMode;
        public HazardActorPhaseSelectorCandidateAuthoring[] Candidates;
    }

    [System.Serializable]
    public struct HazardActorPhaseProgressTransitionAuthoring
    {
        [Min(1)] public int FromPhaseId;
        [Min(1)] public int ToPhaseId;
        [Range(0f, 1f)] public float ProgressThresholdNormalized;
        [Min(0f)] public float TransitionLeadInSec;
    }

    public class HazardActorAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [Min(1)] public int ActorId = 1;

        [Header("Contract")]
        public bool Enabled = true;
        public bool StartSuppressed = false;
        public HazardActorPresenceStateId InitialPresenceState = HazardActorPresenceStateId.Hidden;

        [Header("Presence Policy")]
        [Min(0f)] public float ActivationDurationSec = 0f;
        [Min(0f)] public float RetireDurationSec = 0f;

        [Header("Phase Selector")]
        [Min(1)] public int InitialPhaseId = 1;
        public HazardActorPhaseSelectorPolicyAuthoring[] PhaseSelectorPolicies;

        [Header("Pattern Slots")]
        public HazardActorPatternSlotAuthoring[] PatternSlots =
        {
            new HazardActorPatternSlotAuthoring
            {
                PatternSlotId = HazardActorPatternRuntimeUtility.CompatibilityPatternSlotId,
                BaseWeight = HazardActorPatternRuntimeUtility.CompatibilityBaseWeight,
                AvailabilityFlags = HazardActorPatternRuntimeUtility.CompatibilityAvailabilityFlags,
            }
        };

        [Header("Phase Transition")]
        public HazardActorPhaseProgressTransitionAuthoring[] PhaseProgressTransitions;

        private sealed class Baker : Baker<HazardActorAuthoring>
        {
            public override void Bake(HazardActorAuthoring authoring)
            {
                if (!HazardActorAuthoringValidationUtility.TryValidate(
                        authoring,
                        out var sourceAuthoring,
                        out var compatibilitySeed,
                        out var phaseTransitions,
                        out _,
                        out var error))
                {
                    Debug.LogError($"[HazardActorAuthoring] {error}", authoring);
                    return;
                }

                var actorEntity = GetEntity(TransformUsageFlags.Dynamic);
                var sourceEntity = GetEntity(sourceAuthoring.gameObject, TransformUsageFlags.Dynamic);
                int actorId = math.max(1, authoring.ActorId);
                byte isEnabled = authoring.Enabled ? (byte)1 : (byte)0;
                byte isSuppressed = authoring.StartSuppressed ? (byte)1 : (byte)0;

                AddComponent(actorEntity, new HazardActorComponent
                {
                    ActorId = actorId,
                    SourceEntity = sourceEntity,
                });

                var baselineConfig = new HazardActorAppliedConfigBaselineComponent
                {
                    IsEnabled = isEnabled,
                    IsSuppressed = isSuppressed,
                };
                AddComponent(actorEntity, baselineConfig);
                AddComponent(actorEntity, new HazardActorAppliedConfigComponent
                {
                    IsEnabled = baselineConfig.IsEnabled,
                    IsSuppressed = baselineConfig.IsSuppressed,
                });
                AddComponent(actorEntity, new HazardActorPresencePolicyComponent
                {
                    ActivationDurationSec = math.max(0f, authoring.ActivationDurationSec),
                    RetireDurationSec = math.max(0f, authoring.RetireDurationSec),
                });

                AddComponent(actorEntity, new HazardActorRuntimeBaselineComponent
                {
                    InitialPresenceState = authoring.InitialPresenceState,
                });
                AddComponent(actorEntity, new HazardActorBehaviorPhaseBaselineComponent
                {
                    InitialPhaseId = compatibilitySeed.InitialPhaseId,
                });
                AddComponent(actorEntity, new HazardActorRuntimeStateComponent
                {
                    PresenceState = authoring.InitialPresenceState,
                    StateElapsedSec = 0f,
                });
                AddComponent(actorEntity, new HazardActorBehaviorPhaseStateComponent
                {
                    CurrentPhaseId = compatibilitySeed.InitialPhaseId,
                    PreviousPhaseId = compatibilitySeed.InitialPhaseId,
                    PhaseVersion = 0u,
                });
                AddComponent(actorEntity, new HazardActorPatternSelectorStateComponent
                {
                    CurrentPatternSlotId = -1,
                    LastPatternSlotId = -1,
                    SelectionSequence = 0u,
                    CurrentCandidateOrder = -1,
                    LastResolvedPhaseVersion = 0u,
                    LastConsumedCycleVersion = 0u,
                });
                AddComponent(actorEntity, new HazardActorPhaseTransitionRuntimeComponent
                {
                    State = HazardActorPhaseTransitionStateId.Idle,
                    PendingFromPhaseId = -1,
                    PendingToPhaseId = -1,
                    ElapsedSec = 0f,
                    DurationSec = 0f,
                    TransitionVersion = 0u,
                });
                AddComponent(actorEntity, new HazardActorPhaseTransitionSignalComponent
                {
                    Version = 0u,
                    Cue = HazardActorPhaseTransitionSignalCueId.None,
                    Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold,
                    PreviousPhaseId = compatibilitySeed.InitialPhaseId,
                    CurrentPhaseId = compatibilitySeed.InitialPhaseId,
                    PendingToPhaseId = -1,
                });
                AddComponent(actorEntity, new HazardActorPresencePresentationSignalComponent
                {
                    Version = 0u,
                    Cue = HazardActorPresencePresentationCueId.None,
                });
                AddComponent(actorEntity, new HazardActorEmitStateComponent
                {
                    LifecycleState = HazardActorEmitLifecycleStateId.Dormant,
                    StateElapsedSec = 0f,
                });
                AddComponent(actorEntity, new HazardActorEmitActiveTelegraphComponent
                {
                    AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId,
                    ProfileId = 0,
                    TelegraphDurationSec = 0f,
                });
                AddComponent(actorEntity, new HazardActorEmitActiveEmissionComponent
                {
                    AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId,
                });
                AddComponent(actorEntity, new HazardActorEmitCycleSignalComponent
                {
                    CompletedVersion = 0u,
                });

                var selectorPolicies = AddBuffer<HazardActorPhaseSelectorPolicyBuffer>(actorEntity);
                selectorPolicies.Clear();
                for (int i = 0; i < compatibilitySeed.Policies.Length; i++)
                {
                    selectorPolicies.Add(compatibilitySeed.Policies[i]);
                }

                var selectorCandidates = AddBuffer<HazardActorPhaseSelectorCandidateBuffer>(actorEntity);
                selectorCandidates.Clear();
                for (int i = 0; i < compatibilitySeed.Candidates.Length; i++)
                {
                    selectorCandidates.Add(compatibilitySeed.Candidates[i]);
                }

                if (!HazardActorPatternSlotAuthoringUtility.TryResolveSlots(authoring.PatternSlots, out var resolvedSlots, out var slotError))
                {
                    Debug.LogError($"[HazardActorAuthoring] {slotError}", authoring);
                    return;
                }

                var patternSlots = AddBuffer<HazardActorPatternSlotBuffer>(actorEntity);
                var executionSlots = AddBuffer<HazardActorPatternExecutionSlotBuffer>(actorEntity);
                HazardActorPatternSlotAuthoringUtility.ApplyResolvedSlotsToBuffers(
                    resolvedSlots,
                    ref patternSlots,
                    ref executionSlots);

                SetComponent(actorEntity, HazardActorPatternSlotAuthoringUtility.CreateBaselineTelegraph(resolvedSlots));
                SetComponent(actorEntity, HazardActorPatternSlotAuthoringUtility.CreateBaselineEmission(resolvedSlots));

                var transitionBuffer = AddBuffer<HazardActorPhaseProgressTransitionBuffer>(actorEntity);
                transitionBuffer.Clear();
                for (int i = 0; i < phaseTransitions.Length; i++)
                {
                    transitionBuffer.Add(phaseTransitions[i]);
                }
            }
        }
    }
}
