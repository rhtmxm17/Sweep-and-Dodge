using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [System.Serializable]
    public struct HazardActorPhaseSelectorCandidateAuthoring
    {
        [Min(1)] public int EmitterId;
        [Min(1)] public int PatternSlotId;
    }

    [System.Serializable]
    public struct HazardActorPhaseSelectorPolicyAuthoring
    {
        [Min(1)] public int PhaseId;
        public HazardActorSelectionModeId SelectionMode;
        public HazardActorPhaseSelectorCandidateAuthoring[] Candidates;
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
        public HazardActorPresenceTriggerMode ActivationTrigger = HazardActorPresenceTriggerMode.Immediate;
        [Min(0f)] public float ActivationDurationSec = 0f;
        public HazardActorPresenceTriggerMode RetireTrigger = HazardActorPresenceTriggerMode.None;
        [Min(0f)] public float RetireDurationSec = 0f;

        [Header("Phase Selector")]
        [Min(1)] public int InitialPhaseId = 1;
        public HazardActorPhaseSelectorPolicyAuthoring[] PhaseSelectorPolicies;

        private sealed class Baker : Baker<HazardActorAuthoring>
        {
            public override void Bake(HazardActorAuthoring authoring)
            {
                if (!HazardActorAuthoringValidationUtility.TryValidate(authoring, out var sourceAuthoring, out var compatibilitySeed, out var error))
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
                    ActivationTrigger = authoring.ActivationTrigger,
                    ActivationDurationSec = math.max(0f, authoring.ActivationDurationSec),
                    RetireTrigger = authoring.RetireTrigger,
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
                    TargetEmitterId = -1,
                    CurrentPatternSlotId = -1,
                    LastPatternSlotId = -1,
                    SelectionSequence = 0u,
                    CurrentCandidateOrder = -1,
                    LastResolvedPhaseVersion = 0u,
                    LastConsumedCycleVersion = 0u,
                });
                AddComponent(actorEntity, new HazardActorPresencePresentationSignalComponent
                {
                    Version = 0u,
                    Cue = HazardActorPresencePresentationCueId.None,
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

                var emitterRefs = AddBuffer<HazardActorEmitterRefBuffer>(actorEntity);
                emitterRefs.Clear();
                var emitters = authoring.GetComponentsInChildren<HazardEmitterAuthoring>(includeInactive: true);
                for (int i = 0; i < emitters.Length; i++)
                {
                    var emitter = emitters[i];
                    if (emitter == null)
                        continue;

                    var parentActor = emitter.GetComponentInParent<HazardActorAuthoring>(includeInactive: true);
                    if (parentActor != authoring)
                        continue;

                    emitterRefs.Add(new HazardActorEmitterRefBuffer
                    {
                        EmitterEntity = GetEntity(emitter.gameObject, TransformUsageFlags.Dynamic),
                        EmitterId = math.max(1, emitter.EmitterId),
                    });
                }
            }
        }
    }
}
