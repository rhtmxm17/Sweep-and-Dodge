using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapHazardActorOrchestrationUtility
    {
        public static bool AddRule(
            StageMapDocument document,
            uint sourceStableId,
            HazardActorOrchestrationActionId action,
            int placementInstanceId,
            out int ruleId)
        {
            ruleId = 0;
            if (document == null || sourceStableId == 0u || placementInstanceId <= 0)
                return false;
            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            ruleId = NextRuleId(rules, sourceStableId);
            var next = (StageMapHazardActorOrchestrationRuleData[])rules.Clone();
            Array.Resize(ref next, next.Length + 1);
            next[next.Length - 1] = new StageMapHazardActorOrchestrationRuleData
            {
                OwningSourceStableId = sourceStableId,
                RuleId = ruleId,
                TargetPlacementInstanceIds = new[] { placementInstanceId },
                ActionType = action,
                TriggerType = action == HazardActorOrchestrationActionId.Spawn
                    ? HazardActorOrchestrationTriggerId.OnStageStart
                    : HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove,
                TriggerThresholdNormalized = 0f,
                TargetPhaseId = 1,
            };
            document.HazardActorOrchestrationRules = next;
            return true;
        }

        public static bool UpdateRule(StageMapDocument document, uint sourceStableId, int ruleId, StageMapHazardActorOrchestrationRuleData value)
        {
            if (!TryFindRuleIndex(document, sourceStableId, ruleId, out int index))
                return false;
            var rules = document.HazardActorOrchestrationRules;
            rules[index] = CloneRule(value);
            document.HazardActorOrchestrationRules = rules;
            return true;
        }

        public static bool DuplicateRule(StageMapDocument document, uint sourceStableId, int ruleId, out int newRuleId)
        {
            newRuleId = 0;
            if (!TryFindRuleIndex(document, sourceStableId, ruleId, out int index))
                return false;
            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            newRuleId = NextRuleId(rules, sourceStableId);
            var copy = CloneRule(rules[index]);
            copy.RuleId = newRuleId;
            var next = (StageMapHazardActorOrchestrationRuleData[])rules.Clone();
            Array.Resize(ref next, next.Length + 1);
            next[next.Length - 1] = copy;
            document.HazardActorOrchestrationRules = next;
            return true;
        }

        public static bool MoveRule(StageMapDocument document, uint sourceStableId, int ruleId, int direction)
        {
            if (!TryFindRuleIndex(document, sourceStableId, ruleId, out int index))
                return false;
            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            int next = Mathf.Clamp(index + Math.Sign(direction), 0, rules.Length - 1);
            if (next == index)
                return false;
            var temp = rules[index];
            rules[index] = rules[next];
            rules[next] = temp;
            document.HazardActorOrchestrationRules = rules;
            return true;
        }

        public static bool DeleteRule(StageMapDocument document, uint sourceStableId, int ruleId)
        {
            if (!TryFindRuleIndex(document, sourceStableId, ruleId, out int index))
                return false;
            var rules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            for (int i = index + 1; i < rules.Length; i++)
                rules[i - 1] = rules[i];
            Array.Resize(ref rules, rules.Length - 1);
            document.HazardActorOrchestrationRules = rules;
            return true;
        }

        public static bool TryFindRuleIndex(StageMapDocument document, uint sourceStableId, int ruleId, out int index)
        {
            index = -1;
            var rules = document != null ? document.HazardActorOrchestrationRules : null;
            if (rules == null || sourceStableId == 0u || ruleId <= 0)
                return false;
            for (int i = 0; i < rules.Length; i++)
            {
                if (rules[i].OwningSourceStableId == sourceStableId && rules[i].RuleId == ruleId)
                {
                    if (index >= 0)
                    {
                        index = -1;
                        return false;
                    }
                    index = i;
                }
            }
            return index >= 0;
        }

        public static int[] GetCommonPhaseIds(StageMapDocument document, StageMapHazardActorOrchestrationRuleData rule)
        {
            var intersection = new HashSet<int>();
            bool initialized = false;
            var targets = rule.TargetPlacementInstanceIds ?? Array.Empty<int>();
            for (int i = 0; i < targets.Length; i++)
            {
                if (!TryFindPlacement(document, rule.OwningSourceStableId, targets[i], out var placement)
                    || placement.ActorArchetypePrefab == null)
                    continue;
                var phaseIds = GetPhaseIds(placement.ActorArchetypePrefab);
                if (!initialized)
                {
                    intersection = new HashSet<int>(phaseIds);
                    initialized = true;
                }
                else
                {
                    intersection.IntersectWith(phaseIds);
                }
            }
            return intersection.OrderBy(x => x).ToArray();
        }

        public static bool TryFindPlacement(StageMapDocument document, uint sourceStableId, int placementId, out StageMapHazardActorPlacementData placement)
        {
            placement = default;
            var placements = document != null ? document.HazardActorPlacements : null;
            if (placements == null)
                return false;
            int found = -1;
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].OwningSourceStableId == sourceStableId && placements[i].PlacementInstanceId == placementId)
                {
                    if (found >= 0)
                        return false;
                    found = i;
                }
            }
            if (found < 0)
                return false;
            placement = placements[found];
            return true;
        }

        private static int NextRuleId(StageMapHazardActorOrchestrationRuleData[] rules, uint sourceStableId)
        {
            var used = new HashSet<int>();
            if (rules != null)
            {
                for (int i = 0; i < rules.Length; i++)
                {
                    if (rules[i].OwningSourceStableId == sourceStableId)
                        used.Add(rules[i].RuleId);
                }
            }
            for (int id = 1; id < int.MaxValue; id++)
            {
                if (!used.Contains(id))
                    return id;
            }
            return 1;
        }

        private static IEnumerable<int> GetPhaseIds(GameObject prefab)
        {
            var actor = prefab != null ? prefab.GetComponentInChildren<HazardActorAuthoring>(true) : null;
            if (actor == null
                || !HazardActorAuthoringValidationUtility.TryValidateStandalone(actor, out var seed, out _, out _, out _))
            {
                yield break;
            }
            var policies = seed.Policies ?? Array.Empty<HazardActorPhaseSelectorPolicyBuffer>();
            for (int i = 0; i < policies.Length; i++)
                yield return policies[i].PhaseId;
        }

        private static StageMapHazardActorOrchestrationRuleData CloneRule(StageMapHazardActorOrchestrationRuleData rule)
        {
            rule.TargetPlacementInstanceIds = rule.TargetPlacementInstanceIds != null
                ? (int[])rule.TargetPlacementInstanceIds.Clone()
                : Array.Empty<int>();
            return rule;
        }

    }
}
