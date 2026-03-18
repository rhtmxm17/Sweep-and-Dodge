using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class InWorldDialogueCatalogValidationRulesTests
    {
        [Test]
        public void ValidateCatalog_DuplicateEntryKey_IsReportedAsError()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    CreateEntry("dup_entry", InWorldDialogueTriggerId.StageStart, 1),
                    CreateEntry("dup_entry", InWorldDialogueTriggerId.StageClear, 1),
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD017" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_DuplicateEnabledTriggerTarget_IsReportedAsError()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    CreateEntry("entry_a", InWorldDialogueTriggerId.StageStart, 1),
                    CreateEntry("entry_b", InWorldDialogueTriggerId.StageStart, 1),
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD018" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_InvalidTargetPayloadCombinations_AreReportedAsErrors()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "bad_stage",
                        Trigger = InWorldDialogueTriggerId.StageStart,
                        TargetKind = InWorldDialogueTargetKind.Stage,
                        StageId = 0,
                        ThemeKey = "should_be_empty",
                        BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                        RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                        FullVariant = CreateFullVariant(),
                    },
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "bad_theme",
                        Trigger = InWorldDialogueTriggerId.ThemeTransition,
                        TargetKind = InWorldDialogueTargetKind.Theme,
                        StageId = 2,
                        ThemeKey = string.Empty,
                        BlockingMode = InWorldDialogueBlockingMode.ShellOverlay,
                        RetryPolicy = InWorldDialogueRetryPolicy.OncePerSession,
                        FullVariant = CreateFullVariant(),
                    },
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "bad_global",
                        Trigger = InWorldDialogueTriggerId.ThemeTransition,
                        TargetKind = InWorldDialogueTargetKind.Global,
                        StageId = 1,
                        ThemeKey = "nope",
                        BlockingMode = InWorldDialogueBlockingMode.ShellOverlay,
                        RetryPolicy = InWorldDialogueRetryPolicy.OncePerSession,
                        FullVariant = CreateFullVariant(),
                    },
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD005" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(issues.Any(x => x.Code == "IWD006" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(issues.Any(x => x.Code == "IWD007" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_MissingSpeakerKeyAndEmptyFullVariant_AreReportedAsErrors()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = ScriptableObject.CreateInstance<InWorldDialogueSpeakerCatalogSO>();
            speakerCatalog.Profiles = System.Array.Empty<InWorldDialogueSpeakerProfile>();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "missing_speaker",
                        Trigger = InWorldDialogueTriggerId.StageStart,
                        TargetKind = InWorldDialogueTargetKind.Stage,
                        StageId = 1,
                        BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                        RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                        FullVariant = new InWorldDialogueSequenceVariant
                        {
                            Lines = new[]
                            {
                                new InWorldDialogueLine
                                {
                                    SpeakerKey = "unknown",
                                    Text = "Hello",
                                },
                            },
                        },
                    },
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "empty_full",
                        Trigger = InWorldDialogueTriggerId.StageClear,
                        TargetKind = InWorldDialogueTargetKind.Stage,
                        StageId = 2,
                        BlockingMode = InWorldDialogueBlockingMode.GateClear,
                        RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                        FullVariant = default,
                    },
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD012" && x.Severity == ContentValidationSeverity.Error), Is.True);
                Assert.That(issues.Any(x => x.Code == "IWD009" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_ShortOnRetryWithoutRetryVariant_IsReportedAsWarning()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "short_retry",
                        Trigger = InWorldDialogueTriggerId.StageStart,
                        TargetKind = InWorldDialogueTargetKind.Stage,
                        StageId = 1,
                        BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                        RetryPolicy = InWorldDialogueRetryPolicy.ShortOnRetry,
                        FullVariant = CreateFullVariant(),
                        RetryVariant = default,
                    },
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD013" && x.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_InterventionStageAndGlobalTargets_AreAccepted()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    CreateEntry("intervention_stage", InWorldDialogueTriggerId.InterventionCarryFull, 1),
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "intervention_global",
                        Trigger = InWorldDialogueTriggerId.InterventionFirstHit,
                        TargetKind = InWorldDialogueTargetKind.Global,
                        StageId = 0,
                        ThemeKey = string.Empty,
                        BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                        RetryPolicy = InWorldDialogueRetryPolicy.OncePerSession,
                        FullVariant = CreateFullVariant(),
                    },
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Severity == ContentValidationSeverity.Error), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_InterventionThemeTarget_IsReportedAsError()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    new InWorldDialogueCatalogEntry
                    {
                        Enabled = true,
                        EntryKey = "bad_intervention_theme",
                        Trigger = InWorldDialogueTriggerId.InterventionCarryFull,
                        TargetKind = InWorldDialogueTargetKind.Theme,
                        StageId = 0,
                        ThemeKey = "forbidden",
                        BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                        RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                        FullVariant = CreateFullVariant(),
                    },
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD019" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        [Test]
        public void ValidateCatalog_InterventionNonOverlayBlockingMode_IsReportedAsError()
        {
            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            var speakerCatalog = CreateSpeakerCatalog();

            try
            {
                dialogueCatalog.Entries = new[]
                {
                    CreateInterventionEntryWithBlockingMode(
                        "bad_intervention_blocking",
                        InWorldDialogueTriggerId.InterventionFirstHit,
                        1,
                        InWorldDialogueBlockingMode.GateIntro),
                };

                var issues = Validate(dialogueCatalog, speakerCatalog);
                Assert.That(issues.Any(x => x.Code == "IWD020" && x.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(dialogueCatalog);
                Object.DestroyImmediate(speakerCatalog);
            }
        }

        private static List<ContentValidationIssue> Validate(
            InWorldDialogueCatalogSO dialogueCatalog,
            InWorldDialogueSpeakerCatalogSO speakerCatalog)
        {
            var issues = new List<ContentValidationIssue>();
            InWorldDialogueCatalogValidationRules.ValidateCatalogRecords(
                new[]
                {
                    new ContentValidationRecord<InWorldDialogueCatalogSO>(dialogueCatalog, "dialogue_catalog"),
                },
                new[]
                {
                    new ContentValidationRecord<InWorldDialogueSpeakerCatalogSO>(speakerCatalog, "speaker_catalog"),
                },
                issues);
            return issues;
        }

        private static InWorldDialogueSpeakerCatalogSO CreateSpeakerCatalog()
        {
            var catalog = ScriptableObject.CreateInstance<InWorldDialogueSpeakerCatalogSO>();
            catalog.Profiles = new[]
            {
                new InWorldDialogueSpeakerProfile
                {
                    SpeakerKey = "hero",
                    DisplayName = "Hero",
                    PortraitSide = DialoguePortraitSide.Left,
                },
            };
            return catalog;
        }

        private static InWorldDialogueCatalogEntry CreateEntry(string entryKey, InWorldDialogueTriggerId trigger, int stageId)
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = entryKey,
                Trigger = trigger,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = stageId,
                BlockingMode = trigger == InWorldDialogueTriggerId.StageClear
                    ? InWorldDialogueBlockingMode.GateClear
                    : InWorldDialogueBlockingMode.OverlayOnly,
                RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                FullVariant = CreateFullVariant(),
            };
        }

        private static InWorldDialogueCatalogEntry CreateInterventionEntryWithBlockingMode(
            string entryKey,
            InWorldDialogueTriggerId trigger,
            int stageId,
            InWorldDialogueBlockingMode blockingMode)
        {
            var entry = CreateEntry(entryKey, trigger, stageId);
            entry.BlockingMode = blockingMode;
            return entry;
        }

        private static InWorldDialogueSequenceVariant CreateFullVariant()
        {
            return new InWorldDialogueSequenceVariant
            {
                Lines = new[]
                {
                    new InWorldDialogueLine
                    {
                        SpeakerKey = "hero",
                        Text = "Ready.",
                        Anchor = new InWorldDialogueAnchorRef
                        {
                            Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                            ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                        },
                        MinHoldSec = 0f,
                        AutoAdvanceSec = 0f,
                    },
                },
            };
        }
    }
}
