using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class RuntimeTemplateAuthoringCompatibilityTests
    {
        [Test]
        public void ContentValidation_AcceptsLegacyBulletSourceAuthoringWrapper()
        {
            var sourceRoot = new GameObject("legacy_source_root");
            var sourceAuthoring = sourceRoot.AddComponent<BulletSourceAuthoring>();

            try
            {
                sourceAuthoring.Radius = -4f;
                sourceAuthoring.Size = new Vector2(-5f, -6f);
                sourceAuthoring.PollutionCellSize = 0.01f;
                sourceAuthoring.PollutionMin = -1f;
                sourceAuthoring.PollutionMax = -2f;
                sourceAuthoring.PollutionRegenPerSec = -3f;
                sourceAuthoring.PollutionDropPerCollect = -4f;
                sourceAuthoring.PollutionTopKSampleCount = 0;
                sourceAuthoring.PollutionActiveRatioThreshold = 2f;
                sourceAuthoring.PollutionRecoveryCooldownFrames = -1;
                sourceAuthoring.PollutionRecoveryWaveSeedCount = 0;
                sourceAuthoring.PollutionRecoveryWaveClusterSize = 0;
                sourceAuthoring.PollutionRecoveryRestoreValue = -1f;
                sourceAuthoring.PollutionRecoveryRecentCleanBiasFrames = -1;

                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new[]
                    {
                        new ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>(sourceAuthoring, "legacy_source"),
                    },
                    null);

                var issues = ContentValidationRules.Validate(input);
                var warnings = issues.Where(i => i.Code.StartsWith("CVW")).ToArray();
                Assert.That(warnings.Length, Is.GreaterThan(0));
            }
            finally
            {
                Object.DestroyImmediate(sourceRoot);
            }
        }
    }
}
