using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
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
            sourceAuthoring = null;
            compatibilitySeed = default;
            error = string.Empty;

            if (authoring == null)
            {
                error = "HazardActorAuthoring is null.";
                return false;
            }

            sourceAuthoring = authoring.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>(includeInactive: true);
            if (sourceAuthoring == null)
            {
                error = "HazardActorAuthoring requires a parent SourceRuntimeTemplateAuthoringBase.";
                return false;
            }

            if (authoring.ActorId < 1)
            {
                error = "HazardActorAuthoring requires ActorId >= 1.";
                return false;
            }

            if (authoring.ActivationDurationSec < 0f)
            {
                error = "HazardActorAuthoring requires ActivationDurationSec >= 0.";
                return false;
            }

            if (authoring.RetireDurationSec < 0f)
            {
                error = "HazardActorAuthoring requires RetireDurationSec >= 0.";
                return false;
            }

            var emitters = CollectOwnedEmitters(authoring);
            if (authoring.PhaseSelectorPolicies != null && authoring.PhaseSelectorPolicies.Length > 0)
            {
                if (!TryBuildExplicitSeed(authoring, emitters, out compatibilitySeed, out error))
                    return false;

                return true;
            }

            compatibilitySeed = BuildCompatibilitySeed(emitters);
            return true;
        }

        private static HazardEmitterAuthoring[] CollectOwnedEmitters(HazardActorAuthoring authoring)
        {
            var emitters = authoring.GetComponentsInChildren<HazardEmitterAuthoring>(includeInactive: true);
            var owned = new List<HazardEmitterAuthoring>(emitters.Length);
            for (int i = 0; i < emitters.Length; i++)
            {
                var emitter = emitters[i];
                if (emitter == null)
                    continue;

                var parentActor = emitter.GetComponentInParent<HazardActorAuthoring>(includeInactive: true);
                if (parentActor != authoring)
                    continue;

                owned.Add(emitter);
            }

            return owned.ToArray();
        }

        private static bool TryBuildExplicitSeed(
            HazardActorAuthoring authoring,
            HazardEmitterAuthoring[] emitters,
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

            var emitterById = new Dictionary<int, HazardEmitterAuthoring>();
            for (int i = 0; i < emitters.Length; i++)
            {
                var emitter = emitters[i];
                if (emitter == null)
                    continue;

                if (!emitterById.ContainsKey(emitter.EmitterId))
                    emitterById.Add(emitter.EmitterId, emitter);
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
                    if (candidate.EmitterId < 1)
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}].Candidates[{candidateIndex}] requires EmitterId >= 1.";
                        return false;
                    }

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

                    if (!emitterById.TryGetValue(candidate.EmitterId, out var emitter))
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}].Candidates[{candidateIndex}] references unknown EmitterId {candidate.EmitterId}.";
                        return false;
                    }

                    if (!EmitterContainsSlot(emitter, candidate.PatternSlotId))
                    {
                        error = $"HazardActorAuthoring PhaseSelectorPolicies[{policyIndex}].Candidates[{candidateIndex}] references unknown PatternSlotId {candidate.PatternSlotId} on emitter {candidate.EmitterId}.";
                        return false;
                    }

                    candidates.Add(new HazardActorPhaseSelectorCandidateBuffer
                    {
                        PhaseId = policy.PhaseId,
                        OrderIndex = candidateIndex,
                        EmitterId = candidate.EmitterId,
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

        private static HazardActorPhaseSelectorCompatibilitySeed BuildCompatibilitySeed(HazardEmitterAuthoring[] emitters)
        {
            if (emitters == null || emitters.Length <= 0)
            {
                return new HazardActorPhaseSelectorCompatibilitySeed(
                    CompatibilityPhaseId,
                    Array.Empty<HazardActorPhaseSelectorPolicyBuffer>(),
                    Array.Empty<HazardActorPhaseSelectorCandidateBuffer>());
            }

            var orderedEmitters = new List<HazardEmitterAuthoring>(emitters.Length);
            for (int i = 0; i < emitters.Length; i++)
            {
                if (emitters[i] != null)
                    orderedEmitters.Add(emitters[i]);
            }

            orderedEmitters.Sort(static (a, b) => a.EmitterId.CompareTo(b.EmitterId));
            var candidates = new List<HazardActorPhaseSelectorCandidateBuffer>(orderedEmitters.Count);
            for (int i = 0; i < orderedEmitters.Count; i++)
            {
                if (!TryGetLowestPatternSlotId(orderedEmitters[i], out int patternSlotId))
                    continue;

                candidates.Add(new HazardActorPhaseSelectorCandidateBuffer
                {
                    PhaseId = CompatibilityPhaseId,
                    OrderIndex = candidates.Count,
                    EmitterId = orderedEmitters[i].EmitterId,
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

        private static bool EmitterContainsSlot(HazardEmitterAuthoring emitter, int patternSlotId)
        {
            if (emitter == null || emitter.Slots == null)
                return false;

            for (int i = 0; i < emitter.Slots.Length; i++)
            {
                if (emitter.Slots[i].PatternSlotId == patternSlotId)
                    return true;
            }

            return false;
        }

        private static bool TryGetLowestPatternSlotId(HazardEmitterAuthoring emitter, out int patternSlotId)
        {
            patternSlotId = 0;
            if (emitter == null || emitter.Slots == null || emitter.Slots.Length <= 0)
                return false;

            int selected = int.MaxValue;
            for (int i = 0; i < emitter.Slots.Length; i++)
            {
                int candidate = emitter.Slots[i].PatternSlotId;
                if (candidate < 1 || candidate >= selected)
                    continue;

                selected = candidate;
            }

            if (selected == int.MaxValue)
                return false;

            patternSlotId = selected;
            return true;
        }
    }
}
