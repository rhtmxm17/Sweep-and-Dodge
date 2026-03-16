using System.Collections.Generic;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;

namespace SweepNDodge.DotsBullets.Tests
{
    public class InWorldDialogueSampleAssetsTests
    {
        private const string DialogueCatalogPath = "Assets/_Project/03_Datas/InWorldDialogue/iwdc_demo.asset";
        private const string SpeakerCatalogPath = "Assets/_Project/03_Datas/InWorldDialogue/iwdspk_demo.asset";

        [Test]
        public void InWorldDialogueSampleAssets_Exist()
        {
            var dialogueCatalog = AssetDatabase.LoadAssetAtPath<InWorldDialogueCatalogSO>(DialogueCatalogPath);
            var speakerCatalog = AssetDatabase.LoadAssetAtPath<InWorldDialogueSpeakerCatalogSO>(SpeakerCatalogPath);

            Assert.That(dialogueCatalog, Is.Not.Null, "iwdc_demo.asset must exist.");
            Assert.That(speakerCatalog, Is.Not.Null, "iwdspk_demo.asset must exist.");
        }

        [Test]
        public void InWorldDialogueSampleAssets_PassValidationRules()
        {
            var dialogueCatalog = AssetDatabase.LoadAssetAtPath<InWorldDialogueCatalogSO>(DialogueCatalogPath);
            var speakerCatalog = AssetDatabase.LoadAssetAtPath<InWorldDialogueSpeakerCatalogSO>(SpeakerCatalogPath);

            Assert.That(dialogueCatalog, Is.Not.Null);
            Assert.That(speakerCatalog, Is.Not.Null);

            var issues = new List<ContentValidationIssue>();
            InWorldDialogueCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<InWorldDialogueCatalogSO>(dialogueCatalog, DialogueCatalogPath),
                },
                new[]
                {
                    new ContentValidationRecord<InWorldDialogueSpeakerCatalogSO>(speakerCatalog, SpeakerCatalogPath),
                },
                issues);

            Assert.That(issues, Is.Empty, "In-world dialogue sample assets must satisfy validation rules.");
        }
    }
}
