using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class StageMapEditorInteractionTests
    {
        [Test]
        public void HitTest_ResolvesEverySelectionKind()
        {
            using (var setup = CreateSetup())
            {
                var session = new StageMapEditingSession();
                session.Load(setup.Document);

                AssertKind(setup.Document, session, new Vector3(0.5f, 0f, 0.5f), StageMapSelectionKind.SourceAnchor);
                AssertKind(setup.Document, session, new Vector3(2.5f, 0f, 0.5f), StageMapSelectionKind.DepositAnchor);
                AssertKind(setup.Document, session, new Vector3(1.5f, 0f, 1.5f), StageMapSelectionKind.PlayerStart);
                AssertKind(setup.Document, session, new Vector3(0.5f, 0f, 2.5f), StageMapSelectionKind.HazardActor);
                AssertKind(setup.Document, session, new Vector3(2.5f, 0f, 2.5f), StageMapSelectionKind.Presentation);

                session.ShowAnchorLayer = false;
                session.ShowPlayerStartLayer = false;
                session.ShowHazardActorLayer = false;
                session.ShowPresentationLayer = false;
                session.SelectedLayer = StageMapEditorLayer.Source;
                AssertKind(setup.Document, session, new Vector3(1.5f, 0f, 0.5f), StageMapSelectionKind.SourceRegion);
                session.SelectedLayer = StageMapEditorLayer.Deposit;
                AssertKind(setup.Document, session, new Vector3(1.5f, 0f, 2.5f), StageMapSelectionKind.DepositRegion);
                session.SelectedLayer = StageMapEditorLayer.Grid;
                AssertKind(setup.Document, session, new Vector3(2.5f, 0f, 1.5f), StageMapSelectionKind.Cell);
            }
        }

        [Test]
        public void HitTest_HiddenHazardAndPresentation_AreExcluded()
        {
            using (var setup = CreateSetup())
            {
                var session = new StageMapEditingSession();
                session.Load(setup.Document);
                session.ShowHazardActorLayer = false;
                session.ShowPresentationLayer = false;

                Assert.That(StageMapSelectionUtility.TryHitTest(
                    setup.Document,
                    session,
                    new Vector3(0.5f, 0f, 2.5f),
                    0.2f,
                    out var hazardSelection), Is.True);
                Assert.That(hazardSelection.Kind, Is.Not.EqualTo(StageMapSelectionKind.HazardActor));

                Assert.That(StageMapSelectionUtility.TryHitTest(
                    setup.Document,
                    session,
                    new Vector3(2.5f, 0f, 2.5f),
                    0.2f,
                    out var presentationSelection), Is.True);
                Assert.That(presentationSelection.Kind, Is.Not.EqualTo(StageMapSelectionKind.Presentation));
            }
        }

        [Test]
        public void CoordinateRoundTrip_PreservesAnchorPlayerHazardAndPresentationWorldPoses()
        {
            using (var setup = CreateSetup())
            {
                var session = new StageMapEditingSession();
                session.Load(setup.Document);
                Vector3 anchorWorld = new Vector3(0.7f, 0f, 0.3f);
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(session, StageRegionKind.Source, 100u, anchorWorld, out _), Is.True);
                Assert.That(StageMapSelectionUtility.GetRegionAnchorWorld(setup.Document, StageRegionKind.Source, 100u), Is.EqualTo(anchorWorld).Using(Vector3ComparerWithEqualsOperator.Instance));

                Vector3 playerWorld = new Vector3(1.25f, 0f, 1.75f);
                Assert.That(StageMapEditorMutationUtility.TryPlacePlayerStart(session, playerWorld, 35f, out _), Is.True);
                Assert.That(StageMapSelectionUtility.GetPlayerStartWorld(setup.Document), Is.EqualTo(playerWorld).Using(Vector3ComparerWithEqualsOperator.Instance));

                Vector3 hazardWorld = anchorWorld + new Vector3(0.2f, 0f, 1.1f);
                setup.Document.HazardActorPlacements[0].SourceLocalOffset = StageMapSelectionUtility.ToSourceLocal(setup.Document, 100u, hazardWorld);
                Assert.That(StageMapSelectionUtility.GetHazardActorWorld(setup.Document, 0), Is.EqualTo(hazardWorld).Using(Vector3ComparerWithEqualsOperator.Instance));

                var standalone = setup.Document.PresentationLinks[0];
                Vector3 standaloneWorld = new Vector3(2.2f, 0f, 2.1f);
                standalone.Position = StageMapSelectionUtility.ToPresentationLocal(setup.Document, standalone, standaloneWorld);
                setup.Document.PresentationLinks[0] = standalone;
                Assert.That(StageMapSelectionUtility.GetPresentationWorld(setup.Document, 0), Is.EqualTo(standaloneWorld).Using(Vector3ComparerWithEqualsOperator.Instance));

                var links = setup.Document.PresentationLinks;
                Array.Resize(ref links, 2);
                links[1] = new StageMapPresentationLinkData
                {
                    StableId = 901u,
                    Active = true,
                    PlacementMode = StagePresentationPlacementMode.LinkedToParent,
                    LinkKind = StagePresentationLinkKind.Source,
                    LinkedStableId = 100u,
                    Scale = Vector3.one,
                };
                setup.Document.PresentationLinks = links;
                var linked = setup.Document.PresentationLinks[1];
                Vector3 linkedWorld = anchorWorld + new Vector3(0.4f, 0f, 0.6f);
                linked.Position = StageMapSelectionUtility.ToPresentationLocal(setup.Document, linked, linkedWorld);
                setup.Document.PresentationLinks[1] = linked;
                Assert.That(StageMapSelectionUtility.GetPresentationWorld(setup.Document, 1), Is.EqualTo(linkedWorld).Using(Vector3ComparerWithEqualsOperator.Instance));
            }
        }

        [Test]
        public void LockedLayers_RejectPaintPlaceMoveAndDelete()
        {
            using (var setup = CreateSetup())
            {
                var session = new StageMapEditingSession();
                session.Load(setup.Document);
                session.LockMovementLayer = true;
                session.LockSourceLayer = true;
                session.LockAnchorLayer = true;
                session.LockPlayerStartLayer = true;
                session.LockHazardActorLayer = true;
                session.LockPresentationLayer = true;

                StageCellLayoutData before = setup.Document.Cells[0];
                Assert.That(StageMapEditorMutationUtility.TryPaintMovement(session, Vector2Int.zero, StageCellMovementFlags.BlockPlayer, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryPaintRegion(session, Vector2Int.zero, StageRegionKind.Source, 999u, out _), Is.False);
                Assert.That(setup.Document.Cells[0].MovementFlags, Is.EqualTo(before.MovementFlags));
                Assert.That(setup.Document.Cells[0].SourceRegionId, Is.EqualTo(before.SourceRegionId));
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(session, StageRegionKind.Source, 100u, Vector3.one, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryPlacePlayerStart(session, Vector3.one, 0f, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryPlaceHazardActor(session, 100u, setup.Prefab, Vector3.one, 0f, out _, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryPlacePresentation(session, 999u, "key", StagePresentationPlacementMode.Standalone, StagePresentationLinkKind.None, 0u, Vector3.one, Vector3.zero, Vector3.one, out _), Is.False);

                session.Select(StageMapSelection.ForHazard(100u, 1));
                Vector3 hazardBefore = setup.Document.HazardActorPlacements[0].SourceLocalOffset;
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, Vector3.zero, 90f, Vector3.zero, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryDeleteSelection(session, out _), Is.False);
                Assert.That(setup.Document.HazardActorPlacements, Has.Length.EqualTo(1));
                Assert.That(setup.Document.HazardActorPlacements[0].SourceLocalOffset, Is.EqualTo(hazardBefore));

                session.Select(StageMapSelection.ForPresentation(900u));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(session, Vector3.zero, 0f, Vector3.one, out _), Is.False);
                Assert.That(StageMapEditorMutationUtility.TryDeleteSelection(session, out _), Is.False);
                Assert.That(setup.Document.PresentationLinks, Has.Length.EqualTo(1));
            }
        }

        [Test]
        public void IssueMapper_MapsAndNavigatesEveryTargetKind()
        {
            using (var setup = CreateSetup())
            {
                var raw = new List<ContentValidationIssue>
                {
                    Issue("CELL", "document/Cells(x=1, y=0)"),
                    Issue("REGION", "document/SourceRegions[0]"),
                    Issue("REGION", "document/DepositRegions[0]"),
                    Issue("STG009", "document/SourceRegions[0]"),
                    Issue("STG010", "document/DepositRegions[0]"),
                    Issue("PLAYER", "document/PlayerStart"),
                    Issue("HAZARD", "document/HazardActorPlacements[0]"),
                    Issue("RULE", "document/HazardActorOrchestrationRules[0]"),
                    Issue("PRESENTATION", "document/PresentationLinks[0]"),
                    Issue("SMD900", "document"),
                    Issue("SMD901", "document"),
                    Issue("SMD902", "document"),
                    Issue("SMD903", "document"),
                    Issue("GLOBAL", "document"),
                };
                var mapped = new List<StageMapDocumentIssue>();

                StageMapDocumentIssueMapper.Map(setup.Document, raw, mapped);

                var expected = new[]
                {
                    StageMapIssueTargetKind.Cell,
                    StageMapIssueTargetKind.SourceRegion,
                    StageMapIssueTargetKind.DepositRegion,
                    StageMapIssueTargetKind.SourceAnchor,
                    StageMapIssueTargetKind.DepositAnchor,
                    StageMapIssueTargetKind.PlayerStart,
                    StageMapIssueTargetKind.HazardActor,
                    StageMapIssueTargetKind.HazardActorRule,
                    StageMapIssueTargetKind.Presentation,
                    StageMapIssueTargetKind.TargetLayout,
                    StageMapIssueTargetKind.TargetDefinition,
                    StageMapIssueTargetKind.TargetCatalog,
                    StageMapIssueTargetKind.PresentationCatalog,
                    StageMapIssueTargetKind.Document,
                };
                Assert.That(mapped.Select(x => x.Target.Kind), Is.EqualTo(expected));
                for (int i = 0; i < mapped.Count; i++)
                {
                    Assert.That(StageMapIssueNavigationUtility.TryResolve(
                        setup.Document,
                        mapped[i].Target,
                        out _,
                        out _,
                        out _), Is.True, $"Navigation failed for {mapped[i].Target.Kind}");
                }
            }
        }

        [Test]
        public void QuickFixPreview_CancelIsNoOp_AndApplyCanBeUndone()
        {
            using (var setup = CreateSetup())
            {
                setup.Document.Cells = new[] { new StageCellLayoutData { SourceRegionId = 100u } };
                var issue = Issue("STG003", "document");
                string before = EditorJsonUtility.ToJson(setup.Document);

                Assert.That(StageMapDocumentFixUtility.TryBuildFixPreview(setup.Document, issue, out _), Is.True);
                Assert.That(EditorJsonUtility.ToJson(setup.Document), Is.EqualTo(before));

                Undo.RecordObject(setup.Document, "Apply Test Stage Map Fix");
                Assert.That(StageMapDocumentFixUtility.ApplyFix(setup.Document, issue), Is.True);
                Assert.That(setup.Document.Cells, Has.Length.EqualTo(9));
                Undo.PerformUndo();
                Assert.That(setup.Document.Cells, Has.Length.EqualTo(1));
                Assert.That(setup.Document.Cells[0].SourceRegionId, Is.EqualTo(100u));
            }
        }

        [Test]
        public void SelectionReconcile_TracksStableIdentityAndClearsDeletedElement()
        {
            using (var setup = CreateSetup())
            {
                var session = new StageMapEditingSession();
                session.Load(setup.Document);
                int placementId = setup.Document.HazardActorPlacements[0].PlacementInstanceId;
                session.Select(StageMapSelection.ForHazard(100u, placementId));

                var first = setup.Document.HazardActorPlacements[0];
                var inserted = first;
                inserted.PlacementInstanceId = placementId + 100;
                setup.Document.HazardActorPlacements = new[] { inserted, first };
                session.ReconcileSelection(setup.Document);

                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.HazardActor));
                Assert.That(StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    setup.Document.HazardActorPlacements,
                    session.Selection.OwningSourceStableId,
                    session.Selection.PlacementInstanceId,
                    out int resolvedIndex), Is.True);
                Assert.That(resolvedIndex, Is.EqualTo(1));

                setup.Document.HazardActorPlacements = new[] { inserted };
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
            }
        }

        [Test]
        public void SelectionReconcile_CellCoordinateSurvivesWidthChangeAndClearsWhenCropped()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                session.Load(setup.Document);
                var selectedCell = new Vector2Int(2, 1);
                session.Select(StageMapSelection.ForCell(selectedCell));

                StageGridSpec expanded = setup.Document.Grid;
                expanded.Width = 4;
                var expandPlan = StageMapGridResizeUtility.BuildPreview(setup.Document, expanded);
                Assert.That(StageMapGridResizeUtility.TryApply(expandPlan, true, out string expandError), Is.True, expandError);
                session.ReconcileSelection(setup.Document);

                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.Cell));
                Assert.That(session.Selection.Cell, Is.EqualTo(selectedCell));
                Assert.That(StageMapDocumentCommandUtility.TryGetCellIndex(setup.Document, selectedCell, out int expandedIndex), Is.True);
                Assert.That(expandedIndex, Is.EqualTo(6));

                StageGridSpec cropped = setup.Document.Grid;
                cropped.Width = 2;
                var cropPlan = StageMapGridResizeUtility.BuildPreview(setup.Document, cropped);
                Assert.That(StageMapGridResizeUtility.TryApply(cropPlan, true, out string cropError), Is.True, cropError);
                session.ReconcileSelection(setup.Document);

                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
            }
        }

        [Test]
        public void SelectionReconcile_HazardCompositeIdentityDistinguishesSamePlacementIdAcrossSources()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                var first = setup.Document.HazardActorPlacements[0];
                var second = first;
                second.OwningSourceStableId = 200u;
                setup.Document.HazardActorPlacements = new[] { first, second };
                session.Load(setup.Document);
                session.Select(StageMapSelection.ForHazard(200u, first.PlacementInstanceId));

                setup.Document.HazardActorPlacements = new[] { second, first };
                session.ReconcileSelection(setup.Document);

                Assert.That(session.Selection.OwningSourceStableId, Is.EqualTo(200u));
                Assert.That(session.Selection.PlacementInstanceId, Is.EqualTo(first.PlacementInstanceId));
                Assert.That(StageMapSelectionUtility.TryFindUniqueHazardIndex(
                    setup.Document.HazardActorPlacements,
                    200u,
                    first.PlacementInstanceId,
                    out int index), Is.True);
                Assert.That(index, Is.EqualTo(0));

                setup.Document.HazardActorPlacements = new[] { first };
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
            }
        }

        [Test]
        public void SelectionReconcile_PresentationAndRegionUseStableIdentityAcrossReorderAndDelete()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                var selectedLink = setup.Document.PresentationLinks[0];
                var insertedLink = selectedLink;
                insertedLink.StableId = selectedLink.StableId + 1u;
                setup.Document.PresentationLinks = new[] { insertedLink, selectedLink };
                session.Load(setup.Document);
                session.Select(StageMapSelection.ForPresentation(selectedLink.StableId));
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection, Is.EqualTo(StageMapSelection.ForPresentation(selectedLink.StableId)));

                setup.Document.PresentationLinks = new[] { insertedLink };
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));

                var selectedRegion = setup.Document.SourceRegions[0];
                var insertedRegion = selectedRegion;
                insertedRegion.StableId = selectedRegion.StableId + 1u;
                setup.Document.SourceRegions = new[] { insertedRegion, selectedRegion };
                session.Select(StageMapSelection.ForAnchor(StageRegionKind.Source, selectedRegion.StableId));
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection.StableId, Is.EqualTo(selectedRegion.StableId));

                setup.Document.SourceRegions = new[] { insertedRegion };
                session.ReconcileSelection(setup.Document);
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.None));
            }
        }

        [Test]
        public void InspectorSection_IsExactlyTheCurrentLogicalSelectionKind()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                session.Load(setup.Document);
                var cases = new[]
                {
                    (StageMapSelection.None, StageMapInspectorSection.None),
                    (StageMapSelection.ForCell(Vector2Int.zero), StageMapInspectorSection.Cell),
                    (StageMapSelection.ForRegion(StageRegionKind.Source, 100u), StageMapInspectorSection.RegionOrAnchor),
                    (StageMapSelection.ForAnchor(StageRegionKind.Deposit, 200u), StageMapInspectorSection.RegionOrAnchor),
                    (StageMapSelection.ForPlayerStart(), StageMapInspectorSection.PlayerStart),
                    (StageMapSelection.ForHazard(100u, 1), StageMapInspectorSection.HazardActor),
                    (StageMapSelection.ForHazardRule(100u, 1), StageMapInspectorSection.HazardActorRule),
                    (StageMapSelection.ForPresentation(900u), StageMapInspectorSection.Presentation),
                    (StageMapSelection.ForDocument(setup.Document), StageMapInspectorSection.Document),
                    (StageMapSelection.ForTargetAsset(setup.Layout), StageMapInspectorSection.TargetAsset),
                };

                for (int i = 0; i < cases.Length; i++)
                {
                    session.Select(cases[i].Item1);
                    Assert.That(session.GetInspectorSection(), Is.EqualTo(cases[i].Item2));
                }
            }
        }

        [Test]
        public void EncounterSource_FollowsSelectionUnlessPinned()
        {
            using (var setup = CreateSetup())
            {
                var sources = setup.Document.SourceRegions;
                Array.Resize(ref sources, 2);
                sources[1] = new StageMapRegionData
                {
                    StableId = 300u,
                    Active = true,
                    AnchorCell = new Vector2Int(2, 2),
                };
                setup.Document.SourceRegions = sources;

                var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Encounter Source Test", focus: false);
                try
                {
                    window.LoadDocument(setup.Document);
                    Assert.That(window.TrySelect(StageMapSelection.ForRegion(StageRegionKind.Source, 300u), frame: false), Is.True);
                    Assert.That(window.ResolveEncounterSourceIdForTests(), Is.EqualTo(300u));

                    window.Session.PinHazardEncounterSource = true;
                    window.Session.PinnedHazardEncounterSourceStableId = 100u;
                    Assert.That(window.TrySelect(StageMapSelection.ForRegion(StageRegionKind.Source, 300u), frame: false), Is.True);
                    Assert.That(window.ResolveEncounterSourceIdForTests(), Is.EqualTo(100u));

                    window.Session.PinHazardEncounterSource = false;
                    Assert.That(window.ResolveEncounterSourceIdForTests(), Is.EqualTo(300u));
                }
                finally
                {
                    HazardActorPreviewCoordinator.Shutdown();
                    window.Close();
                }
            }
        }

        [Test]
        public void EncounterRuleProgress_CommandClampsAndPreservesRuleIdentity()
        {
            using (var setup = CreateSetup())
            {
                var window = EditorWindow.GetWindow<StageMapEditorWindow>(utility: true, title: "Stage Map Editor Rule Progress Test", focus: false);
                try
                {
                    window.LoadDocument(setup.Document);
                    Assert.That(window.TrySetEncounterRuleProgress(100u, 1, 0.75f), Is.False);

                    Assert.That(StageMapHazardActorOrchestrationUtility.AddRule(
                        setup.Document,
                        100u,
                        HazardActorOrchestrationActionId.PhaseSet,
                        1,
                        out int progressRuleId), Is.True);

                    Assert.That(window.TrySetEncounterRuleProgress(100u, progressRuleId, 0.75f), Is.True);
                    var rule = setup.Document.HazardActorOrchestrationRules.Single(x => x.RuleId == progressRuleId);
                    Assert.That(rule.OwningSourceStableId, Is.EqualTo(100u));
                    Assert.That(rule.TriggerThresholdNormalized, Is.EqualTo(0.75f).Within(0.0001f));

                    Assert.That(window.TrySetEncounterRuleProgress(100u, progressRuleId, 2f), Is.True);
                    rule = setup.Document.HazardActorOrchestrationRules.Single(x => x.RuleId == progressRuleId);
                    Assert.That(rule.TriggerThresholdNormalized, Is.EqualTo(1f).Within(0.0001f));
                }
                finally
                {
                    window.Close();
                }
            }
        }

        [Test]
        public void AnchorMutation_RequiresOwnerAndAnchorLayersButSelectionRemainsAvailable()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                session.Load(setup.Document);
                session.Select(StageMapSelection.ForAnchor(StageRegionKind.Source, 100u));

                session.LockSourceLayer = true;
                session.LockAnchorLayer = false;
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(
                    session, StageRegionKind.Source, 100u, new Vector3(0.6f, 0f, 0.6f), out _), Is.False);
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.SourceAnchor));

                session.LockSourceLayer = false;
                session.LockAnchorLayer = true;
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(
                    session, new Vector3(0.7f, 0f, 0.7f), 0f, Vector3.zero, out _), Is.False);

                session.LockAnchorLayer = false;
                session.LockDepositLayer = true;
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(
                    session, StageRegionKind.Deposit, 200u, new Vector3(2.4f, 0f, 0.4f), out _), Is.False);
                session.LockDepositLayer = false;
                session.LockAnchorLayer = true;
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(
                    session, StageRegionKind.Deposit, 200u, new Vector3(2.4f, 0f, 0.4f), out _), Is.False);
            }
        }

        [Test]
        public void HiddenSelection_RemainsNavigableButHasNoSceneHandlePolicy()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                session.Load(setup.Document);
                session.ShowAnchorLayer = false;
                session.Select(StageMapSelection.ForAnchor(StageRegionKind.Source, 100u));
                session.ReconcileSelection(setup.Document);

                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.SourceAnchor));
                Assert.That(StageMapEditingPolicy.IsSelectionVisible(session, session.Selection), Is.False);
                Assert.That(StageMapEditingPolicy.CanMutateSelection(session, session.Selection, out _), Is.True);
            }
        }

        [Test]
        public void CenterOffsetLocks_AreIndependentUndoableAndEnforcedForPlaceAndMove()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                setup.Document.SourceRegions[0].AnchorOffset = new Vector2(0.2f, -0.15f);
                setup.Document.PlayerStart.AnchorOffset = new Vector2(-0.25f, 0.1f);
                session.Load(setup.Document);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.Not.EqualTo(Vector2.zero));
                Assert.That(setup.Document.PlayerStart.AnchorOffset, Is.Not.EqualTo(Vector2.zero));

                session.Select(StageMapSelection.ForAnchor(StageRegionKind.Source, 100u));
                Undo.RecordObjects(new[] { setup.Document, session.UndoTarget }, "Enable Region Center Lock");
                Assert.That(StageMapEditorMutationUtility.TrySetCenterRegionAnchors(session, true, out _), Is.True);
                Assert.That(session.CenterRegionAnchors, Is.True);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(session.CenterPlayerStart, Is.False);
                Assert.That(setup.Document.PlayerStart.AnchorOffset, Is.Not.EqualTo(Vector2.zero));

                Undo.PerformUndo();
                session.ReconcileSelection(setup.Document);
                Assert.That(session.CenterRegionAnchors, Is.False);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.EqualTo(new Vector2(0.2f, -0.15f)));
                Assert.That(session.Selection.Kind, Is.EqualTo(StageMapSelectionKind.SourceAnchor));

                Undo.PerformRedo();
                session.ReconcileSelection(setup.Document);
                Assert.That(session.CenterRegionAnchors, Is.True);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(StageMapEditorMutationUtility.TryPlaceAnchor(
                    session, StageRegionKind.Source, 100u, new Vector3(1.7f, 0f, 1.3f), out _), Is.True);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(
                    session, new Vector3(2.7f, 0f, 2.2f), 0f, Vector3.zero, out _), Is.True);
                Assert.That(setup.Document.SourceRegions[0].AnchorOffset, Is.EqualTo(Vector2.zero));

                session.Select(StageMapSelection.ForPlayerStart());
                Undo.RecordObjects(new[] { setup.Document, session.UndoTarget }, "Enable Player Center Lock");
                Assert.That(StageMapEditorMutationUtility.TrySetCenterPlayerStart(session, true, out _), Is.True);
                Assert.That(session.CenterPlayerStart, Is.True);
                Assert.That(setup.Document.PlayerStart.AnchorOffset, Is.EqualTo(Vector2.zero));
                Assert.That(StageMapEditorMutationUtility.TryMoveSelection(
                    session, new Vector3(2.8f, 0f, 1.2f), 80f, Vector3.zero, out _), Is.True);
                Assert.That(setup.Document.PlayerStart.AnchorOffset, Is.EqualTo(Vector2.zero));
            }
        }

        [Test]
        public void QuickFix_RespectsCompositeAnchorLocks()
        {
            using (var setup = CreateSetup())
            using (var session = new StageMapEditingSession())
            {
                session.Load(setup.Document);
                var issue = Issue("STG009", "document/SourceRegions[0]");
                StageMapIssueTarget target = StageMapDocumentIssueMapper.ResolveTarget(setup.Document, issue);

                session.LockSourceLayer = true;
                Assert.That(StageMapEditorMutationUtility.CanApplyFix(session, issue, target, out _), Is.False);
                session.LockSourceLayer = false;
                session.LockAnchorLayer = true;
                Assert.That(StageMapEditorMutationUtility.CanApplyFix(session, issue, target, out _), Is.False);
                session.LockAnchorLayer = false;
                Assert.That(StageMapEditorMutationUtility.CanApplyFix(session, issue, target, out _), Is.True);
            }
        }

        private static ContentValidationIssue Issue(string code, string location)
        {
            return new ContentValidationIssue(ContentValidationSeverity.Error, code, location, "test issue");
        }

        private static void AssertKind(
            StageMapDocument document,
            StageMapEditingSession session,
            Vector3 world,
            StageMapSelectionKind expected)
        {
            Assert.That(StageMapSelectionUtility.TryHitTest(document, session, world, 0.2f, out var selection), Is.True);
            Assert.That(selection.Kind, Is.EqualTo(expected));
        }

        private static Setup CreateSetup()
        {
            var document = ScriptableObject.CreateInstance<StageMapDocument>();
            var prefab = new GameObject("hazard_prefab");
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            var presentationCatalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            document.Grid = new StageGridSpec { Width = 3, Height = 3, CellSize = 1f, Origin = Vector3.zero };
            document.Cells = new StageCellLayoutData[9];
            document.Cells[1].SourceRegionId = 100u;
            document.Cells[7].DepositRegionId = 200u;
            document.SourceRegions = new[]
            {
                new StageMapRegionData { StableId = 100u, Active = true, AnchorCell = new Vector2Int(0, 0) },
            };
            document.DepositRegions = new[]
            {
                new StageMapRegionData { StableId = 200u, Active = true, AnchorCell = new Vector2Int(2, 0) },
            };
            document.PlayerStart = new StagePlayerStartLayoutData { Active = true, AnchorCell = new Vector2Int(1, 1) };
            document.HazardActorPlacements = new[]
            {
                new StageMapHazardActorPlacementData
                {
                    PlacementInstanceId = 1,
                    OwningSourceStableId = 100u,
                    ActorArchetypePrefab = prefab,
                    SourceLocalOffset = new Vector3(0f, 0f, 2f),
                }
            };
            document.HazardActorOrchestrationRules = new[]
            {
                new StageMapHazardActorOrchestrationRuleData
                {
                    OwningSourceStableId = 100u,
                    RuleId = 1,
                    TargetPlacementInstanceIds = new[] { 1 },
                    ActionType = HazardActorOrchestrationActionId.Spawn,
                    TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    TargetPhaseId = 1,
                }
            };
            document.PresentationLinks = new[]
            {
                new StageMapPresentationLinkData
                {
                    StableId = 900u,
                    Active = true,
                    PlacementMode = StagePresentationPlacementMode.Standalone,
                    Position = new Vector3(2.5f, 0f, 2.5f),
                    Scale = Vector3.one,
                }
            };
            document.TargetLayout = layout;
            document.TargetDefinition = definition;
            document.TargetCatalog = catalog;
            document.PresentationCatalog = presentationCatalog;
            return new Setup(document, prefab, layout, definition, catalog, presentationCatalog);
        }

        private readonly struct Setup : IDisposable
        {
            public Setup(
                StageMapDocument document,
                GameObject prefab,
                StageLayoutSO layout,
                StageDefinitionSO definition,
                StageCatalogSO catalog,
                StagePresentationCatalogSO presentationCatalog)
            {
                Document = document;
                Prefab = prefab;
                Layout = layout;
                Definition = definition;
                Catalog = catalog;
                PresentationCatalog = presentationCatalog;
            }

            public StageMapDocument Document { get; }
            public GameObject Prefab { get; }
            public StageLayoutSO Layout { get; }
            public StageDefinitionSO Definition { get; }
            public StageCatalogSO Catalog { get; }
            public StagePresentationCatalogSO PresentationCatalog { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Document);
                UnityEngine.Object.DestroyImmediate(Prefab);
                UnityEngine.Object.DestroyImmediate(Layout);
                UnityEngine.Object.DestroyImmediate(Definition);
                UnityEngine.Object.DestroyImmediate(Catalog);
                UnityEngine.Object.DestroyImmediate(PresentationCatalog);
            }
        }
    }
}
