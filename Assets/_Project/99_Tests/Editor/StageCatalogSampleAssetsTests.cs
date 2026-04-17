using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageCatalogSampleAssetsTests
    {
        private const string StageCatalogPath = "Assets/_Project/03_Datas/StageCatalog/sc_demo.asset";
        private const string StageDefinition1Path = "Assets/_Project/03_Datas/StageCatalog/sd_demo_1.asset";
        private const string StageDefinition2Path = "Assets/_Project/03_Datas/StageCatalog/sd_demo_2.asset";
        private const string StageDefinition3Path = "Assets/_Project/03_Datas/StageCatalog/sd_demo_3.asset";
        private const string StageLayout1Path = "Assets/_Project/03_Datas/StageCatalog/sl_demo_1.asset";
        private const string StageLayout2Path = "Assets/_Project/03_Datas/StageCatalog/sl_demo_2.asset";
        private const string StageLayout3Path = "Assets/_Project/03_Datas/StageCatalog/sl_demo_3.asset";
        private const string SampleScenePath = "Assets/_Project/01_Scenes/StageLayoutEditingSampleV1.unity";
        private const string TestStageCatalogPath = "Assets/_Project/99_Tests/TestData/StageCatalog/sc_test_sample_verification.asset";
        private const string TestHazardSourceTemplatePrefabPath = "Assets/_Project/99_Tests/TestData/pf_test_hazard_actor_source_template.prefab";
        private const string OperationalHazardSourceTemplatePrefabPath = "Assets/_Project/04_Prefabs/StageTopology/pf_stage_source_template.prefab";
        private const string TestHazardArchetypePrefabPath = "Assets/_Project/99_Tests/TestData/pf_test_hazard_actor_archetype.prefab";
        private const string OperationalHazardArchetypePrefabPath = "Assets/_Project/04_Prefabs/StageTopology/pf_stage_hazard_actor_archetype.prefab";
        private static readonly string[] WaveClipSearchRoots =
        {
            "Assets/_Project/03_Datas/WaveClips",
            "Assets/_Project/99_Tests/TestData/WaveClips",
        };
        private static readonly string[] DeprecatedPaintAssetPaths =
        {
            "Assets/_Project/03_Datas/StageCatalog/srp_stage1_source.asset",
            "Assets/_Project/03_Datas/StageCatalog/srp_stage1_deposit.asset",
            "Assets/_Project/03_Datas/StageCatalog/srp_stage2_source.asset",
            "Assets/_Project/03_Datas/StageCatalog/srp_stage2_deposit.asset",
            "Assets/_Project/03_Datas/StageCatalog/srp_stage3_source.asset",
            "Assets/_Project/03_Datas/StageCatalog/srp_stage3_deposit.asset",
        };

        [Test]
        public void DemoStageCatalog_ContainsThreeEnabledEntriesInOrder()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(StageCatalogPath);
            Assert.That(catalog, Is.Not.Null, "sc_demo.asset must exist.");
            Assert.That(catalog.Entries, Is.Not.Null);
            Assert.That(catalog.Entries.Length, Is.EqualTo(3));

            for (int i = 0; i < catalog.Entries.Length; i++)
            {
                var entry = catalog.Entries[i];
                Assert.That(entry.Enabled, Is.True, $"Entry[{i}] must be enabled.");
                Assert.That(entry.Definition, Is.Not.Null, $"Entry[{i}] definition must exist.");
                Assert.That(entry.Layout, Is.Not.Null, $"Entry[{i}] layout must exist.");
                Assert.That(entry.Definition.StageId, Is.EqualTo(i + 1), $"Entry[{i}] definition stage id mismatch.");
                Assert.That(entry.Layout.StageId, Is.EqualTo(i + 1), $"Entry[{i}] layout stage id mismatch.");
            }
        }

        [Test]
        public void DemoStageAssets_DefinitionsAndLayouts_ArePresentAndPopulated()
        {
            var definition1 = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(StageDefinition1Path);
            var definition2 = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(StageDefinition2Path);
            var definition3 = AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(StageDefinition3Path);
            var layout1 = AssetDatabase.LoadAssetAtPath<StageLayoutSO>(StageLayout1Path);
            var layout2 = AssetDatabase.LoadAssetAtPath<StageLayoutSO>(StageLayout2Path);
            var layout3 = AssetDatabase.LoadAssetAtPath<StageLayoutSO>(StageLayout3Path);

            Assert.That(definition1, Is.Not.Null);
            Assert.That(definition2, Is.Not.Null);
            Assert.That(definition3, Is.Not.Null);
            Assert.That(layout1, Is.Not.Null);
            Assert.That(layout2, Is.Not.Null);
            Assert.That(layout3, Is.Not.Null);

            Assert.That(definition1.SourceBindings, Is.Not.Null.And.Length.GreaterThan(0));
            Assert.That(definition2.SourceBindings, Is.Not.Null.And.Length.GreaterThan(0));
            Assert.That(definition3.SourceBindings, Is.Not.Null.And.Length.GreaterThan(0));

            AssertLayoutPopulated(layout1);
            AssertLayoutPopulated(layout2);
            AssertLayoutPopulated(layout3);

            AssertLayoutMatchesSceneAuthoring(layout1, 1);
            AssertLayoutMatchesSceneAuthoring(layout2, 2);
            AssertLayoutMatchesSceneAuthoring(layout3, 3);

            for (int i = 0; i < DeprecatedPaintAssetPaths.Length; i++)
                Assert.That(AssetDatabase.LoadMainAssetAtPath(DeprecatedPaintAssetPaths[i]), Is.Null, $"Deprecated paint asset must be removed: {DeprecatedPaintAssetPaths[i]}");
        }

        [Test]
        public void DemoStageCatalog_PassesValidationRules()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(StageCatalogPath);
            Assert.That(catalog, Is.Not.Null);

            var issues = new List<ContentValidationIssue>();
            StageCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<StageCatalogSO>(catalog, StageCatalogPath)
                },
                issues);

            Assert.That(issues, Is.Empty, "sc_demo.asset must satisfy StageCatalog validation rules.");
        }

        [Test]
        public void SampleVerificationStageCatalog_PassesValidationRules()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalogSO>(TestStageCatalogPath);
            Assert.That(catalog, Is.Not.Null);

            var issues = new List<ContentValidationIssue>();
            StageCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<StageCatalogSO>(catalog, TestStageCatalogPath)
                },
                issues);

            Assert.That(issues, Is.Empty, "sc_test_sample_verification.asset must satisfy StageCatalog validation rules.");
        }

        [Test]
        public void HazardActorAssets_SatisfyStandaloneContractAndActorFreeSources()
        {
            ValidateActorFreeSourceTemplatePrefab(TestHazardSourceTemplatePrefabPath);
            ValidateActorFreeSourceTemplatePrefab(OperationalHazardSourceTemplatePrefabPath);
            ValidateHazardActorArchetypePrefabContract(TestHazardArchetypePrefabPath);
            ValidateHazardActorArchetypePrefabContract(OperationalHazardArchetypePrefabPath);
        }

        [Test]
        public void WaveClipAssets_UseCanonicalTypedAuthoringOnly()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WaveClipSO)}", WaveClipSearchRoots);
            Assert.That(guids.Length, Is.GreaterThan(0));

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<WaveClipSO>(path);
                Assert.That(clip, Is.Not.Null, path);

                var segments = clip.Segments ?? Array.Empty<WaveClipSO.ClipSegment>();
                for (int s = 0; s < segments.Length; s++)
                {
                    var segment = segments[s];
                    int typedCount = segment.Directives?.Length ?? 0;
                    Assert.That(typedCount, Is.GreaterThan(0), $"{path} segment[{s}] must keep typed directives.");

                    for (int d = 0; d < typedCount; d++)
                    {
                        var directive = segment.Directives[d];
                        Assert.That(directive.Payload.Bullet, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Payload.Bullet.");
                        Assert.That(directive.Emission, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Emission.");
                        Assert.That(directive.Sampling, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.");
                        Assert.That(directive.Sampling.Anchor, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.Anchor.");
                        Assert.That(directive.Sampling.AreaSampler, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.AreaSampler.");
                        Assert.That(directive.PositionPattern, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep PositionPattern.");
                        Assert.That(directive.Aim, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Aim.");
                        Assert.That(directive.ShotPattern, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep ShotPattern.");
                    }
                }

                var sharedManagedReferenceIssues = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);
                Assert.That(sharedManagedReferenceIssues, Is.Empty, $"{path} must not keep shared SerializeReference graph.");

                string yaml = File.ReadAllText(path);
                Assert.That(yaml.Contains("EndSec:"), Is.False, $"{path} must not keep legacy EndSec serialization.");
            }
        }

        private static void AssertLayoutPopulated(StageLayoutSO layout)
        {
            Assert.That(layout.SchemaVersion, Is.EqualTo(2));
            Assert.That(layout.Grid.Width, Is.GreaterThan(0));
            Assert.That(layout.Grid.Height, Is.GreaterThan(0));
            Assert.That(layout.Cells, Is.Not.Null.And.Length.EqualTo(layout.Grid.Width * layout.Grid.Height));
            Assert.That(layout.SourceRegions, Is.Not.Null.And.Length.GreaterThan(0));
            Assert.That(layout.DepositRegions, Is.Not.Null.And.Length.GreaterThan(0));
        }

        private static void AssertLayoutMatchesSceneAuthoring(StageLayoutSO layout, int stageId)
        {
            Assert.That(EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Single).IsValid(), Is.True);

            var stage = UnityEngine.Object.FindObjectsByType<StageLayoutStageMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(x => x.StageId == stageId);
            Assert.That(stage.TryGetComponent(out StageGridAuthoring authoring), Is.True);
            Assert.That(authoring.RegionTilemap, Is.Not.Null, $"Stage {stageId} sample authoring must use unified RegionTilemap.");
            Assert.That(stage.GetComponentsInChildren<StageRegionAnchorMarker>(true), Is.Not.Empty, $"Stage {stageId} must keep region anchor markers.");

            Assert.That(layout.Grid.Width, Is.EqualTo(authoring.BoundsSize.x));
            Assert.That(layout.Grid.Height, Is.EqualTo(authoring.BoundsSize.y));

            for (int i = 0; i < layout.Cells.Length; i++)
            {
                int x = i % layout.Grid.Width;
                int y = i / layout.Grid.Width;
                Assert.That(layout.Cells[i].SourceRegionId, Is.EqualTo(ResolveRegionStableId(authoring, StageRegionKind.Source, x, y)), $"Source mismatch at cell[{i}] / ({x}, {y}).");
                Assert.That(layout.Cells[i].DepositRegionId, Is.EqualTo(ResolveRegionStableId(authoring, StageRegionKind.Deposit, x, y)), $"Deposit mismatch at cell[{i}] / ({x}, {y}).");
            }

            AssertSourceAndDepositAnchorsMatchScene(authoring, stage, layout);
            AssertPlayerStartMatchesScene(authoring, stage, layout);
            AssertPresentationsMatchScene(stage, layout);
        }

        private static uint ResolveRegionStableId(StageGridAuthoring authoring, StageRegionKind kind, int localX, int localY)
        {
            var tile = authoring.RegionTilemap.GetTile(authoring.GetTilemapCell(localX, localY)) as StageRegionTile;
            if (tile == null || tile.RegionKind != kind || tile.RegionSlotIndex <= 0)
                return 0u;

            return authoring.TryResolveStableId(kind, tile.RegionSlotIndex, out uint stableId) ? stableId : 0u;
        }

        private static void AssertSourceAndDepositAnchorsMatchScene(StageGridAuthoring authoring, StageLayoutStageMarker stage, StageLayoutSO layout)
        {
            var expectedSources = stage.GetComponentsInChildren<StageRegionAnchorMarker>(true)
                .Where(x => x.RegionKind == StageRegionKind.Source)
                .ToDictionary(
                    x => ResolveStableId(authoring, x),
                    x => authoring.GetLocalCell(x.AnchorCell));
            var expectedDeposits = stage.GetComponentsInChildren<StageRegionAnchorMarker>(true)
                .Where(x => x.RegionKind == StageRegionKind.Deposit)
                .ToDictionary(
                    x => ResolveStableId(authoring, x),
                    x => authoring.GetLocalCell(x.AnchorCell));

            Assert.That(layout.SourceRegions.Length, Is.EqualTo(expectedSources.Count));
            Assert.That(layout.DepositRegions.Length, Is.EqualTo(expectedDeposits.Count));

            for (int i = 0; i < layout.SourceRegions.Length; i++)
            {
                var region = layout.SourceRegions[i];
                Assert.That(expectedSources.TryGetValue(region.StableId, out var anchorCell), Is.True, $"Missing source anchor for stableId={region.StableId}.");
                Assert.That(region.AnchorCell, Is.EqualTo(anchorCell), $"Source anchor mismatch for stableId={region.StableId}.");
            }

            for (int i = 0; i < layout.DepositRegions.Length; i++)
            {
                var region = layout.DepositRegions[i];
                Assert.That(expectedDeposits.TryGetValue(region.StableId, out var anchorCell), Is.True, $"Missing deposit anchor for stableId={region.StableId}.");
                Assert.That(region.AnchorCell, Is.EqualTo(anchorCell), $"Deposit anchor mismatch for stableId={region.StableId}.");
            }
        }

        private static void AssertPlayerStartMatchesScene(StageGridAuthoring authoring, StageLayoutStageMarker stage, StageLayoutSO layout)
        {
            var marker = stage.GetComponentsInChildren<StagePlayerStartMarker>(true).Single();
            Assert.That(layout.PlayerStart.Active, Is.EqualTo(marker.Active));
            Assert.That(layout.PlayerStart.AnchorCell, Is.EqualTo(authoring.GetLocalCell(marker.AnchorCell)));
            Assert.That(layout.PlayerStart.AnchorOffset, Is.EqualTo(marker.AnchorOffset));
            Assert.That(layout.PlayerStart.YawDeg, Is.EqualTo(marker.YawDeg).Within(0.001f));
        }

        private static void AssertPresentationsMatchScene(StageLayoutStageMarker stage, StageLayoutSO layout)
        {
            var expected = StagePresentationEditorUtility.GetPresentationMarkers(stage)
                .OrderBy(x => x.StableId)
                .ThenBy(x => BuildHierarchyPath(x.transform), StringComparer.Ordinal)
                .Select(ToPresentationData)
                .ToArray();
            var actual = layout.Presentations ?? Array.Empty<StagePresentationLayoutData>();

            Assert.That(actual.Length, Is.EqualTo(expected.Length), $"Stage {stage.StageId} presentation count mismatch.");
            for (int i = 0; i < actual.Length; i++)
            {
                AssertPresentationEquals(expected[i], actual[i], stage.StageId, i);
            }
        }

        private static StagePresentationLayoutData ToPresentationData(StagePresentationMarker marker)
        {
            var transform = marker.transform;
            bool linked = marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent;
            var linkKind = StagePresentationLinkKind.None;
            uint linkedStableId = 0u;
            if (linked)
                StagePresentationEditorUtility.TryFindLinkedParent(transform, out linkKind, out linkedStableId, out _);

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

        private static void AssertPresentationEquals(StagePresentationLayoutData expected, StagePresentationLayoutData actual, int stageId, int index)
        {
            string prefix = $"Stage {stageId} presentation[{index}]";
            Assert.That(actual.StableId, Is.EqualTo(expected.StableId), $"{prefix} stable id mismatch.");
            Assert.That(actual.Active, Is.EqualTo(expected.Active), $"{prefix} active flag mismatch.");
            Assert.That(actual.PlacementMode, Is.EqualTo(expected.PlacementMode), $"{prefix} placement mode mismatch.");
            Assert.That(actual.LinkKind, Is.EqualTo(expected.LinkKind), $"{prefix} link kind mismatch.");
            Assert.That(actual.LinkedStableId, Is.EqualTo(expected.LinkedStableId), $"{prefix} linked stable id mismatch.");
            Assert.That(actual.PresentationKey, Is.EqualTo(expected.PresentationKey), $"{prefix} presentation key mismatch.");
            AssertVector3(actual.Position, expected.Position, $"{prefix} position mismatch.");
            AssertVector3(actual.Euler, expected.Euler, $"{prefix} euler mismatch.");
            AssertVector3(actual.Scale, expected.Scale, $"{prefix} scale mismatch.");
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f), $"{message} (x)");
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f), $"{message} (y)");
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f), $"{message} (z)");
        }

        private static void ValidateActorFreeSourceTemplatePrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var actor = root.GetComponentInChildren<HazardActorAuthoring>(true);
                var emitter = root.GetComponentInChildren<HazardEmitterAuthoring>(true);

                Assert.That(actor, Is.Null, $"{prefabPath} must stay actor-free after SP-4 cutover.");
                Assert.That(emitter, Is.Null, $"{prefabPath} must stay actor-free after SP-4 cutover.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidateHazardActorArchetypePrefabContract(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var actors = root.GetComponentsInChildren<HazardActorAuthoring>(true);
                Assert.That(actors, Is.Not.Null.And.Length.EqualTo(1), $"{prefabPath} must contain exactly one HazardActorAuthoring.");

                var actor = actors[0];
                Assert.That(actor, Is.Not.Null, prefabPath);

                bool valid = HazardActorAuthoringValidationUtility.TryValidateStandalone(
                    actor,
                    out var selectorSeed,
                    out var phaseTransitions,
                    out var errorKind,
                    out var error);
                Assert.That(valid, Is.True, $"{prefabPath} standalone actor contract failed. kind={errorKind}, error={error}");
                Assert.That(selectorSeed.Policies, Is.Not.Null.And.Length.GreaterThan(0), $"{prefabPath} must resolve at least one selector policy.");

                var ownedEmitters = actor.GetComponentsInChildren<HazardEmitterAuthoring>(true)
                    .Where(x => x != null && x.GetComponentInParent<HazardActorAuthoring>(true) == actor)
                    .ToArray();
                Assert.That(ownedEmitters, Is.Not.Empty, $"{prefabPath} must contain at least one owned HazardEmitterAuthoring.");

                for (int i = 0; i < ownedEmitters.Length; i++)
                {
                    var emitter = ownedEmitters[i];
                    Assert.That(emitter.EmitterId, Is.GreaterThan(0), $"{prefabPath} emitter[{i}] requires EmitterId >= 1.");

                    bool slotsResolved = HazardEmitterPatternSlotAuthoringUtility.TryResolveSlots(
                        emitter.Slots,
                        out var resolvedSlots,
                        out var slotError);
                    Assert.That(slotsResolved, Is.True, $"{prefabPath} emitter[{i}] slot contract failed. error={slotError}");
                    Assert.That(resolvedSlots, Is.Not.Null.And.Length.GreaterThan(0), $"{prefabPath} emitter[{i}] must resolve at least one slot.");
                }

                if (phaseTransitions.Length > 0)
                {
                    Assert.That(
                        phaseTransitions.Any(x => x.FromPhaseId != x.ToPhaseId),
                        Is.True,
                        $"{prefabPath} phase transitions must contain at least one non-self transition when transitions are authored.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static uint ResolveStableId(StageGridAuthoring authoring, StageRegionAnchorMarker marker)
        {
            return authoring.TryResolveStableId(marker.RegionKind, marker.RegionSlotIndex, out uint stableId) ? stableId : 0u;
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
