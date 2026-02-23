using System.Linq;
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

            string summary = string.Join(
                "\n",
                errors.Take(10).Select(e => $"{e.Code} {e.Location} - {e.Message}"));

            Assert.Fail($"Content validation errors detected: {errors.Length}\n{summary}");
        }
    }
}
