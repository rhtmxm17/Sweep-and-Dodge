using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageHazardActorPlacementEditorUtilityTests
    {
        private const string HazardPrefabPath =
            "Assets/_Project/04_Prefabs/StageTopology/pf_stage_hazard_actor_archetype.prefab";
        private const string AlternateHazardPrefabPath =
            "Assets/_Project/04_Prefabs/StageTopology/pf_stage1_simple_crossing_sentry.prefab";

        [Test]
        public void TryCreatePlacement_UsesStageGlobalNextIdAndDirectSourceParent()
        {
            var setup = CreateStageWithSources();
            try
            {
                var existingGo = new GameObject("existing");
                existingGo.transform.SetParent(setup.SourceA.transform, false);
                existingGo.AddComponent<StageHazardActorMarker>().PlacementInstanceId = 7;

                bool created = StageHazardActorPlacementEditorUtility.TryCreatePlacement(
                    setup.SourceB,
                    out var marker,
                    out string error);

                Assert.That(created, Is.True, error);
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.PlacementInstanceId, Is.EqualTo(8));
                Assert.That(marker.transform.parent, Is.EqualTo(setup.SourceB.transform));
                Assert.That(marker.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(marker.transform.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void TryCreatePlacement_RejectsNullAndSourceOutsideStage()
        {
            Assert.That(
                StageHazardActorPlacementEditorUtility.TryCreatePlacement(null, out _, out string nullError),
                Is.False);
            Assert.That(nullError, Does.Contain("Select"));

            var go = new GameObject("source");
            try
            {
                var source = go.AddComponent<SourceRuntimeTemplateAuthoring>();
                Assert.That(
                    StageHazardActorPlacementEditorUtility.TryCreatePlacement(source, out _, out string stageError),
                    Is.False);
                Assert.That(stageError, Does.Contain("StageLayoutStageMarker"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveSourceForPlacement_UsesNearestParentSource()
        {
            var setup = CreateStageWithSources();
            try
            {
                var child = new GameObject("selected_child");
                child.transform.SetParent(setup.SourceA.transform, false);

                var resolved = StageHazardActorPlacementEditorUtility.ResolveSourceForPlacement(child);

                Assert.That(resolved, Is.EqualTo(setup.SourceA));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void LocalPoseAndGenerator_UseSourceTransformSpaceAndTransformYaw()
        {
            var setup = CreateStageWithSources();
            try
            {
                setup.SourceA.transform.SetPositionAndRotation(
                    new Vector3(10f, 2f, -5f),
                    Quaternion.Euler(0f, 90f, 0f));
                setup.SourceA.transform.localScale = new Vector3(2f, 1f, 3f);

                var markerGo = new GameObject("placement");
                markerGo.transform.SetParent(setup.SourceA.transform, false);
                markerGo.transform.localPosition = new Vector3(2f, 0f, -1f);
                markerGo.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                var marker = markerGo.AddComponent<StageHazardActorMarker>();
                marker.PlacementInstanceId = 3;
                marker.LocalYawDeg = 240f;

                bool hasPose = StageHazardActorPlacementEditorUtility.TryGetLocalPose(
                    marker,
                    out var owner,
                    out Vector3 localOffset,
                    out float localYawDeg);
                var generated = StageDefinitionGenerator.BuildHazardActorPlacements(setup.SourceA);

                Assert.That(hasPose, Is.True);
                Assert.That(owner, Is.EqualTo(setup.SourceA));
                AssertVector3(localOffset, new Vector3(2f, 0f, -1f));
                Assert.That(localYawDeg, Is.EqualTo(35f).Within(0.001f));
                Assert.That(generated, Has.Length.EqualTo(1));
                AssertVector3(generated[0].LocalOffset, new Vector3(2f, 0f, -1f));
                Assert.That(generated[0].LocalYawDeg, Is.EqualTo(35f).Within(0.001f));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void BuildHazardActorPlacements_ReturnsStablePlacementIdOrder()
        {
            var setup = CreateStageWithSources();
            try
            {
                var lateGo = new GameObject("late_in_hierarchy");
                lateGo.transform.SetParent(setup.SourceA.transform, false);
                var late = lateGo.AddComponent<StageHazardActorMarker>();
                late.PlacementInstanceId = 9;

                var earlyGo = new GameObject("early_in_id_order");
                earlyGo.transform.SetParent(setup.SourceA.transform, false);
                var early = earlyGo.AddComponent<StageHazardActorMarker>();
                early.PlacementInstanceId = 2;

                var generated = StageDefinitionGenerator.BuildHazardActorPlacements(setup.SourceA);

                Assert.That(generated.Select(x => x.PlacementInstanceId).ToArray(), Is.EqualTo(new[] { 2, 9 }));
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void SyncDefinitionsFromAuthoring_ClonesHazardActorRules()
        {
            var rootGo = new GameObject("root");
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            try
            {
                rootGo.AddComponent<StageLayoutRootMarker>();
                var stageGo = new GameObject("stage");
                stageGo.transform.SetParent(rootGo.transform, false);
                var stage = stageGo.AddComponent<StageLayoutStageMarker>();
                stage.StageId = 12;
                stage.TargetLayout = layout;
                stage.TargetDefinition = definition;
                layout.SchemaVersion = 2;
                const uint stableId = 990001u;
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData { StableId = stableId, Active = true },
                };

                var sourceGo = new GameObject("source");
                sourceGo.transform.SetParent(stageGo.transform, false);
                var source = sourceGo.AddComponent<SourceRuntimeTemplateAuthoring>();
                source.StableIdOverride = stableId;
                var rulesMarker = sourceGo.AddComponent<HazardActorSourceAuthoringMarker>();
                rulesMarker.Rules = new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceIds = new[] { 5 },
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                };

                bool synced = StageDefinitionGenerator.TrySyncDefinitionsForRoot(
                    rootGo.GetComponent<StageLayoutRootMarker>(),
                    out var issues,
                    saveAssets: false);

                Assert.That(synced, Is.True, string.Join("\n", issues.Select(x => x.Message)));
                var generatedRules = definition.SourceBindings[0].HazardActorOrchestrationRules;
                Assert.That(generatedRules, Has.Length.EqualTo(1));
                Assert.That(generatedRules[0].TargetPlacementInstanceIds, Is.Not.SameAs(rulesMarker.Rules[0].TargetPlacementInstanceIds));
                rulesMarker.Rules[0].TargetPlacementInstanceIds[0] = 99;
                Assert.That(generatedRules[0].TargetPlacementInstanceIds, Is.EqualTo(new[] { 5 }));
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
                Object.DestroyImmediate(layout);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void Validation_ReportsMissingPrefabDuplicateIdAndNestedParent()
        {
            var setup = CreateStageWithSources();
            try
            {
                var firstGo = new GameObject("first");
                firstGo.transform.SetParent(setup.SourceA.transform, false);
                var first = firstGo.AddComponent<StageHazardActorMarker>();
                first.PlacementInstanceId = 4;

                var container = new GameObject("container");
                container.transform.SetParent(setup.SourceB.transform, false);
                var secondGo = new GameObject("second");
                secondGo.transform.SetParent(container.transform, false);
                var second = secondGo.AddComponent<StageHazardActorMarker>();
                second.PlacementInstanceId = 4;

                var errors = StageHazardActorPlacementEditorUtility.CollectValidationErrors(second);

                Assert.That(errors.Any(x => x.Contains("direct child")), Is.True);
                Assert.That(errors.Any(x => x.Contains("duplicated")), Is.True);
                Assert.That(errors.Any(x => x.Contains("ActorArchetypePrefab is required")), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void DefinitionSyncPlan_ReportsDiffAndApplyPreservesNonHazardBindingFields()
        {
            var setup = CreateStageWithSources();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefabPath);
            var alternatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlternateHazardPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(alternatePrefab, Is.Not.Null);

            try
            {
                var markerGo = new GameObject("placement");
                markerGo.transform.SetParent(setup.SourceA.transform, false);
                markerGo.transform.localPosition = new Vector3(3f, 0f, 4f);
                markerGo.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                var marker = markerGo.AddComponent<StageHazardActorMarker>();
                marker.PlacementInstanceId = 10;
                marker.ActorArchetypePrefab = prefab;

                var rulesMarker = setup.SourceA.gameObject.AddComponent<HazardActorSourceAuthoringMarker>();
                rulesMarker.Rules = new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceIds = new[] { 10 },
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                };

                setup.Definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = setup.SourceA.StableIdOverride,
                        ThresholdWeakened = 123,
                        ThresholdDepleted = 456,
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 10,
                                ActorArchetypePrefab = alternatePrefab,
                                LocalOffset = Vector3.zero,
                                LocalYawDeg = 0f,
                            },
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 20,
                                ActorArchetypePrefab = prefab,
                                LocalOffset = Vector3.one,
                            },
                        },
                        HazardActorOrchestrationRules = System.Array.Empty<HazardActorOrchestrationRuleBinding>(),
                    },
                };

                bool built = StageHazardActorPlacementEditorUtility.TryBuildDefinitionSyncPlan(
                    setup.SourceA,
                    out var plan,
                    out var errors);

                Assert.That(built, Is.True, string.Join("\n", errors));
                Assert.That(plan.AddCount, Is.EqualTo(0));
                Assert.That(plan.UpdateCount, Is.EqualTo(1));
                Assert.That(plan.RemoveCount, Is.EqualTo(1));
                Assert.That(plan.PrefabReplacementCount, Is.EqualTo(1));
                Assert.That(plan.RulesChanged, Is.True);
                Assert.That(plan.RequiresConfirmation, Is.True);

                bool applied = StageHazardActorPlacementEditorUtility.TryApplyDefinitionSyncPlan(
                    plan,
                    saveAssets: false,
                    out string applyError);

                Assert.That(applied, Is.True, applyError);
                var binding = setup.Definition.SourceBindings[0];
                Assert.That(binding.ThresholdWeakened, Is.EqualTo(123));
                Assert.That(binding.ThresholdDepleted, Is.EqualTo(456));
                Assert.That(binding.HazardActorPlacements, Has.Length.EqualTo(1));
                Assert.That(binding.HazardActorPlacements[0].PlacementInstanceId, Is.EqualTo(10));
                Assert.That(binding.HazardActorPlacements[0].ActorArchetypePrefab, Is.EqualTo(prefab));
                AssertVector3(binding.HazardActorPlacements[0].LocalOffset, new Vector3(3f, 0f, 4f));
                Assert.That(binding.HazardActorPlacements[0].LocalYawDeg, Is.EqualTo(45f).Within(0.001f));
                Assert.That(binding.HazardActorOrchestrationRules, Has.Length.EqualTo(1));

                Undo.FlushUndoRecordObjects();
                Undo.PerformUndo();
                binding = setup.Definition.SourceBindings[0];
                Assert.That(binding.ThresholdWeakened, Is.EqualTo(123));
                Assert.That(binding.HazardActorPlacements, Has.Length.EqualTo(2));
                Assert.That(binding.HazardActorOrchestrationRules, Is.Empty);

                Undo.PerformRedo();
                binding = setup.Definition.SourceBindings[0];
                Assert.That(binding.HazardActorPlacements, Has.Length.EqualTo(1));
                Assert.That(binding.HazardActorOrchestrationRules, Has.Length.EqualTo(1));
            }
            finally
            {
                Undo.ClearAll();
                setup.Dispose();
            }
        }

        [Test]
        public void DefinitionSyncPlan_RejectsMissingBindingAndDanglingRuleTarget()
        {
            var setup = CreateStageWithSources();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            try
            {
                setup.Definition.SourceBindings = System.Array.Empty<StageSourceBinding>();
                Assert.That(
                    StageHazardActorPlacementEditorUtility.TryBuildDefinitionSyncPlan(
                        setup.SourceA,
                        out _,
                        out var missingBindingErrors),
                    Is.False);
                Assert.That(missingBindingErrors.Any(x => x.Contains("no SourceBinding")), Is.True);

                setup.Definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = setup.SourceA.StableIdOverride,
                        HazardActorPlacements = System.Array.Empty<HazardActorPlacementBinding>(),
                        HazardActorOrchestrationRules = System.Array.Empty<HazardActorOrchestrationRuleBinding>(),
                    },
                };
                var markerGo = new GameObject("placement");
                markerGo.transform.SetParent(setup.SourceA.transform, false);
                var marker = markerGo.AddComponent<StageHazardActorMarker>();
                marker.PlacementInstanceId = 3;
                marker.ActorArchetypePrefab = prefab;
                var rulesMarker = setup.SourceA.gameObject.AddComponent<HazardActorSourceAuthoringMarker>();
                rulesMarker.Rules = new[]
                {
                    new HazardActorOrchestrationRuleBinding
                    {
                        RuleId = 1,
                        TargetPlacementInstanceIds = new[] { 99 },
                        ActionType = HazardActorOrchestrationActionId.Spawn,
                        TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                    },
                };

                Assert.That(
                    StageHazardActorPlacementEditorUtility.TryBuildDefinitionSyncPlan(
                        setup.SourceA,
                        out _,
                        out var danglingTargetErrors),
                    Is.False);
                Assert.That(danglingTargetErrors.Any(x => x.Contains("missing PlacementInstanceId 99")), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void DefinitionSyncPlan_RejectsStageGlobalIdConflictInAnotherBinding()
        {
            var setup = CreateStageWithSources();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            try
            {
                var markerGo = new GameObject("placement");
                markerGo.transform.SetParent(setup.SourceA.transform, false);
                var marker = markerGo.AddComponent<StageHazardActorMarker>();
                marker.PlacementInstanceId = 5;
                marker.ActorArchetypePrefab = prefab;

                setup.Definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = setup.SourceA.StableIdOverride,
                        HazardActorPlacements = System.Array.Empty<HazardActorPlacementBinding>(),
                    },
                    new StageSourceBinding
                    {
                        SourceStableId = setup.SourceB.StableIdOverride,
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 5,
                                ActorArchetypePrefab = prefab,
                            },
                        },
                    },
                };

                Assert.That(
                    StageHazardActorPlacementEditorUtility.TryBuildDefinitionSyncPlan(
                        setup.SourceA,
                        out _,
                        out var errors),
                    Is.False);
                Assert.That(errors.Any(x => x.Contains("conflicts with Definition SourceBinding 1002")), Is.True);
            }
            finally
            {
                setup.Dispose();
            }
        }

        [Test]
        public void DefinitionSyncPlan_DoesNotBlockOnUnownedIncompleteMarker()
        {
            var setup = CreateStageWithSources();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HazardPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            try
            {
                var currentGo = new GameObject("current_placement");
                currentGo.transform.SetParent(setup.SourceA.transform, false);
                var current = currentGo.AddComponent<StageHazardActorMarker>();
                current.PlacementInstanceId = 5;
                current.ActorArchetypePrefab = prefab;

                var incompleteOtherGo = new GameObject("other_incomplete_placement");
                incompleteOtherGo.transform.SetParent(setup.SourceB.transform, false);
                var incompleteOther = incompleteOtherGo.AddComponent<StageHazardActorMarker>();
                incompleteOther.PlacementInstanceId = 6;

                setup.Definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = setup.SourceA.StableIdOverride,
                        HazardActorPlacements = System.Array.Empty<HazardActorPlacementBinding>(),
                        HazardActorOrchestrationRules = System.Array.Empty<HazardActorOrchestrationRuleBinding>(),
                    },
                };

                bool built = StageHazardActorPlacementEditorUtility.TryBuildDefinitionSyncPlan(
                    setup.SourceA,
                    out var plan,
                    out var errors);

                Assert.That(built, Is.True, string.Join("\n", errors));
                Assert.That(plan.AddCount, Is.EqualTo(1));
            }
            finally
            {
                setup.Dispose();
            }
        }

        private static Setup CreateStageWithSources()
        {
            var stageGo = new GameObject("stage");
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            var stage = stageGo.AddComponent<StageLayoutStageMarker>();
            stage.StageId = 12;
            stage.TargetDefinition = definition;
            var sourceAGo = new GameObject("source_a");
            sourceAGo.transform.SetParent(stageGo.transform, false);
            var sourceBGo = new GameObject("source_b");
            sourceBGo.transform.SetParent(stageGo.transform, false);
            var setup = new Setup(
                stageGo,
                definition,
                sourceAGo.AddComponent<SourceRuntimeTemplateAuthoring>(),
                sourceBGo.AddComponent<SourceRuntimeTemplateAuthoring>());
            setup.SourceA.StableIdOverride = 1001u;
            setup.SourceB.StableIdOverride = 1002u;
            return setup;
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.001f));
        }

        private sealed class Setup
        {
            public Setup(
                GameObject stage,
                StageDefinitionSO definition,
                SourceRuntimeTemplateAuthoringBase sourceA,
                SourceRuntimeTemplateAuthoringBase sourceB)
            {
                Stage = stage;
                Definition = definition;
                SourceA = sourceA;
                SourceB = sourceB;
            }

            public GameObject Stage { get; }
            public StageDefinitionSO Definition { get; }
            public SourceRuntimeTemplateAuthoringBase SourceA { get; }
            public SourceRuntimeTemplateAuthoringBase SourceB { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(Stage);
                Object.DestroyImmediate(Definition);
            }
        }
    }
}
