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
                        Usage = StagePresentationUsageFlags.ObstacleLinked,
                    },
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "wall_basic",
                        Prefab = prefab,
                        Usage = StagePresentationUsageFlags.ObstacleLinked,
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
                        Usage = StagePresentationUsageFlags.DepositLinked,
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

        [Test]
        public void ValidateCatalog_UsageMismatchWithLayout_IsReportedAsWarning()
        {
            var catalog = ScriptableObject.CreateInstance<StagePresentationCatalogSO>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var prefab = new GameObject("presentation_prefab");

            try
            {
                catalog.Entries = new[]
                {
                    new StagePresentationCatalogEntry
                    {
                        PresentationKey = "bin_basic",
                        Prefab = prefab,
                        Usage = StagePresentationUsageFlags.DepositLinked,
                    },
                };

                layout.StageId = 1;
                layout.Presentations = new[]
                {
                    new StagePresentationLayoutData
                    {
                        StableId = 9001,
                        Active = true,
                        PlacementMode = StagePresentationPlacementMode.Standalone,
                        LinkKind = StagePresentationLinkKind.None,
                        LinkedStableId = 0,
                        PresentationKey = "bin_basic",
                        Position = Vector3.zero,
                        Euler = Vector3.zero,
                        Scale = Vector3.one,
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StagePresentationCatalogValidationRules.ValidateCatalogRecords(
                    new[] { new ContentValidationRecord<StagePresentationCatalogSO>(catalog, "catalog") },
                    new[] { new ContentValidationRecord<StageLayoutSO>(layout, "layout") },
                    issues);

                Assert.That(issues.Any(x => x.Code == "SPC005" && x.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
