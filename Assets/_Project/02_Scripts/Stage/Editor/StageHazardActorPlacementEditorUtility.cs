using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    /// <summary>
    /// Owns editor-time creation, source-relative pose conversion, and validation for Hazard Actor placements.
    /// </summary>
    public static class StageHazardActorPlacementEditorUtility
    {
        private const float RotationToleranceDeg = 0.001f;
        private const float PositionToleranceSqr = 0.000001f;

        public sealed class DefinitionSyncPlan
        {
            internal DefinitionSyncPlan(
                SourceRuntimeTemplateAuthoringBase source,
                StageDefinitionSO definition,
                int bindingIndex,
                HazardActorPlacementBinding[] placements,
                HazardActorOrchestrationRuleBinding[] rules,
                int addCount,
                int updateCount,
                int removeCount,
                int prefabReplacementCount,
                bool rulesChanged,
                HazardActorPlacementBinding[] existingPlacements,
                HazardActorOrchestrationRuleBinding[] existingRules)
            {
                Source = source;
                Definition = definition;
                BindingIndex = bindingIndex;
                Placements = placements;
                Rules = rules;
                AddCount = addCount;
                UpdateCount = updateCount;
                RemoveCount = removeCount;
                PrefabReplacementCount = prefabReplacementCount;
                RulesChanged = rulesChanged;
                ExistingPlacements = existingPlacements;
                ExistingRules = existingRules;
            }

            public SourceRuntimeTemplateAuthoringBase Source { get; }
            public StageDefinitionSO Definition { get; }
            public int BindingIndex { get; }
            public HazardActorPlacementBinding[] Placements { get; }
            public HazardActorOrchestrationRuleBinding[] Rules { get; }
            public int AddCount { get; }
            public int UpdateCount { get; }
            public int RemoveCount { get; }
            public int PrefabReplacementCount { get; }
            public bool RulesChanged { get; }
            public bool HasChanges => AddCount > 0 || UpdateCount > 0 || RemoveCount > 0 || RulesChanged;
            public bool RequiresConfirmation => RemoveCount > 0 || PrefabReplacementCount > 0;
            internal HazardActorPlacementBinding[] ExistingPlacements { get; }
            internal HazardActorOrchestrationRuleBinding[] ExistingRules { get; }

            public string Summary =>
                $"Placements: +{AddCount} / ~{UpdateCount} / -{RemoveCount}, "
                + $"prefab replacements: {PrefabReplacementCount}, rules: {(RulesChanged ? "modified" : "unchanged")}";
        }

        public static bool TryCreatePlacement(
            SourceRuntimeTemplateAuthoringBase source,
            out StageHazardActorMarker marker,
            out string error)
        {
            marker = null;
            error = null;
            if (source == null)
            {
                error = "Select a GameObject with SourceRuntimeTemplateAuthoringBase.";
                return false;
            }

            var stage = source.GetComponentInParent<StageLayoutStageMarker>();
            if (stage == null)
            {
                error = "The selected source must be under a StageLayoutStageMarker.";
                return false;
            }

            int placementId = GetNextPlacementInstanceId(stage);
            var placementObject = new GameObject($"HazardActor_Placement_{placementId}");
            Undo.RegisterCreatedObjectUndo(placementObject, "Add Hazard Actor Placement");
            Undo.SetTransformParent(placementObject.transform, source.transform, "Parent Hazard Actor Placement");
            placementObject.transform.localPosition = Vector3.zero;
            placementObject.transform.localRotation = Quaternion.identity;
            placementObject.transform.localScale = Vector3.one;

            marker = Undo.AddComponent<StageHazardActorMarker>(placementObject);
            marker.PlacementInstanceId = placementId;
            marker.LocalYawDeg = 0f;
            EditorUtility.SetDirty(marker);
            Selection.activeGameObject = placementObject;
            return true;
        }

        public static int GetNextPlacementInstanceId(StageLayoutStageMarker stage)
        {
            if (stage == null)
                return 1;

            int maxId = 0;
            var markers = stage.GetComponentsInChildren<StageHazardActorMarker>(includeInactive: true);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null || marker.GetComponentInParent<StageLayoutStageMarker>() != stage)
                    continue;

                maxId = Mathf.Max(maxId, marker.PlacementInstanceId);
            }

            return Mathf.Max(1, maxId + 1);
        }

        public static SourceRuntimeTemplateAuthoringBase ResolveSourceForPlacement(GameObject selected)
        {
            return selected != null
                ? selected.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>()
                : null;
        }

        public static bool TryGetLocalPose(
            StageHazardActorMarker marker,
            out SourceRuntimeTemplateAuthoringBase source,
            out Vector3 localOffset,
            out float localYawDeg)
        {
            source = null;
            localOffset = default;
            localYawDeg = 0f;
            if (marker == null)
                return false;

            source = marker.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>();
            if (source == null)
                return false;

            localOffset = source.transform.InverseTransformPoint(marker.transform.position);
            Quaternion localRotation = Quaternion.Inverse(source.transform.rotation) * marker.transform.rotation;
            localYawDeg = NormalizeYaw(localRotation.eulerAngles.y);
            return true;
        }

        public static bool SyncCachedYawFromTransform(StageHazardActorMarker marker, bool recordUndo)
        {
            if (!TryGetLocalPose(marker, out _, out _, out float localYawDeg)
                || Mathf.Abs(Mathf.DeltaAngle(marker.LocalYawDeg, localYawDeg)) <= RotationToleranceDeg)
            {
                return false;
            }

            if (recordUndo)
                Undo.RecordObject(marker, "Rotate Hazard Actor Placement");

            marker.LocalYawDeg = localYawDeg;
            EditorUtility.SetDirty(marker);
            return true;
        }

        public static bool ApplyCachedYawToTransform(StageHazardActorMarker marker, bool recordUndo)
        {
            if (marker == null)
                return false;

            var source = marker.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>();
            if (source == null)
                return false;

            float localYawDeg = NormalizeYaw(marker.LocalYawDeg);
            Quaternion worldRotation = source.transform.rotation * Quaternion.Euler(0f, localYawDeg, 0f);
            bool cacheMatches = Mathf.Abs(Mathf.DeltaAngle(marker.LocalYawDeg, localYawDeg)) <= RotationToleranceDeg;
            bool transformMatches = Quaternion.Angle(marker.transform.rotation, worldRotation) <= RotationToleranceDeg;
            if (cacheMatches && transformMatches)
                return false;

            if (recordUndo)
                Undo.RecordObjects(new UnityEngine.Object[] { marker, marker.transform }, "Apply Hazard Actor Local Yaw");

            marker.LocalYawDeg = localYawDeg;
            marker.transform.rotation = worldRotation;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(marker.transform);
            return true;
        }

        public static List<string> CollectValidationErrors(StageHazardActorMarker marker)
        {
            var errors = new List<string>(4);
            if (marker == null)
            {
                errors.Add("Hazard Actor placement marker is null.");
                return errors;
            }

            var source = marker.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>();
            if (source == null)
            {
                errors.Add("Placement must be parented under a SourceRuntimeTemplateAuthoringBase.");
                return errors;
            }

            if (marker.transform.parent != source.transform)
                errors.Add("Placement must be a direct child of its SourceRuntimeTemplateAuthoringBase.");

            var stage = source.GetComponentInParent<StageLayoutStageMarker>();
            if (stage == null)
            {
                errors.Add("Owning source must be under a StageLayoutStageMarker.");
            }
            else
            {
                int duplicateCount = 0;
                var stageMarkers = stage.GetComponentsInChildren<StageHazardActorMarker>(includeInactive: true);
                for (int i = 0; i < stageMarkers.Length; i++)
                {
                    var candidate = stageMarkers[i];
                    if (candidate != null
                        && candidate.GetComponentInParent<StageLayoutStageMarker>() == stage
                        && candidate.PlacementInstanceId == marker.PlacementInstanceId)
                    {
                        duplicateCount++;
                    }
                }

                if (marker.PlacementInstanceId < 1)
                    errors.Add("PlacementInstanceId must be >= 1.");
                else if (duplicateCount > 1)
                    errors.Add($"PlacementInstanceId {marker.PlacementInstanceId} is duplicated in Stage {stage.StageId}.");
            }

            if (marker.ActorArchetypePrefab == null)
            {
                errors.Add("ActorArchetypePrefab is required.");
            }
            else
            {
                var authorings = marker.ActorArchetypePrefab.GetComponentsInChildren<HazardActorAuthoring>(true);
                if (authorings == null || authorings.Length != 1)
                    errors.Add($"ActorArchetypePrefab must contain exactly one HazardActorAuthoring. found={authorings?.Length ?? 0}");
            }

            return errors;
        }

        public static bool TryBuildDefinitionSyncPlan(
            SourceRuntimeTemplateAuthoringBase source,
            out DefinitionSyncPlan plan,
            out List<string> errors)
        {
            plan = null;
            errors = new List<string>(8);
            if (source == null)
            {
                errors.Add("Select a GameObject with SourceRuntimeTemplateAuthoringBase.");
                return false;
            }

            var stage = source.GetComponentInParent<StageLayoutStageMarker>();
            if (stage == null)
            {
                errors.Add("The selected source must be under a StageLayoutStageMarker.");
                return false;
            }

            if (stage.TargetDefinition == null)
            {
                errors.Add("Owning StageLayoutStageMarker.TargetDefinition is not assigned.");
                return false;
            }

            if (source.StableIdOverride < 1)
                errors.Add("Source StableIdOverride must be >= 1 before Hazard Actor data can be synchronized.");

            var bindings = stage.TargetDefinition.SourceBindings ?? Array.Empty<StageSourceBinding>();
            var matchingBindingIndices = new List<int>(2);
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i].SourceStableId == source.StableIdOverride)
                    matchingBindingIndices.Add(i);
            }

            if (matchingBindingIndices.Count == 0)
            {
                errors.Add(
                    $"StageDefinition has no SourceBinding for StableId {source.StableIdOverride}. "
                    + "Run Ensure Missing Stage Definition Bindings first.");
            }
            else if (matchingBindingIndices.Count > 1)
            {
                errors.Add($"StageDefinition has duplicate SourceBindings for StableId {source.StableIdOverride}.");
            }

            var sourceMarkers = source.GetComponentsInChildren<StageHazardActorMarker>(includeInactive: true);
            var uniqueErrors = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < sourceMarkers.Length; i++)
            {
                var marker = sourceMarkers[i];
                if (marker == null || marker.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>() != source)
                    continue;

                var markerErrors = CollectValidationErrors(marker);
                for (int errorIndex = 0; errorIndex < markerErrors.Count; errorIndex++)
                {
                    string message = $"{BuildHierarchyPath(marker.transform)}: {markerErrors[errorIndex]}";
                    if (uniqueErrors.Add(message))
                        errors.Add(message);
                }
            }

            if (errors.Count > 0)
                return false;

            int bindingIndex = matchingBindingIndices[0];
            var placements = StageDefinitionGenerator.BuildHazardActorPlacements(source)
                .OrderBy(x => x.PlacementInstanceId)
                .ToArray();
            var rulesMarker = source.GetComponent<HazardActorSourceAuthoringMarker>();
            var rules = CloneRules(rulesMarker != null ? rulesMarker.Rules : null);
            var authoredPlacementIds = new HashSet<int>(placements.Select(x => x.PlacementInstanceId));
            for (int i = 0; i < bindings.Length; i++)
            {
                if (i == bindingIndex)
                    continue;

                var otherPlacements = bindings[i].HazardActorPlacements
                    ?? Array.Empty<HazardActorPlacementBinding>();
                for (int placementIndex = 0; placementIndex < otherPlacements.Length; placementIndex++)
                {
                    int placementId = otherPlacements[placementIndex].PlacementInstanceId;
                    if (authoredPlacementIds.Contains(placementId))
                    {
                        errors.Add(
                            $"PlacementInstanceId {placementId} conflicts with Definition SourceBinding "
                            + $"{bindings[i].SourceStableId}. Synchronize or fix the other source first.");
                    }
                }
            }

            if (errors.Count > 0)
                return false;

            var prospectiveBinding = bindings[bindingIndex];
            prospectiveBinding.HazardActorPlacements = placements;
            prospectiveBinding.HazardActorOrchestrationRules = rules;
            var validationIssues = new List<ContentValidationIssue>(8);
            StageCatalogValidationRules.ValidateHazardActorData(
                prospectiveBinding,
                $"Stage {stage.StageId}/Source {source.StableIdOverride}",
                validationIssues);
            for (int i = 0; i < validationIssues.Count; i++)
            {
                if (validationIssues[i].Severity == ContentValidationSeverity.Error)
                    errors.Add($"{validationIssues[i].Code}: {validationIssues[i].Message}");
            }

            if (errors.Count > 0)
                return false;

            var existingPlacements = bindings[bindingIndex].HazardActorPlacements
                ?? Array.Empty<HazardActorPlacementBinding>();
            ComputePlacementDiff(
                existingPlacements,
                placements,
                out int addCount,
                out int updateCount,
                out int removeCount,
                out int prefabReplacementCount);
            bool rulesChanged = !RulesEqual(
                bindings[bindingIndex].HazardActorOrchestrationRules,
                rules);

            plan = new DefinitionSyncPlan(
                source,
                stage.TargetDefinition,
                bindingIndex,
                placements,
                rules,
                addCount,
                updateCount,
                removeCount,
                prefabReplacementCount,
                rulesChanged,
                ClonePlacements(existingPlacements),
                CloneRules(bindings[bindingIndex].HazardActorOrchestrationRules));
            return true;
        }

        public static bool TryApplyDefinitionSyncPlan(
            DefinitionSyncPlan plan,
            bool saveAssets,
            out string error)
        {
            error = null;
            if (plan == null || plan.Source == null || plan.Definition == null)
            {
                error = "Hazard Actor sync plan is no longer valid.";
                return false;
            }

            if (!TryBuildDefinitionSyncPlan(plan.Source, out var currentPlan, out var errors))
            {
                error = errors.Count > 0
                    ? string.Join("\n", errors)
                    : "Hazard Actor data validation failed.";
                return false;
            }

            if (!PlansEqual(plan, currentPlan))
            {
                error = "Scene or Definition data changed after the sync preview. Review the updated diff and apply again.";
                return false;
            }

            if (!currentPlan.HasChanges)
                return true;

            Undo.RecordObject(currentPlan.Definition, "Apply Hazard Actor Data To Definition");
            var bindings = currentPlan.Definition.SourceBindings;
            var binding = bindings[currentPlan.BindingIndex];
            binding.HazardActorPlacements = ClonePlacements(currentPlan.Placements);
            binding.HazardActorOrchestrationRules = CloneRules(currentPlan.Rules);
            bindings[currentPlan.BindingIndex] = binding;
            currentPlan.Definition.SourceBindings = bindings;
            EditorUtility.SetDirty(currentPlan.Definition);
            if (saveAssets && AssetDatabase.Contains(currentPlan.Definition))
                AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("GameObject/Stage/Add Hazard Actor Placement", false, 20)]
        private static void AddPlacementMenu()
        {
            var source = ResolveSourceForPlacement(Selection.activeGameObject);
            if (!TryCreatePlacement(source, out _, out string error))
                Debug.LogError($"[HazardActorPlacement] {error}");
        }

        [MenuItem("GameObject/Stage/Add Hazard Actor Placement", true)]
        private static bool ValidateAddPlacementMenu()
        {
            return ResolveSourceForPlacement(Selection.activeGameObject) != null;
        }

        private static void ComputePlacementDiff(
            HazardActorPlacementBinding[] existing,
            HazardActorPlacementBinding[] authored,
            out int addCount,
            out int updateCount,
            out int removeCount,
            out int prefabReplacementCount)
        {
            var existingById = new Dictionary<int, HazardActorPlacementBinding>();
            int duplicateExistingCount = 0;
            for (int i = 0; i < existing.Length; i++)
            {
                if (existingById.ContainsKey(existing[i].PlacementInstanceId))
                {
                    duplicateExistingCount++;
                    continue;
                }

                existingById.Add(existing[i].PlacementInstanceId, existing[i]);
            }

            var authoredById = authored.ToDictionary(x => x.PlacementInstanceId);
            addCount = 0;
            updateCount = 0;
            removeCount = duplicateExistingCount;
            prefabReplacementCount = 0;

            foreach (var pair in authoredById)
            {
                if (!existingById.TryGetValue(pair.Key, out var previous))
                {
                    addCount++;
                    continue;
                }

                if (PlacementEqual(previous, pair.Value))
                    continue;

                updateCount++;
                if (previous.ActorArchetypePrefab != pair.Value.ActorArchetypePrefab)
                    prefabReplacementCount++;
            }

            foreach (var pair in existingById)
            {
                if (!authoredById.ContainsKey(pair.Key))
                    removeCount++;
            }
        }

        private static bool PlansEqual(DefinitionSyncPlan left, DefinitionSyncPlan right)
        {
            return left.Source == right.Source
                && left.Definition == right.Definition
                && left.BindingIndex == right.BindingIndex
                && PlacementsEqual(left.ExistingPlacements, right.ExistingPlacements)
                && RulesEqual(left.ExistingRules, right.ExistingRules)
                && PlacementsEqual(left.Placements, right.Placements)
                && RulesEqual(left.Rules, right.Rules);
        }

        private static bool PlacementsEqual(
            HazardActorPlacementBinding[] left,
            HazardActorPlacementBinding[] right)
        {
            left = left ?? Array.Empty<HazardActorPlacementBinding>();
            right = right ?? Array.Empty<HazardActorPlacementBinding>();
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (!PlacementEqual(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private static bool PlacementEqual(
            HazardActorPlacementBinding left,
            HazardActorPlacementBinding right)
        {
            return left.PlacementInstanceId == right.PlacementInstanceId
                && left.ActorArchetypePrefab == right.ActorArchetypePrefab
                && (left.LocalOffset - right.LocalOffset).sqrMagnitude <= PositionToleranceSqr
                && Mathf.Abs(Mathf.DeltaAngle(left.LocalYawDeg, right.LocalYawDeg)) <= RotationToleranceDeg;
        }

        private static bool RulesEqual(
            HazardActorOrchestrationRuleBinding[] left,
            HazardActorOrchestrationRuleBinding[] right)
        {
            left = left ?? Array.Empty<HazardActorOrchestrationRuleBinding>();
            right = right ?? Array.Empty<HazardActorOrchestrationRuleBinding>();
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                var a = left[i];
                var b = right[i];
                if (a.RuleId != b.RuleId
                    || a.ActionType != b.ActionType
                    || a.TriggerType != b.TriggerType
                    || !Mathf.Approximately(a.TriggerThresholdNormalized, b.TriggerThresholdNormalized)
                    || a.TargetPhaseId != b.TargetPhaseId
                    || !(a.TargetPlacementInstanceIds ?? Array.Empty<int>())
                        .SequenceEqual(b.TargetPlacementInstanceIds ?? Array.Empty<int>()))
                {
                    return false;
                }
            }

            return true;
        }

        private static HazardActorPlacementBinding[] ClonePlacements(HazardActorPlacementBinding[] placements)
        {
            return placements != null
                ? (HazardActorPlacementBinding[])placements.Clone()
                : Array.Empty<HazardActorPlacementBinding>();
        }

        private static HazardActorOrchestrationRuleBinding[] CloneRules(
            HazardActorOrchestrationRuleBinding[] rules)
        {
            if (rules == null || rules.Length == 0)
                return Array.Empty<HazardActorOrchestrationRuleBinding>();

            var clones = new HazardActorOrchestrationRuleBinding[rules.Length];
            for (int i = 0; i < rules.Length; i++)
            {
                clones[i] = rules[i];
                clones[i].TargetPlacementInstanceIds = rules[i].TargetPlacementInstanceIds != null
                    ? (int[])rules[i].TargetPlacementInstanceIds.Clone()
                    : Array.Empty<int>();
            }

            return clones;
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            var parent = transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private static float NormalizeYaw(float yawDeg)
        {
            float normalized = Mathf.Repeat(yawDeg, 360f);
            return Mathf.Approximately(normalized, 360f) ? 0f : normalized;
        }
    }
}
