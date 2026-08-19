using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class HazardActorWorkbenchCommandUtility
    {
        public static int GetNextPhaseId(HazardActorAuthoring actor)
        {
            var used = new HashSet<int>();
            var policies = actor != null ? actor.PhaseSelectorPolicies : null;
            if (policies != null)
            {
                for (int i = 0; i < policies.Length; i++)
                    used.Add(policies[i].PhaseId);
            }
            for (int id = 1; id < int.MaxValue; id++)
            {
                if (!used.Contains(id))
                    return id;
            }
            return 1;
        }

        public static int GetNextPatternSlotId(HazardActorAuthoring actor)
        {
            var used = new HashSet<int>();
            var slots = actor != null ? actor.PatternSlots : null;
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                    used.Add(slots[i].PatternSlotId);
            }
            for (int id = 1; id < int.MaxValue; id++)
            {
                if (!used.Contains(id))
                    return id;
            }
            return 1;
        }

        public static bool AddPhase(HazardActorAuthoring actor, out int phaseId)
        {
            phaseId = 0;
            if (actor == null)
                return false;
            phaseId = GetNextPhaseId(actor);
            Record(actor, "Add Hazard Actor Phase");
            var policies = actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            Array.Resize(ref policies, policies.Length + 1);
            policies[policies.Length - 1] = new HazardActorPhaseSelectorPolicyAuthoring
            {
                PhaseId = phaseId,
                SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                Candidates = BuildDefaultCandidates(actor),
            };
            actor.PhaseSelectorPolicies = policies;
            if (actor.InitialPhaseId <= 0)
                actor.InitialPhaseId = phaseId;
            Dirty(actor);
            return true;
        }

        public static bool DuplicatePhase(HazardActorAuthoring actor, int sourcePhaseId, out int phaseId)
        {
            phaseId = 0;
            if (actor == null || !TryFindPhaseIndex(actor, sourcePhaseId, out int index))
                return false;
            phaseId = GetNextPhaseId(actor);
            Record(actor, "Duplicate Hazard Actor Phase");
            var policies = actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            var copy = policies[index];
            copy.PhaseId = phaseId;
            copy.Candidates = copy.Candidates != null
                ? (HazardActorPhaseSelectorCandidateAuthoring[])copy.Candidates.Clone()
                : Array.Empty<HazardActorPhaseSelectorCandidateAuthoring>();
            Array.Resize(ref policies, policies.Length + 1);
            policies[policies.Length - 1] = copy;
            actor.PhaseSelectorPolicies = policies;
            Dirty(actor);
            return true;
        }

        public static bool MovePhase(HazardActorAuthoring actor, int phaseId, int direction)
        {
            if (actor == null || !TryFindPhaseIndex(actor, phaseId, out int index))
                return false;
            var policies = actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            int next = Mathf.Clamp(index + Math.Sign(direction), 0, policies.Length - 1);
            if (next == index)
                return false;
            Record(actor, "Move Hazard Actor Phase");
            Swap(ref policies[index], ref policies[next]);
            actor.PhaseSelectorPolicies = policies;
            Dirty(actor);
            return true;
        }

        public static bool RemovePhase(HazardActorAuthoring actor, int phaseId, out string error)
        {
            error = string.Empty;
            if (actor == null || !TryFindPhaseIndex(actor, phaseId, out int index))
                return false;
            if (actor.InitialPhaseId == phaseId)
            {
                error = "Cannot remove the initial phase. Assign another InitialPhaseId first.";
                return false;
            }
            if (ReferencesPhase(actor, phaseId))
            {
                error = "Cannot remove a phase while transitions reference it.";
                return false;
            }

            Record(actor, "Remove Hazard Actor Phase");
            var policies = actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            RemoveAt(ref policies, index);
            actor.PhaseSelectorPolicies = policies;
            Dirty(actor);
            return true;
        }

        public static bool AddPattern(HazardActorAuthoring actor, out int patternSlotId)
        {
            patternSlotId = 0;
            if (actor == null)
                return false;
            patternSlotId = GetNextPatternSlotId(actor);
            Record(actor, "Add Hazard Actor Pattern");
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            Array.Resize(ref slots, slots.Length + 1);
            slots[slots.Length - 1] = new HazardActorPatternSlotAuthoring
            {
                PatternSlotId = patternSlotId,
                BaseWeight = HazardActorPatternRuntimeUtility.CompatibilityBaseWeight,
                AvailabilityFlags = HazardActorPatternRuntimeUtility.CompatibilityAvailabilityFlags,
            };
            actor.PatternSlots = slots;
            Dirty(actor);
            return true;
        }

        public static bool DuplicatePattern(HazardActorAuthoring actor, int sourcePatternSlotId, out int patternSlotId)
        {
            patternSlotId = 0;
            if (actor == null || !TryFindPatternIndex(actor, sourcePatternSlotId, out int index))
                return false;
            patternSlotId = GetNextPatternSlotId(actor);
            Record(actor, "Duplicate Hazard Actor Pattern");
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            var copy = slots[index];
            copy.PatternSlotId = patternSlotId;
            Array.Resize(ref slots, slots.Length + 1);
            slots[slots.Length - 1] = copy;
            actor.PatternSlots = slots;
            Dirty(actor);
            return true;
        }

        public static bool MovePattern(HazardActorAuthoring actor, int patternSlotId, int direction)
        {
            if (actor == null || !TryFindPatternIndex(actor, patternSlotId, out int index))
                return false;
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            int next = Mathf.Clamp(index + Math.Sign(direction), 0, slots.Length - 1);
            if (next == index)
                return false;
            Record(actor, "Move Hazard Actor Pattern");
            Swap(ref slots[index], ref slots[next]);
            actor.PatternSlots = slots;
            Dirty(actor);
            return true;
        }

        public static bool RemovePattern(HazardActorAuthoring actor, int patternSlotId, out string error)
        {
            error = string.Empty;
            if (actor == null || !TryFindPatternIndex(actor, patternSlotId, out int index))
                return false;
            if (ReferencesPattern(actor, patternSlotId))
            {
                error = "Cannot remove a pattern while selector candidates reference it.";
                return false;
            }

            Record(actor, "Remove Hazard Actor Pattern");
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            RemoveAt(ref slots, index);
            actor.PatternSlots = slots;
            Dirty(actor);
            return true;
        }

        public static bool AddTransition(HazardActorAuthoring actor, int fromPhaseId, int toPhaseId)
        {
            if (actor == null || fromPhaseId <= 0 || toPhaseId <= 0 || fromPhaseId == toPhaseId)
                return false;
            Record(actor, "Add Hazard Actor Phase Transition");
            var transitions = actor.PhaseProgressTransitions ?? Array.Empty<HazardActorPhaseProgressTransitionAuthoring>();
            Array.Resize(ref transitions, transitions.Length + 1);
            transitions[transitions.Length - 1] = new HazardActorPhaseProgressTransitionAuthoring
            {
                FromPhaseId = fromPhaseId,
                ToPhaseId = toPhaseId,
                ProgressThresholdNormalized = 0.5f,
                TransitionLeadInSec = 0f,
            };
            actor.PhaseProgressTransitions = transitions;
            Dirty(actor);
            return true;
        }

        public static bool RemoveTransition(HazardActorAuthoring actor, int fromPhaseId)
        {
            if (actor == null || !TryFindTransitionIndex(actor, fromPhaseId, out int index))
                return false;
            Record(actor, "Remove Hazard Actor Phase Transition");
            var transitions = actor.PhaseProgressTransitions ?? Array.Empty<HazardActorPhaseProgressTransitionAuthoring>();
            RemoveAt(ref transitions, index);
            actor.PhaseProgressTransitions = transitions;
            Dirty(actor);
            return true;
        }

        public static bool DuplicateAndAssignEmissionProfile(
            HazardActorAuthoring actor,
            int patternSlotId,
            string destinationPath,
            out EmissionProfileSO duplicate,
            out string error)
        {
            duplicate = null;
            error = string.Empty;
            if (actor == null || !TryFindPatternIndex(actor, patternSlotId, out int slotIndex))
                return false;

            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            var source = slots[slotIndex].Emission.Profile;
            if (source == null)
            {
                error = "Selected pattern has no emission profile to duplicate.";
                return false;
            }

            string sourcePath = AssetDatabase.GetAssetPath(source);
            if (string.IsNullOrEmpty(sourcePath))
            {
                error = "Selected emission profile must be a project asset.";
                return false;
            }

            string path = string.IsNullOrWhiteSpace(destinationPath)
                ? AssetDatabase.GenerateUniqueAssetPath(sourcePath.Replace(".asset", "_copy.asset"))
                : AssetDatabase.GenerateUniqueAssetPath(destinationPath);
            if (!AssetDatabase.CopyAsset(sourcePath, path))
            {
                error = $"Failed to copy emission profile to '{path}'.";
                return false;
            }

            duplicate = AssetDatabase.LoadAssetAtPath<EmissionProfileSO>(path);
            if (duplicate == null)
            {
                error = $"Duplicated asset could not be loaded. path={path}";
                return false;
            }

            Record(actor, "Duplicate And Assign Emission Profile");
            var slot = slots[slotIndex];
            slot.Emission.Profile = duplicate;
            slots[slotIndex] = slot;
            actor.PatternSlots = slots;
            Dirty(actor);
            return true;
        }

        public static int CountEmissionProfileUsers(EmissionProfileSO profile)
        {
            if (profile == null)
                return 0;
            int count = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            for (int i = 0; i < guids.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
                var actor = prefab != null ? prefab.GetComponentInChildren<HazardActorAuthoring>(true) : null;
                var slots = actor != null ? actor.PatternSlots : null;
                if (slots == null)
                    continue;
                for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
                {
                    if (slots[slotIndex].Emission.Profile == profile)
                        count++;
                }
            }
            return count;
        }

        public static bool TryFindPhaseIndex(HazardActorAuthoring actor, int phaseId, out int index)
        {
            index = -1;
            var policies = actor != null ? actor.PhaseSelectorPolicies : null;
            if (policies == null)
                return false;
            for (int i = 0; i < policies.Length; i++)
            {
                if (policies[i].PhaseId == phaseId)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryFindPatternIndex(HazardActorAuthoring actor, int patternSlotId, out int index)
        {
            index = -1;
            var slots = actor != null ? actor.PatternSlots : null;
            if (slots == null)
                return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].PatternSlotId == patternSlotId)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        public static bool TryFindTransitionIndex(HazardActorAuthoring actor, int fromPhaseId, out int index)
        {
            index = -1;
            var transitions = actor != null ? actor.PhaseProgressTransitions : null;
            if (transitions == null)
                return false;
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].FromPhaseId == fromPhaseId)
                {
                    index = i;
                    return true;
                }
            }
            return false;
        }

        private static HazardActorPhaseSelectorCandidateAuthoring[] BuildDefaultCandidates(HazardActorAuthoring actor)
        {
            var slots = actor.PatternSlots ?? Array.Empty<HazardActorPatternSlotAuthoring>();
            if (slots.Length == 0)
                return Array.Empty<HazardActorPhaseSelectorCandidateAuthoring>();
            return new[]
            {
                new HazardActorPhaseSelectorCandidateAuthoring
                {
                    PatternSlotId = Math.Max(1, slots[0].PatternSlotId),
                }
            };
        }

        private static bool ReferencesPhase(HazardActorAuthoring actor, int phaseId)
        {
            var transitions = actor.PhaseProgressTransitions ?? Array.Empty<HazardActorPhaseProgressTransitionAuthoring>();
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].FromPhaseId == phaseId || transitions[i].ToPhaseId == phaseId)
                    return true;
            }
            return false;
        }

        private static bool ReferencesPattern(HazardActorAuthoring actor, int patternSlotId)
        {
            var policies = actor.PhaseSelectorPolicies ?? Array.Empty<HazardActorPhaseSelectorPolicyAuthoring>();
            for (int i = 0; i < policies.Length; i++)
            {
                var candidates = policies[i].Candidates ?? Array.Empty<HazardActorPhaseSelectorCandidateAuthoring>();
                for (int j = 0; j < candidates.Length; j++)
                {
                    if (candidates[j].PatternSlotId == patternSlotId)
                        return true;
                }
            }
            return false;
        }

        private static void Record(UnityEngine.Object target, string name)
        {
            Undo.RecordObject(target, name);
        }

        private static void Dirty(UnityEngine.Object target)
        {
            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }

        private static void RemoveAt<T>(ref T[] values, int index)
        {
            for (int i = index + 1; i < values.Length; i++)
                values[i - 1] = values[i];
            Array.Resize(ref values, values.Length - 1);
        }

        private static void Swap<T>(ref T left, ref T right)
        {
            T temp = left;
            left = right;
            right = temp;
        }
    }
}
