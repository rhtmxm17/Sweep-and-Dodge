using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StageCatalogValidationRulesTests
    {
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
                layout.Sources = new[]
                {
                    new StageSourceLayoutData
                    {
                        StableId = 777u,
                        Active = true,
                    }
                };

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
                layout.Sources = new[]
                {
                    new StageSourceLayoutData
                    {
                        StableId = 2002u,
                        Active = true,
                    }
                };

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

        private static List<ContentValidationIssue> ValidateCatalog(StageCatalogSO catalog)
        {
            var issues = new List<ContentValidationIssue>();
            StageCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<StageCatalogSO>(catalog, "catalog")
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

        private static StageLayoutSO CreateLayout(List<ScriptableObject> created, int stageId)
        {
            var layout = ScriptableObject.CreateInstance<StageLayoutSO>();
            layout.StageId = stageId;
            layout.Sources = Array.Empty<StageSourceLayoutData>();
            layout.Deposits = Array.Empty<StageDepositLayoutData>();
            layout.Obstacles = Array.Empty<StageObstacleLayoutData>();
            layout.Visuals = Array.Empty<StageVisualLayoutData>();
            created.Add(layout);
            return layout;
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

        private static void DestroyAll(List<ScriptableObject> created)
        {
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                    UnityEngine.Object.DestroyImmediate(created[i]);
            }

            created.Clear();
        }
    }
}
