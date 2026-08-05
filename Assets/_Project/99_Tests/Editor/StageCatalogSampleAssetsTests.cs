using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
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
                        Assert.That(directive.Profile, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Profile.");
                        Assert.That(directive.Profile.Bullet, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Profile.Bullet.");
                        Assert.That(directive.Emission, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Emission.");
                        Assert.That(directive.Sampling, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.");
                        Assert.That(directive.Sampling.Anchor, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.Anchor.");
                        Assert.That(directive.Sampling.AreaSampler, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Sampling.AreaSampler.");
                        Assert.That(directive.Profile.PositionPattern, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Profile.PositionPattern.");
                        Assert.That(directive.Profile.Aim, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Profile.Aim.");
                        Assert.That(directive.Profile.ShotPattern, Is.Not.Null, $"{path} segment[{s}] directive[{d}] must keep Profile.ShotPattern.");
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

        private static void ValidateActorFreeSourceTemplatePrefab(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                var actor = root.GetComponentInChildren<HazardActorAuthoring>(true);
                var patternSlotOwner = root.GetComponentInChildren<HazardActorAuthoring>(true);

                Assert.That(actor, Is.Null, $"{prefabPath} must stay actor-free after SP-4 cutover.");
                Assert.That(patternSlotOwner, Is.Null, $"{prefabPath} must stay actor-free after SP-4 cutover.");
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

                bool slotsResolved = HazardActorPatternSlotAuthoringUtility.TryResolveSlots(
                    actor.PatternSlots,
                    out var resolvedSlots,
                    out var slotError);
                Assert.That(slotsResolved, Is.True, $"{prefabPath} pattern slot contract failed. error={slotError}");
                Assert.That(resolvedSlots, Is.Not.Null.And.Length.GreaterThan(0), $"{prefabPath} must resolve at least one actor pattern slot.");

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

    }
}
