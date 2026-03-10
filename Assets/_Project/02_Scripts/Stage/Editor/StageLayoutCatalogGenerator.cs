using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageLayoutCatalogGenerator
    {
        [MenuItem("Tools/Project/Stage Layout/Generate Stage Layout Assets From Open Scenes")]
        private static void GenerateStageLayoutsFromOpenScenesMenu()
        {
            int generated = GenerateStageLayoutsFromOpenScenes(saveAssets: true);
            Debug.Log($"[StageLayout] StageLayoutSO generation complete. layouts={generated}");
        }

        public static int GenerateStageLayoutsFromOpenScenes(bool saveAssets)
        {
            var roots = UnityEngine.Object.FindObjectsByType<StageLayoutRootMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(roots, CompareRoots);

            int generatedCount = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                if (TryGenerateLayoutsForRoot(root, out var issues, saveAssets))
                    generatedCount += CountGeneratedLayoutTargets(root);

                ReportIssues(root, issues);
            }

            return generatedCount;
        }

        public static bool TryGenerateLayoutsForRoot(StageLayoutRootMarker root, out List<ContentValidationIssue> issues, bool saveAssets = false)
        {
            issues = new List<ContentValidationIssue>(16);
            if (root == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL900", "(null)", "StageLayoutRootMarker is null."));
                return false;
            }

            var stageNodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            if (stageNodes == null || stageNodes.Length <= 0)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, "STL902", BuildHierarchyPath(root.transform), "No StageLayoutStageMarker was found under root."));
                return true;
            }

            Array.Sort(stageNodes, (a, b) =>
            {
                int stageOrder = a.StageId.CompareTo(b.StageId);
                if (stageOrder != 0)
                    return stageOrder;
                return string.CompareOrdinal(BuildHierarchyPath(a.transform), BuildHierarchyPath(b.transform));
            });

            for (int i = 0; i < stageNodes.Length; i++)
                TryGenerateLayoutForStage(stageNodes[i], issues, saveAssets);

            return !HasError(issues);
        }

        private static bool TryGenerateLayoutForStage(StageLayoutStageMarker stageNode, List<ContentValidationIssue> issues, bool saveAssets)
        {
            if (stageNode == null)
                return false;

            string location = BuildHierarchyPath(stageNode.transform);
            if (stageNode.TargetLayout == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL901", location, "TargetLayout is not assigned."));
                return false;
            }

            ValidatePresentationMarkers(stageNode, issues);
            var layout = BuildStageLayout(stageNode);
            var validation = new List<ContentValidationIssue>(8);
            StageLayoutValidationRules.ValidateLayout(layout, location, validation);
            issues.AddRange(validation);
            if (HasError(validation))
            {
                UnityEngine.Object.DestroyImmediate(layout);
                return false;
            }

            Undo.RecordObject(stageNode.TargetLayout, "Generate Stage Layout");
            stageNode.TargetLayout.StageId = layout.StageId;
            stageNode.TargetLayout.Sources = layout.Sources;
            stageNode.TargetLayout.Deposits = layout.Deposits;
            stageNode.TargetLayout.Obstacles = layout.Obstacles;
            stageNode.TargetLayout.Presentations = layout.Presentations;
            EditorUtility.SetDirty(stageNode.TargetLayout);
            UnityEngine.Object.DestroyImmediate(layout);
            if (saveAssets)
                AssetDatabase.SaveAssets();
            return true;
        }

        private static StageLayoutSO BuildStageLayout(StageLayoutStageMarker stageNode)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = stageNode.StageId;
            layout.Sources = stageNode.GetComponentsInChildren<StageSourceMarker>(includeInactive: true)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToSourceData)
                .ToArray();
            layout.Deposits = stageNode.GetComponentsInChildren<StageDepositMarker>(includeInactive: true)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToDepositData)
                .ToArray();
            layout.Obstacles = stageNode.GetComponentsInChildren<StageObstacleMarker>(includeInactive: true)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToObstacleData)
                .ToArray();
            layout.Presentations = stageNode.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToPresentationData)
                .ToArray();
            return layout;
        }

        private static int CountGeneratedLayoutTargets(StageLayoutRootMarker root)
        {
            if (root == null)
                return 0;

            int count = 0;
            var nodes = root.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null && nodes[i].TargetLayout != null)
                    count++;
            }

            return count;
        }

        private static StageSourceLayoutData ToSourceData(StageSourceMarker marker)
        {
            var transform = marker.transform;
            var yawOnlyRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return new StageSourceLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                YawDeg = yawOnlyRotation.eulerAngles.y,
                Shape = marker.Shape,
                Radius = Mathf.Max(0f, marker.Radius),
                Size = new Vector2(Mathf.Max(0f, marker.Size.x), Mathf.Max(0f, marker.Size.y)),
            };
        }

        private static StageDepositLayoutData ToDepositData(StageDepositMarker marker)
        {
            var transform = marker.transform;
            var yawOnlyRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return new StageDepositLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                YawDeg = yawOnlyRotation.eulerAngles.y,
                Shape = marker.Shape,
                Radius = Mathf.Max(0f, marker.Radius),
                Size = new Vector2(Mathf.Max(0f, marker.Size.x), Mathf.Max(0f, marker.Size.y)),
            };
        }

        private static StageObstacleLayoutData ToObstacleData(StageObstacleMarker marker)
        {
            var transform = marker.transform;
            var yawOnlyRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            return new StageObstacleLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                Position = transform.position,
                YawDeg = yawOnlyRotation.eulerAngles.y,
                Shape = marker.Shape,
                Radius = Mathf.Max(0f, marker.Radius),
                Size = new Vector2(Mathf.Max(0f, marker.Size.x), Mathf.Max(0f, marker.Size.y)),
                CollisionMask = marker.CollisionMask,
            };
        }

        private static StagePresentationLayoutData ToPresentationData(StagePresentationMarker marker)
        {
            var transform = marker.transform;
            ResolvePresentationLink(marker, out var linkKind, out var linkedStableId);
            bool linked = marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent;
            return new StagePresentationLayoutData
            {
                StableId = marker.StableId,
                Active = marker.Active,
                PlacementMode = marker.PlacementMode,
                LinkKind = linked ? linkKind : StagePresentationLinkKind.None,
                LinkedStableId = linked ? linkedStableId : 0u,
                PresentationKey = marker.PresentationKey != null ? marker.PresentationKey.Trim() : string.Empty,
                Position = linked ? transform.localPosition : transform.position,
                Euler = linked ? transform.localEulerAngles : transform.eulerAngles,
                Scale = transform.localScale,
            };
        }

        private static void ResolvePresentationLink(StagePresentationMarker marker, out StagePresentationLinkKind linkKind, out uint linkedStableId)
        {
            linkKind = StagePresentationLinkKind.None;
            linkedStableId = 0u;

            if (marker == null || marker.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                return;

            TryFindLinkedParent(marker.transform, out linkKind, out linkedStableId);
        }

        private static bool TryFindLinkedParent(Transform transform, out StagePresentationLinkKind linkKind, out uint linkedStableId)
        {
            linkKind = StagePresentationLinkKind.None;
            linkedStableId = 0u;

            var current = transform != null ? transform.parent : null;
            while (current != null)
            {
                if (current.TryGetComponent<StageSourceMarker>(out var source))
                {
                    linkKind = StagePresentationLinkKind.Source;
                    linkedStableId = source.StableId;
                    return true;
                }

                if (current.TryGetComponent<StageDepositMarker>(out var deposit))
                {
                    linkKind = StagePresentationLinkKind.Deposit;
                    linkedStableId = deposit.StableId;
                    return true;
                }

                if (current.TryGetComponent<StageObstacleMarker>(out var obstacle))
                {
                    linkKind = StagePresentationLinkKind.Obstacle;
                    linkedStableId = obstacle.StableId;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static int CompareRoots(StageLayoutRootMarker a, StageLayoutRootMarker b)
        {
            return string.CompareOrdinal(BuildHierarchyPath(a != null ? a.transform : null), BuildHierarchyPath(b != null ? b.transform : null));
        }

        private static void ValidatePresentationMarkers(StageLayoutStageMarker stageNode, List<ContentValidationIssue> issues)
        {
            var markers = stageNode.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                string location = BuildHierarchyPath(marker.transform);
                bool hasTopologyOnSelf = marker.TryGetComponent<StageSourceMarker>(out _)
                    || marker.TryGetComponent<StageDepositMarker>(out _)
                    || marker.TryGetComponent<StageObstacleMarker>(out _);
                if (hasTopologyOnSelf)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL010", location, "StagePresentationMarker must not share a GameObject with Source/Deposit/Obstacle marker."));

                bool hasParentTopology = TryFindLinkedParent(marker.transform, out _, out _);
                if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && !hasParentTopology)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL011", location, "LinkedToParent presentation requires a parent Source/Deposit/Obstacle marker."));

                if (marker.PlacementMode == StagePresentationPlacementMode.Standalone && hasParentTopology)
                    issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "STL012", location, "Standalone presentation must not be authored under a topology marker parent."));
            }
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
                string line = $"[StageLayout][{rootName}] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
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
                path = $"{current.name}/{path}";
                current = current.parent;
            }

            return path;
        }
    }
}
