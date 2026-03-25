using System.Collections.Generic;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;

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

        private static void AssertLayoutPopulated(StageLayoutSO layout)
        {
            Assert.That(layout.SchemaVersion, Is.EqualTo(2));
            Assert.That(layout.Grid.Width, Is.GreaterThan(0));
            Assert.That(layout.Grid.Height, Is.GreaterThan(0));
            Assert.That(layout.Cells, Is.Not.Null.And.Length.EqualTo(layout.Grid.Width * layout.Grid.Height));
            Assert.That(layout.SourceRegions, Is.Not.Null.And.Length.GreaterThan(0));
            Assert.That(layout.DepositRegions, Is.Not.Null.And.Length.GreaterThan(0));
            Assert.That(layout.Sources == null || layout.Sources.Length == 0, Is.True);
            Assert.That(layout.Deposits == null || layout.Deposits.Length == 0, Is.True);
            Assert.That(layout.Obstacles == null || layout.Obstacles.Length == 0, Is.True);
        }
    }
}
