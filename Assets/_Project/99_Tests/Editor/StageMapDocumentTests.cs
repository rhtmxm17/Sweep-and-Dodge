using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapDocumentTests
    {
        [Test]
        public void ValidDocument_PassesWithoutValidationErrors()
        {
            var setup = CreateValidSetup();
            try
            {
                var issues = Validate(setup.Document);

                Assert.That(issues.Where(x => x.Severity == ContentValidationSeverity.Error), Is.Empty);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void LayoutSnapshot_UsesRuntimeLayoutSchemaOnly()
        {
            var setup = CreateValidSetup();
            try
            {
                var layout = StageMapDocumentExporter.BuildLayoutSnapshot(setup.Document);
                try
                {
                    Assert.That(layout.SchemaVersion, Is.EqualTo(2));
                    Assert.That(layout.StageId, Is.EqualTo(setup.Document.StageId));
                    Assert.That(layout.Cells.Length, Is.EqualTo(setup.Document.Grid.Width * setup.Document.Grid.Height));
                    Assert.That(layout.SourceRegions.Select(x => x.StableId), Is.EquivalentTo(new[] { 100u }));
                    Assert.That(layout.DepositRegions.Select(x => x.StableId), Is.EquivalentTo(new[] { 200u }));
                    Assert.That(layout.PlayerStart.Active, Is.True);
                }
                finally
                {
                    Object.DestroyImmediate(layout);
                }
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void Validation_RejectsHazardPlacementWithMissingSourceOwner()
        {
            var setup = CreateValidSetup();
            var prefab = new GameObject("HazardPrefab");
            try
            {
                setup.Document.HazardActorPlacements = new[]
                {
                    new StageMapHazardActorPlacementData
                    {
                        OwningSourceStableId = 999u,
                        PlacementInstanceId = 1,
                        ActorArchetypePrefab = prefab,
                    }
                };

                var issues = Validate(setup.Document);

                Assert.That(issues.Any(x => x.Code == "SMD022" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                setup.Dispose();
            }
        }

        [Test]
        public void Validation_RejectsHazardPlacementWithoutHazardActorAuthoring_AndApplyFails()
        {
            var setup = CreateValidSetup();
            var prefab = new GameObject("HazardPrefab");
            try
            {
                setup.Document.HazardActorPlacements = new[]
                {
                    new StageMapHazardActorPlacementData
                    {
                        OwningSourceStableId = 100u,
                        PlacementInstanceId = 1,
                        ActorArchetypePrefab = prefab,
                    }
                };

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);
                bool applied = StageMapApplyPlanner.TryApplyPlan(plan, saveAssets: false, confirmed: true, out string error);

                Assert.That(plan.ValidationIssues.Any(x => x.Code == "STC034" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(applied, Is.False);
                Assert.That(error, Does.Contain("validation failed"));
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                setup.Dispose();
            }
        }

        [Test]
        public void Validation_RejectsTestOnlyHazardActorPrefabBeforeApply()
        {
            var setup = CreateValidSetup();
            var prefabSource = new GameObject("TestOnlyHazardPrefab");
            const string PrefabPath = "Assets/_Project/99_Tests/TestData/__GeneratedStageMapHazardActor.prefab";
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, PrefabPath);
                setup.Document.HazardActorPlacements = new[]
                {
                    new StageMapHazardActorPlacementData
                    {
                        OwningSourceStableId = 100u,
                        PlacementInstanceId = 1,
                        ActorArchetypePrefab = prefab,
                    }
                };

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                Assert.That(plan.ValidationIssues.Any(x => x.Code == "STC037" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(PrefabPath);
                Object.DestroyImmediate(prefabSource);
                setup.Dispose();
            }
        }

        [Test]
        public void ApplyPlan_RejectsStaleDocumentMutation()
        {
            var setup = CreateValidSetup();
            try
            {
                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);
                setup.Document.StageTimeLimitSec += 1f;

                bool applied = StageMapApplyPlanner.TryApplyPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.False);
                Assert.That(error, Does.Contain("changed after dry-run"));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void ApplyPlan_AppliesGeneratedRuntimeAssets()
        {
            var setup = CreateValidSetup();
            try
            {
                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);

                bool applied = StageMapApplyPlanner.TryApplyPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.True, error);
                Assert.That(setup.Layout.StageId, Is.EqualTo(setup.Document.StageId));
                Assert.That(setup.Layout.Cells.Length, Is.EqualTo(4));
                Assert.That(setup.Definition.StageId, Is.EqualTo(setup.Document.StageId));
                Assert.That(setup.Definition.SourceBindings.Any(x => x.SourceStableId == 100u), Is.True);
                Assert.That(setup.Catalog.Entries.Any(x => x.EntryKey == "stage_07" && x.Definition == setup.Definition && x.Layout == setup.Layout), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void ApplyPlan_RemovesCatalogEntry_WhenDocumentIsExcludedFromCatalog()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_07",
                        Definition = setup.Definition,
                        Layout = setup.Layout,
                    }
                };
                setup.Document.IncludeInCatalog = false;

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);
                bool applied = StageMapApplyPlanner.TryApplyPlan(plan, saveAssets: false, confirmed: true, out string error);

                Assert.That(plan.Changes.Any(x => x.Kind == StageMapApplyChangeKind.Remove && x.Target == "StageCatalogSO"), Is.True);
                Assert.That(plan.RequiresConfirmation, Is.True);
                Assert.That(applied, Is.True, error);
                Assert.That(setup.Catalog.Entries.Any(x => x.EntryKey == "stage_07"), Is.False);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void ApplyPlan_RejectsStaleCatalogRemoval()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Catalog.Entries = new[]
                {
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_07",
                        Definition = setup.Definition,
                        Layout = setup.Layout,
                    }
                };
                setup.Document.IncludeInCatalog = false;
                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);
                setup.Catalog.Entries[0].Enabled = false;

                bool applied = StageMapApplyPlanner.TryApplyPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.False);
                Assert.That(error, Does.Contain("changed after dry-run"));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void DefinitionSnapshot_UsesDocumentActiveSourcesAsSourceBindingSsot()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 100u,
                        InitialSourceState = SourceStateId.Weakened,
                        ThresholdWeakened = 11,
                        ThresholdDepleted = 22,
                        SustainSlots = new[] { new SustainSlotBinding { State = SourceStateId.Normal } },
                        EventSlots = new[] { new EventSlotBinding { TriggerState = SourceStateId.Depleted } },
                        HazardActorOrchestrationRules = new[]
                        {
                            new HazardActorOrchestrationRuleBinding { RuleId = 1, ActionType = HazardActorOrchestrationActionId.Spawn },
                        },
                    },
                    new StageSourceBinding
                    {
                        SourceStableId = 999u,
                        ThresholdWeakened = 33,
                        ThresholdDepleted = 44,
                    },
                };

                var snapshot = StageMapDocumentExporter.BuildDefinitionSnapshot(setup.Document);
                try
                {
                    Assert.That(snapshot.SourceBindings.Select(x => x.SourceStableId), Is.EqualTo(new[] { 100u }));
                    var preserved = snapshot.SourceBindings.Single();
                    Assert.That(preserved.InitialSourceState, Is.EqualTo(SourceStateId.Weakened));
                    Assert.That(preserved.ThresholdWeakened, Is.EqualTo(11));
                    Assert.That(preserved.ThresholdDepleted, Is.EqualTo(22));
                    Assert.That(preserved.SustainSlots.Length, Is.EqualTo(1));
                    Assert.That(preserved.EventSlots.Length, Is.EqualTo(1));
                    Assert.That(preserved.HazardActorOrchestrationRules.Length, Is.EqualTo(1));
                }
                finally
                {
                    Object.DestroyImmediate(snapshot);
                }

                var plan = StageMapApplyPlanner.BuildPlan(setup.Document);
                Assert.That(plan.Changes.Any(x => x.Target == "StageDefinitionSO" && x.Field == nameof(StageDefinitionSO.SourceBindings)), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void CommandUtility_PaintsMovementAndRegions_ThroughDocumentCells()
        {
            var setup = CreateValidSetup();
            try
            {
                bool movementChanged = StageMapDocumentCommandUtility.PaintMovement(
                    setup.Document,
                    new Vector2Int(1, 0),
                    StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet);
                bool sourceChanged = StageMapDocumentCommandUtility.PaintRegion(
                    setup.Document,
                    new Vector2Int(1, 0),
                    StageRegionKind.Source,
                    300u);

                Assert.That(movementChanged, Is.True);
                Assert.That(sourceChanged, Is.True);
                Assert.That(setup.Document.Cells.Length, Is.EqualTo(4));
                Assert.That(setup.Document.Cells[1].MovementFlags, Is.EqualTo(StageCellMovementFlags.BlockPlayer | StageCellMovementFlags.BlockBullet));
                Assert.That(setup.Document.Cells[1].SourceRegionId, Is.EqualTo(300u));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void CommandUtility_PlacesAnchorPlayerHazardAndPresentation_InDocumentOnly()
        {
            var setup = CreateValidSetup();
            var prefab = new GameObject("HazardPrefab");
            try
            {
                bool anchorChanged = StageMapDocumentCommandUtility.PlaceAnchor(
                    setup.Document,
                    StageRegionKind.Source,
                    300u,
                    new Vector2Int(1, 0),
                    Vector2.zero);
                bool playerChanged = StageMapDocumentCommandUtility.PlacePlayerStart(
                    setup.Document,
                    new Vector2Int(1, 0),
                    Vector2.zero,
                    45f);
                bool hazardChanged = StageMapDocumentCommandUtility.PlaceHazardActor(
                    setup.Document,
                    300u,
                    prefab,
                    StageMapDocumentCommandUtility.GetCellCenterWorld(setup.Document, new Vector2Int(1, 0)),
                    15f,
                    out int placementId);
                bool presentationChanged = StageMapDocumentCommandUtility.PlacePresentationLink(
                    setup.Document,
                    900u,
                    "source_core",
                    StagePresentationPlacementMode.LinkedToParent,
                    StagePresentationLinkKind.Source,
                    300u,
                    StageMapDocumentCommandUtility.GetCellCenterWorld(setup.Document, new Vector2Int(1, 0)),
                    Vector3.zero,
                    Vector3.one);

                Assert.That(anchorChanged, Is.True);
                Assert.That(playerChanged, Is.True);
                Assert.That(hazardChanged, Is.True);
                Assert.That(presentationChanged, Is.True);
                Assert.That(setup.Document.SourceRegions.Any(x => x.StableId == 300u && x.AnchorCell == new Vector2Int(1, 0)), Is.True);
                Assert.That(setup.Document.PlayerStart.YawDeg, Is.EqualTo(45f));
                Assert.That(setup.Document.HazardActorPlacements.Single(x => x.PlacementInstanceId == placementId).OwningSourceStableId, Is.EqualTo(300u));
                Assert.That(setup.Document.PresentationLinks.Single(x => x.StableId == 900u).LinkKind, Is.EqualTo(StagePresentationLinkKind.Source));
                Assert.That(setup.Layout.Cells, Is.Null.Or.Empty);
                Assert.That(setup.Definition.SourceBindings, Is.Null.Or.Empty);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                setup.Dispose();
            }
        }

        [Test]
        public void Validation_RejectsVisualTileKeyLengthMismatch()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Document.VisualTileKeys = new[] { "floor_a" };

                var issues = Validate(setup.Document);

                Assert.That(issues.Any(x => x.Code == "SMD010" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void Validation_ReportsPresentationKeyAndDuplicateStableIdIssues()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Document.PresentationLinks = new[]
                {
                    new StageMapPresentationLinkData
                    {
                        StableId = 1u,
                        Active = true,
                        PlacementMode = StagePresentationPlacementMode.Standalone,
                        PresentationKey = string.Empty,
                        Scale = Vector3.one,
                    },
                    new StageMapPresentationLinkData
                    {
                        StableId = 1u,
                        Active = true,
                        PlacementMode = StagePresentationPlacementMode.Standalone,
                        PresentationKey = "missing_key_for_stage_map_document_test",
                        Scale = Vector3.one,
                    },
                };

                var issues = Validate(setup.Document);

                Assert.That(issues.Any(x => x.Code == "STL007" && x.Severity == ContentValidationSeverity.Warning), Is.True);
                Assert.That(issues.Any(x => x.Code == "SMD032" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(issues.Any(x => x.Code == "SMD034" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void OverlayCacheBuilder_BuildsLayerGeometry()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Document.Cells[1] = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.BlockBullet,
                };

                using (var cache = StageMapOverlayCacheBuilder.Build(setup.Document))
                {
                    var stats = cache.Stats;
                    Assert.That(stats.ScannedCellCount, Is.EqualTo(4));
                    Assert.That(stats.MovementCellCount, Is.EqualTo(1));
                    Assert.That(stats.SourceCellCount, Is.EqualTo(1));
                    Assert.That(stats.DepositCellCount, Is.EqualTo(1));
                    Assert.That(stats.MovementVertexCount, Is.EqualTo(4));
                    Assert.That(stats.SourceVertexCount, Is.EqualTo(4));
                    Assert.That(stats.DepositVertexCount, Is.EqualTo(4));
                    Assert.That(cache.GetDrawSubmissionCount(true, true, true), Is.EqualTo(3));
                }
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void QuickFix_ResizesDocumentArrays_PreservesExistingData()
        {
            var setup = CreateValidSetup();
            try
            {
                var preservedCell = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.BlockBullet,
                    SourceRegionId = 100u,
                };
                setup.Document.Cells = new[] { preservedCell };
                setup.Document.VisualTileKeys = new[] { "floor_a" };

                var cellIssue = new ContentValidationIssue(ContentValidationSeverity.Error, "STG003", "document", "Cells length mismatch.");
                var visualIssue = new ContentValidationIssue(ContentValidationSeverity.Error, "SMD010", "document", "VisualTileKeys length mismatch.");

                bool visualFixed = StageMapDocumentFixUtility.ApplyFix(setup.Document, visualIssue);
                bool cellFixed = StageMapDocumentFixUtility.ApplyFix(setup.Document, cellIssue);

                Assert.That(cellFixed, Is.True);
                Assert.That(visualFixed, Is.True);
                Assert.That(setup.Document.Cells.Length, Is.EqualTo(4));
                Assert.That(setup.Document.Cells[0], Is.EqualTo(preservedCell));
                Assert.That(setup.Document.VisualTileKeys.Length, Is.EqualTo(4));
                Assert.That(setup.Document.VisualTileKeys[0], Is.EqualTo("floor_a"));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void QuickFix_PaintsMissingSourceRegionAtAnchorCell()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Document.Cells[0].SourceRegionId = 0u;
                var issues = Validate(setup.Document);
                var issue = issues.Single(x => x.Code == "STG009");

                Assert.That(StageMapDocumentFixUtility.TryBuildFixPreview(setup.Document, issue, out var preview), Is.True);
                Assert.That(preview.FixId, Is.EqualTo("paint-source-anchor-cell"));

                bool fixedIssue = StageMapDocumentFixUtility.ApplyFix(setup.Document, issue);
                issues = Validate(setup.Document);

                Assert.That(fixedIssue, Is.True);
                Assert.That(setup.Document.Cells[0].SourceRegionId, Is.EqualTo(100u));
                Assert.That(issues.Any(x => x.Code == "STG009"), Is.False);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void QuickFix_MovesPlayerStartToWalkableCell()
        {
            var setup = CreateValidSetup();
            try
            {
                setup.Document.Cells[0].MovementFlags = StageCellMovementFlags.BlockPlayer;
                setup.Document.PlayerStart = new StagePlayerStartLayoutData
                {
                    Active = true,
                    AnchorCell = new Vector2Int(0, 0),
                    AnchorOffset = Vector2.zero,
                    YawDeg = 90f,
                };
                var issues = Validate(setup.Document);
                var issue = issues.Single(x => x.Code == "STG017");

                bool fixedIssue = StageMapDocumentFixUtility.ApplyFix(setup.Document, issue);
                issues = Validate(setup.Document);

                Assert.That(fixedIssue, Is.True);
                Assert.That(setup.Document.PlayerStart.Active, Is.True);
                Assert.That(setup.Document.PlayerStart.AnchorCell, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(setup.Document.PlayerStart.YawDeg, Is.EqualTo(90f));
                Assert.That(issues.Any(x => x.Code == "STG017"), Is.False);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_AppliesTilemapAuthoringIntoDocument()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            try
            {
                bool planned = StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan);

                Assert.That(planned, Is.True);
                Assert.That(plan.HasChanges, Is.True);

                bool applied = StageMapLegacyImportUtility.TryApplyImportPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.True, error);
                Assert.That(document.StageId, Is.EqualTo(legacy.Stage.StageId));
                Assert.That(document.Grid.Width, Is.EqualTo(2));
                Assert.That(document.Cells.Length, Is.EqualTo(4));
                Assert.That(document.Cells[0].SourceRegionId, Is.EqualTo(1001u));
                Assert.That(document.Cells[3].DepositRegionId, Is.EqualTo(2001u));
                Assert.That(document.Cells[2].MovementFlags, Is.EqualTo(StageCellMovementFlags.BlockBullet));
                Assert.That(document.SourceRegions.Single().AnchorCell, Is.EqualTo(new Vector2Int(0, 0)));
                Assert.That(document.DepositRegions.Single().AnchorCell, Is.EqualTo(new Vector2Int(1, 1)));
                Assert.That(document.PlayerStart.AnchorCell, Is.EqualTo(new Vector2Int(1, 0)));
                Assert.That(document.TargetLayout, Is.EqualTo(legacy.Layout));
                Assert.That(document.TargetDefinition, Is.EqualTo(legacy.Definition));
                Assert.That(document.TargetCatalog, Is.EqualTo(legacy.Catalog));
            }
            finally
            {
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_ExportedLayoutMatchesLegacySnapshot()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            StageLayoutSO legacySnapshot = null;
            StageLayoutSO exportedSnapshot = null;
            try
            {
                bool planned = StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan);
                Assert.That(planned, Is.True);
                Assert.That(StageMapLegacyImportUtility.TryApplyImportPlan(plan, saveAssets: false, out string importError), Is.True, importError);

                bool builtLegacySnapshot = StageLayoutCatalogGenerator.TryBuildStageLayoutSnapshot(legacy.Stage, out legacySnapshot, out var issues);
                exportedSnapshot = StageMapDocumentExporter.BuildLayoutSnapshot(document);

                Assert.That(builtLegacySnapshot, Is.True, issues.Count > 0 ? issues[0].Message : string.Empty);
                AssertLayoutEquivalent(legacySnapshot, exportedSnapshot);
            }
            finally
            {
                if (legacySnapshot != null)
                    Object.DestroyImmediate(legacySnapshot);
                if (exportedSnapshot != null)
                    Object.DestroyImmediate(exportedSnapshot);
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_RejectsStaleDocumentMutation()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            try
            {
                bool planned = StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan);
                Assert.That(planned, Is.True);

                document.StageId = 99;

                bool applied = StageMapLegacyImportUtility.TryApplyImportPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.False);
                Assert.That(error, Does.Contain("changed after import preview"));
            }
            finally
            {
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_RejectsStaleTilemapCellMutation()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            try
            {
                bool planned = StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan);
                Assert.That(planned, Is.True);

                legacy.RegionTilemap.SetTile(new Vector3Int(0, 0, 0), null);

                bool applied = StageMapLegacyImportUtility.TryApplyImportPlan(plan, saveAssets: false, out string error);

                Assert.That(applied, Is.False);
                Assert.That(error, Does.Contain("changed after import preview"));
            }
            finally
            {
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_PreviewsMetadataTargetsAndCatalogSettings()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var otherLayout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var otherDefinition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var otherCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            try
            {
                document.SchemaVersion = 1;
                document.DisplayName = "Different metadata";
                document.TargetLayout = otherLayout;
                document.TargetDefinition = otherDefinition;
                document.TargetCatalog = otherCatalog;
                document.PresentationCatalog = null;
                document.IncludeInCatalog = !legacy.Stage.IncludeInCatalog;
                document.EnabledInCatalog = !legacy.Stage.EnabledInCatalog;
                document.CatalogEntryKey = "different_key";

                Assert.That(StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan), Is.True);
                string[] fields = plan.Changes.Select(x => x.Field).ToArray();

                Assert.That(fields, Does.Contain(nameof(StageMapDocument.SchemaVersion)));
                Assert.That(fields, Does.Contain("StageMetadata"));
                Assert.That(fields, Does.Contain("GeneratedTargets"));
                Assert.That(fields, Does.Contain("CatalogSettings"));
            }
            finally
            {
                Object.DestroyImmediate(document);
                Object.DestroyImmediate(otherLayout);
                Object.DestroyImmediate(otherDefinition);
                Object.DestroyImmediate(otherCatalog);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_RejectsInvalidHazardMarkerSnapshotWithoutMutatingDocument()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var markerObject = new GameObject("invalid_hazard");
            markerObject.transform.SetParent(legacy.Stage.transform);
            markerObject.AddComponent<StageHazardActorMarker>();
            string before = StageMapApplyPlanner.ComputeSignature(document);
            try
            {
                bool planned = StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan);

                Assert.That(planned, Is.False);
                Assert.That(plan.ValidationIssues.Any(x => x.Code == "SMI920"), Is.True);
                Assert.That(StageMapApplyPlanner.ComputeSignature(document), Is.EqualTo(before));
            }
            finally
            {
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_RejectsStaleMarkerMutation()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            try
            {
                Assert.That(StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan), Is.True);

                legacy.Stage.EntryKey += "_changed";

                Assert.That(StageMapLegacyImportUtility.TryApplyImportPlan(plan, false, out string error), Is.False);
                Assert.That(error, Does.Contain("changed after import preview"));
            }
            finally
            {
                Object.DestroyImmediate(document);
                legacy.Dispose();
            }
        }

        [Test]
        public void LegacyImportPlan_RejectsStaleTargetReferenceMutation()
        {
            var legacy = CreateLegacyImportSetup();
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var replacementCatalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            replacementCatalog.name = "replacement_catalog_reference";
            try
            {
                Assert.That(StageMapLegacyImportUtility.TryBuildImportPlan(legacy.Stage, document, out var plan), Is.True);

                legacy.Stage.GetComponentInParent<StageLayoutRootMarker>().TargetStageCatalog = replacementCatalog;

                Assert.That(StageMapLegacyImportUtility.TryApplyImportPlan(plan, false, out string error), Is.False);
                Assert.That(error, Does.Contain("changed after import preview"));
            }
            finally
            {
                Object.DestroyImmediate(document);
                Object.DestroyImmediate(replacementCatalog);
                legacy.Dispose();
            }
        }

        private static List<ContentValidationIssue> Validate(StageMapDocument document)
        {
            var issues = new List<ContentValidationIssue>();
            StageMapDocumentValidationRules.ValidateDocument(document, "document", issues);
            return issues;
        }

        private static TestSetup CreateValidSetup()
        {
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();

            document.SchemaVersion = StageMapDocument.CurrentSchemaVersion;
            document.StageId = 7;
            document.DisplayName = "Stage 7";
            document.StageTimeLimitSec = 120f;
            document.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            document.Cells = new[]
            {
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, SourceRegionId = 100u },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, DepositRegionId = 200u },
            };
            document.SourceRegions = new[]
            {
                new StageMapRegionData
                {
                    StableId = 100u,
                    Active = true,
                    AnchorCell = new Vector2Int(0, 0),
                    AnchorOffset = Vector2.zero,
                }
            };
            document.DepositRegions = new[]
            {
                new StageMapRegionData
                {
                    StableId = 200u,
                    Active = true,
                    AnchorCell = new Vector2Int(1, 1),
                    AnchorOffset = Vector2.zero,
                }
            };
            document.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = new Vector2Int(1, 0),
                AnchorOffset = Vector2.zero,
                YawDeg = 0f,
            };
            document.HazardActorPlacements = System.Array.Empty<StageMapHazardActorPlacementData>();
            document.PresentationLinks = System.Array.Empty<StageMapPresentationLinkData>();
            document.TargetLayout = layout;
            document.TargetDefinition = definition;
            document.TargetCatalog = catalog;
            document.PresentationCatalog = LoadProjectPresentationCatalog();
            document.IncludeInCatalog = true;
            document.EnabledInCatalog = true;

            return new TestSetup(document, layout, definition, catalog);
        }

        private static LegacyImportSetup CreateLegacyImportSetup()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var gridGo = new GameObject("grid");
            var movementGo = new GameObject("movement_tilemap");
            var regionGo = new GameObject("region_tilemap");
            stageGo.transform.SetParent(rootGo.transform);
            gridGo.transform.SetParent(stageGo.transform);
            movementGo.transform.SetParent(gridGo.transform);
            regionGo.transform.SetParent(gridGo.transform);

            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = 3;
            definition.DisplayName = "Legacy Stage";
            definition.StageTimeLimitSec = 90f;

            var root = rootGo.AddComponent<StageLayoutRootMarker>();
            root.TargetStageCatalog = catalog;
            root.TargetPresentationCatalog = LoadProjectPresentationCatalog();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.StageId = 3;
            stage.EntryKey = "legacy_stage";
            stage.TargetLayout = layout;
            stage.TargetDefinition = definition;

            var grid = gridGo.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            var movementTilemap = AddTilemap(movementGo);
            var regionTilemap = AddTilemap(regionGo);

            var authoring = stageGo.AddComponent<StageGridAuthoring>();
            authoring.Grid = grid;
            authoring.MovementTilemap = movementTilemap;
            authoring.RegionTilemap = regionTilemap;
            authoring.BoundsMinCell = Vector2Int.zero;
            authoring.BoundsSize = new Vector2Int(2, 2);
            authoring.SourceRegionMappings = new[]
            {
                new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 1001u },
            };
            authoring.DepositRegionMappings = new[]
            {
                new StageRegionSlotMapping { RegionSlotIndex = 1, StableId = 2001u },
            };

            var sourceTile = ScriptableObject.CreateInstance<StageRegionTile>();
            sourceTile.RegionKind = StageRegionKind.Source;
            sourceTile.RegionSlotIndex = 1;
            var depositTile = ScriptableObject.CreateInstance<StageRegionTile>();
            depositTile.RegionKind = StageRegionKind.Deposit;
            depositTile.RegionSlotIndex = 1;
            var movementTile = ScriptableObject.CreateInstance<StageMovementTile>();
            movementTile.MovementFlags = StageCellMovementFlags.BlockBullet;
            regionTilemap.SetTile(new Vector3Int(0, 0, 0), sourceTile);
            regionTilemap.SetTile(new Vector3Int(1, 1, 0), depositTile);
            movementTilemap.SetTile(new Vector3Int(0, 1, 0), movementTile);

            CreateAnchor(stageGo.transform, StageRegionKind.Source, 1, new Vector2Int(0, 0));
            CreateAnchor(stageGo.transform, StageRegionKind.Deposit, 1, new Vector2Int(1, 1));
            CreatePlayerStart(stageGo.transform, new Vector2Int(1, 0));

            return new LegacyImportSetup(rootGo, catalog, layout, definition, sourceTile, depositTile, movementTile, stage, movementTilemap, regionTilemap);
        }

        private static Tilemap AddTilemap(GameObject go)
        {
            var tilemap = go.AddComponent<Tilemap>();
            go.AddComponent<TilemapRenderer>();
            return tilemap;
        }

        private static StagePresentationCatalogSO LoadProjectPresentationCatalog()
        {
            string[] guids = AssetDatabase.FindAssets("t:StagePresentationCatalogSO");
            Assert.That(guids, Has.Length.EqualTo(1));
            return AssetDatabase.LoadAssetAtPath<StagePresentationCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static void CreateAnchor(Transform parent, StageRegionKind kind, int slot, Vector2Int cell)
        {
            var go = new GameObject($"{kind}_anchor");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StageRegionAnchorMarker>();
            marker.RegionKind = kind;
            marker.RegionSlotIndex = slot;
            marker.AnchorCell = cell;
            marker.AnchorOffset = Vector2.zero;
            marker.Active = true;
        }

        private static void AssertLayoutEquivalent(StageLayoutSO expected, StageLayoutSO actual)
        {
            Assert.That(actual.SchemaVersion, Is.EqualTo(expected.SchemaVersion));
            Assert.That(actual.StageId, Is.EqualTo(expected.StageId));
            Assert.That(actual.Grid.Width, Is.EqualTo(expected.Grid.Width));
            Assert.That(actual.Grid.Height, Is.EqualTo(expected.Grid.Height));
            Assert.That(actual.Grid.CellSize, Is.EqualTo(expected.Grid.CellSize));
            Assert.That(actual.Grid.Origin, Is.EqualTo(expected.Grid.Origin));
            Assert.That(actual.Cells, Is.EqualTo(expected.Cells));
            Assert.That(actual.SourceRegions, Is.EqualTo(expected.SourceRegions));
            Assert.That(actual.DepositRegions, Is.EqualTo(expected.DepositRegions));
            Assert.That(actual.PlayerStart, Is.EqualTo(expected.PlayerStart));
            Assert.That(actual.Presentations, Is.EqualTo(expected.Presentations));
        }

        private static void CreatePlayerStart(Transform parent, Vector2Int cell)
        {
            var go = new GameObject("player_start");
            go.transform.SetParent(parent);
            var marker = go.AddComponent<StagePlayerStartMarker>();
            marker.Active = true;
            marker.AnchorCell = cell;
            marker.AnchorOffset = Vector2.zero;
        }

        private readonly struct TestSetup
        {
            public readonly StageMapDocument Document;
            public readonly StageLayoutSO Layout;
            public readonly StageDefinitionSO Definition;
            public readonly StageCatalogSO Catalog;

            public TestSetup(StageMapDocument document, StageLayoutSO layout, StageDefinitionSO definition, StageCatalogSO catalog)
            {
                Document = document;
                Layout = layout;
                Definition = definition;
                Catalog = catalog;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Document);
                Object.DestroyImmediate(Layout);
                Object.DestroyImmediate(Definition);
                Object.DestroyImmediate(Catalog);
            }
        }

        private readonly struct LegacyImportSetup
        {
            private readonly GameObject _root;
            private readonly Object[] _assets;

            public LegacyImportSetup(
                GameObject root,
                StageCatalogSO catalog,
                StageLayoutSO layout,
                StageDefinitionSO definition,
                Object sourceTile,
                Object depositTile,
                Object movementTile,
                StageLayoutStageMarker stage,
                Tilemap movementTilemap,
                Tilemap regionTilemap)
            {
                _root = root;
                _assets = new[] { catalog, layout, definition, sourceTile, depositTile, movementTile };
                Stage = stage;
                Catalog = catalog;
                Layout = layout;
                Definition = definition;
                MovementTilemap = movementTilemap;
                RegionTilemap = regionTilemap;
            }

            public StageLayoutStageMarker Stage { get; }
            public StageCatalogSO Catalog { get; }
            public StageLayoutSO Layout { get; }
            public StageDefinitionSO Definition { get; }
            public Tilemap MovementTilemap { get; }
            public Tilemap RegionTilemap { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
                for (int i = 0; i < _assets.Length; i++)
                    Object.DestroyImmediate(_assets[i]);
            }
        }
    }
}
