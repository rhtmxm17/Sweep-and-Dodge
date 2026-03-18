using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class StagePlayInterventionBridgeTests
    {
        private static readonly MethodInfo UpdateMethod = typeof(StagePlayInterventionBridge)
            .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo UpdateStartTriggerStateMethod = typeof(DemoShellDialogueBridge)
            .GetMethod("UpdateStartTriggerState", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [TearDown]
        public void TearDown()
        {
            DemoShellSessionStaging.ResetSessionMetrics();
            DemoShellSessionStaging.ResetHintSessionState();
            DemoShellSessionStaging.ResetDialogueSessionState();
        }

        [Test]
        public void FirstHit_UsesFeedbackVersionEdgeOnly()
        {
            using var context = CreateContext();
            SetHudSnapshot(context.RuntimeHudBridge, new PlayerHudSnapshotComponent { CarryCapacity = 5, CarryLoad = 1 });
            SetFeedbackSnapshot(context.RuntimeHudBridge, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 1u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Value = 1,
            });

            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.InterventionFirstHit));
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.True);
            Assert.That(context.DialogueBridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_first_hit"));

            context.DialogueBridge.Skip();
            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.None));
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.False);
        }

        [Test]
        public void CarryFull_StartsOncePerRun_AndMarksSeenOnSkip()
        {
            using var context = CreateContext();
            SetHudSnapshot(context.RuntimeHudBridge, new PlayerHudSnapshotComponent { CarryCapacity = 5, CarryLoad = 5 });

            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.InterventionCarryFull));
            Assert.That(context.DialogueBridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_carry_full"));
            Assert.That(DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(1, InWorldDialogueTriggerId.InterventionCarryFull), Is.False);

            context.DialogueBridge.Skip();
            Assert.That(DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(1, InWorldDialogueTriggerId.InterventionCarryFull), Is.True);

            InvokeUpdate(context.InterventionBridge);
            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.None));
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.False);
        }

        [Test]
        public void DoesNotStartWhileDialogueIsAlreadyActive()
        {
            using var context = CreateContext();
            SetHudSnapshot(context.RuntimeHudBridge, new PlayerHudSnapshotComponent { CarryCapacity = 5, CarryLoad = 5 });

            Assert.That(context.DialogueBridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionCarryFull, 1), Is.True);

            SetFeedbackSnapshot(context.RuntimeHudBridge, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 2u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Value = 1,
            });

            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.None));
            Assert.That(context.DialogueBridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_carry_full"));
        }

        [Test]
        public void GateDialogueActive_DropsIntervention_WithoutReplacingActivePresentation()
        {
            using var context = CreateContext();
            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            InvokeStartTrigger(context.DialogueBridge);
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.True);
            Assert.That(context.DialogueBridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageStart));

            SetFeedbackSnapshot(context.RuntimeHudBridge, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 5u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Value = 2,
            });

            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.None));
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.True);
            Assert.That(context.DialogueBridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageStart));
            Assert.That(context.DialogueBridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_start"));
        }

        [Test]
        public void DoesNotStartWhilePaused()
        {
            using var context = CreateContext();
            SetHudSnapshot(context.RuntimeHudBridge, new PlayerHudSnapshotComponent { CarryCapacity = 5, CarryLoad = 1 });
            SetFeedbackSnapshot(context.RuntimeHudBridge, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 3u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Value = 1,
            });

            Assert.That(context.PauseBridge.RequestPause(), Is.True);
            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.None));
            Assert.That(context.DialogueBridge.IsDialogueActive, Is.False);
        }

        [Test]
        public void FirstHit_TakesPriorityOverCarryFull()
        {
            using var context = CreateContext();
            SetHudSnapshot(context.RuntimeHudBridge, new PlayerHudSnapshotComponent { CarryCapacity = 5, CarryLoad = 5 });
            SetFeedbackSnapshot(context.RuntimeHudBridge, new PlayerUiFeedbackPresentationSnapshotComponent
            {
                Version = 4u,
                Type = PlayerUiFeedbackEventType.PlayerHazardHit,
                Value = 1,
            });

            InvokeUpdate(context.InterventionBridge);

            Assert.That(context.InterventionBridge.LastTriggeredIntervention, Is.EqualTo(InWorldDialogueTriggerId.InterventionFirstHit));
            Assert.That(context.DialogueBridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_first_hit"));
        }

        private static void InvokeUpdate(StagePlayInterventionBridge bridge)
        {
            Assert.That(UpdateMethod, Is.Not.Null, "StagePlayInterventionBridge.Update method not found.");
            UpdateMethod.Invoke(bridge, null);
        }

        private static void InvokeStartTrigger(DemoShellDialogueBridge bridge)
        {
            Assert.That(UpdateStartTriggerStateMethod, Is.Not.Null, "DemoShellDialogueBridge.UpdateStartTriggerState method not found.");
            UpdateStartTriggerStateMethod.Invoke(bridge, null);
        }

        private static void SetHudSnapshot(PlayerRuntimeHudBridge runtimeHudBridge, PlayerHudSnapshotComponent snapshot)
        {
            SetPrivateField(runtimeHudBridge, "_lastSnapshot", snapshot);
            SetPrivateField(runtimeHudBridge, "_hasSnapshot", true);
        }

        private static void SetFeedbackSnapshot(PlayerRuntimeHudBridge runtimeHudBridge, PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            SetPrivateField(runtimeHudBridge, "_lastFeedbackSnapshot", snapshot);
        }

        private static void SetShellStagePlay(DemoShellFlowController shell, int stageIndex)
        {
            SetPrivateField(shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(shell, "_currentStageIndex", stageIndex);
            SetPrivateField(shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.Running);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static TestContextData CreateContext()
        {
            var shellGo = new GameObject("StagePlayInterventionBridge_Test");
            shellGo.SetActive(false);

            var shell = shellGo.AddComponent<DemoShellFlowController>();
            shell.StageProfiles = new[]
            {
                new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", StageTimeLimitSec = 60f },
            };

            var pauseController = shellGo.AddComponent<DemoShellGameplayPauseController>();
            pauseController.LogBindWarnings = false;

            var pauseBridge = shellGo.AddComponent<DemoShellPauseBridge>();
            pauseBridge.DemoShell = shell;
            pauseBridge.PauseController = pauseController;
            pauseBridge.LogBindWarnings = false;

            var runtimeHudBridge = shellGo.AddComponent<PlayerRuntimeHudBridge>();
            runtimeHudBridge.DemoShell = shell;

            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            dialogueCatalog.Entries = new[]
            {
                CreateStageStartEntry(),
                CreateInterventionCarryFullEntry(),
                CreateInterventionFirstHitEntry(),
            };

            var speakerCatalog = ScriptableObject.CreateInstance<InWorldDialogueSpeakerCatalogSO>();
            speakerCatalog.Profiles = new[]
            {
                new InWorldDialogueSpeakerProfile
                {
                    SpeakerKey = "hero",
                    DisplayName = "Hero",
                    PortraitSide = DialoguePortraitSide.Left,
                },
            };

            var dialogueBridge = shellGo.AddComponent<DemoShellDialogueBridge>();
            dialogueBridge.DemoShell = shell;
            dialogueBridge.PauseController = pauseController;
            dialogueBridge.DialogueCatalog = dialogueCatalog;
            dialogueBridge.SpeakerCatalog = speakerCatalog;
            dialogueBridge.LogBindWarnings = false;

            var interventionBridge = shellGo.AddComponent<StagePlayInterventionBridge>();
            interventionBridge.DemoShell = shell;
            interventionBridge.RuntimeHudBridge = runtimeHudBridge;
            interventionBridge.DialogueBridge = dialogueBridge;
            interventionBridge.PauseBridge = pauseBridge;
            interventionBridge.LogBindWarnings = false;

            shellGo.SetActive(true);
            SetShellStagePlay(shell, stageIndex: 0);
            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            DemoShellSessionStaging.BeginDialogueStageRun(1);

            return new TestContextData(
                shellGo,
                shell,
                pauseBridge,
                runtimeHudBridge,
                dialogueBridge,
                interventionBridge,
                dialogueCatalog,
                speakerCatalog);
        }

        private static InWorldDialogueCatalogEntry CreateInterventionCarryFullEntry()
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "stage1_carry_full",
                Trigger = InWorldDialogueTriggerId.InterventionCarryFull,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = 1,
                Priority = 10,
                BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Carry is full",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                            },
                            AutoAdvanceSec = 0.25f,
                        },
                    },
                },
            };
        }

        private static InWorldDialogueCatalogEntry CreateInterventionFirstHitEntry()
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "stage1_first_hit",
                Trigger = InWorldDialogueTriggerId.InterventionFirstHit,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = 1,
                Priority = 20,
                BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                RetryPolicy = InWorldDialogueRetryPolicy.OncePerSession,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "That hurt",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                            },
                            AutoAdvanceSec = 0.25f,
                        },
                    },
                },
            };
        }

        private static InWorldDialogueCatalogEntry CreateStageStartEntry()
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "stage1_start",
                Trigger = InWorldDialogueTriggerId.StageStart,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = 1,
                Priority = 10,
                BlockingMode = InWorldDialogueBlockingMode.GateIntro,
                RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Stage intro",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                            },
                        },
                    },
                },
            };
        }

        private readonly struct TestContextData : System.IDisposable
        {
            private readonly GameObject _shellGo;

            public TestContextData(
                GameObject shellGo,
                DemoShellFlowController shell,
                DemoShellPauseBridge pauseBridge,
                PlayerRuntimeHudBridge runtimeHudBridge,
                DemoShellDialogueBridge dialogueBridge,
                StagePlayInterventionBridge interventionBridge,
                InWorldDialogueCatalogSO dialogueCatalog,
                InWorldDialogueSpeakerCatalogSO speakerCatalog)
            {
                _shellGo = shellGo;
                Shell = shell;
                PauseBridge = pauseBridge;
                RuntimeHudBridge = runtimeHudBridge;
                DialogueBridge = dialogueBridge;
                InterventionBridge = interventionBridge;
                DialogueCatalog = dialogueCatalog;
                SpeakerCatalog = speakerCatalog;
            }

            public DemoShellFlowController Shell { get; }
            public DemoShellPauseBridge PauseBridge { get; }
            public PlayerRuntimeHudBridge RuntimeHudBridge { get; }
            public DemoShellDialogueBridge DialogueBridge { get; }
            public StagePlayInterventionBridge InterventionBridge { get; }
            public InWorldDialogueCatalogSO DialogueCatalog { get; }
            public InWorldDialogueSpeakerCatalogSO SpeakerCatalog { get; }

            public void Dispose()
            {
                if (_shellGo != null)
                    Object.DestroyImmediate(_shellGo);
                if (DialogueCatalog != null)
                    Object.DestroyImmediate(DialogueCatalog);
                if (SpeakerCatalog != null)
                    Object.DestroyImmediate(SpeakerCatalog);
            }
        }
    }
}
