using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StagePresentationCatalogValidationRulesTests
    {
        [Test]
        public void ValidateCatalog_DuplicateKey_IsReportedAsError()
        {
            var catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            var prefab = new GameObject("presentation_prefab");

            try
            {
                catalog.Entries = new[]
                {
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "wall_basic",
                        Prefab = prefab,
                    },
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "wall_basic",
                        Prefab = prefab,
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StagePresentationCatalogValidationRules.ValidateCatalogRecords(
                    new[] { new ContentValidationRecord<StagePresentationCatalogSO>(catalog, "catalog") },
                    null,
                    issues);

                Assert.That(issues.Any(x => x.Code == "SPC001" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void ValidateCatalog_NullPrefab_IsReportedAsError()
        {
            var catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();

            try
            {
                catalog.Entries = new[]
                {
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "bin_basic",
                        Prefab = null,
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StagePresentationCatalogValidationRules.ValidateCatalogRecords(
                    new[] { new ContentValidationRecord<StagePresentationCatalogSO>(catalog, "catalog") },
                    null,
                    issues);

                Assert.That(issues.Any(x => x.Code == "SPC003" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
