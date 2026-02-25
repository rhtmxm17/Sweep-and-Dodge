using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;

namespace SweepNDodge.DotsBullets.Tests
{
    public class ContentValidationGateTests
    {
        [Test]
        public void ProjectContentValidation_HasNoErrors()
        {
            var issues = ContentValidationRunner.ValidateProjectAssets();
            var errors = issues.Where(i => i.Severity == ContentValidationSeverity.Error).ToArray();

            if (errors.Length <= 0)
                return;

            string summary = ContentValidationRunner.BuildErrorSummary(issues);
            string histogram = string.Join(
                ", ",
                errors.GroupBy(e => e.Code)
                    .OrderBy(g => g.Key)
                    .Select(g => $"{g.Key}={g.Count()}"));

            Assert.Fail($"Content validation errors detected: {errors.Length}\ncodeHistogram={histogram}\n{summary}");
        }

        [Test]
        public void ErrorSummary_IsDeterministicAndIndexed()
        {
            var issues = new List<ContentValidationIssue>
            {
                new ContentValidationIssue(ContentValidationSeverity.Warning, "CVW001", "path/c", "warning"),
                new ContentValidationIssue(ContentValidationSeverity.Error, "CV010", "path/b", "b"),
                new ContentValidationIssue(ContentValidationSeverity.Error, "CV001", "path/a", "a"),
                new ContentValidationIssue(ContentValidationSeverity.Error, "CV001", "path/c", "c"),
            };

            string summary = ContentValidationRunner.BuildErrorSummary(issues, 3);

            Assert.That(summary, Does.Contain("errors=3, shown=3"));
            Assert.That(summary, Does.Contain("[1] CV001 path/a - a"));
            Assert.That(summary, Does.Contain("[2] CV001 path/c - c"));
            Assert.That(summary, Does.Contain("[3] CV010 path/b - b"));
        }

        [Test]
        public void WarningCap_Default100_IsAppliedWithSuppressedAggregation()
        {
            var issues = new List<ContentValidationIssue>();
            for (int i = 0; i < 103; i++)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Warning,
                    "CVW900",
                    $"w/{i}",
                    "warning"));
            }

            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Error, "CV001", "e/0", "error"));

            var counts = ContentValidationRunner.CalculateIssueReportCounts(issues);
            Assert.That(counts.ErrorCount, Is.EqualTo(1));
            Assert.That(counts.WarningCount, Is.EqualTo(103));
            Assert.That(counts.WarningLogsToEmit, Is.EqualTo(100));
            Assert.That(counts.SuppressedWarningCount, Is.EqualTo(3));
        }
    }
}
