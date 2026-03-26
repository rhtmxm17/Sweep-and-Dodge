using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageGridLayoutValidationRulesTests
    {
        [Test]
        public void ValidDenseGridLayout_PassesWithoutErrors()
        {
            var layout = CreateValidGridLayout();
            try
            {
                var issues = Validate(layout);
                Assert.That(issues.Where(x => x.Severity == ContentValidationSeverity.Error), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void CellsLengthMismatch_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells = new StageCellLayoutData[3];

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG003" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void DuplicateRegionStableIds_AreReportedAsErrors()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData { StableId = 100u, Active = true, AnchorCell = new Vector2Int(0, 0) },
                    new StageSourceRegionLayoutData { StableId = 100u, Active = true, AnchorCell = new Vector2Int(0, 0) },
                };
                layout.DepositRegions = new[]
                {
                    new StageDepositRegionLayoutData { StableId = 200u, Active = true, AnchorCell = new Vector2Int(1, 1) },
                    new StageDepositRegionLayoutData { StableId = 200u, Active = true, AnchorCell = new Vector2Int(1, 1) },
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG004" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(issues.Any(x => x.Code == "STG005" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void MissingRegionTableEntryFromCellReference_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells[0] = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.None,
                    SourceRegionId = 999u,
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG006" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void SourceAndDepositOverlapOnSameCell_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells[0] = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.None,
                    SourceRegionId = 100u,
                    DepositRegionId = 200u,
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG008" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void SourceRegionReferencedByZeroCells_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells[0] = default;
                layout.SourceRegions[0] = new StageSourceRegionLayoutData
                {
                    StableId = 100u,
                    Active = true,
                    AnchorCell = new Vector2Int(0, 0),
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG009" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void DepositRegionReferencedOnlyByBlockedCells_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells[3] = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.BlockPlayer,
                    DepositRegionId = 200u,
                };
                layout.DepositRegions[0] = new StageDepositRegionLayoutData
                {
                    StableId = 200u,
                    Active = true,
                    AnchorCell = new Vector2Int(1, 1),
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG010" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void AnchorOutOfBounds_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.SourceRegions[0] = new StageSourceRegionLayoutData
                {
                    StableId = 100u,
                    Active = true,
                    AnchorCell = new Vector2Int(9, 9),
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG011" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void AnchorCellOutsideRegion_IsReportedAsError()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.SourceRegions[0] = new StageSourceRegionLayoutData
                {
                    StableId = 100u,
                    Active = true,
                    AnchorCell = new Vector2Int(1, 0),
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG012" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        [Test]
        public void DepositAnchorOnBlockedCell_IsAllowed()
        {
            var layout = CreateValidGridLayout();
            try
            {
                layout.Cells[3] = new StageCellLayoutData
                {
                    MovementFlags = StageCellMovementFlags.BlockPlayer,
                    DepositRegionId = 200u,
                };
                layout.DepositRegions[0] = new StageDepositRegionLayoutData
                {
                    StableId = 200u,
                    Active = true,
                    AnchorCell = new Vector2Int(1, 1),
                };

                var issues = Validate(layout);
                Assert.That(issues.Any(x => x.Code == "STG012" && x.Severity == ContentValidationSeverity.Error), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        private static StageLayoutSO CreateValidGridLayout()
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.SchemaVersion = 2;
            layout.StageId = 1;
            layout.Grid = new StageGridSpec
            {
                Width = 2,
                Height = 2,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            layout.Cells = new[]
            {
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, SourceRegionId = 100u },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None },
                new StageCellLayoutData { MovementFlags = StageCellMovementFlags.None, DepositRegionId = 200u },
            };
            layout.SourceRegions = new[]
            {
                new StageSourceRegionLayoutData
                {
                    StableId = 100u,
                    Active = true,
                    AnchorCell = new Vector2Int(0, 0),
                    AnchorOffset = Vector2.zero,
                }
            };
            layout.DepositRegions = new[]
            {
                new StageDepositRegionLayoutData
                {
                    StableId = 200u,
                    Active = true,
                    AnchorCell = new Vector2Int(1, 1),
                    AnchorOffset = Vector2.zero,
                }
            };
            layout.Presentations = System.Array.Empty<StagePresentationLayoutData>();
            return layout;
        }

        private static List<ContentValidationIssue> Validate(StageLayoutSO layout)
        {
            var issues = new List<ContentValidationIssue>();
            StageGridLayoutValidationRules.ValidateLayoutRecords(
                new List<ContentValidationRecord<StageLayoutSO>>
                {
                    new ContentValidationRecord<StageLayoutSO>(layout, "layout")
                },
                issues);
            return issues;
        }
    }
}
