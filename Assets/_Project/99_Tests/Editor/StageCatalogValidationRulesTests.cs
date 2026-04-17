using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageCatalogValidationRulesTests
    {
        private const string GeneratedOperationalRoot = "Assets/__GeneratedStageCatalogValidation";
        private const string GeneratedTestRoot = "Assets/_Project/99_Tests/TestData/__GeneratedStageCatalogValidation";

        private sealed class HazardActorArchetypeFixture : IDisposable
        {
            public GameObject Root;
            public HazardEmitterTelegraphProfileSO Telegraph;
            public HazardEmitterEmissionProfileSO Emission;
            public BulletDefinitionSO Bullet;

            public void Dispose()
            {
                if (Root != null)
                    UnityEngine.Object.DestroyImmediate(Root);
                if (Telegraph != null)
                    UnityEngine.Object.DestroyImmediate(Telegraph);
                if (Emission != null)
                    UnityEngine.Object.DestroyImmediate(Emission);
                if (Bullet != null)
                    UnityEngine.Object.DestroyImmediate(Bullet);
            }
        }

        [Test]
        public void ValidateCatalog_NullReferences_AreReportedAsErrors()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_01",
                    Definition = null,
                    Layout = null,
                });

                var issues = ValidateCatalog(catalog);

                Assert.That(HasIssue(issues, "STC002", ContentValidationSeverity.Error), Is.True);
                Assert.That(HasIssue(issues, "STC003", ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_DuplicateAndMismatchContracts_AreReportedAsErrors()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition1 = CreateDefinition(created, stageId: 1);
                var definition2 = CreateDefinition(created, stageId: 1);
                var layout1 = CreateLayout(created, stageId: 1);
                var layout2 = CreateLayout(created, stageId: 2);
                var catalog = CreateCatalog(
                    created,
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "dup-key",
                        Definition = definition1,
                        Layout = layout1,
                    },
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "dup-key",
                        Definition = definition2,
                        Layout = layout2,
                    });

                var issues = ValidateCatalog(catalog);

                Assert.That(HasIssue(issues, "STC006", ContentValidationSeverity.Error), Is.True, "Definition/Layout StageId mismatch must fail.");
                Assert.That(HasIssue(issues, "STC007", ContentValidationSeverity.Error), Is.True, "Duplicate EntryKey must fail.");
                Assert.That(HasIssue(issues, "STC008", ContentValidationSeverity.Error), Is.True, "Duplicate enabled StageId must fail.");
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_ThresholdAndClipPhaseContracts_AreReportedAsErrors()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var sustainWrongPhase = CreateClip(created, clipId: 100, phase: SourceWavePhaseId.OnStateEnterOnce);
                var eventWrongPhase = CreateClip(created, clipId: 200, phase: SourceWavePhaseId.Sustain);

                var definition = CreateDefinition(created, stageId: 5);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 777u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 5,
                        ThresholdDepleted = 4,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Normal,
                                Lane = SourceSpawnLaneId.Hazard,
                                Clips = new[] { sustainWrongPhase },
                                Weights = new[] { 1f },
                            }
                        },
                        EventSlots = new[]
                        {
                            new EventSlotBinding
                            {
                                TriggerState = SourceStateId.Weakened,
                                EventClips = new[] { eventWrongPhase },
                            }
                        },
                    }
                };

                var layout = CreateLayout(created, stageId: 5);
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData
                    {
                        StableId = 777u,
                        Active = true,
                        AnchorCell = new Vector2Int(0, 0),
                    }
                };
                layout.Cells[0] = new StageCellLayoutData { SourceRegionId = 777u };

                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_05",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);

                Assert.That(HasIssue(issues, "STC011", ContentValidationSeverity.Error), Is.True, "Threshold order must fail.");
                Assert.That(HasIssue(issues, "STC016", ContentValidationSeverity.Error), Is.True, "Sustain phase mismatch must fail.");
                Assert.That(HasIssue(issues, "STC020", ContentValidationSeverity.Error), Is.True, "Event phase mismatch must fail.");
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_SourceCrossMappingMismatch_IsReportedAsWarnings()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition = CreateDefinition(created, stageId: 3);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1001u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 0,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                    }
                };

                var layout = CreateLayout(created, stageId: 3);
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData
                    {
                        StableId = 2002u,
                        Active = true,
                        AnchorCell = new Vector2Int(0, 0),
                    }
                };
                layout.Cells[0] = new StageCellLayoutData { SourceRegionId = 2002u };

                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_03",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);

                Assert.That(HasIssue(issues, "STC021", ContentValidationSeverity.Warning), Is.True);
                Assert.That(HasIssue(issues, "STC022", ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_SourceRegionWithoutBinding_IsReportedAsWarning()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition = CreateDefinition(created, stageId: 6);
                var layout = CreateLayout(created, stageId: 6);
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData
                    {
                        StableId = 601u,
                        Active = true,
                        AnchorCell = new Vector2Int(0, 0),
                    }
                };
                layout.Cells[0] = new StageCellLayoutData { SourceRegionId = 601u };

                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_06",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC022", ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_BindingWithoutActiveSourceRegion_IsReportedAsWarning()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition = CreateDefinition(created, stageId: 7);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 701u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 0,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                    }
                };
                var layout = CreateLayout(created, stageId: 7);
                layout.SourceRegions = new[]
                {
                    new StageSourceRegionLayoutData
                    {
                        StableId = 701u,
                        Active = false,
                        AnchorCell = new Vector2Int(0, 0),
                    }
                };

                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_07",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC021", ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_DepositOnlyLayout_DoesNotCreateSourceCrossMappingWarnings()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition = CreateDefinition(created, stageId: 8);
                var layout = CreateLayout(created, stageId: 8);
                layout.DepositRegions = new[]
                {
                    new StageDepositRegionLayoutData
                    {
                        StableId = 801u,
                        Active = true,
                        AnchorCell = new Vector2Int(0, 0),
                    }
                };
                layout.Cells[0] = new StageCellLayoutData { DepositRegionId = 801u };

                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_08",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC021", ContentValidationSeverity.Warning), Is.False);
                Assert.That(HasIssue(issues, "STC022", ContentValidationSeverity.Warning), Is.False);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_OperationalCatalogReferencingTestOnlyDefinition_IsReportedAsError()
        {
            string scope = System.Guid.NewGuid().ToString("N");
            string operationalRoot = EnsureFolder($"{GeneratedOperationalRoot}/{scope}");
            string testRoot = EnsureFolder($"{GeneratedTestRoot}/{scope}");

            try
            {
                var definition = CreateDefinitionAsset($"{testRoot}/sd_test.asset", stageId: 1);
                var layout = CreateLayoutAsset($"{operationalRoot}/sl_operational.asset", stageId: 1);
                var catalog = CreateCatalogAsset(
                    $"{operationalRoot}/sc_operational.asset",
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_test",
                        Definition = definition,
                        Layout = layout,
                    });

                var issues = ValidateCatalog(catalog, operationalRoot);
                Assert.That(HasIssue(issues, "STC023", ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                DeleteAssetFolder($"{GeneratedOperationalRoot}/{scope}");
                DeleteAssetFolder($"{GeneratedTestRoot}/{scope}");
            }
        }

        [Test]
        public void ValidateCatalog_OperationalCatalogReferencingTestOnlyWaveClip_IsReportedAsError()
        {
            string scope = System.Guid.NewGuid().ToString("N");
            string operationalRoot = EnsureFolder($"{GeneratedOperationalRoot}/{scope}");
            string testRoot = EnsureFolder($"{GeneratedTestRoot}/{scope}");

            try
            {
                var clip = CreateClipAsset($"{testRoot}/bwc_test.asset", clipId: 9101, phase: SourceWavePhaseId.Sustain);
                var definition = CreateDefinitionAsset($"{operationalRoot}/sd_operational.asset", stageId: 2);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1001u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 0,
                        SustainSlots = new[]
                        {
                            new SustainSlotBinding
                            {
                                State = SourceStateId.Normal,
                                Lane = SourceSpawnLaneId.Hazard,
                                Clips = new[] { clip },
                                Weights = new[] { 1f },
                            }
                        },
                        EventSlots = System.Array.Empty<EventSlotBinding>(),
                    }
                };
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();

                var layout = CreateLayoutAsset($"{operationalRoot}/sl_operational.asset", stageId: 2);
                var catalog = CreateCatalogAsset(
                    $"{operationalRoot}/sc_operational.asset",
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_wave_test",
                        Definition = definition,
                        Layout = layout,
                    });

                var issues = ValidateCatalog(catalog, operationalRoot);
                Assert.That(HasIssue(issues, "STC025", ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                DeleteAssetFolder($"{GeneratedOperationalRoot}/{scope}");
                DeleteAssetFolder($"{GeneratedTestRoot}/{scope}");
            }
        }

        [Test]
        public void ValidateCatalog_HazardActorPlacements_DuplicatePlacementIds_AreReportedAsErrors()
        {
            var created = new List<ScriptableObject>();
            using var archetypeA = CreateHazardActorArchetype("archetype_a", actorId: 1, emitterId: 11);
            using var archetypeB = CreateHazardActorArchetype("archetype_b", actorId: 2, emitterId: 22);

            try
            {
                var definition = CreateDefinition(created, stageId: 12);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1201u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 0,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 5,
                                ActorArchetypePrefab = archetypeA.Root,
                                LocalOffset = new Vector3(1f, 0f, 0f),
                            },
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 5,
                                ActorArchetypePrefab = archetypeB.Root,
                                LocalOffset = new Vector3(-1f, 0f, 0f),
                            },
                        },
                    },
                };

                var layout = CreateLayout(created, stageId: 12);
                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_12",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC036", ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_HazardActorPlacements_NullPrefab_IsReportedAsError()
        {
            var created = new List<ScriptableObject>();
            try
            {
                var definition = CreateDefinition(created, stageId: 13);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1301u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 0,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 7,
                                ActorArchetypePrefab = null,
                                LocalOffset = Vector3.zero,
                            }
                        },
                    }
                };

                var layout = CreateLayout(created, stageId: 13);
                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_13",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC033", ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_HazardActorOrchestration_InvalidRuleTargetsAndThresholds_AreReported()
        {
            var created = new List<ScriptableObject>();
            using var archetype = CreateHazardActorArchetype("orchestration_phase_archetype", actorId: 3, emitterId: 33);

            try
            {
                var actor = archetype.Root.GetComponent<HazardActorAuthoring>();
                actor.ActivationTrigger = HazardActorPresenceTriggerMode.Immediate;
                actor.PhaseSelectorPolicies = new[]
                {
                    new HazardActorPhaseSelectorPolicyAuthoring
                    {
                        PhaseId = 1,
                        SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                        Candidates = new[]
                        {
                            new HazardActorPhaseSelectorCandidateAuthoring { PatternSlotId = 1 },
                        },
                    },
                    new HazardActorPhaseSelectorPolicyAuthoring
                    {
                        PhaseId = 2,
                        SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                        Candidates = new[]
                        {
                            new HazardActorPhaseSelectorCandidateAuthoring { PatternSlotId = 1 },
                        },
                    },
                };
                actor.PhaseProgressTransitions = new[]
                {
                    new HazardActorPhaseProgressTransitionAuthoring
                    {
                        FromPhaseId = 1,
                        ToPhaseId = 2,
                        ProgressThresholdNormalized = 0.5f,
                        TransitionLeadInSec = 0.25f,
                    }
                };

                var definition = CreateDefinition(created, stageId: 13);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1301u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 4,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 41,
                                ActorArchetypePrefab = archetype.Root,
                                LocalOffset = Vector3.zero,
                            }
                        },
                        HazardActorOrchestrationRules = new[]
                        {
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 1,
                                TargetPlacementInstanceId = 999,
                                ActionType = HazardActorOrchestrationActionId.Spawn,
                                TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                            },
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 2,
                                TargetPlacementInstanceId = 41,
                                ActionType = HazardActorOrchestrationActionId.PhaseSet,
                                TriggerType = HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove,
                                TriggerThresholdNormalized = 1.25f,
                                TargetPhaseId = 3,
                            },
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 3,
                                TargetPlacementInstanceId = 41,
                                ActionType = HazardActorOrchestrationActionId.PhaseSet,
                                TriggerType = HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove,
                                TriggerThresholdNormalized = 0.5f,
                                TargetPhaseId = 2,
                            },
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 4,
                                TargetPlacementInstanceId = 41,
                                ActionType = HazardActorOrchestrationActionId.PhaseSet,
                                TriggerType = HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove,
                                TriggerThresholdNormalized = 0.75f,
                                TargetPhaseId = 2,
                            },
                        },
                    }
                };

                var layout = CreateLayout(created, stageId: 13);
                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_13",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC040", ContentValidationSeverity.Error), Is.True);
                Assert.That(HasIssue(issues, "STC041", ContentValidationSeverity.Error), Is.True);
                Assert.That(HasIssue(issues, "STC042", ContentValidationSeverity.Error), Is.True);
                Assert.That(HasIssue(issues, "STC045", ContentValidationSeverity.Error), Is.True);
                Assert.That(HasIssue(issues, "STC044", ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_HazardActorOrchestration_ExactSameTriggerConflicts_AreReportedAsWarning()
        {
            var created = new List<ScriptableObject>();
            using var archetype = CreateHazardActorArchetype("orchestration_conflict_archetype", actorId: 4, emitterId: 44);

            try
            {
                var definition = CreateDefinition(created, stageId: 14);
                definition.SourceBindings = new[]
                {
                    new StageSourceBinding
                    {
                        SourceStableId = 1401u,
                        InitialSourceState = SourceStateId.Normal,
                        ThresholdWeakened = 0,
                        ThresholdDepleted = 4,
                        SustainSlots = Array.Empty<SustainSlotBinding>(),
                        EventSlots = Array.Empty<EventSlotBinding>(),
                        HazardActorPlacements = new[]
                        {
                            new HazardActorPlacementBinding
                            {
                                PlacementInstanceId = 51,
                                ActorArchetypePrefab = archetype.Root,
                                LocalOffset = Vector3.zero,
                            }
                        },
                        HazardActorOrchestrationRules = new[]
                        {
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 1,
                                TargetPlacementInstanceId = 51,
                                ActionType = HazardActorOrchestrationActionId.Spawn,
                                TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                            },
                            new HazardActorOrchestrationRuleBinding
                            {
                                RuleId = 2,
                                TargetPlacementInstanceId = 51,
                                ActionType = HazardActorOrchestrationActionId.Retire,
                                TriggerType = HazardActorOrchestrationTriggerId.OnStageStart,
                            },
                        },
                    }
                };

                var layout = CreateLayout(created, stageId: 14);
                var catalog = CreateCatalog(created, new StageCatalogEntry
                {
                    Enabled = true,
                    EntryKey = "stage_14",
                    Definition = definition,
                    Layout = layout,
                });

                var issues = ValidateCatalog(catalog);
                Assert.That(HasIssue(issues, "STC043", ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                DestroyAll(created);
            }
        }

        [Test]
        public void ValidateCatalog_TestOnlyCatalogMayReferenceOperationalAssets()
        {
            string scope = System.Guid.NewGuid().ToString("N");
            string operationalRoot = EnsureFolder($"{GeneratedOperationalRoot}/{scope}");
            string testRoot = EnsureFolder($"{GeneratedTestRoot}/{scope}");

            try
            {
                var definition = CreateDefinitionAsset($"{operationalRoot}/sd_operational.asset", stageId: 3);
                var layout = CreateLayoutAsset($"{operationalRoot}/sl_operational.asset", stageId: 3);
                var catalog = CreateCatalogAsset(
                    $"{testRoot}/sc_test.asset",
                    new StageCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "stage_test_only_catalog",
                        Definition = definition,
                        Layout = layout,
                    });

                var issues = ValidateCatalog(catalog, testRoot);
                Assert.That(issues.Any(i => i.Code == "STC023" || i.Code == "STC024" || i.Code == "STC025"), Is.False);
            }
            finally
            {
                DeleteAssetFolder($"{GeneratedOperationalRoot}/{scope}");
                DeleteAssetFolder($"{GeneratedTestRoot}/{scope}");
            }
        }

        private static List<ContentValidationIssue> ValidateCatalog(StageCatalogSO catalog, string location = "catalog")
        {
            var issues = new List<ContentValidationIssue>();
            StageCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<StageCatalogSO>(catalog, location)
                },
                issues);
            return issues;
        }

        private static bool HasIssue(IEnumerable<ContentValidationIssue> issues, string code, ContentValidationSeverity severity)
        {
            return issues.Any(issue => issue.Code == code && issue.Severity == severity);
        }

        private static StageCatalogSO CreateCatalog(List<ScriptableObject> created, params StageCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            catalog.SchemaVersion = 1;
            catalog.Entries = entries;
            created.Add(catalog);
            return catalog;
        }

        private static StageCatalogSO CreateCatalogAsset(string assetPath, params StageCatalogEntry[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<StageCatalogSO>();
            catalog.SchemaVersion = 1;
            catalog.Entries = entries;
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<StageCatalogSO>(assetPath);
        }

        private static StageDefinitionSO CreateDefinition(List<ScriptableObject> created, int stageId)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = stageId;
            definition.DisplayName = $"Stage {stageId}";
            definition.StageTimeLimitSec = 120f;
            definition.SourceBindings = Array.Empty<StageSourceBinding>();
            created.Add(definition);
            return definition;
        }

        private static StageDefinitionSO CreateDefinitionAsset(string assetPath, int stageId)
        {
            var definition = ScriptableObject.CreateInstance<StageDefinitionSO>();
            definition.StageId = stageId;
            definition.DisplayName = $"Stage {stageId}";
            definition.StageTimeLimitSec = 120f;
            definition.SourceBindings = System.Array.Empty<StageSourceBinding>();
            AssetDatabase.CreateAsset(definition, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<StageDefinitionSO>(assetPath);
        }

        private static StageLayoutSO CreateLayout(List<ScriptableObject> created, int stageId)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.SchemaVersion = 2;
            layout.StageId = stageId;
            layout.Grid = new StageGridSpec
            {
                Width = 1,
                Height = 1,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            layout.Cells = new StageCellLayoutData[1];
            layout.SourceRegions = Array.Empty<StageSourceRegionLayoutData>();
            layout.DepositRegions = Array.Empty<StageDepositRegionLayoutData>();
            layout.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = Vector2Int.zero,
                AnchorOffset = Vector2.zero,
                YawDeg = 0f,
            };
            layout.Presentations = Array.Empty<StagePresentationLayoutData>();
            created.Add(layout);
            return layout;
        }

        private static StageLayoutSO CreateLayoutAsset(string assetPath, int stageId)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.SchemaVersion = 2;
            layout.StageId = stageId;
            layout.Grid = new StageGridSpec
            {
                Width = 1,
                Height = 1,
                CellSize = 1f,
                Origin = Vector3.zero,
            };
            layout.Cells = new StageCellLayoutData[1];
            layout.SourceRegions = System.Array.Empty<StageSourceRegionLayoutData>();
            layout.DepositRegions = System.Array.Empty<StageDepositRegionLayoutData>();
            layout.PlayerStart = new StagePlayerStartLayoutData
            {
                Active = true,
                AnchorCell = Vector2Int.zero,
                AnchorOffset = Vector2.zero,
                YawDeg = 0f,
            };
            layout.Presentations = System.Array.Empty<StagePresentationLayoutData>();
            AssetDatabase.CreateAsset(layout, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<StageLayoutSO>(assetPath);
        }

        private static WaveClipSO CreateClip(List<ScriptableObject> created, int clipId, SourceWavePhaseId phase)
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            clip.ClipId = clipId;
            clip.Phase = phase;
            clip.DurationSec = 1f;
            clip.Segments = Array.Empty<WaveClipSO.ClipSegment>();
            created.Add(clip);
            return clip;
        }

        private static WaveClipSO CreateClipAsset(string assetPath, int clipId, SourceWavePhaseId phase)
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            clip.ClipId = clipId;
            clip.Phase = phase;
            clip.DurationSec = 1f;
            clip.Segments = System.Array.Empty<WaveClipSO.ClipSegment>();
            AssetDatabase.CreateAsset(clip, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<WaveClipSO>(assetPath);
        }

        private static HazardEmitterTelegraphProfileSO CreateTelegraphProfileAsset(string assetPath)
        {
            var profile = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<HazardEmitterTelegraphProfileSO>(assetPath);
        }

        private static HazardEmitterEmissionProfileSO CreateEmissionProfileAsset(string assetPath)
        {
            var profile = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<HazardEmitterEmissionProfileSO>(assetPath);
        }

        private static string EnsureFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalized))
                return normalized;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }

            return normalized;
        }

        private static void DeleteAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(normalized))
                return;

            AssetDatabase.DeleteAsset(normalized);
            AssetDatabase.Refresh();
        }

        private static void DestroyAll(List<ScriptableObject> created)
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }

        private static HazardActorArchetypeFixture CreateHazardActorArchetype(string name, int actorId, int emitterId)
        {
            var fixture = new HazardActorArchetypeFixture();
            fixture.Root = new GameObject(name);
            var actor = fixture.Root.AddComponent<HazardActorAuthoring>();
            actor.ActorId = actorId;

            fixture.Telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            fixture.Bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            fixture.Bullet.Editor_SetDefinitionId(5000 + emitterId);
            fixture.Emission = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            fixture.Emission.Bullet = fixture.Bullet;
            fixture.Emission.PositionPattern = new SinglePointPositionPatternAuthoring();
            fixture.Emission.Aim = new FixedAimAuthoring();
            fixture.Emission.ShotPattern = new SingleShotPatternAuthoring();
            fixture.Emission.EventRepeatCount = 1;
            fixture.Emission.EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
            fixture.Emission.EventShotIntervalSec = 0f;
            fixture.Emission.CooldownSec = 1f;

            actor.PatternSlots = new[]
            {
                new HazardActorPatternSlotAuthoring
                {
                    PatternSlotId = 1,
                    TelegraphProfile = fixture.Telegraph,
                    EmissionProfile = fixture.Emission,
                    BaseWeight = 1f,
                    AvailabilityFlags = 0u,
                }
            };

            return fixture;
        }
    }
}
