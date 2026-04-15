using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageDefinitionGeneratorTests
    {
        [Test]
        public void SyncDefinitionsForRoot_PreservesExistingBindings_SeedsMissing_AndWarnsOnOrphan()
        {
            var rootGo = new GameObject("root");
            var stageGo = new GameObject("stage");
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var authoringGo = new GameObject("source_authoring");

            try
            {
                stageGo.transform.SetParent(rootGo.transform);

                var root = rootGo.AddComponent<StageLayoutRootMarker>();
                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                stage.StageId = 1;
                stage.TargetDefinition = definition;
                stage.TargetLayout = layout;

                layout.SchemaVersion = 2;
                layout.StageId = 1;
                layout.Grid = new StageGridSpec
                {
                    Width = 2,
                    Height = 1,
                    CellSize = 1f,
                    Origin = Vector3.zero,
                };
                layout.Cells = new[]
                {
                    new StageCellLayoutData { SourceRegionId = 1001u },
                    new StageCellLayoutData { SourceRegionId = 1002u },
                };
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData { StableId = 1001u, Active = true, AnchorCell = new Vector2Int(0, 0) },
                    new StageSourceRegionLayoutData { StableId = 1002u, Active = true, AnchorCell = new Vector2Int(1, 0) },
                };
                layout.DepositRegions = System.Array.Empty<StageDepositRegionLayoutData>();
                layout.PlayerStart = new StagePlayerStartLayoutData
                {
                    Active = true,
                    AnchorCell = new Vector2Int(0, 0),
                    AnchorOffset = Vector2.zero,
                    YawDeg = 0f,
                };

                definition.StageId = 1;
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1001u,
                        InitialSourceState = SourceStateId.Depleted,
                        ThresholdWeakened = 77,
                        ThresholdDepleted = 88,
                        SustainSlots = System.Array.Empty<SustainSlotBinding>(),
                        EventSlots = System.Array.Empty<EventSlotBinding>(),
                    },
                    new StageSourceBinding
                    {
                        SourceStableId = 9001u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 5,
                        ThresholdDepleted = 10,
                        SustainSlots = System.Array.Empty<SustainSlotBinding>(),
                        EventSlots = System.Array.Empty<EventSlotBinding>(),
                    },
                };

                var authoring = authoringGo.AddComponent<SourceRuntimeTemplateAuthoring>();
                authoring.StableIdOverride = 1002u;
                authoring.InitialState = SourceStateId.Weakened;
                authoring.ThresholdWeakened = 11;
                authoring.ThresholdDepleted = 22;
                authoring.SustainClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring>();
                authoring.EventClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.EventClipSlotAuthoring>();

                bool ok = StageDefinitionGenerator.TrySyncDefinitionsForRoot(root, out var issues, saveAssets: false);

                Assert.That(ok, Is.True);
                Assert.That(definition.SourceBindings.Select(x => x.SourceStableId), Is.EqualTo(new uint[] { 1001u, 1002u, 9001u }));
                Assert.That(definition.SourceBindings[0].ThresholdWeakened, Is.EqualTo(77));
                Assert.That(definition.SourceBindings[0].ThresholdDepleted, Is.EqualTo(88));
                Assert.That(definition.SourceBindings[0].InitialSourceState, Is.EqualTo(SourceStateId.Depleted));
                Assert.That(definition.SourceBindings[1].ThresholdWeakened, Is.EqualTo(11));
                Assert.That(definition.SourceBindings[1].ThresholdDepleted, Is.EqualTo(22));
                Assert.That(definition.SourceBindings[1].InitialSourceState, Is.EqualTo(SourceStateId.Weakened));
                Assert.That(definition.SourceBindings[1].HazardActorPlacements, Is.Empty);
                Assert.That(definition.HazardActorOrchestrationRules, Is.Empty);
                Assert.That(issues.Any(x => x.Code == "SDF905"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(authoringGo);
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(rootGo);
            }
        }
    }
}



