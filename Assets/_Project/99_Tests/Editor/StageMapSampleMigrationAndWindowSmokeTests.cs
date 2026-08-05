using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapSampleMigrationAndWindowSmokeTests
    {
        [Test]
        public void ActualDocument_ValidationAndRuntimeTargetsAreConsistentWithoutMutation()
        {
            var document = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(document, Is.Not.Null, "The committed sample migration document is missing.");
            Assert.That(document.TargetLayout, Is.Not.Null);
            Assert.That(document.TargetDefinition, Is.Not.Null);
            Assert.That(document.TargetCatalog, Is.Not.Null);
            Assert.That(document.PresentationCatalog, Is.Not.Null);
            string[] before = CaptureTargetSignatures(document);

            var issues = new List<ContentValidationIssue>(32);
            StageMapDocumentValidationRules.ValidateDocument(document, StageMapSampleMigrationUtility.DocumentAssetPath, issues);
            Assert.That(issues.Any(x => x.Severity == ContentValidationSeverity.Error), Is.False, string.Join("\n", issues.Select(x => x.Message)));

            StageMapApplyPlan plan = StageMapApplyPlanner.BuildPlan(document);
            Assert.That(plan.HasErrors, Is.False, string.Join("\n", plan.ValidationIssues.Select(x => x.Message)));
            Assert.That(
                plan.Changes,
                Is.Empty,
                "The current authoring document has unapplied runtime differences: "
                + string.Join("\n", plan.Changes.Select(x => $"{x.Kind}: {x.Target}.{x.Field} - {x.Description}")));
            Assert.That(CaptureTargetSignatures(document), Is.EqualTo(before), "Dry-run and validation must not mutate actual content assets.");
        }

        [Test]
        public void LegacyImporter_SavedSampleRoundTripUsesTemporaryTargetsAndPreservesActualAssets()
        {
            var actualDocument = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(actualDocument, Is.Not.Null);
            string[] actualBefore = CaptureTargetSignatures(actualDocument);
            Scene sampleScene = SceneManager.GetSceneByPath(StageMapSampleMigrationUtility.SampleScenePath);
            bool openedByTest = !sampleScene.IsValid() || !sampleScene.isLoaded;
            if (openedByTest)
                sampleScene = EditorSceneManager.OpenScene(StageMapSampleMigrationUtility.SampleScenePath, OpenSceneMode.Additive);

            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            try
            {
                Assert.That(sampleScene.isDirty, Is.False, "Automated legacy round-trip requires the saved sample scene state.");
                StageLayoutStageMarker sourceStage = FindStage(sampleScene, actualDocument.StageId);
                Assert.That(sourceStage, Is.Not.Null);

                Assert.That(StageMapLegacyImportUtility.TryBuildImportPlan(sourceStage, document, out var importPlan), Is.True,
                    string.Join("\n", importPlan.ValidationIssues.Select(x => x.Message)));
                Assert.That(importPlan.Changes, Is.Not.Empty);
                Assert.That(StageMapLegacyImportUtility.TryApplyImportPlan(importPlan, saveAssets: false, out string importError), Is.True, importError);

                Assert.That(document.TargetCatalog, Is.Not.Null);
                EditorUtility.CopySerialized(document.TargetCatalog, catalog);
                document.TargetLayout = layout;
                document.TargetDefinition = definition;
                document.TargetCatalog = catalog;
                StageMapApplyPlan applyPlan = StageMapApplyPlanner.BuildPlan(document);
                Assert.That(applyPlan.HasErrors, Is.False, string.Join("\n", applyPlan.ValidationIssues.Select(x => x.Message)));
                Assert.That(StageMapApplyPlanner.TryApplyPlan(
                    applyPlan,
                    saveAssets: false,
                    confirmed: true,
                    out string applyError), Is.True, applyError);
                Assert.That(StageMapSampleMigrationUtility.TryValidateEquivalence(sourceStage, document, out string report), Is.True, report);
                Assert.That(StageMapApplyPlanner.BuildPlan(document).Changes, Is.Empty);
                Assert.That(CaptureTargetSignatures(actualDocument), Is.EqualTo(actualBefore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(document);
                UnityEngine.Object.DestroyImmediate(layout);
                UnityEngine.Object.DestroyImmediate(definition);
                UnityEngine.Object.DestroyImmediate(catalog);
                if (openedByTest && sampleScene.IsValid() && sampleScene.isLoaded)
                    EditorSceneManager.CloseScene(sampleScene, removeScene: true);
            }
        }

        [Test]
        public void AutomatedMigrationAndImport_RejectDirtyActiveSampleSceneWithoutMutatingActualAssets()
        {
            var actualDocument = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(actualDocument, Is.Not.Null);
            string[] actualBefore = CaptureTargetSignatures(actualDocument);
            Scene previousActive = SceneManager.GetActiveScene();
            Scene sampleScene = SceneManager.GetSceneByPath(StageMapSampleMigrationUtility.SampleScenePath);
            bool openedByTest = !sampleScene.IsValid() || !sampleScene.isLoaded;
            if (openedByTest)
                sampleScene = EditorSceneManager.OpenScene(StageMapSampleMigrationUtility.SampleScenePath, OpenSceneMode.Additive);

            var temporaryDocument = ScriptableObject.CreateInstance<StageMapDocument>();
            try
            {
                Assert.That(SceneManager.SetActiveScene(sampleScene), Is.True);
                EditorSceneManager.MarkSceneDirty(sampleScene);
                Assert.That(sampleScene.isDirty, Is.True);

                Assert.That(StageMapSampleMigrationUtility.TryMigrateSampleStage1(out string report), Is.False);
                StringAssert.Contains("unsaved changes", report);

                StageLayoutStageMarker sourceStage = FindStage(sampleScene, actualDocument.StageId);
                Assert.That(StageMapLegacyImportUtility.TryBuildImportPlan(sourceStage, temporaryDocument, out var importPlan), Is.False);
                Assert.That(importPlan.ValidationIssues.Any(x => x.Code == "SMI923"), Is.True);
                Assert.That(CaptureTargetSignatures(actualDocument), Is.EqualTo(actualBefore));
            }
            finally
            {
                if (previousActive.IsValid() && previousActive.isLoaded && previousActive != sampleScene)
                    SceneManager.SetActiveScene(previousActive);
                UnityEngine.Object.DestroyImmediate(temporaryDocument);
                if (openedByTest && sampleScene.IsValid() && sampleScene.isLoaded)
                    EditorSceneManager.CloseScene(sampleScene, removeScene: true);
            }
        }

        [Test]
        public void Window_OpenLoadSwitchAndUndoRedo_InvalidatesOverlayWithoutLosingDocument()
        {
            var first = CreateDenseDocument();
            var second = CreateDenseDocument();
            var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Smoke", focus: false);
            try
            {
                LoadDocument(window, first);
                StageMapOverlayCache overlay = GetOverlay(window);
                overlay.EnsureBuilt(first);
                int initialBuildCount = overlay.Stats.BuildCount;
                Assert.That(GetActiveDocument(window), Is.SameAs(first));

                LoadDocument(window, second);
                Assert.That(GetActiveDocument(window), Is.SameAs(second));

                LoadDocument(window, first);
                Undo.RecordObject(first, "Stage Map Window Smoke Mutation");
                var cells = (StageCellLayoutData[])first.Cells.Clone();
                cells[0].MovementFlags = StageCellMovementFlags.BlockBullet;
                first.Cells = cells;
                EditorUtility.SetDirty(first);
                Undo.PerformUndo();
                overlay = GetOverlay(window);
                overlay.EnsureBuilt(first);

                Assert.That(GetActiveDocument(window), Is.SameAs(first));
                Assert.That(overlay.Stats.BuildCount, Is.GreaterThan(initialBuildCount));
                Assert.That(first.Cells[0].MovementFlags, Is.EqualTo(StageCellMovementFlags.None));
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void ActualWindow_LoadsDocumentAndRoutesNavigatorSceneIssueAndContextualInspectorWithoutMutation()
        {
            var document = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(document, Is.Not.Null);
            string[] before = CaptureTargetSignatures(document);
            var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Actual UX Smoke", focus: false);
            try
            {
                window.LoadDocument(document);
                window.RefreshProjectDocuments();
                Assert.That(window.ActiveDocument, Is.SameAs(document));
                Assert.That(window.ProjectDocuments, Does.Contain(document));
                Assert.That(document.TargetLayout, Is.Not.Null);
                Assert.That(document.TargetDefinition, Is.Not.Null);
                Assert.That(document.TargetCatalog, Is.Not.Null);
                Assert.That(document.PresentationCatalog, Is.Not.Null);

                AssertSelectionSection(window, StageMapSelection.ForCell(Vector2Int.zero), StageMapInspectorSection.Cell);
                StageMapRegionData source = document.SourceRegions.First(x => x.StableId > 0u);
                StageMapRegionData deposit = document.DepositRegions.First(x => x.StableId > 0u);
                AssertSelectionSection(window, StageMapSelection.ForRegion(StageRegionKind.Source, source.StableId), StageMapInspectorSection.RegionOrAnchor);
                AssertSelectionSection(window, StageMapSelection.ForAnchor(StageRegionKind.Source, source.StableId), StageMapInspectorSection.RegionOrAnchor);
                AssertSelectionSection(window, StageMapSelection.ForRegion(StageRegionKind.Deposit, deposit.StableId), StageMapInspectorSection.RegionOrAnchor);
                AssertSelectionSection(window, StageMapSelection.ForAnchor(StageRegionKind.Deposit, deposit.StableId), StageMapInspectorSection.RegionOrAnchor);
                AssertSelectionSection(window, StageMapSelection.ForPlayerStart(), StageMapInspectorSection.PlayerStart);

                StageMapHazardActorPlacementData hazard = document.HazardActorPlacements.First();
                AssertSelectionSection(
                    window,
                    StageMapSelection.ForHazard(hazard.OwningSourceStableId, hazard.PlacementInstanceId),
                    StageMapInspectorSection.HazardActor);
                StageMapPresentationLinkData presentation = document.PresentationLinks.First();
                AssertSelectionSection(window, StageMapSelection.ForPresentation(presentation.StableId), StageMapInspectorSection.Presentation);

                Vector3 playerWorld = StageMapSelectionUtility.GetPlayerStartWorld(document);
                Assert.That(StageMapSelectionUtility.TryHitTest(
                    document,
                    window.Session,
                    playerWorld,
                    Mathf.Max(0.1f, document.Grid.CellSize * 0.2f),
                    out StageMapSelection sceneSelection), Is.True);
                Assert.That(window.TrySelect(sceneSelection, frame: false), Is.True);
                Assert.That(window.CurrentInspectorSection, Is.EqualTo(window.Session.GetInspectorSection()));

                var issue = new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "STG009",
                    "document/SourceRegions[0]",
                    "test issue");
                StageMapIssueTarget issueTarget = StageMapDocumentIssueMapper.ResolveTarget(document, issue);
                Assert.That(window.TryNavigateToIssueTarget(issueTarget), Is.True);
                Assert.That(window.Session.Selection, Is.EqualTo(StageMapSelection.ForAnchor(StageRegionKind.Source, source.StableId)));
                Assert.That(window.CurrentInspectorSection, Is.EqualTo(StageMapInspectorSection.RegionOrAnchor));

                window.Session.ShowAnchorLayer = false;
                Assert.That(window.CanDrawSelectionHandle(), Is.False);
                Assert.That(window.Session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.SourceAnchor));

                window.BuildDryRun();
                Assert.That(window.CurrentApplyPlan.HasErrors, Is.False);
                Assert.That(window.CurrentApplyPlan.Changes, Is.Empty);
                Assert.That(CaptureTargetSignatures(document), Is.EqualTo(before));
            }
            finally
            {
                window.Close();
            }
        }

        [Test]
        public void WindowCommands_ReconcileResizeDeleteUndoCenterLocksVisibilityAndStalePlansOnDocumentClone()
        {
            var sourceDocument = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(sourceDocument, Is.Not.Null);
            string[] actualBefore = CaptureTargetSignatures(sourceDocument);
            var document = UnityEngine.Object.Instantiate(sourceDocument);
            var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Command UX Smoke", focus: false);
            try
            {
                window.LoadDocument(document);
                window.BuildDryRun();
                StageMapApplyPlan stalePlan = window.CurrentApplyPlan;
                string previousName = document.DisplayName;
                document.DisplayName = previousName + " stale";
                Assert.That(StageMapApplyPlanner.TryApplyPlan(
                    stalePlan,
                    saveAssets: false,
                    confirmed: true,
                    out string staleError), Is.False);
                StringAssert.Contains("changed", staleError);
                document.DisplayName = previousName;

                StageMapRegionData source = document.SourceRegions.First(x => x.Active);
                int sourceIndex = Array.FindIndex(document.SourceRegions, x => x.StableId == source.StableId);
                document.SourceRegions[sourceIndex].AnchorOffset = new Vector2(0.2f, -0.1f);
                Assert.That(window.TrySelect(StageMapSelection.ForAnchor(StageRegionKind.Source, source.StableId), frame: false), Is.True);
                Assert.That(window.TrySetCenterRegionAnchors(true), Is.True);
                Assert.That(document.SourceRegions[sourceIndex].AnchorOffset, Is.EqualTo(Vector2.zero));
                Undo.PerformUndo();
                Assert.That(window.Session.CenterRegionAnchors, Is.False);
                Assert.That(document.SourceRegions[sourceIndex].AnchorOffset, Is.EqualTo(new Vector2(0.2f, -0.1f)));
                Undo.PerformRedo();
                Assert.That(window.Session.CenterRegionAnchors, Is.True);
                Assert.That(document.SourceRegions[sourceIndex].AnchorOffset, Is.EqualTo(Vector2.zero));

                window.Session.LockSourceLayer = true;
                window.Session.LockAnchorLayer = false;
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(
                    window.Session,
                    StageMapSelectionUtility.GetRegionAnchorWorld(document, StageRegionKind.Source, source.StableId) + Vector3.right,
                    0f,
                    Vector3.zero,
                    out _), Is.False);
                window.Session.LockSourceLayer = false;
                window.Session.LockAnchorLayer = true;
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(
                    window.Session,
                    StageMapSelectionUtility.GetRegionAnchorWorld(document, StageRegionKind.Source, source.StableId) + Vector3.right,
                    0f,
                    Vector3.zero,
                    out _), Is.False);
                window.Session.LockAnchorLayer = false;
                window.Session.ShowAnchorLayer = false;
                Assert.That(window.CanDrawSelectionHandle(), Is.False);
                Assert.That(window.Session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.SourceAnchor));
                window.Session.ShowAnchorLayer = true;

                var selectedCell = new Vector2Int(document.Grid.Width - 1, 0);
                Assert.That(StageMapDocumentCommandUtility.TryGetCellIndex(document, selectedCell, out int selectedCellIndex), Is.True);
                document.Cells[selectedCellIndex].MovementFlags = StageCellMovementFlags.BlockBullet;
                Assert.That(window.TrySelect(StageMapSelection.ForCell(selectedCell), frame: false), Is.True);
                StageGridSpec expanded = document.Grid;
                expanded.Width += 1;
                var expandPlan = StageMapGridResizeUtility.BuildPreview(document, expanded);
                Assert.That(StageMapGridResizeUtility.TryApply(expandPlan, true, out string expandError), Is.True, expandError);
                window.ReconcileAfterExternalMutation();
                Assert.That(window.Session.Selection.Cell, Is.EqualTo(selectedCell));

                StageGridSpec cropped = document.Grid;
                cropped.Width = selectedCell.x;
                var cropPlan = StageMapGridResizeUtility.BuildPreview(document, cropped);
                Assert.That(cropPlan.RequiresConfirmation, Is.True);
                Assert.That(StageMapGridResizeUtility.TryApply(cropPlan, false, out _), Is.False);
                Assert.That(StageMapGridResizeUtility.TryApply(cropPlan, true, out string cropError), Is.True, cropError);
                window.ReconcileAfterExternalMutation();
                Assert.That(window.Session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));

                StageMapHazardActorPlacementData hazard = document.HazardActorPlacements.First();
                Assert.That(window.TrySelect(
                    StageMapSelection.ForHazard(hazard.OwningSourceStableId, hazard.PlacementInstanceId),
                    frame: false), Is.True);
                int hazardCount = document.HazardActorPlacements.Length;
                Undo.RecordObject(document, "Delete Selected Hazard For Window Test");
                Assert.That(StageMapEditorMutationUtility.TryDeleteSelection(window.Session, out _), Is.True);
                Assert.That(window.Session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
                Undo.PerformUndo();
                Assert.That(document.HazardActorPlacements, Has.Length.EqualTo(hazardCount));
                Assert.That(window.Session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
                Assert.That(CaptureTargetSignatures(sourceDocument), Is.EqualTo(actualBefore));
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(document);
            }
        }

        [Test]
        public void WindowSceneTool_MigratedDocumentClone_SupportsV1AuthoringWorkflow()
        {
            var source = AssetDatabase.LoadAssetAtPath<StageMapDocument>(StageMapSampleMigrationUtility.DocumentAssetPath);
            Assert.That(source, Is.Not.Null);
            var document = UnityEngine.Object.Instantiate(source);
            var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Tool Smoke", focus: false);
            try
            {
                LoadDocument(window, document);
                StageMapEditingSession session = GetSession(window);
                StageMapRegionData sourceRegion = document.SourceRegions.First(x => x.Active);
                StageMapRegionData depositRegion = document.DepositRegions.First(x => x.Active);
                StageMapHazardActorPlacementData hazardTemplate = document.HazardActorPlacements.First();
                StageMapPresentationLinkData presentationTemplate = document.PresentationLinks.First(x => x.Active);
                int cellIndex = Array.FindIndex(document.Cells, x => x.MovementFlags == StageCellMovementFlags.None);
                Assert.That(cellIndex, Is.GreaterThanOrEqualTo(0));
                var cell = new Vector2Int(cellIndex % document.Grid.Width, cellIndex / document.Grid.Width);
                Vector3 world = StageMapDocumentCommandUtility.GetCellCenterWorld(document, cell);

                session.SelectedTool = StageMapEditorToolMode.PaintMovement;
                session.MovementBrush = StageCellMovementFlags.BlockBullet;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);
                session.MovementBrush = StageCellMovementFlags.None;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);

                session.SelectedTool = StageMapEditorToolMode.PaintRegion;
                session.RegionBrushKind = StageRegionKind.Source;
                session.RegionBrushStableId = sourceRegion.StableId;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);
                session.RegionBrushStableId = 0u;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);

                session.SelectedTool = StageMapEditorToolMode.PlaceAnchor;
                session.AnchorBrushKind = StageRegionKind.Source;
                session.AnchorBrushStableId = sourceRegion.StableId;
                Assert.That(ExecuteSceneTool(window, cell, world + new Vector3(0.2f, 0f, 0.1f)), Is.True);
                session.Select(StageMapSelection.ForAnchor(StageRegionKind.Source, sourceRegion.StableId));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, world, 0f, Vector3.zero, out _), Is.True);

                session.SelectedTool = StageMapEditorToolMode.PlacePlayerStart;
                session.PlayerStartYawDeg = 35f;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);
                session.Select(StageMapSelection.ForPlayerStart());
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, world + new Vector3(0.1f, 0f, 0.1f), 70f, Vector3.zero, out _), Is.True);

                int hazardCount = document.HazardActorPlacements.Length;
                session.SelectedTool = StageMapEditorToolMode.PlaceHazardActor;
                session.HazardActorSourceStableId = sourceRegion.StableId;
                session.HazardActorArchetypePrefab = hazardTemplate.ActorArchetypePrefab;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);
                Assert.That(document.HazardActorPlacements, Has.Length.EqualTo(hazardCount + 1));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, world + Vector3.forward, 45f, Vector3.zero, out _), Is.True);
                Assert.That(StageMapEditorMutationUtility.TryDeleteSelection(session, out _), Is.True);

                int presentationCount = document.PresentationLinks.Length;
                session.SelectedTool = StageMapEditorToolMode.PlacePresentationLink;
                session.PresentationStableId = StageMapDocumentCommandUtility.GetNextPresentationStableId(document);
                session.PresentationKey = presentationTemplate.PresentationKey;
                session.PresentationPlacementMode = StagePresentationPlacementMode.Standalone;
                session.PresentationLinkKind = StagePresentationLinkKind.None;
                Assert.That(ExecuteSceneTool(window, cell, world), Is.True);
                Assert.That(document.PresentationLinks, Has.Length.EqualTo(presentationCount + 1));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, world + Vector3.right, 0f, new Vector3(0f, 25f, 0f), out _), Is.True);
                Assert.That(StageMapEditorMutationUtility.TryDeleteSelection(session, out _), Is.True);

                Assert.That(StageMapDocumentCommandUtility.TryPaintRegion(document, cell, StageRegionKind.Source, sourceRegion.StableId, out _), Is.True);
                Assert.That(StageMapDocumentCommandUtility.TryPaintRegion(document, cell, StageRegionKind.Deposit, depositRegion.StableId, out _), Is.True);
                StageMapOverlayCache overlay = GetOverlay(window);
                overlay.Invalidate();
                overlay.EnsureBuilt(document);
                Assert.That(overlay.Stats.OverlapCellCount, Is.GreaterThan(0));

                session.LockSourceLayer = true;
                Assert.That(StageMapEditorMutationUtility.TryPaintRegion(session, cell, StageRegionKind.Source, 0u, out _), Is.False);
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(document);
            }
        }

        private static StageLayoutStageMarker FindStage(Scene scene, int stageId)
        {
            return scene
                .GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<StageLayoutStageMarker>(includeInactive: true))
                .SingleOrDefault(x => x.StageId == stageId);
        }

        private static void AssertSelectionSection(
            StageMapEditorWindow window,
            StageMapSelection selection,
            StageMapInspectorSection expectedSection)
        {
            Assert.That(window.TrySelect(selection, frame: false), Is.True, StageMapSelectionUtility.GetSelectionSummary(window.ActiveDocument, selection));
            Assert.That(window.Session.Selection, Is.EqualTo(selection));
            Assert.That(window.CurrentInspectorSection, Is.EqualTo(expectedSection));
        }

        private static string[] CaptureTargetSignatures(StageMapDocument document)
        {
            return new[]
            {
                StageMapApplyPlanner.ComputeSignature(document),
                StageMapApplyPlanner.ComputeSignature(document != null ? document.TargetLayout : null),
                StageMapApplyPlanner.ComputeSignature(document != null ? document.TargetDefinition : null),
                StageMapApplyPlanner.ComputeSignature(document != null ? document.TargetCatalog : null),
                StageMapApplyPlanner.ComputeSignature(document != null ? document.PresentationCatalog : null),
            };
        }

        private static StageMapDocument CreateDenseDocument()
        {
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            document.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            document.Cells = new StageCellLayoutData[4];
            document.VisualTileKeys = Array.Empty<string>();
            return document;
        }

        private static void LoadDocument(StageMapEditorWindow window, StageMapDocument document)
        {
            window.LoadDocument(document);
        }

        private static StageMapDocument GetActiveDocument(StageMapEditorWindow window)
        {
            return window.ActiveDocument;
        }

        private static StageMapOverlayCache GetOverlay(StageMapEditorWindow window)
        {
            return window.OverlayCache;
        }

        private static StageMapEditingSession GetSession(StageMapEditorWindow window)
        {
            return window.Session;
        }

        private static bool ExecuteSceneTool(StageMapEditorWindow window, Vector2Int cell, Vector3 world)
        {
            return window.TryExecuteSceneTool(cell, world, continuous: false);
        }
    }
}
