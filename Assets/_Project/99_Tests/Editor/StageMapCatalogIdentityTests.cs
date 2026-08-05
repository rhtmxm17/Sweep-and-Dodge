using System;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapCatalogIdentityTests
    {
        [Test]
        public void InitialAdd_UpdatesLastAppliedIdentity()
        {
            using (var setup = CreateSetup())
            {
                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                Assert.That(plan.HasErrors, Is.False, JoinIssues(plan));
                Assert.That(plan.Changes.Any(x => x.Kind == StageMapApplyChangeKind.Add), Is.True);
                Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, out string error), Is.True, error);
                Assert.That(setup.Catalog.Entries, Has.Length.EqualTo(1));
                Assert.That(setup.Document.LastAppliedCatalogEntryKey, Is.EqualTo("stage_new"));
            }
        }

        [Test]
        public void Rename_UsesLastAppliedIdentity_RequiresConfirmation_AndDoesNotOrphanEntry()
        {
            using (var setup = CreateSetup())
            {
                setup.Catalog.Entries = new[] { Entry("stage_old", setup.Definition, setup.Layout) };
                SetLastApplied(setup.Document, "stage_old");
                string layoutBefore = StageMapApplyPlanner.ComputeSignature(setup.Layout);
                string definitionBefore = StageMapApplyPlanner.ComputeSignature(setup.Definition);
                string catalogBefore = StageMapApplyPlanner.ComputeSignature(setup.Catalog);
                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                Assert.That(plan.HasErrors, Is.False, JoinIssues(plan));
                Assert.That(plan.CatalogIdentityKey, Is.EqualTo("stage_old"));
                Assert.That(plan.RequiresConfirmation, Is.True);
                Assert.That(plan.Changes.Any(x => x.Kind == StageMapApplyChangeKind.Remove && x.Field == "CatalogEntryIdentity"), Is.True);
                Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, out _), Is.False);
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Layout), Is.EqualTo(layoutBefore));
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Definition), Is.EqualTo(definitionBefore));
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Catalog), Is.EqualTo(catalogBefore));

                Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, true, out string error), Is.True, error);
                Assert.That(setup.Catalog.Entries, Has.Length.EqualTo(1));
                Assert.That(setup.Catalog.Entries[0].EntryKey, Is.EqualTo("stage_new"));
                Assert.That(setup.Document.LastAppliedCatalogEntryKey, Is.EqualTo("stage_new"));
            }
        }

        [Test]
        public void AmbiguousDefinitionLayoutPair_IsRejectedBeforeMutation()
        {
            using (var setup = CreateSetup())
            {
                setup.Catalog.Entries = new[]
                {
                    Entry("pair_a", setup.Definition, setup.Layout),
                    Entry("pair_b", setup.Definition, setup.Layout),
                };
                string before = StageMapApplyPlanner.ComputeSignature(setup.Catalog);

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                Assert.That(plan.ValidationIssues.Any(x => x.Code == "SMC101"), Is.True);
                Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, true, out _), Is.False);
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Catalog), Is.EqualTo(before));
            }
        }

        [Test]
        public void RenameThatCreatesDuplicateCandidate_IsRejectedBeforeAnyAssetMutation()
        {
            using (var setup = CreateSetup())
            {
                var otherDefinition = ScriptableObject.CreateInstance<StageDefinitionSO>();
                var otherLayout = ScriptableObject.CreateInstance<StageLayoutSO>();
                try
                {
                    otherDefinition.StageId = setup.Document.StageId + 1;
                    otherDefinition.StageTimeLimitSec = 1f;
                    otherLayout.SchemaVersion = 2;
                    otherLayout.StageId = otherDefinition.StageId;
                    otherLayout.Grid = setup.Document.Grid;
                    otherLayout.Cells = new StageCellLayoutData[1];
                    setup.Catalog.Entries = new[]
                    {
                        Entry("stage_old", setup.Definition, setup.Layout),
                        Entry("stage_new", otherDefinition, otherLayout),
                    };
                    SetLastApplied(setup.Document, "stage_old");
                    string catalogBefore = StageMapApplyPlanner.ComputeSignature(setup.Catalog);

                    var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                    Assert.That(plan.HasErrors, Is.True);
                    Assert.That(plan.ValidationIssues.Any(x => x.Code == "STC007"), Is.True);
                    Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, true, out _), Is.False);
                    Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Catalog), Is.EqualTo(catalogBefore));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(otherDefinition);
                    UnityEngine.Object.DestroyImmediate(otherLayout);
                }
            }
        }

        [Test]
        public void MissingTargetCatalog_IsRejectedWithoutLayoutOrDefinitionMutation()
        {
            using (var setup = CreateSetup())
            {
                setup.Document.TargetCatalog = null;
                string layoutBefore = StageMapApplyPlanner.ComputeSignature(setup.Layout);
                string definitionBefore = StageMapApplyPlanner.ComputeSignature(setup.Definition);

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                Assert.That(plan.ValidationIssues.Any(x => x.Code == "SMD902"), Is.True);
                Assert.That(StageMapApplyPlanner.TryApplyPlan(plan, false, out _), Is.False);
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Layout), Is.EqualTo(layoutBefore));
                Assert.That(StageMapApplyPlanner.ComputeSignature(setup.Definition), Is.EqualTo(definitionBefore));
            }
        }

        [Test]
        public void SourceBinding_AddRemoveReorder_ExportsDeterministicallyByStableId()
        {
            using (var setup = CreateSetup())
            {
                setup.Document.SourceRegions = new[]
                {
                    new StageMapRegionData { StableId = 30u, Active = true },
                    new StageMapRegionData { StableId = 10u, Active = true },
                    new StageMapRegionData { StableId = 20u, Active = false },
                };
                var first = StageMapDocumentExporter.BuildDefinitionSnapshot(setup.Document);
                setup.Document.SourceRegions = new[]
                {
                    new StageMapRegionData { StableId = 10u, Active = true },
                    new StageMapRegionData { StableId = 30u, Active = true },
                };
                var second = StageMapDocumentExporter.BuildDefinitionSnapshot(setup.Document);
                try
                {
                    Assert.That(first.SourceBindings.Select(x => x.SourceStableId), Is.EqualTo(new[] { 10u, 30u }));
                    Assert.That(second.SourceBindings.Select(x => x.SourceStableId), Is.EqualTo(new[] { 10u, 30u }));
                    Assert.That(EditorJsonUtility.ToJson(first), Is.EqualTo(EditorJsonUtility.ToJson(second)));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(first);
                    UnityEngine.Object.DestroyImmediate(second);
                }
            }
        }

        private static Setup CreateSetup()
        {
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            var presentation = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            document.SchemaVersion = StageMapDocument.CurrentSchemaVersion;
            document.StageId = 41;
            document.DisplayName = "Stage";
            document.StageTimeLimitSec = 1f;
            document.Grid = new StageGridSpec { Width = 1, Height = 1, CellSize = 1f };
            document.Cells = new[] { new StageCellLayoutData() };
            document.VisualTileKeys = new string[1];
            document.PlayerStart = new StagePlayerStartLayoutData { Active = true, AnchorCell = Vector2Int.zero };
            document.TargetLayout = layout;
            document.TargetDefinition = definition;
            document.TargetCatalog = catalog;
            document.PresentationCatalog = presentation;
            document.IncludeInCatalog = true;
            document.EnabledInCatalog = true;
            document.CatalogEntryKey = "stage_new";
            return new Setup(document, layout, definition, catalog, presentation);
        }

        private static StageCatalogEntry Entry(string key, StageDefinitionSO definition, StageLayoutSO layout)
        {
            return new StageCatalogEntry { EntryKey = key, Enabled = true, Definition = definition, Layout = layout };
        }

        private static void SetLastApplied(StageMapDocument document, string key)
        {
            var serialized = new SerializedObject(document);
            serialized.FindProperty("_lastAppliedCatalogEntryKey").stringValue = key;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string JoinIssues(StageMapApplyPlan plan)
        {
            return string.Join("\n", plan.ValidationIssues.Select(x => $"{x.Code}: {x.Message}"));
        }

        private readonly struct Setup : IDisposable
        {
            public Setup(StageMapDocument document, StageLayoutSO layout, StageDefinitionSO definition, StageCatalogSO catalog, StagePresentationCatalogSO presentation)
            {
                Document = document;
                Layout = layout;
                Definition = definition;
                Catalog = catalog;
                Presentation = presentation;
            }

            public StageMapDocument Document { get; }
            public StageLayoutSO Layout { get; }
            public StageDefinitionSO Definition { get; }
            public StageCatalogSO Catalog { get; }
            public StagePresentationCatalogSO Presentation { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Document);
                UnityEngine.Object.DestroyImmediate(Layout);
                UnityEngine.Object.DestroyImmediate(Definition);
                UnityEngine.Object.DestroyImmediate(Catalog);
                UnityEngine.Object.DestroyImmediate(Presentation);
            }
        }
    }
}
