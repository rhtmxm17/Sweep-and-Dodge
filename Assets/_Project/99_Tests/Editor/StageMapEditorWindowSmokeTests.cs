using System.Collections.Generic;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapEditorWindowSmokeTests
    {
        [Test]
        public void Window_LoadSwitchSelectAndUndo_UsesTransientDocumentsOnly()
        {
            StageMapDocument first = CreateDocument(1);
            StageMapDocument second = CreateDocument(2);
            StageMapEditorWindow window = EditorWindow.GetWindow<StageMapEditorWindow>(
                utility: true,
                title: "Stage Map Editor Synthetic Smoke",
                focus: false);

            try
            {
                window.LoadDocument(first);
                Assert.That(window.ActiveDocument, Is.SameAs(first));
                window.EnsureTableSummaryForTests();
                int initialTableBuildCount = window.TableSummaryBuildCount;
                Assert.That(window.TrySelect(StageMapSelection.ForCell(Vector2Int.zero), frame: false), Is.True);
                Assert.That(window.CurrentInspectorSection, Is.EqualTo(StageMapInspectorSection.Cell));

                window.OverlayCache.EnsureBuilt(first);
                int initialBuildCount = window.OverlayCache.Stats.BuildCount;

                window.LoadDocument(second);
                Assert.That(window.ActiveDocument, Is.SameAs(second));
                window.EnsureTableSummaryForTests();
                Assert.That(window.TableSummaryBuildCount, Is.GreaterThan(initialTableBuildCount));
                window.LoadDocument(first);
                window.EnsureTableSummaryForTests();

                Undo.RecordObject(first, "Stage Map Editor Synthetic Smoke Mutation");
                StageCellLayoutData[] cells = (StageCellLayoutData[])first.Cells.Clone();
                cells[0].MovementFlags = StageCellMovementFlags.BlockBullet;
                first.Cells = cells;
                Undo.PerformUndo();

                window.ReconcileAfterExternalMutation();
                window.EnsureTableSummaryForTests();
                window.OverlayCache.EnsureBuilt(first);
                Assert.That(first.Cells[0].MovementFlags, Is.EqualTo(StageCellMovementFlags.None));
                Assert.That(window.OverlayCache.Stats.BuildCount, Is.GreaterThan(initialBuildCount));
                int rebuiltTableCount = window.TableSummaryBuildCount;
                Assert.That(rebuiltTableCount, Is.GreaterThan(initialTableBuildCount));
                window.EnsureTableSummaryForTests();
                Assert.That(window.TableSummaryBuildCount, Is.EqualTo(rebuiltTableCount));
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void UiPolicy_MapsMovementRowsLayersAndSplitterWithoutTouchingDocument()
        {
            StageMapDocument document = CreateDocument(1);
            var session = new StageMapEditingSession();
            try
            {
                session.Load(document);
                string before = StageMapApplyPlanner.ComputeSignature(document);

                Assert.That(StageMapEditorUiPolicy.GetMovementPreset(0), Is.EqualTo(StageCellMovementFlags.None));
                Assert.That(StageMapEditorUiPolicy.GetMovementPreset(1), Is.EqualTo(StageCellMovementFlags.BlockPlayer));
                Assert.That(StageMapEditorUiPolicy.GetMovementPreset(2), Is.EqualTo(StageCellMovementFlags.BlockBullet));
                Assert.That(
                    StageMapEditorUiPolicy.GetMovementPreset(3),
                    Is.EqualTo(StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet));
                for (int i = 0; i < 4; i++)
                    Assert.That(StageMapEditorUiPolicy.GetMovementPresetIndex(StageMapEditorUiPolicy.GetMovementPreset(i)), Is.EqualTo(i));

                Assert.That(StageMapEditorUiPolicy.GetRowAction(0, 1), Is.EqualTo(StageMapTableRowAction.Select));
                Assert.That(StageMapEditorUiPolicy.GetRowAction(0, 2), Is.EqualTo(StageMapTableRowAction.SelectAndFrame));
                Assert.That(StageMapEditorUiPolicy.GetRowAction(1, 2), Is.EqualTo(StageMapTableRowAction.None));
                Assert.That(StageMapEditorUiPolicy.CanEraseStableId(StageMapEditorToolMode.PaintRegion), Is.True);
                Assert.That(StageMapEditorUiPolicy.CanEraseStableId(StageMapEditorToolMode.PlaceAnchor), Is.False);

                foreach (StageMapEditorLayer layerId in System.Enum.GetValues(typeof(StageMapEditorLayer)))
                {
                    StageMapEditorUiPolicy.SetLayerState(
                        session,
                        layerId,
                        active: true,
                        visible: false,
                        locked: true);
                    StageMapLayerUiState layer = StageMapEditorUiPolicy.GetLayerState(session, layerId);
                    Assert.That(layer.Active, Is.True, layerId.ToString());
                    Assert.That(layer.Visible, Is.False, layerId.ToString());
                    Assert.That(layer.Locked, Is.True, layerId.ToString());
                }

                Assert.That(StageMapEditorUiPolicy.ClampRightPanelWidth(100f, 1000f), Is.EqualTo(400f));
                Assert.That(StageMapEditorUiPolicy.ClampRightPanelWidth(900f, 2000f), Is.EqualTo(720f));
                Assert.That(StageMapEditorUiPolicy.ClampRightPanelWidth(520f, 700f), Is.EqualTo(336f));
                Assert.That(StageMapApplyPlanner.ComputeSignature(document), Is.EqualTo(before));
            }
            finally
            {
                session.Dispose();
                Object.DestroyImmediate(document);
            }
        }

        [Test]
        public void TableCache_BuildsCanonicalRowsAndDoesNotRebuildOnSteadyEnsure()
        {
            StageMapDocument document = CreateDocument(1);
            try
            {
                document.Cells[0].SourceRegionId = 10u;
                document.Cells[1].SourceRegionId = 10u;
                document.SourceRegions = new[]
                {
                    new StageMapRegionData
                    {
                        StableId = 10u,
                        Active = true,
                        AnchorCell = new Vector2Int(1, 0),
                    },
                };
                document.HazardActorPlacements = new[]
                {
                    new StageMapHazardActorPlacementData
                    {
                        OwningSourceStableId = 10u,
                        PlacementInstanceId = 7,
                        LocalYawDeg = 45f,
                    },
                };
                document.HazardActorOrchestrationRules = new[]
                {
                    new StageMapHazardActorOrchestrationRuleData
                    {
                        OwningSourceStableId = 10u,
                        RuleId = 3,
                        TargetPlacementInstanceIds = new[] { 7 },
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                };
                document.PresentationLinks = new[]
                {
                    new StageMapPresentationLinkData
                    {
                        StableId = 90u,
                        Active = true,
                        PresentationKey = "portal",
                        PlacementMode = StagePresentationPlacementMode.LinkedToParent,
                        LinkKind = StagePresentationLinkKind.Source,
                        LinkedStableId = 10u,
                    },
                };

                var rawIssues = new List<ContentValidationIssue>
                {
                    new ContentValidationIssue(
                        ContentValidationSeverity.Warning,
                        "REGION",
                        "document/SourceRegions[0]",
                        "region warning"),
                    new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "HAZARD",
                        "document/HazardActorPlacements[0]",
                        "hazard error"),
                };
                var mappedIssues = new List<StageMapDocumentIssue>();
                StageMapDocumentIssueMapper.Map(document, rawIssues, mappedIssues);

                var cache = new StageMapEditorTableCache();
                cache.EnsureBuilt(document, mappedIssues, null);
                Assert.That(cache.BuildCount, Is.EqualTo(1));
                Assert.That(cache.Regions.Count, Is.EqualTo(1));
                Assert.That(cache.Regions[0].CellCountLabel, Is.EqualTo("2"));
                Assert.That(cache.Regions[0].IssueLabel, Is.EqualTo("1"));
                Assert.That(cache.Regions[0].RegionSelection, Is.EqualTo(StageMapSelection.ForRegion(StageRegionKind.Source, 10u)));
                Assert.That(cache.Regions[0].AnchorSelection, Is.EqualTo(StageMapSelection.ForAnchor(StageRegionKind.Source, 10u)));
                Assert.That(cache.Hazards[0].Selection, Is.EqualTo(StageMapSelection.ForHazard(10u, 7)));
                Assert.That(cache.Hazards[0].IssueLabel, Is.EqualTo("1"));
                Assert.That(cache.Rules[0].Selection, Is.EqualTo(StageMapSelection.ForHazardRule(10u, 3)));
                Assert.That(cache.Links[0].Selection, Is.EqualTo(StageMapSelection.ForPresentation(90u)));
                Assert.That(cache.SourceIds, Is.EqualTo(new uint[] { 10u }));

                cache.EnsureBuilt(document, mappedIssues, null);
                Assert.That(cache.BuildCount, Is.EqualTo(1), "Steady repaint path must reuse row summaries.");
                long beforeAllocation = System.GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++)
                    cache.EnsureBuilt(document, mappedIssues, null);
                long allocated = System.GC.GetAllocatedBytesForCurrentThread() - beforeAllocation;
                Assert.That(allocated, Is.EqualTo(0L));

                cache.Invalidate();
                cache.EnsureBuilt(document, mappedIssues, null);
                Assert.That(cache.BuildCount, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(document);
            }
        }

        private static StageMapDocument CreateDocument(int stageId)
        {
            StageMapDocument document = ScriptableObject.CreateInstance<StageMapDocument>();
            document.SchemaVersion = StageMapDocument.CurrentSchemaVersion;
            document.StageId = stageId;
            document.DisplayName = $"Transient Stage {stageId}";
            document.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            document.Cells = new StageCellLayoutData[4];
            document.VisualTileKeys = new string[4];
            document.SourceRegions = System.Array.Empty<StageMapRegionData>();
            document.DepositRegions = System.Array.Empty<StageMapRegionData>();
            document.HazardActorPlacements = System.Array.Empty<StageMapHazardActorPlacementData>();
            document.HazardActorOrchestrationRules = System.Array.Empty<StageMapHazardActorOrchestrationRuleData>();
            document.PresentationLinks = System.Array.Empty<StageMapPresentationLinkData>();
            return document;
        }
    }
}
