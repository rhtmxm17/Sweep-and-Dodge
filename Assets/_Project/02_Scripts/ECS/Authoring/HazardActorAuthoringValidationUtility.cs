using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum HazardActorAuthoringValidationErrorKind : byte
    {
        None = 0,
        General = 1,
        SelectorPolicy = 2,
        PhaseTransition = 3,
    }

    public readonly struct HazardActorPhaseSelectorCompatibilitySeed
    {
        public HazardActorPhaseSelectorCompatibilitySeed(
            int initialPhaseId,
            HazardActorPhaseSelectorPolicyBuffer[] policies,
            HazardActorPhaseSelectorCandidateBuffer[] candidates)
        {
            InitialPhaseId = initialPhaseId;
            Policies = policies ?? Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
            Candidates = candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateBuffer>();
        }

        public int InitialPhaseId { get; }
        public HazardActorPhaseSelectorPolicyBuffer[] Policies { get; }
        public HazardActorPhaseSelectorCandidateBuffer[] Candidates { get; }
    }

    public static class HazardActorAuthoringValidationUtility
    {
        private const int CompatibilityPhaseId = 1;

        public static bool TryValidate(
            HazardActorAuthoring authoring,
            out SourceRuntimeTemplateAuthoringBase sourceAuthoring,
            out HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            out string error)
        {
            return TryValidate(
                authoring,
                out sourceAuthoring,
                out compatibilitySeed,
                out _,
                out _,
                out error);
        }

        public static bool TryValidate(
            HazardActorAuthoring authoring,
            out SourceRuntimeTemplateAuthoringBase sourceAuthoring,
            out HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            out HazardActorPhaseProgressTransitionBuffer[] phaseTransitions,
            out HazardActorAuthoringValidationErrorKind errorKind,
            out string error)
        {
            return TryValidateInternal(
                authoring,
                requireSourceParent: true,
                out sourceAuthoring,
                out compatibilitySeed,
                out phaseTransitions,
                out errorKind,
                out error);
        }

        public static bool TryValidateStandalone(
            HazardActorAuthoring authoring,
            out HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            out string error)
        {
            return TryValidateStandalone(
                authoring,
                out compatibilitySeed,
                out _,
                out _,
                out error);
        }

        public static bool TryValidateStandalone(
            HazardActorAuthoring authoring,
            out HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            out HazardActorPhaseProgressTransitionBuffer[] phaseTransitions,
            out HazardActorAuthoringValidationErrorKind errorKind,
            out string error)
        {
            return TryValidateInternal(
                authoring,
                requireSourceParent: false,
                out _,
                out compatibilitySeed,
                out phaseTransitions,
                out errorKind,
                out error);
        }

        private static bool TryValidateInternal(
            HazardActorAuthoring authoring,
            bool requireSourceParent,
            out SourceRuntimeTemplateAuthoringBase sourceAuthoring,
            out HazardActorPhaseSelectorCompatibilitySeed compatibilitySeed,
            out HazardActorPhaseProgressTransitionBuffer[] phaseTransitions,
            out HazardActorAuthoringValidationErrorKind errorKind,
            out string error)
        {
            sourceAuthoring = null;
            compatibilitySeed = default;
            phaseTransitions = Array.Empty<HazardActorPhaseProgressTransitionBuffer>();
            errorKind = HazardActorAuthoringValidationErrorKind.None;
            error = string.Empty;

            if (authoring == null)
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                error = "HazardActorAuthoring is null.";
                return false;
            }

            sourceAuthoring = authoring.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>(includeInactive: true);
            if (requireSourceParent && sourceAuthoring == null)
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                error = "HazardActorAuthoring requires a parent SourceRuntimeTemplateAuthoringBase.";
                return false;
            }

            if (authoring.ActorId < 1)
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                error = "HazardActorAuthoring requires ActorId >= 1.";
                return false;
            }

            if (authoring.ActivationDurationSec < 0f)
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                error = "HazardActorAuthoring requires ActivationDurationSec >= 0.";
                return false;
            }

            if (authoring.RetireDurationSec < 0f)
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                error = "HazardActorAuthoring requires RetireDurationSec >= 0.";
                return false;
            }

            if (!HazardActorPatternSlotAuthoringUtility.TryResolveSlots(
                    authoring.PatternSlots,
                    out _,
                    out error))
            {
                errorKind = HazardActorAuthoringValidationErrorKind.General;
                return false;
            }

            if (authoring.PhaseSelectorPolicies != null && authoring.PhaseSelectorPolicies.Length > 0)
            {
                if (!TryBuildExplicitSeed(authoring, out compatibilitySeed, out error))
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.SelectorPolicy;
                    return false;
                }

                return TryBuildTransitions(
                    authoring,
                    compatibilitySeed,
                    out phaseTransitions,
                    out errorKind,
                    out error);
            }

            compatibilitySeed = BuildCompatibilitySeed(authoring);
            return TryBuildTransitions(
                authoring,
                compatibilitySeed,
                out phaseTransitions,
                out errorKind,
                out error);
        }

        private static bool TryBuildTransitions(
            HazardActorAuthoring authoring,
            HazardActorPhaseSelectorCompatibilitySeed selectorSeed,
            out HazardActorPhaseProgressTransitionBuffer[] transitions,
            out HazardActorAuthoringValidationErrorKind errorKind,
            out string error)
        {
            transitions = Array.Empty<HazardActorPhaseProgressTransitionBuffer>();
            errorKind = HazardActorAuthoringValidationErrorKind.None;
            error = string.Empty;

            if (authoring.PhaseProgressTransitions == null || authoring.PhaseProgressTransitions.Length <= 0)
                return true;

            var validPhaseIds = new HashSet<int>();
            for (int i = 0; i < selectorSeed.Policies.Length; i++)
                validPhaseIds.Add(selectorSeed.Policies[i].PhaseId);

            var transitionList = new HazardActorPhaseProgressTransitionBuffer[authoring.PhaseProgressTransitions.Length];
            var outgoingPhaseIds = new HashSet<int>();

            for (int transitionIndex = 0; transitionIndex < authoring.PhaseProgressTransitions.Length; transitionIndex++)
            {
                var transition = authoring.PhaseProgressTransitions[transitionIndex];
                if (transition.FromPhaseId < 1)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] requires FromPhaseId >= 1.";
                    return false;
                }

                if (transition.ToPhaseId < 1)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] requires ToPhaseId >= 1.";
                    return false;
                }

                if (!validPhaseIds.Contains(transition.FromPhaseId))
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] references unknown FromPhaseId {transition.FromPhaseId}.";
                    return false;
                }

                if (!validPhaseIds.Contains(transition.ToPhaseId))
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] references unknown ToPhaseId {transition.ToPhaseId}.";
                    return false;
                }

                if (transition.FromPhaseId == transition.ToPhaseId)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] cannot self-loop on PhaseId {transition.FromPhaseId}.";
                    return false;
                }

                if (transition.ToPhaseId < transition.FromPhaseId)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] cannot transition backward from PhaseId {transition.FromPhaseId} to {transition.ToPhaseId}.";
                    return false;
                }

                if (transition.ProgressThresholdNormalized < 0f || transition.ProgressThresholdNormalized > 1f)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] requires ProgressThresholdNormalized within [0, 1].";
                    return false;
                }

                if (transition.TransitionLeadInSec < 0f)
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions[{transitionIndex}] requires TransitionLeadInSec >= 0.";
                    return false;
                }

                if (!outgoingPhaseIds.Add(transition.FromPhaseId))
                {
                    errorKind = HazardActorAuthoringValidationErrorKind.PhaseTransition;
                    error = $"HazardActorAuthoring PhaseProgressTransitions duplicates outgoing transition for PhaseId {transition.FromPhaseId}.";
                    return false;
                }

                transitionList[transitionIndex] = new HazardActorPhaseProgressTransitionBuffer
                {
                    FromPhaseId = transition.FromPhaseId,
                    ToPhaseId = transition.ToPhaseId,
                    ProgressThresholdNormalized = transition.ProgressThresholdNormalized,
                    TransitionLeadInSec = transition.TransitionLeadInSec,
                };
            }

            Array.Sort(transitionList, static (a, b) => a.FromPhaseId.CompareTo(b.FromPhaseId));
            transitions = transitionList;
            return true;
        }

        private static bool TryBuildExplicitSeed(
            HazardActorAuthoring authoring,
            out HazardActorPhaseSelectorCompatibilitySeed seed,
            out string error)
        {
            seed = default;
            error = string.Empty;

            if (authoring.InitialPhaseId < 1)
            {
                error = "HazardActorAuthoring requires InitialPhaseId >= 1.";
                return false;
            }

            if (authoring.PatternSlots == null || authoring.PatternSlots.Length <= 0)
            {
                error = "HazardActorAuthoring requires at least one PatternSlots entry.";
                return false;
            }

            var phaseIds = new HashSet<int>();
            var orderKeys = new HashSet<long>();
            var policies = new HazardActorPhaseSelectorPolicyBuffer[authoring.PhaseSelectorPolicies.Length];
            var candidates = new List<HazardActorPhaseSelectorCandidateBuffer>(authoring.PhaseSelectorPolicies.Length * 2);

            for (int policyIndex = 0; policyIndex < authoring.PhaseSelectorPolicies.Length; policyIndex++)
            {
                var policy = authoring.PhaseSelectorPolicies[policyIndex];
                if (policy.PhaseId < 1)
                {
                    error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}] requires PhaseId >= 1.";
                    return false;
                }

                if (!phaseIds.Add(policy.PhaseId))
                {
                    error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}] duplicates PhaseId {policy.PhaseId}.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(HazardActorSelectionModeId), policy.SelectionMode))
                {
                    error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}] uses unsupported SelectionMode {policy.SelectionMode}.";
                    return false;
                }

                if (policy.Candidates == null || policy.Candidates.Length <= 0)
                {
                    error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}] requires at least one candidate.";
                    return false;
                }

                policies[policyIndex] = new HazardActorPhaseSelectorPolicyBuffer
                {
                    PhaseId = policy.PhaseId,
                    SelectionMode = policy.SelectionMode,
                };

                for (int candidateIndex = 0; candidateIndex < policy.Candidates.Length; candidateIndex++)
                {
                    var candidate = policy.Candidates[candidateIndex];
                    if (candidate.PatternSlotId < 1)
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}].Candidates[{candidateIndex}] requires PatternSlotId >= 1.";
                        return false;
                    }

                    long orderKey = ((long)policy.PhaseId << 32) | (uint)candidateIndex;
                    if (!orderKeys.Add(orderKey))
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}] duplicates OrderIndex {candidateIndex} for PhaseId {policy.PhaseId}.";
                        return false;
                    }

                    if (!ActorContainsSlot(authoring.PatternSlots, candidate.PatternSlotId))
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}].Candidates[{candidateIndex}] references unknown PatternSlotId {candidate.PatternSlotId}.";
                        return false;
                    }

                    candidates.Add(new HazardActorPhaseSelectorCandidateBuffer
                    {
                        PhaseId = policy.PhaseId,
                        OrderIndex = candidateIndex,
                        PatternSlotId = candidate.PatternSlotId,
                    });
                }
            }

            bool initialPhaseExists = false;
            for (int i = 0; i < policies.Length; i++)
            {
                if (policies[i].PhaseId != authoring.InitialPhaseId)
                    continue;

                initialPhaseExists = true;
                break;
            }

            if (!initialPhaseExists)
            {
                error = $"HazardActorAuthoring InitialPhaseId {authoring.InitialPhaseId} must exist in PhaseSelectorPolicies.";
                return false;
            }

            Array.Sort(policies, static (a, b) => a.PhaseId.CompareTo(b.PhaseId));
            candidates.Sort(static (a, b) =>
            {
                int phaseCompare = a.PhaseId.CompareTo(b.PhaseId);
                return phaseCompare != 0 ? phaseCompare : a.OrderIndex.CompareTo(b.OrderIndex);
            });

            seed = new HazardActorPhaseSelectorCompatibilitySeed(
                authoring.InitialPhaseId,
                policies,
                candidates.ToArray());
            return true;
        }

        private static HazardActorPhaseSelectorCompatibilitySeed BuildCompatibilitySeed(HazardActorAuthoring authoring)
        {
            if (authoring == null || authoring.PatternSlots == null || authoring.PatternSlots.Length <= 0)
            {
                return new HazardActorPhaseSelectorCompatibilitySeed(
                    CompatibilityPhaseId,
                    Array.Empty<HazardActorPhaseSelectorPolicyBuffer>(),
                    Array.Empty<HazardActorPhaseSelectorCandidateBuffer>());
            }

            var orderedSlots = new List<HazardActorPatternSlotAuthoring>(authoring.PatternSlots.Length);
            for (int i = 0; i < authoring.PatternSlots.Length; i++)
            {
                if (authoring.PatternSlots[i].PatternSlotId >= 1)
                    orderedSlots.Add(authoring.PatternSlots[i]);
            }

            orderedSlots.Sort(static (a, b) => a.PatternSlotId.CompareTo(b.PatternSlotId));
            var candidates = new List<HazardActorPhaseSelectorCandidateBuffer>(orderedSlots.Count);
            var usedSlotIds = new HashSet<int>();
            for (int i = 0; i < orderedSlots.Count; i++)
            {
                int patternSlotId = orderedSlots[i].PatternSlotId;
                if (!usedSlotIds.Add(patternSlotId))
                    continue;

                candidates.Add(new HazardActorPhaseSelectorCandidateBuffer
                {
                    PhaseId = CompatibilityPhaseId,
                    OrderIndex = candidates.Count,
                    PatternSlotId = patternSlotId,
                });
            }

            if (candidates.Count <= 0)
            {
                return new HazardActorPhaseSelectorCompatibilitySeed(
                    CompatibilityPhaseId,
                    Array.Empty<HazardActorPhaseSelectorPolicyBuffer>(),
                    Array.Empty<HazardActorPhaseSelectorCandidateBuffer>());
            }

            return new HazardActorPhaseSelectorCompatibilitySeed(
                CompatibilityPhaseId,
                new[]
                {
                    new HazardActorPhaseSelectorPolicyBuffer
                    {
                        PhaseId = CompatibilityPhaseId,
                        SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                    }
                },
                candidates.ToArray());
        }

        private static bool ActorContainsSlot(HazardActorPatternSlotAuthoring[] patternSlots, int patternSlotId)
        {
            if (patternSlots == null)
                return false;

            for (int i = 0; i < patternSlots.Length; i++)
            {
                if (patternSlots[i].PatternSlotId == patternSlotId)
                    return true;
            }

            return false;
        }
    }
}
