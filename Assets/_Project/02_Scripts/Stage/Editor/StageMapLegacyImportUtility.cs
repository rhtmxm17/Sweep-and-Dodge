using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapLegacyImportUtility
    {
        public static bool TryBuildImportPlan(
            StageLayoutStageMarker sourceStage,
            StageMapDocument document,
            out StageMapLegacyImportPlan plan)
        {
            var issues = new List<ContentValidationIssue>(16);
            var changes = new List<StageMapApplyPlanChange>(8);
            plan = null;

            if (sourceStage == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMI900", "(null)", "Source StageLayoutStageMarker is null."));
                plan = new StageMapLegacyImportPlan(null, document, string.Empty, StageMapApplyPlanner.ComputeSignature(document), issues, changes);
                return false;
            }

            var sourceScene = sourceStage.gameObject.scene;
            if (sourceScene.IsValid()
                && sourceScene.isDirty
                && !string.IsNullOrEmpty(sourceScene.path))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMI923",
                    string.IsNullOrEmpty(sourceScene.path) ? sourceScene.name : sourceScene.path,
                    "Legacy import refuses an unsaved scene state. Save or revert the source scene, then rebuild the preview."));
                plan = new StageMapLegacyImportPlan(
                    sourceStage,
                    document,
                    ComputeLegacyStageSignature(sourceStage),
                    StageMapApplyPlanner.ComputeSignature(document),
                    issues,
                    changes);
                return false;
            }

            if (document == null)
            {
                issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "SMI901", BuildHierarchyPath(sourceStage.transform), "Target StageMapDocument is null."));
                plan = new StageMapLegacyImportPlan(sourceStage, null, ComputeLegacyStageSignature(sourceStage), string.Empty, issues, changes);
                return false;
            }

            if (!TryBuildDocumentSnapshot(sourceStage, document, out var snapshot, out var importIssues))
            {
                issues.AddRange(importIssues);
                plan = new StageMapLegacyImportPlan(
                    sourceStage,
                    document,
                    ComputeLegacyStageSignature(sourceStage),
                    StageMapApplyPlanner.ComputeSignature(document),
                    issues,
                    changes);
                return false;
            }

            try
            {
                issues.AddRange(importIssues);
                CollectImportChanges(document, snapshot, changes);
                plan = new StageMapLegacyImportPlan(
                    sourceStage,
                    document,
                    ComputeLegacyStageSignature(sourceStage),
                    StageMapApplyPlanner.ComputeSignature(document),
                    issues,
                    changes);
                return !plan.HasErrors;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        public static bool TryApplyImportPlan(StageMapLegacyImportPlan plan, bool saveAssets, out string error)
        {
            error = null;
            if (plan == null || plan.SourceStage == null || plan.Document == null)
            {
                error = "Stage map legacy import plan is invalid.";
                return false;
            }

            string currentSourceSignature = ComputeLegacyStageSignature(plan.SourceStage);
            string currentDocumentSignature = StageMapApplyPlanner.ComputeSignature(plan.Document);
            if (currentSourceSignature != plan.SourceSignature
                || currentDocumentSignature != plan.DocumentSignature)
            {
                error = "Legacy source or document changed after import preview. Rebuild the import plan before applying.";
                return false;
            }

            if (!TryBuildImportPlan(plan.SourceStage, plan.Document, out var currentPlan))
            {
                error = "Legacy import validation failed.";
                return false;
            }

            if (currentPlan.SourceSignature != plan.SourceSignature
                || currentPlan.DocumentSignature != plan.DocumentSignature)
            {
                error = "Legacy source or document changed after import preview. Rebuild the import plan before applying.";
                return false;
            }

            if (!TryBuildDocumentSnapshot(plan.SourceStage, plan.Document, out var snapshot, out var issues))
            {
                error = issues.Count > 0 ? issues[0].Message : "Legacy import snapshot could not be built.";
                return false;
            }

            try
            {
                Undo.RecordObject(plan.Document, "Import Legacy Stage Map Document");
                CopyDocumentData(snapshot, plan.Document);
                EditorUtility.SetDirty(plan.Document);
                if (saveAssets && AssetDatabase.Contains(plan.Document))
                    AssetDatabase.SaveAssets();
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(snapshot);
            }
        }

        public static bool TryBuildDocumentSnapshot(
            StageLayoutStageMarker sourceStage,
            StageMapDocument existingDocument,
            out StageMapDocument snapshot,
            out List<ContentValidationIssue> issues)
        {
            snapshot = null;
            if (!StageLayoutCatalogGenerator.TryBuildStageLayoutSnapshot(sourceStage, out var layout, out issues))
                return false;

            try
            {
                snapshot = ScriptableObject.CreateInstance<StageMapDocument>();
                snapshot.SchemaVersion = StageMapDocument.CurrentSchemaVersion;
                snapshot.StageId = Mathf.Max(1, sourceStage.StageId);
                if (sourceStage.TargetDefinition != null)
                {
                    snapshot.DisplayName = sourceStage.TargetDefinition.DisplayName;
                    snapshot.IsFinalStage = sourceStage.TargetDefinition.IsFinalStage;
                    snapshot.StageTimeLimitSec = Mathf.Max(0.01f, sourceStage.TargetDefinition.StageTimeLimitSec);
                }
                else
                {
                    snapshot.DisplayName = $"Stage {snapshot.StageId}";
                    snapshot.StageTimeLimitSec = 150f;
                }

                snapshot.Grid = layout.Grid;
                snapshot.Cells = StageMapDocumentExporter.CloneCells(layout.Cells);
                snapshot.VisualTileKeys = Array.Empty<string>();
                snapshot.SourceRegions = ToMapRegions(layout.SourceRegions);
                snapshot.DepositRegions = ToMapRegions(layout.DepositRegions);
                snapshot.PlayerStart = layout.PlayerStart;
                snapshot.HazardActorPlacements = CollectHazardPlacements(sourceStage, issues);
                snapshot.PresentationLinks = ToMapPresentationLinks(layout.Presentations);

                var root = sourceStage.GetComponentInParent<StageLayoutRootMarker>();
                snapshot.TargetLayout = sourceStage.TargetLayout != null ? sourceStage.TargetLayout : existingDocument != null ? existingDocument.TargetLayout : null;
                snapshot.TargetDefinition = sourceStage.TargetDefinition != null ? sourceStage.TargetDefinition : existingDocument != null ? existingDocument.TargetDefinition : null;
                snapshot.TargetCatalog = root != null && root.TargetStageCatalog != null ? root.TargetStageCatalog : existingDocument != null ? existingDocument.TargetCatalog : null;
                snapshot.PresentationCatalog = root != null && root.TargetPresentationCatalog != null
                    ? root.TargetPresentationCatalog
                    : existingDocument != null ? existingDocument.PresentationCatalog : null;
                snapshot.IncludeInCatalog = sourceStage.IncludeInCatalog;
                snapshot.EnabledInCatalog = sourceStage.EnabledInCatalog;
                snapshot.CatalogEntryKey = !string.IsNullOrWhiteSpace(sourceStage.EntryKey)
                    ? sourceStage.EntryKey.Trim()
                    : existingDocument != null ? existingDocument.CatalogEntryKey : string.Empty;
                snapshot.SetLastAppliedCatalogEntryKey(ResolveCatalogEntryIdentity(
                    snapshot.TargetCatalog,
                    snapshot.TargetDefinition,
                    snapshot.TargetLayout,
                    existingDocument != null ? existingDocument.LastAppliedCatalogEntryKey : string.Empty,
                    BuildHierarchyPath(sourceStage.transform),
                    issues));

                StageMapDocumentValidationRules.ValidateDocument(
                    snapshot,
                    BuildHierarchyPath(sourceStage.transform),
                    issues);
                if (HasErrors(issues))
                {
                    UnityEngine.Object.DestroyImmediate(snapshot);
                    snapshot = null;
                    return false;
                }
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(layout);
            }
        }

        public static string ComputeLegacyStageSignature(StageLayoutStageMarker sourceStage)
        {
            if (sourceStage == null)
                return string.Empty;

            var builder = new StringBuilder(4096);
            var sourceScene = sourceStage.gameObject.scene;
            builder.Append("Scene|")
                .Append(sourceScene.path ?? string.Empty).Append('|')
                .Append(sourceScene.name ?? string.Empty).Append('|')
                .Append(sourceScene.isDirty ? '1' : '0').AppendLine();
            AppendObjectSignature(builder, sourceStage);
            var root = sourceStage.GetComponentInParent<StageLayoutRootMarker>();
            AppendObjectSignature(builder, root);
            if (root != null)
            {
                AppendObjectSignature(builder, root.TargetStageCatalog);
                AppendObjectSignature(builder, root.TargetPresentationCatalog);
            }
            if (sourceStage.TryGetComponent(out StageGridAuthoring authoring))
            {
                AppendObjectSignature(builder, authoring);
                AppendTilemapCellsSignature(builder, "MovementTilemap", authoring.MovementTilemap, authoring);
                AppendTilemapCellsSignature(builder, "RegionTilemap", authoring.RegionTilemap, authoring);
            }

            AppendComponentArraySignature(builder, sourceStage.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true));
            AppendComponentArraySignature(builder, sourceStage.GetComponentsInChildren<StagePlayerStartMarker>(includeInactive: true));
            AppendComponentArraySignature(builder, sourceStage.GetComponentsInChildren<StageHazardActorMarker>(includeInactive: true));
            AppendComponentArraySignature(builder, sourceStage.GetComponentsInChildren<StagePresentationMarker>(includeInactive: true));
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static void CopyDocumentData(StageMapDocument source, StageMapDocument target)
        {
            target.SchemaVersion = source.SchemaVersion;
            target.StageId = source.StageId;
            target.DisplayName = source.DisplayName;
            target.IsFinalStage = source.IsFinalStage;
            target.StageTimeLimitSec = source.StageTimeLimitSec;
            target.Grid = source.Grid;
            target.Cells = StageMapDocumentExporter.CloneCells(source.Cells);
            target.VisualTileKeys = source.VisualTileKeys != null ? (string[])source.VisualTileKeys.Clone() : Array.Empty<string>();
            target.SourceRegions = source.SourceRegions != null ? (StageMapRegionData[])source.SourceRegions.Clone() : Array.Empty<StageMapRegionData>();
            target.DepositRegions = source.DepositRegions != null ? (StageMapRegionData[])source.DepositRegions.Clone() : Array.Empty<StageMapRegionData>();
            target.PlayerStart = source.PlayerStart;
            target.HazardActorPlacements = source.HazardActorPlacements != null ? (StageMapHazardActorPlacementData[])source.HazardActorPlacements.Clone() : Array.Empty<StageMapHazardActorPlacementData>();
            target.PresentationLinks = source.PresentationLinks != null ? (StageMapPresentationLinkData[])source.PresentationLinks.Clone() : Array.Empty<StageMapPresentationLinkData>();
            target.TargetLayout = source.TargetLayout;
            target.TargetDefinition = source.TargetDefinition;
            target.TargetCatalog = source.TargetCatalog;
            target.PresentationCatalog = source.PresentationCatalog;
            target.IncludeInCatalog = source.IncludeInCatalog;
            target.EnabledInCatalog = source.EnabledInCatalog;
            target.CatalogEntryKey = source.CatalogEntryKey;
            target.SetLastAppliedCatalogEntryKey(source.LastAppliedCatalogEntryKey);
        }

        private static void CollectImportChanges(StageMapDocument current, StageMapDocument imported, List<StageMapApplyPlanChange> changes)
        {
            if (current.StageId != imported.StageId)
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", nameof(StageMapDocument.StageId), "Update document StageId from legacy source."));
            if (current.SchemaVersion != imported.SchemaVersion)
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", nameof(StageMapDocument.SchemaVersion), "Update document schema version from legacy import snapshot."));
            if (!string.Equals(current.DisplayName, imported.DisplayName, StringComparison.Ordinal)
                || current.IsFinalStage != imported.IsFinalStage
                || !Mathf.Approximately(current.StageTimeLimitSec, imported.StageTimeLimitSec))
            {
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", "StageMetadata", "Update display name, final-stage flag, and time limit from legacy targets."));
            }
            if (!JsonEqual(current.Grid, imported.Grid)
                || !JsonEqual(current.Cells, imported.Cells)
                || !JsonEqual(current.VisualTileKeys, imported.VisualTileKeys))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", "Grid/Cells/VisualTileKeys", "Update grid, dense cells, and visual keys from legacy Tilemap source."));
            if (!JsonEqual(current.SourceRegions, imported.SourceRegions) || !JsonEqual(current.DepositRegions, imported.DepositRegions))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", "Regions", "Update source/deposit region tables from legacy anchors."));
            if (!JsonEqual(current.PlayerStart, imported.PlayerStart))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", nameof(StageMapDocument.PlayerStart), "Update PlayerStart from legacy marker."));
            if (!JsonEqual(current.HazardActorPlacements, imported.HazardActorPlacements))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", nameof(StageMapDocument.HazardActorPlacements), "Update HazardActor placements from legacy markers."));
            if (!JsonEqual(current.PresentationLinks, imported.PresentationLinks))
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", nameof(StageMapDocument.PresentationLinks), "Update presentation links from legacy markers."));
            if (current.TargetLayout != imported.TargetLayout
                || current.TargetDefinition != imported.TargetDefinition
                || current.TargetCatalog != imported.TargetCatalog
                || current.PresentationCatalog != imported.PresentationCatalog)
            {
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", "GeneratedTargets", "Update Layout, Definition, Catalog, and PresentationCatalog targets from legacy source."));
            }
            if (current.IncludeInCatalog != imported.IncludeInCatalog
                || current.EnabledInCatalog != imported.EnabledInCatalog
                || !string.Equals(current.CatalogEntryKey, imported.CatalogEntryKey, StringComparison.Ordinal)
                || !string.Equals(current.LastAppliedCatalogEntryKey, imported.LastAppliedCatalogEntryKey, StringComparison.Ordinal))
            {
                changes.Add(new StageMapApplyPlanChange(StageMapApplyChangeKind.Update, "StageMapDocument", "CatalogSettings", "Update include/enabled flags, catalog key, and applied entry identity from legacy source."));
            }
        }

        private static string ResolveCatalogEntryIdentity(
            StageCatalogSO catalog,
            StageDefinitionSO definition,
            StageLayoutSO layout,
            string fallback,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (catalog == null || definition == null || layout == null)
                return fallback ?? string.Empty;

            var entries = catalog.Entries ?? Array.Empty<StageCatalogEntry>();
            string match = string.Empty;
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Definition != definition || entries[i].Layout != layout)
                    continue;

                match = entries[i].EntryKey;
                count++;
            }

            if (count == 1 && !string.IsNullOrWhiteSpace(match))
                return match.Trim();

            if (count > 1)
            {
                issues?.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "SMI921",
                    location,
                    $"Legacy import catalog identity is ambiguous for the target Definition/Layout pair. count={count}"));
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(fallback))
                return string.Empty;

            int fallbackMatches = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (string.Equals(entries[i].EntryKey, fallback.Trim(), StringComparison.Ordinal))
                    fallbackMatches++;
            }
            if (fallbackMatches == 1)
                return fallback.Trim();

            issues?.Add(new ContentValidationIssue(
                ContentValidationSeverity.Error,
                "SMI922",
                location,
                $"Legacy import last-applied catalog identity does not resolve exactly once. key={fallback}, count={fallbackMatches}"));
            return string.Empty;
        }

        private static bool HasErrors(IReadOnlyList<ContentValidationIssue> issues)
        {
            if (issues == null)
                return false;

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    return true;
            }

            return false;
        }

        private static StageMapRegionData[] ToMapRegions(StageSourceRegionLayoutData[] regions)
        {
            if (regions == null || regions.Length == 0)
                return Array.Empty<StageMapRegionData>();

            var result = new StageMapRegionData[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                result[i] = new StageMapRegionData
                {
                    StableId = regions[i].StableId,
                    Active = regions[i].Active,
                    AnchorCell = regions[i].AnchorCell,
                    AnchorOffset = regions[i].AnchorOffset,
                };
            }

            return result;
        }

        private static StageMapRegionData[] ToMapRegions(StageDepositRegionLayoutData[] regions)
        {
            if (regions == null || regions.Length == 0)
                return Array.Empty<StageMapRegionData>();

            var result = new StageMapRegionData[regions.Length];
            for (int i = 0; i < regions.Length; i++)
            {
                result[i] = new StageMapRegionData
                {
                    StableId = regions[i].StableId,
                    Active = regions[i].Active,
                    AnchorCell = regions[i].AnchorCell,
                    AnchorOffset = regions[i].AnchorOffset,
                };
            }

            return result;
        }

        private static StageMapPresentationLinkData[] ToMapPresentationLinks(StagePresentationLayoutData[] presentations)
        {
            if (presentations == null || presentations.Length == 0)
                return Array.Empty<StageMapPresentationLinkData>();

            var result = new StageMapPresentationLinkData[presentations.Length];
            for (int i = 0; i < presentations.Length; i++)
            {
                result[i] = new StageMapPresentationLinkData
                {
                    StableId = presentations[i].StableId,
                    Active = presentations[i].Active,
                    PresentationKey = presentations[i].PresentationKey,
                    PlacementMode = presentations[i].PlacementMode,
                    LinkKind = presentations[i].LinkKind,
                    LinkedStableId = presentations[i].LinkedStableId,
                    Position = presentations[i].Position,
                    Euler = presentations[i].Euler,
                    Scale = presentations[i].Scale,
                };
            }

            return result;
        }

        private static StageMapHazardActorPlacementData[] CollectHazardPlacements(
            StageLayoutStageMarker sourceStage,
            List<ContentValidationIssue> issues)
        {
            var markers = sourceStage.GetComponentsInChildren<StageHazardActorMarker>(includeInactive: true);
            var result = new List<StageMapHazardActorPlacementData>(markers.Length);
            for (int i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                if (marker == null)
                    continue;

                var source = marker.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>();
                if (source == null || source.StableIdOverride == 0u)
                {
                    issues?.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "SMI920",
                        BuildHierarchyPath(marker.transform),
                        "HazardActor marker must be parented under a source authoring object with a non-zero stable id."));
                    continue;
                }

                StageHazardActorPlacementEditorUtility.TryGetLocalPose(
                    marker,
                    out _,
                    out Vector3 localOffset,
                    out float localYawDeg);
                result.Add(new StageMapHazardActorPlacementData
                {
                    OwningSourceStableId = source.StableIdOverride,
                    PlacementInstanceId = marker.PlacementInstanceId,
                    ActorArchetypePrefab = marker.ActorArchetypePrefab,
                    SourceLocalOffset = localOffset,
                    LocalYawDeg = localYawDeg,
                });
            }

            result.Sort((a, b) => a.PlacementInstanceId.CompareTo(b.PlacementInstanceId));
            return result.ToArray();
        }

        private static void AppendComponentArraySignature<T>(StringBuilder builder, T[] components)
            where T : Component
        {
            if (components == null)
                return;

            Array.Sort(components, (a, b) => string.CompareOrdinal(BuildHierarchyPath(a != null ? a.transform : null), BuildHierarchyPath(b != null ? b.transform : null)));
            for (int i = 0; i < components.Length; i++)
            {
                AppendObjectSignature(builder, components[i]);
                AppendTransformSignature(builder, components[i] != null ? components[i].transform : null);
            }
        }

        private static void AppendTilemapCellsSignature(StringBuilder builder, string label, Tilemap tilemap, StageGridAuthoring authoring)
        {
            builder.Append(label).Append('|');
            if (tilemap == null || authoring == null)
            {
                builder.Append("(null)").Append('\n');
                return;
            }

            int width = Mathf.Max(0, authoring.BoundsSize.x);
            int height = Mathf.Max(0, authoring.BoundsSize.y);
            builder.Append("bounds=")
                .Append(authoring.BoundsMinCell.x)
                .Append(',')
                .Append(authoring.BoundsMinCell.y)
                .Append(',')
                .Append(width)
                .Append(',')
                .Append(height)
                .Append('\n');

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3Int cell = authoring.GetTilemapCell(x, y);
                    var tile = tilemap.GetTile(cell);
                    builder.Append(label)
                        .Append('[')
                        .Append(cell.x)
                        .Append(',')
                        .Append(cell.y)
                        .Append(',')
                        .Append(cell.z)
                        .Append("]=");
                    AppendTileSignature(builder, tile);
                    builder.Append('\n');
                }
            }
        }

        private static void AppendTileSignature(StringBuilder builder, TileBase tile)
        {
            if (tile == null)
            {
                builder.Append("(empty)");
                return;
            }

            builder.Append(tile.GetType().FullName)
                .Append('|')
                .Append(tile.name)
                .Append('|')
                .Append(AssetDatabase.GetAssetPath(tile))
                .Append('|')
                .Append(EditorJsonUtility.ToJson(tile));
        }

        private static void AppendObjectSignature(StringBuilder builder, UnityEngine.Object target)
        {
            if (target == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(target);
            builder.Append(target.GetType().FullName)
                .Append('|')
                .Append(target.name)
                .Append('|')
                .Append(assetPath)
                .Append('|')
                .Append(string.IsNullOrEmpty(assetPath) ? string.Empty : AssetDatabase.AssetPathToGUID(assetPath))
                .Append('|')
                .Append(EditorJsonUtility.ToJson(target))
                .Append('\n');
        }

        private static void AppendTransformSignature(StringBuilder builder, Transform transform)
        {
            if (transform == null)
                return;

            builder.Append("Transform|")
                .Append(BuildHierarchyPath(transform)).Append('|');
            AppendFloat(builder, transform.localPosition.x).Append(',');
            AppendFloat(builder, transform.localPosition.y).Append(',');
            AppendFloat(builder, transform.localPosition.z).Append('|');
            AppendFloat(builder, transform.localRotation.x).Append(',');
            AppendFloat(builder, transform.localRotation.y).Append(',');
            AppendFloat(builder, transform.localRotation.z).Append(',');
            AppendFloat(builder, transform.localRotation.w).Append('|');
            AppendFloat(builder, transform.localScale.x).Append(',');
            AppendFloat(builder, transform.localScale.y).Append(',');
            AppendFloat(builder, transform.localScale.z).Append('\n');
        }

        private static StringBuilder AppendFloat(StringBuilder builder, float value)
        {
            return builder.Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static bool JsonEqual<T>(T left, T right)
        {
            return string.Equals(JsonUtility.ToJson(new JsonBox<T>(left)), JsonUtility.ToJson(new JsonBox<T>(right)), StringComparison.Ordinal);
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

        [Serializable]
        private struct JsonBox<T>
        {
            public T Value;

            public JsonBox(T value)
            {
                Value = value;
            }
        }
    }
}
