using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageLayoutValidationRulesTests
    {
        [Test]
        public void DuplicateStageId_IsReportedAsError()
        {
            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            try
            {
                catalog.Stages = new[]
                {
                    new StageMapDefinition { StageId = 1, Sources = System.Array.Empty<StageSourceLayoutData>(), Deposits = System.Array.Empty<StageDepositLayoutData>() },
                    new StageMapDefinition { StageId = 1, Sources = System.Array.Empty<StageSourceLayoutData>(), Deposits = System.Array.Empty<StageDepositLayoutData>() },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateCatalogRecords(
                    new List<ContentValidationRecord<StageMapCatalogSO>>
                    {
                        new ContentValidationRecord<StageMapCatalogSO>(catalog, "catalog")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STG002" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void DuplicateSourceStableIdInSameStage_IsReportedAsError()
        {
            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            try
            {
                catalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 3,
                        Sources = new[]
                        {
                            new StageSourceLayoutData { StableId = 100, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 4f },
                            new StageSourceLayoutData { StableId = 100, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 5f },
                        },
                        Deposits = new[]
                        {
                            new StageDepositLayoutData { StableId = 200, Active = true, Radius = 1f },
                        },
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateCatalogRecords(
                    new List<ContentValidationRecord<StageMapCatalogSO>>
                    {
                        new ContentValidationRecord<StageMapCatalogSO>(catalog, "catalog")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STG003" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void EmptyVisualKey_IsReportedAsWarning()
        {
            var catalog = ScriptableObject.CreateInstance<StageMapCatalogSO>();
            try
            {
                catalog.Stages = new[]
                {
                    new StageMapDefinition
                    {
                        StageId = 5,
                        Sources = new[]
                        {
                            new StageSourceLayoutData { StableId = 10, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 2f },
                        },
                        Deposits = new[]
                        {
                            new StageDepositLayoutData { StableId = 20, Active = true, Radius = 1f },
                        },
                        Visuals = new[]
                        {
                            new StageVisualLayoutData { StableId = 30, Active = true, VisualKey = "" },
                        },
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateCatalogRecords(
                    new List<ContentValidationRecord<StageMapCatalogSO>>
                    {
                        new ContentValidationRecord<StageMapCatalogSO>(catalog, "catalog")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STG007" && x.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }
    }
}
