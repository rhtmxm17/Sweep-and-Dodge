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
        public void InvalidStageId_IsReportedAsError()
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            try
            {
                layout.StageId = 0;
                layout.Sources = System.Array.Empty<StageSourceLayoutData>();
                layout.Deposits = System.Array.Empty<StageDepositLayoutData>();

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateLayoutRecords(
                    new List<ContentValidationRecord<StageLayoutSO>>
                    {
                        new ContentValidationRecord<StageLayoutSO>(layout, "layout")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STL001" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void DuplicateSourceStableIdInSameLayout_IsReportedAsError()
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            try
            {
                layout.StageId = 3;
                layout.Sources = new[]
                {
                    new StageSourceLayoutData { StableId = 100, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 4f },
                    new StageSourceLayoutData { StableId = 100, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 5f },
                };
                layout.Deposits = new[]
                {
                    new StageDepositLayoutData { StableId = 200, Active = true, Radius = 1f },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateLayoutRecords(
                    new List<ContentValidationRecord<StageLayoutSO>>
                    {
                        new ContentValidationRecord<StageLayoutSO>(layout, "layout")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STL003" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void EmptyVisualKey_IsReportedAsWarning()
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            try
            {
                layout.StageId = 5;
                layout.Sources = new[]
                {
                    new StageSourceLayoutData { StableId = 10, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 2f },
                };
                layout.Deposits = new[]
                {
                    new StageDepositLayoutData { StableId = 20, Active = true, Radius = 1f },
                };
                layout.Visuals = new[]
                {
                    new StageVisualLayoutData { StableId = 30, Active = true, VisualKey = "" },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateLayoutRecords(
                    new List<ContentValidationRecord<StageLayoutSO>>
                    {
                        new ContentValidationRecord<StageLayoutSO>(layout, "layout")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STL007" && x.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void ObstacleWithEmptyCollisionMask_IsReportedAsError()
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            try
            {
                layout.StageId = 7;
                layout.Sources = new[]
                {
                    new StageSourceLayoutData { StableId = 10, Active = true, FieldShape = BulletFieldShapeId.Circle, FieldRadius = 2f },
                };
                layout.Deposits = new[]
                {
                    new StageDepositLayoutData { StableId = 20, Active = true, Radius = 1f },
                };
                layout.Obstacles = new[]
                {
                    new StageObstacleLayoutData
                    {
                        StableId = 30,
                        Active = true,
                        Shape = ObstacleShape.Box,
                        Size = new Vector2(2f, 2f),
                        CollisionMask = ObstacleCollisionMask.None,
                    },
                };

                var issues = new List<ContentValidationIssue>();
                StageLayoutValidationRules.ValidateLayoutRecords(
                    new List<ContentValidationRecord<StageLayoutSO>>
                    {
                        new ContentValidationRecord<StageLayoutSO>(layout, "layout")
                    },
                    issues);

                Assert.That(issues.Any(x => x.Code == "STL009" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }
    }
}
