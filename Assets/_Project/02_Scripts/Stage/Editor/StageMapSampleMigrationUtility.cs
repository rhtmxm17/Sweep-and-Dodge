using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class StageMapSampleMigrationUtility
    {
        public const string SampleScenePath = "Assets/_Project/01_Scenes/StageLayoutEditingSampleV1.unity";
        public const string DocumentFolderPath = "Assets/_Project/03_Datas/StageMapDocuments";
        public const string DocumentAssetPath = DocumentFolderPath + "/smd_demo_1.asset";

        [MenuItem("Tools/Project/Stage Map Editor/Migrate Sample Stage 1 Document")]
        public static void MigrateSampleStage1Menu()
        {
            if (TryMigrateSampleStage1(out string report))
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static bool TryMigrateSampleStage1(out string report)
        {
            var lines = new StringBuilder(1024);
            if (!TryOpenSampleScene(out string sceneError))
            {
                report = sceneError;
                return false;
            }

            StageLayoutStageMarker sourceStage = FindStageOneMarker();
            if (sourceStage == null)
            {
                report = "StageMap sample migration failed: StageId 1 marker was not found in the sample scene.";
                return false;
            }

            EnsureDocumentFolder();
            var document = AssetDatabase.LoadAssetAtPath<StageMapDocument>(DocumentAssetPath);
            bool created = document == null;
            if (created)
            {
                document = ScriptableObject.CreateInstance<StageMapDocument>();
                AssetDatabase.CreateAsset(document, DocumentAssetPath);
                AssetDatabase.SaveAssets();
            }

            if (!StageMapLegacyImportUtility.TryBuildImportPlan(sourceStage, document, out var importPlan))
            {
                report = BuildFailure("Legacy import preview failed.", importPlan?.ValidationIssues);
                CleanupCreatedDocument(created);
                return false;
            }

            if (!StageMapLegacyImportUtility.TryApplyImportPlan(importPlan, saveAssets: true, out string importError))
            {
                report = "StageMap sample migration failed while applying import: " + importError;
                CleanupCreatedDocument(created);
                return false;
            }

            var validationIssues = new List<ContentValidationIssue>(32);
            StageMapDocumentValidationRules.ValidateDocument(document, DocumentAssetPath, validationIssues);
            if (HasErrors(validationIssues))
            {
                report = BuildFailure("Imported document validation failed.", validationIssues);
                return false;
            }

            if (!TryValidateEquivalence(sourceStage, document, out string equivalenceReport))
            {
                report = equivalenceReport;
                return false;
            }

            var applyPlan = StageMapApplyPlanner.BuildPlan(document);
            if (applyPlan.HasErrors)
            {
                report = BuildFailure("Document dry-run validation failed.", applyPlan.ValidationIssues);
                return false;
            }

            if (applyPlan.Changes.Count > 0)
            {
                lines.AppendLine("StageMap sample migration stopped because generated runtime assets have unexplained differences:");
                for (int i = 0; i < applyPlan.Changes.Count; i++)
                {
                    var change = applyPlan.Changes[i];
                    lines.AppendLine($"- {change.Kind}: {change.Target}.{change.Field} - {change.Description}");
                }
                report = lines.ToString();
                return false;
            }

            if (!StageMapApplyPlanner.TryApplyPlan(applyPlan, saveAssets: true, confirmed: true, out string applyError))
            {
                report = "StageMap sample migration failed while applying generated assets: " + applyError;
                return false;
            }

            EditorUtility.SetDirty(document);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report =
                $"StageMap sample migration completed. document={DocumentAssetPath}, " +
                $"importChanges={importPlan.Changes.Count}, generatedDiffs={applyPlan.Changes.Count}. {equivalenceReport}";
            return true;
        }

        public static bool TryValidateEquivalence(
            StageLayoutStageMarker sourceStage,
            StageMapDocument document,
            out string report)
        {
            report = string.Empty;
            if (sourceStage == null || document == null)
            {
                report = "StageMap equivalence validation requires source stage and document.";
                return false;
            }

            if (!StageLayoutCatalogGenerator.TryBuildStageLayoutSnapshot(sourceStage, out var legacyLayout, out var issues))
            {
                report = BuildFailure("Legacy layout snapshot failed.", issues);
                return false;
            }

            var documentLayout = StageMapDocumentExporter.BuildLayoutSnapshot(document);
            var documentDefinition = StageMapDocumentExporter.BuildDefinitionSnapshot(document);
            try
            {
                if (!JsonEqualLayout(legacyLayout, documentLayout))
                {
                    report = "StageMap equivalence failed: legacy layout snapshot and document layout export differ.";
                    return false;
                }

                if (document.TargetDefinition == null || !JsonEqualDefinition(document.TargetDefinition, documentDefinition))
                {
                    report = "StageMap equivalence failed: target StageDefinitionSO and document definition export differ.";
                    return false;
                }

                if (!TryFindSingleCatalogPair(document, out StageCatalogEntry existingEntry, out string catalogError))
                {
                    report = catalogError;
                    return false;
                }

                StageCatalogEntry candidateEntry = StageMapDocumentExporter.BuildCatalogEntry(document);
                if (!JsonEqual(existingEntry, candidateEntry))
                {
                    report = "StageMap equivalence failed: existing StageCatalogSO entry and document candidate entry differ.";
                    return false;
                }

                report = "Layout/Definition/Catalog/HazardActor/Presentation equivalence passed.";
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacyLayout);
                UnityEngine.Object.DestroyImmediate(documentLayout);
                UnityEngine.Object.DestroyImmediate(documentDefinition);
            }
        }

        private static bool TryOpenSampleScene(out string error)
        {
            error = null;
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isDirty)
            {
                error = $"StageMap sample migration did not open the sample because the active scene has unsaved changes. scene={activeScene.path}";
                return false;
            }

            if (activeScene.path == SampleScenePath)
                return true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SampleScenePath) == null)
            {
                error = $"StageMap sample scene was not found. path={SampleScenePath}";
                return false;
            }

            EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single);
            return true;
        }

        private static StageLayoutStageMarker FindStageOneMarker()
        {
            var markers = UnityEngine.Object.FindObjectsByType<StageLayoutStageMarker>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            StageLayoutStageMarker match = null;
            for (int i = 0; i < markers.Length; i++)
            {
                if (markers[i].StageId != 1)
                    continue;
                if (match != null)
                    return null;
                match = markers[i];
            }
            return match;
        }

        private static void EnsureDocumentFolder()
        {
            const string dataRoot = "Assets/_Project/03_Datas";
            if (!AssetDatabase.IsValidFolder(DocumentFolderPath))
                AssetDatabase.CreateFolder(dataRoot, "StageMapDocuments");
        }

        private static void CleanupCreatedDocument(bool created)
        {
            if (created)
                AssetDatabase.DeleteAsset(DocumentAssetPath);
        }

        private static bool TryFindSingleCatalogPair(
            StageMapDocument document,
            out StageCatalogEntry entry,
            out string error)
        {
            entry = default;
            error = null;
            if (document.TargetCatalog == null)
            {
                error = "StageMap equivalence failed: TargetCatalog is null.";
                return false;
            }

            var entries = document.TargetCatalog.Entries ?? Array.Empty<StageCatalogEntry>();
            int count = 0;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Definition != document.TargetDefinition || entries[i].Layout != document.TargetLayout)
                    continue;
                entry = entries[i];
                count++;
            }

            if (count == 1)
                return true;
            error = $"StageMap equivalence failed: catalog Definition/Layout pair match count must be one. count={count}";
            return false;
        }

        private static bool JsonEqualLayout(StageLayoutSO left, StageLayoutSO right)
        {
            return left.SchemaVersion == right.SchemaVersion
                && left.StageId == right.StageId
                && JsonEqual(left.Grid, right.Grid)
                && JsonEqual(left.Cells, right.Cells)
                && JsonEqual(left.SourceRegions, right.SourceRegions)
                && JsonEqual(left.DepositRegions, right.DepositRegions)
                && JsonEqual(left.PlayerStart, right.PlayerStart)
                && JsonEqual(left.Presentations, right.Presentations);
        }

        private static bool JsonEqualDefinition(StageDefinitionSO left, StageDefinitionSO right)
        {
            return left.StageId == right.StageId
                && string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal)
                && left.IsFinalStage == right.IsFinalStage
                && Mathf.Approximately(left.StageTimeLimitSec, right.StageTimeLimitSec)
                && JsonEqual(left.SourceBindings, right.SourceBindings);
        }

        private static bool JsonEqual<T>(T left, T right)
        {
            return string.Equals(
                JsonUtility.ToJson(new JsonBox<T>(left)),
                JsonUtility.ToJson(new JsonBox<T>(right)),
                StringComparison.Ordinal);
        }

        private static bool HasErrors(IReadOnlyList<ContentValidationIssue> issues)
        {
            return issues != null && issues.Any(x => x.Severity == ContentValidationSeverity.Error);
        }

        private static string BuildFailure(string heading, IReadOnlyList<ContentValidationIssue> issues)
        {
            var builder = new StringBuilder(heading);
            if (issues == null)
                return builder.ToString();
            for (int i = 0; i < issues.Count; i++)
                builder.AppendLine().Append(issues[i].Code).Append(": ").Append(issues[i].Message);
            return builder.ToString();
        }

        [Serializable]
        private struct JsonBox<T>
        {
            public T Value;
            public JsonBox(T value) { Value = value; }
        }
    }
}
