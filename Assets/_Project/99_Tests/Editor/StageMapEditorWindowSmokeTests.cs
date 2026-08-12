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
                Assert.That(window.TrySelect(StageMapSelection.ForCell(Vector2Int.zero), frame: false), Is.True);
                Assert.That(window.CurrentInspectorSection, Is.EqualTo(StageMapInspectorSection.Cell));

                window.OverlayCache.EnsureBuilt(first);
                int initialBuildCount = window.OverlayCache.Stats.BuildCount;

                window.LoadDocument(second);
                Assert.That(window.ActiveDocument, Is.SameAs(second));
                window.LoadDocument(first);

                Undo.RecordObject(first, "Stage Map Editor Synthetic Smoke Mutation");
                StageCellLayoutData[] cells = (StageCellLayoutData[])first.Cells.Clone();
                cells[0].MovementFlags = StageCellMovementFlags.BlockBullet;
                first.Cells = cells;
                Undo.PerformUndo();

                window.ReconcileAfterExternalMutation();
                window.OverlayCache.EnsureBuilt(first);
                Assert.That(first.Cells[0].MovementFlags, Is.EqualTo(StageCellMovementFlags.None));
                Assert.That(window.OverlayCache.Stats.BuildCount, Is.GreaterThan(initialBuildCount));
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
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
