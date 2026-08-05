using System;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapDocumentMigrationAndResizeTests
    {
        [Test]
        public void MigrationPreview_CurrentSchema_IsNoOp()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                var plan = StageMapDocumentMigrationUtility.BuildPreview(setup.Document);

                Assert.That(plan.HasErrors, Is.False);
                Assert.That(plan.HasChanges, Is.False);
                Assert.That(plan.SourceVersion, Is.EqualTo(StageMapDocument.CurrentSchemaVersion));
                Assert.That(StageMapDocumentMigrationUtility.TryApply(plan, false, out string error), Is.True, error);
            }
        }

        [Test]
        public void MigrationPreview_V1_DoesNotMutateRuntimeAssets_AndApplyIsUndoable()
        {
            using (var setup = CreateDocument(1))
            {
                setup.Catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        EntryKey = "existing_entry",
                        Enabled = true,
                        Definition = setup.Definition,
                        Layout = setup.Layout,
                    }
                };
                string layoutSignature = StageMapApplyPlanner.ComputeSignature(setup.Layout);
                string definitionSignature = StageMapApplyPlanner.ComputeSignature(setup.Definition);
                string catalogSignature = StageMapApplyPlanner.ComputeSignature(setup.Catalog);

                var plan = StageMapDocumentMigrationUtility.BuildPreview(setup.Document);

                Assert.That(plan.HasErrors, Is.False, string.Join("\n", plan.Issues.Select(x => x.Message)));
                Assert.That(plan.HasChanges, Is.True);
                Assert.That(plan.LastAppliedCatalogEntryKey, Is.EqualTo("existing_entry"));
                Assert.That(plan.PresentationCatalog, Is.Not.Null);
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Layout), Is.EqualTo(layoutSignature));
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Definition), Is.EqualTo(definitionSignature));
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Catalog), Is.EqualTo(catalogSignature));

                Assert.That(StageMapDocumentMigrationUtility.TryApply(plan, false, out string error), Is.True, error);
                Assert.That(setup.Document.SchemaVersion, Is.EqualTo(StageMapDocument.CurrentSchemaVersion));
                Assert.That(setup.Document.LastAppliedCatalogEntryKey, Is.EqualTo("existing_entry"));

                Undo.PerformUndo();
                Assert.That(setup.Document.SchemaVersion, Is.EqualTo(1));
            }
        }

        [Test]
        public void MigrationPreview_UnsupportedFutureVersion_IsRejected()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion + 1))
            {
                var plan = StageMapDocumentMigrationUtility.BuildPreview(setup.Document);

                Assert.That(plan.HasErrors, Is.True);
                Assert.That(plan.Issues.Any(x => x.Code == "SMM001"), Is.True);
                Assert.That(StageMapDocumentMigrationUtility.TryApply(plan, false, out _), Is.False);
            }
        }

        [Test]
        public void Paint_RejectsCorruptDenseArray_WithoutMutatingExistingData()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.Cells = new[]
                {
                    new StageCellLayoutData { SourceRegionId = 17u },
                };
                var before = (StageCellLayoutData[])setup.Document.Cells.Clone();

                bool changed = StageMapDocumentCommandUtility.TryPaintMovement(
                    setup.Document,
                    Vector2Int.zero,
                    StageCellMovementFlags.BlockPlayer,
                    out var issue);

                Assert.That(changed, Is.False);
                Assert.That(issue.Code, Is.EqualTo("SMC003"));
                Assert.That(setup.Document.Cells, Is.EqualTo(before));
            }
        }

        [Test]
        public void GridResize_Expand_PreservesCellsAndVisualKeysByCoordinate()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.Cells[3] = new StageCellLayoutData { SourceRegionId = 42u };
                setup.Document.VisualTileKeys = new[] { "a", "b", "c", "corner" };
                StageGridSpec target = setup.Document.Grid;
                target.Width = 3;
                target.Height = 3;

                var plan = StageMapGridResizeUtility.BuildPreview(setup.Document, target);

                Assert.That(plan.HasErrors, Is.False);
                Assert.That(plan.RequiresConfirmation, Is.False);
                Assert.That(StageMapGridResizeUtility.TryApply(plan, false, out string error), Is.True, error);
                Assert.That(setup.Document.Cells[4].SourceRegionId, Is.EqualTo(42u));
                Assert.That(setup.Document.VisualTileKeys[4], Is.EqualTo("corner"));
            }
        }

        [Test]
        public void GridResize_WithOptionalEmptyVisualKeys_PreservesEmptyRepresentation()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.VisualTileKeys = Array.Empty<string>();
                StageGridSpec target = setup.Document.Grid;
                target.Width = 3;

                var plan = StageMapGridResizeUtility.BuildPreview(setup.Document, target);

                Assert.That(plan.HasErrors, Is.False);
                Assert.That(StageMapGridResizeUtility.TryApply(plan, false, out string error), Is.True, error);
                Assert.That(setup.Document.VisualTileKeys, Is.Empty);
            }
        }

        [Test]
        public void GridResize_Shrink_ReportsDestructiveDiffAndRequiresConfirmation()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.Cells[3] = new StageCellLayoutData { DepositRegionId = 91u };
                setup.Document.VisualTileKeys = new[] { null, null, null, "cropped" };
                StageGridSpec target = setup.Document.Grid;
                target.Width = 1;
                target.Height = 1;

                var plan = StageMapGridResizeUtility.BuildPreview(setup.Document, target);

                Assert.That(plan.HasErrors, Is.False);
                Assert.That(plan.RequiresConfirmation, Is.True);
                Assert.That(plan.Changes.Any(x => x.Kind == StageMapApplyChangeKind.Remove), Is.True);
                Assert.That(StageMapGridResizeUtility.TryApply(plan, false, out _), Is.False);
                Assert.That(setup.Document.Grid.Width, Is.EqualTo(2));
                Assert.That(StageMapGridResizeUtility.TryApply(plan, true, out string error), Is.True, error);
                Assert.That(setup.Document.Cells, Has.Length.EqualTo(1));
            }
        }

        [Test]
        public void DenseArrayLengthRepair_PreservesAvailableFlattenedCoordinates()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.Cells = new[]
                {
                    new StageCellLayoutData { SourceRegionId = 1u },
                    new StageCellLayoutData { DepositRegionId = 2u },
                    new StageCellLayoutData { MovementFlags = StageCellMovementFlags.BlockBullet },
                };
                var issue = new ContentValidationIssue(ContentValidationSeverity.Error, "STG003", "document", "Cells length mismatch.");

                Assert.That(StageMapDocumentFixUtility.ApplyFix(setup.Document, issue), Is.True);
                Assert.That(setup.Document.Cells, Has.Length.EqualTo(4));
                Assert.That(setup.Document.Cells[0].SourceRegionId, Is.EqualTo(1u));
                Assert.That(setup.Document.Cells[1].DepositRegionId, Is.EqualTo(2u));
                Assert.That(setup.Document.Cells[2].MovementFlags, Is.EqualTo(StageCellMovementFlags.BlockBullet));
                Assert.That(setup.Document.VisualTileKeys, Has.Length.EqualTo(4));
            }
        }

        [Test]
        public void DenseArrayLengthRepair_DoesNotDensifyOptionalEmptyVisualKeys()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                setup.Document.Cells = new StageCellLayoutData[1];
                setup.Document.VisualTileKeys = Array.Empty<string>();
                var issue = new ContentValidationIssue(ContentValidationSeverity.Error, "STG003", "document", "Cells length mismatch.");

                Assert.That(StageMapDocumentFixUtility.ApplyFix(setup.Document, issue), Is.True);
                Assert.That(setup.Document.Cells, Has.Length.EqualTo(4));
                Assert.That(setup.Document.VisualTileKeys, Is.Empty);
            }
        }

        [Test]
        public void OverlayGeometry_DenseGrid_HasFourVerticesPerCellAndFixedLayerSubmissions()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            {
                const int size = 32;
                setup.Document.Grid = new StageGridSpec { Width = size, Height = size, CellSize = 1f };
                setup.Document.Cells = new StageCellLayoutData[size * size];
                for (int i = 0; i < setup.Document.Cells.Length; i++)
                {
                    setup.Document.Cells[i] = new StageCellLayoutData
                    {
                        MovementFlags = StageCellMovementFlags.BlockBullet,
                        SourceRegionId = 1u,
                        DepositRegionId = 2u,
                    };
                }

                using (var cache = StageMapOverlayCacheBuilder.Build(setup.Document))
                {
                    var stats = cache.Stats;
                    Assert.That(stats.MovementVertexCount, Is.EqualTo(size * size * 4));
                    Assert.That(stats.SourceVertexCount, Is.EqualTo(size * size * 4));
                    Assert.That(stats.DepositVertexCount, Is.EqualTo(size * size * 4));
                    Assert.That(stats.OverlapVertexCount, Is.EqualTo(size * size * 4));
                    Assert.That(stats.MovementIndexCount, Is.EqualTo(size * size * 6));
                    Assert.That(stats.SourceIndexCount, Is.EqualTo(size * size * 6));
                    Assert.That(stats.DepositIndexCount, Is.EqualTo(size * size * 6));
                    Assert.That(stats.OverlapIndexCount, Is.EqualTo(size * size * 6));
                    Assert.That(cache.GetDrawSubmissionCount(true, true, true), Is.EqualTo(4));
                }
            }
        }

        [Test]
        public void OverlayGeometry_UnchangedEnsure_DoesNotRebuildOrAllocateManagedMemory()
        {
            using (var setup = CreateDocument(StageMapDocument.CurrentSchemaVersion))
            using (var cache = StageMapOverlayCacheBuilder.Build(setup.Document))
            {
                cache.EnsureBuilt(setup.Document);
                int buildCount = cache.Stats.BuildCount;
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 256; i++)
                    cache.EnsureBuilt(setup.Document);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

                Assert.That(cache.Stats.BuildCount, Is.EqualTo(buildCount));
                Assert.That(allocated, Is.EqualTo(0L));
            }
        }

        private static Setup CreateDocument(int schemaVersion)
        {
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            document.SchemaVersion = schemaVersion;
            document.StageId = 1;
            document.StageTimeLimitSec = 1f;
            document.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            document.Cells = new StageCellLayoutData[4];
            document.VisualTileKeys = new string[4];
            document.TargetLayout = layout;
            document.TargetDefinition = definition;
            document.TargetCatalog = catalog;
            document.PresentationCatalog = FindSinglePresentationCatalog();
            document.CatalogEntryKey = "stage_1";
            return new Setup(document, layout, definition, catalog);
        }

        private static StagePresentationCatalogSO FindSinglePresentationCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:StagePresentationCatalogSO");
            Assert.That(guids, Has.Length.EqualTo(1), "Migration fixture requires the project's single explicit presentation catalog.");
            return AssetDatabase.LoadAssetAtPath<StagePresentationCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private readonly struct Setup : IDisposable
        {
            public Setup(StageMapDocument document, StageLayoutSO layout, StageDefinitionSO definition, StageCatalogSO catalog)
            {
                Document = document;
                Layout = layout;
                Definition = definition;
                Catalog = catalog;
            }

            public StageMapDocument Document { get; }
            public StageLayoutSO Layout { get; }
            public StageDefinitionSO Definition { get; }
            public StageCatalogSO Catalog { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Document);
                UnityEngine.Object.DestroyImmediate(Layout);
                UnityEngine.Object.DestroyImmediate(Definition);
                UnityEngine.Object.DestroyImmediate(Catalog);
            }
        }
    }
}
