using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageDefinitionGenerator
    {
        [MenuItem("Tools/Project/Stage Layout/Ensure Missing Stage Definition Bindings From Runtime Template Sources")]
        private static void EnsureMissingDefinitionsFromOpenScenesMenu()
        {
            int seeded = SyncDefinitionsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageDefinition] Ensure missing runtime template bindings complete. definitions={seeded}");
        }

        public static int SyncDefinitionsFromOpenScenes(bool saveAssets)
        {
            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, (a, b) => string.CompareOrdinal(BuildHierarchyPath(a != null ? a.transform : null), BuildHierarchyPath(b != null ? b.transform : null)));

            int synced = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (TrySyncDefinitionsForRoot(root, out var issues, saveAssets))
                    synced += CountSyncedDefinitions(root);

                ReportIssues(root, issues);
            }

            return synced;
        }

        public static bool TrySyncDefinitionsForRoot(
            StageLayoutRootMarker root,
            out List<ContentValidationIssue> issues,
            bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SDF900", "(null)", "StageLayoutRootMarker is null."));
                return false;
            }

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "SDF901", BuildHierarchyPath(root.transform), "No StageLayoutStageMarker was found under root."));
                return true;
            }

            var authorings = UnityEngine.Object.FindObjectsByType<SourceRuntimeTemplateAuthoringBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var authoringByStableId = BuildAuthoringIndex(authorings, issues);

            Array.Sort(stageNodes, (a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;
                return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
            });

            for (int i = 0; i < stageNodes.Length; i++)
            {
                var stageNode = stageNodes[i];
                if (stageNode == null)
                    continue;

                SyncDefinitionForStage(stageNode, authoringByStableId, issues, saveAssets);
            }

            return !HasError(issues);
        }

        private static void SyncDefinitionForStage(
            StageLayoutStageMarker stageNode,
            Dictionary<uint, SourceRuntimeTemplateAuthoringBase> authoringByStableId,
            List<ContentValidationIssue> issues,
            bool saveAssets)
        {
            string location = BuildHierarchyPath(stageNode.transform);
            if (stageNode.TargetDefinition == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SDF902", location, "TargetDefinition is not assigned."));
                return;
            }

            var stableIds = CollectSourceStableIds(stageNode);
            var existingBindings = stageNode.TargetDefinition.SourceBindings ?? Array.Empty<StageSourceBinding>();
            var bindingByStableId = new Dictionary<uint, StageSourceBinding>();
            for (int i = 0; i < existingBindings.Length; i++)
            {
                uint stableId = Math.Max(1u, existingBindings[i].SourceStableId);
                if (!bindingByStableId.ContainsKey(stableId))
                    bindingByStableId.Add(stableId, existingBindings[i]);
            }

            var sourceBindings = new List<StageSourceBinding>(Math.Max(stableIds.Count, existingBindings.Length));
            for (int i = 0; i < stableIds.Count; i++)
            {
                uint stableId = stableIds[i];
                if (bindingByStableId.TryGetValue(stableId, out var existing))
                {
                    sourceBindings.Add(existing);
                    continue;
                }

                if (!authoringByStableId.TryGetValue(stableId, out var authoring) || authoring == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "SDF903",
                        location,
                        $"No source runtime template authoring with StableIdOverride={stableId} was found. Default binding template will be generated."));
                    sourceBindings.Add(CreateDefaultBinding(stableId));
                    continue;
                }

                sourceBindings.Add(BuildBindingFromAuthoring(stableId, authoring));
            }

            for (int i = 0; i < existingBindings.Length; i++)
            {
                uint stableId = Math.Max(1u, existingBindings[i].SourceStableId);
                if (stableIds.Contains(stableId))
                    continue;

                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Warning,
                    "SDF905",
                    location,
                    $"StageDefinition contains orphan binding not present in layout. stableId={stableId}"));
                sourceBindings.Add(existingBindings[i]);
            }

            Undo.RecordObject(stageNode.TargetDefinition, "Ensure Stage Definition Bindings");
            stageNode.TargetDefinition.StageId = Math.Max(1, stageNode.StageId);
            if (string.IsNullOrWhiteSpace(stageNode.TargetDefinition.DisplayName))
                stageNode.TargetDefinition.DisplayName = $"Stage {stageNode.TargetDefinition.StageId}";
            if (stageNode.TargetDefinition.StageTimeLimitSec <= 0f)
                stageNode.TargetDefinition.StageTimeLimitSec = 150f;
            stageNode.TargetDefinition.SourceBindings = sourceBindings.ToArray();
            EditorUtility.SetDirty(stageNode.TargetDefinition);
            if (saveAssets)
                AssetDatabase.SaveAssets();
        }

        private static Dictionary<uint, SourceRuntimeTemplateAuthoringBase> BuildAuthoringIndex(
            SourceRuntimeTemplateAuthoringBase[] authorings,
            List<ContentValidationIssue> issues)
        {
            var result = new Dictionary<uint, SourceRuntimeTemplateAuthoringBase>();
            if (authorings == null)
                return result;

            for (int i = 0; i < authorings.Length; i++)
            {
                var authoring = authorings[i];
                if (authoring == null)
                    continue;

                uint stableId = authoring.StableIdOverride;
                if (stableId == 0)
                    continue;

                if (result.ContainsKey(stableId))
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "SDF904",
                        BuildHierarchyPath(authoring.transform),
                        $"Duplicate source runtime template StableIdOverride detected: {stableId}. First instance will be used."));
                    continue;
                }

                result.Add(stableId, authoring);
            }

            return result;
        }

        private static List<uint> CollectSourceStableIds(StageLayoutStageMarker stageNode)
        {
            var ids = new List<uint>(8);
            var unique = new HashSet<uint>();

            if (stageNode.TargetLayout != null && StageGridLayoutValidationRules.UsesGridSchema(stageNode.TargetLayout))
            {
                var sourceRegions = stageNode.TargetLayout.SourceRegions ?? Array.Empty<StageSourceRegionLayoutData>();
                for (int i = 0; i < sourceRegions.Length; i++)
                {
                    uint stableId = Math.Max(1u, sourceRegions[i].StableId);
                    if (unique.Add(stableId))
                        ids.Add(stableId);
                }

                ids.Sort();
                return ids;
            }

            ids.Sort();
            return ids;
        }

        private static StageSourceBinding CreateDefaultBinding(uint stableId)
        {
            return new StageSourceBinding
            {
                SourceStableId = Math.Max(1u, stableId),
                InitialSourceState = SourceStateId.Normal,
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                SustainSlots = Array.Empty<SustainSlotBinding>(),
                EventSlots = Array.Empty<EventSlotBinding>(),
                HazardActors = Array.Empty<HazardActorBinding>(),
                HazardActorPlacements = Array.Empty<HazardActorPlacementBinding>(),
            };
        }

        private static StageSourceBinding BuildBindingFromAuthoring(uint stableId, SourceRuntimeTemplateAuthoringBase authoring)
        {
            return new StageSourceBinding
            {
                SourceStableId = Math.Max(1u, stableId),
                InitialSourceState = authoring.InitialState,
                ThresholdWeakened = Mathf.Max(0, authoring.ThresholdWeakened),
                ThresholdDepleted = Mathf.Max(Mathf.Max(0, authoring.ThresholdWeakened), authoring.ThresholdDepleted),
                SustainSlots = BuildSustainSlots(authoring.SustainClipSlots),
                EventSlots = BuildEventSlots(authoring.EventClipSlots),
                HazardActors = Array.Empty<HazardActorBinding>(),
                HazardActorPlacements = Array.Empty<HazardActorPlacementBinding>(),
            };
        }

        private static SustainSlotBinding[] BuildSustainSlots(SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring[] slots)
        {
            if (slots == null || slots.Length <= 0)
                return Array.Empty<SustainSlotBinding>();

            var result = new SustainSlotBinding[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var clips = slot.Clips != null ? slot.Clips.Where(c => c != null).ToArray() : Array.Empty<WaveClipSO>();
                var weights = BuildWeights(slot.Weights, clips.Length);
                result[i] = new SustainSlotBinding
                {
                    State = slot.State,
                    Lane = slot.Lane,
                    Clips = clips,
                    Weights = weights,
                };
            }

            return result;
        }

        private static EventSlotBinding[] BuildEventSlots(SourceRuntimeTemplateAuthoringBase.EventClipSlotAuthoring[] slots)
        {
            if (slots == null || slots.Length <= 0)
                return Array.Empty<EventSlotBinding>();

            var result = new EventSlotBinding[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                result[i] = new EventSlotBinding
                {
                    TriggerState = slot.TriggerState,
                    EventClips = slot.EventClips != null ? slot.EventClips.Where(c => c != null).ToArray() : Array.Empty<WaveClipSO>(),
                };
            }

            return result;
        }

        private static float[] BuildWeights(float[] sourceWeights, int length)
        {
            if (length <= 0)
                return Array.Empty<float>();

            var weights = new float[length];
            for (int i = 0; i < length; i++)
            {
                float w = sourceWeights != null && i < sourceWeights.Length ? sourceWeights[i] : 1f;
                weights[i] = w > 0f ? w : 1f;
            }

            return weights;
        }

        private static int CountSyncedDefinitions(StageLayoutRootMarker root)
        {
            if (root == null)
                return 0;

            int count = 0;
            var nodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].TargetDefinition != null)
                    count++;
            }

            return count;
        }

        private static bool HasError(IReadOnlyList<ContentValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    return true;
            }

            return false;
        }

        private static void ReportIssues(StageLayoutRootMarker root, IReadOnlyList<ContentValidationIssue> issues)
        {
            if (issues == null || issues.Count <= 0)
                return;

            string rootName = root != null ? BuildHierarchyPath(root.transform) : "(null-root)";
            for (int i = 0; i < issues.Count; i++)
            {
                string line = $"[StageDefinition][{rootName}] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    Debug.LogError(line);
                else
                    Debug.LogWarning(line);
            }
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            if (transform == null)
                return "(null)";

            string path = transform.name;
            var current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }
    }
}


