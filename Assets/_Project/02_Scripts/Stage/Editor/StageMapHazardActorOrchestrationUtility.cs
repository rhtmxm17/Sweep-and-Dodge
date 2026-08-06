using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class StageMapHazardActorOrchestrationImportPlan
    {
        internal StageMapHazardActorOrchestrationImportPlan(
            StageMapDocument document,
            string documentSignature,
            string targetDefinitionSignature,
            IReadOnlyList<StageMapHazardActorOrchestrationRuleData> candidateRules,
            IReadOnlyList<ContentValidationIssue> issues,
            IReadOnlyList<StageMapApplyPlanChange> changes)
        {
            Document = document;
            DocumentSignature = documentSignature ?? string.Empty;
            TargetDefinitionSignature = targetDefinitionSignature ?? string.Empty;
            CandidateRules = candidateRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            Issues = issues ?? Array.Empty<ContentValidationIssue>();
            Changes = changes ?? Array.Empty<StageMapApplyPlanChange>();
        }

        public StageMapDocument Document { get; }
        public string DocumentSignature { get; }
        public string TargetDefinitionSignature { get; }
        public IReadOnlyList<StageMapHazardActorOrchestrationRuleData> CandidateRules { get; }
        public IReadOnlyList<ContentValidationIssue> Issues { get; }
        public IReadOnlyList<StageMapApplyPlanChange> Changes { get; }
        public bool HasErrors => Issues.Any(x => x.Severity == ContentValidationSeverity.Error);
        public bool HasChanges => Changes.Count > 0;
    }

    public static class StageMapHazardActorOrchestrationUtility
    {
        public static StageMapHazardActorOrchestrationImportPlan BuildImportPreview(StageMapDocument document)
        {
            var issues = new List<ContentValidationIssue>(8);
            var changes = new List<StageMapApplyPlanChange>(8);
            var candidates = new List<StageMapHazardActorOrchestrationRuleData>(8);
            if (document == null)
            {
                issues.Add(Issue("SHO900", "(null)", "StageMapDocument is null."));
                return new StageMapHazardActorOrchestrationImportPlan(null, string.Empty, string.Empty, candidates, issues, changes);
            }

            string location = AssetDatabase.GetAssetPath(document);
            if (string.IsNullOrEmpty(location))
                location = document.name;
            if (document.TargetDefinition == null)
            {
                issues.Add(Issue("SHO901", location, "TargetDefinition is required for orchestration import preview."));
                return BuildPlan(document, candidates, issues, changes);
            }

            var placementLookup = BuildPlacementLookup(document.HazardActorPlacements);
            var documentRuleKeys = new HashSet<string>();
            var existingRules = document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            for (int i = 0; i < existingRules.Length; i++)
                documentRuleKeys.Add(Key(existingRules[i].OwningSourceStableId, existingRules[i].RuleId));

            var sourceBindings = document.TargetDefinition.SourceBindings ?? Array.Empty<StageSourceBinding>();
            for (int sourceIndex = 0; sourceIndex < sourceBindings.Length; sourceIndex++)
            {
                var binding = sourceBindings[sourceIndex];
                uint sourceId = binding.SourceStableId;
                if (sourceId == 0u)
                {
                    issues.Add(Issue("SHO001", $"{location}/TargetDefinition.SourceBindings[{sourceIndex}]", "Source binding identity is missing."));
                    continue;
                }

                var rules = binding.HazardActorOrchestrationRules ?? Array.Empty<HazardActorOrchestrationRuleBinding>();
                var localRuleIds = new HashSet<int>();
                for (int ruleIndex = 0; ruleIndex < rules.Length; ruleIndex++)
                {
                    var rule = rules[ruleIndex];
                    string ruleLocation = $"{location}/TargetDefinition.SourceBindings[{sourceIndex}].HazardActorOrchestrationRules[{ruleIndex}]";
                    if (rule.RuleId <= 0)
                    {
                        issues.Add(Issue("SHO002", ruleLocation, "RuleId must be >= 1."));
                        continue;
                    }

                    if (!localRuleIds.Add(rule.RuleId))
                    {
                        issues.Add(Issue("SHO003", ruleLocation, $"Duplicate source-local RuleId {rule.RuleId}."));
                        continue;
                    }

                    if (documentRuleKeys.Contains(Key(sourceId, rule.RuleId)))
                    {
                        issues.Add(Issue("SHO004", ruleLocation, $"Document already contains source-local RuleId {rule.RuleId}. Clear or edit document rules before importing."));
                        continue;
                    }

                    var targetIds = rule.TargetPlacementInstanceIds ?? Array.Empty<int>();
                    if (targetIds.Length == 0 && rule.TargetPlacementInstanceId > 0)
                        targetIds = new[] { rule.TargetPlacementInstanceId };
                    if (targetIds.Length == 0)
                    {
                        issues.Add(Issue("SHO005", ruleLocation, "Rule has no target placement identity."));
                        continue;
                    }

                    bool validTargets = true;
                    for (int targetIndex = 0; targetIndex < targetIds.Length; targetIndex++)
                    {
                        if (!placementLookup.TryGetValue(Key(sourceId, targetIds[targetIndex]), out _))
                        {
                            issues.Add(Issue("SHO006", ruleLocation, $"Target placement {targetIds[targetIndex]} is missing, ambiguous, or owned by another source."));
                            validTargets = false;
                        }
                    }
                    if (!validTargets)
                        continue;

                    candidates.Add(new StageMapHazardActorOrchestrationRuleData
                    {
                        OwningSourceStableId = sourceId,
                        RuleId = rule.RuleId,
                        TargetPlacementInstanceIds = (int[])targetIds.Clone(),
                        ActionType = rule.ActionType,
                        TriggerType = rule.TriggerType,
                        TriggerThresholdNormalized = Mathf.Clamp01(rule.TriggerThresholdNormalized),
                        TargetPhaseId = Mathf.Max(1, rule.TargetPhaseId),
                    });
                }
            }

            if (candidates.Count > 0)
            {
                changes.Add(new StageMapApplyPlanChange(
                    StageMapApplyChangeKind.Add,
                    nameof(StageMapDocument),
                    nameof(StageMapDocument.HazardActorOrchestrationRules),
                    $"Import {candidates.Count} TargetDefinition orchestration rule(s) into StageMapDocument."));
            }
            return BuildPlan(document, candidates, issues, changes);
        }

        public static bool TryApplyImport(StageMapHazardActorOrchestrationImportPlan plan, bool saveAssets, out string error)
        {
            error = string.Empty;
            if (plan == null || plan.Document == null)
            {
                error = "HazardActor orchestration import plan is invalid.";
                return false;
            }
            if (StageMapApplyPlanner.ComputeSignature(plan.Document) != plan.DocumentSignature
                || StageMapApplyPlanner.ComputeSignature(plan.Document.TargetDefinition) != plan.TargetDefinitionSignature)
            {
                error = "StageMapDocument or TargetDefinition changed after orchestration import preview. Rebuild the plan.";
                return false;
            }
            if (plan.HasErrors)
            {
                error = "HazardActor orchestration import validation failed.";
                return false;
            }
            if (!plan.HasChanges)
                return true;

            Undo.RecordObject(plan.Document, "Import Hazard Actor Orchestration Rules");
            var current = plan.Document.HazardActorOrchestrationRules ?? Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            var next = (StageMapHazardActorOrchestrationRuleData[])current.Clone();
            int oldLength = next.Length;
            Array.Resize(ref next, oldLength + plan.CandidateRules.Count);
            for (int i = 0; i < plan.CandidateRules.Count; i++)
                next[oldLength + i] = CloneRule(plan.CandidateRules[i]);
            plan.Document.HazardActorOrchestrationRules = next;
            EditorUtility.SetDirty(plan.Document);
            if (saveAssets && AssetDatabase.Contains(plan.Document))
                AssetDatabase.SaveAssets();
            return true;
        }

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

        private static StageMapHazardActorOrchestrationImportPlan BuildPlan(
            StageMapDocument document,
            List<StageMapHazardActorOrchestrationRuleData> candidates,
            List<ContentValidationIssue> issues,
            List<StageMapApplyPlanChange> changes)
        {
            return new StageMapHazardActorOrchestrationImportPlan(
                document,
                StageMapApplyPlanner.ComputeSignature(document),
                StageMapApplyPlanner.ComputeSignature(document != null ? document.TargetDefinition : null),
                candidates.Select(CloneRule).ToArray(),
                issues,
                changes);
        }

        private static Dictionary<string, StageMapHazardActorPlacementData> BuildPlacementLookup(StageMapHazardActorPlacementData[] placements)
        {
            var result = new Dictionary<string, StageMapHazardActorPlacementData>();
            var duplicate = new HashSet<string>();
            if (placements != null)
            {
                for (int i = 0; i < placements.Length; i++)
                {
                    string key = Key(placements[i].OwningSourceStableId, placements[i].PlacementInstanceId);
                    if (result.ContainsKey(key))
                    {
                        duplicate.Add(key);
                        result.Remove(key);
                    }
                    else if (!duplicate.Contains(key))
                    {
                        result.Add(key, placements[i]);
                    }
                }
            }
            return result;
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

        private static ContentValidationIssue Issue(string code, string location, string message)
        {
            return new ContentValidationIssue(ContentValidationSeverity.Error, code, location, message);
        }

        private static string Key(uint sourceStableId, int id)
        {
            return $"{sourceStableId}:{id}";
        }
    }
}
