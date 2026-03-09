using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class RuntimeTemplateAuthoringCompatibilityTests
    {
        [Test]
        public void StageDefinitionGenerator_ReadsLegacyBulletSourceAuthoringWrapper()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var authoringGo = new GameObject("legacy_source_authoring");

            try
            {
                stageGo.transform.SetParent(rootGo.transform);

                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                stage.StageId = 2;
                stage.TargetDefinition = definition;
                stage.TargetLayout = layout;

                layout.StageId = 2;
                layout.Sources = new[]
                {
                    new StageSourceLayoutData { StableId = 2001u, Active = true, Shape = Shape2DKind.Circle, Radius = 2f },
                };

                definition.StageId = 2;
                definition.SourceBindings = System.Array.Empty<StageSourceBinding>();

                var authoring = authoringGo.AddComponent<BulletSourceAuthoring>();
                authoring.StableIdOverride = 2001u;
                authoring.InitialState = SourceStateId.Weakened;
                authoring.ThresholdWeakened = 31;
                authoring.ThresholdDepleted = 62;
                authoring.SustainClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring>();
                authoring.EventClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.EventClipSlotAuthoring>();

                bool ok = StageDefinitionGenerator.TrySyncDefinitionsForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True);
                Assert.That(issues.Any(x => x.Severity == ContentValidationSeverity.Error), Is.False);
                Assert.That(definition.SourceBindings.Length, Is.EqualTo(1));
                Assert.That(definition.SourceBindings[0].SourceStableId, Is.EqualTo(2001u));
                Assert.That(definition.SourceBindings[0].ThresholdWeakened, Is.EqualTo(31));
                Assert.That(definition.SourceBindings[0].ThresholdDepleted, Is.EqualTo(62));
                Assert.That(definition.SourceBindings[0].InitialSourceState, Is.EqualTo(SourceStateId.Weakened));
            }
            finally
            {
                Object.DestroyImmediate(authoringGo);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(rootGo);
            }
        }

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
