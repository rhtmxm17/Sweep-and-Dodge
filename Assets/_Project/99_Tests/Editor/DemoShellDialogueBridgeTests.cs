using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class DemoShellDialogueBridgeTests
    {
        private static readonly MethodInfo UpdateStartTriggerStateMethod = typeof(DemoShellDialogueBridge)
            .GetMethod("UpdateStartTriggerState", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo TickMethod = typeof(DemoShellDialogueBridge)
            .GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo HandlePreResultClearPresentationRequestedMethod = typeof(DemoShellDialogueBridge)
            .GetMethod("HandlePreResultClearPresentationRequested", BindingFlags.Instance | BindingFlags.NonPublic);

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
        public void StageStart_RunningEdge_StartsOnlyOncePerStage()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Starting);

            SetPrivateField(context.Bridge, "_wasRunningLastFrame", false);
            InvokeUpdateStartTriggerState(context.Bridge);
            Assert.That(context.Bridge.IsDialogueActive, Is.False);

            SetPrivateField(context.Shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);

            Assert.That(context.Bridge.IsDialogueActive, Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Visible, Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageStart));
            Assert.That(context.Bridge.CurrentPresentation.EntryKey, Is.EqualTo("stage1_start"));

            context.Bridge.Skip();
            InvokeUpdateStartTriggerState(context.Bridge);
            Assert.That(context.Bridge.IsDialogueActive, Is.False, "Running state must not retrigger without a new stage edge.");
        }

        [Test]
        public void RetryPolicies_UseAttemptCountAndSessionSeenRules()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);
            Assert.That(context.Bridge.CurrentPresentation.BodyText, Is.EqualTo("Full intro"));
            context.Bridge.Skip();
            Assert.That(DemoShellSessionStaging.HasSeenDialogueEntry("stage1_start"), Is.True);

            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.Lobby);
            InvokeUpdateStartTriggerState(context.Bridge);
            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);
            Assert.That(context.Bridge.CurrentPresentation.BodyText, Is.EqualTo("Retry intro"));
            context.Bridge.Skip();

            Assert.That(context.Bridge.TryStartThemeTransition("forest"), Is.True);
            context.Bridge.Skip();
            Assert.That(context.Bridge.TryStartThemeTransition("forest"), Is.False, "OncePerSession entry must not replay after skip completion.");
        }

        [Test]
        public void Advance_RespectsMinHold_AndAutoAdvance_CompletesSequence()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);

            Assert.That(context.Bridge.Advance(), Is.False, "Advance must be blocked before MinHoldSec.");
            InvokeTick(context.Bridge, 0.25f);
            Assert.That(context.Bridge.Advance(), Is.True);
            Assert.That(context.Bridge.CurrentPresentation.LineIndex, Is.EqualTo(1));

            InvokeTick(context.Bridge, 0.11f);
            Assert.That(context.Bridge.IsDialogueActive, Is.False, "AutoAdvanceSec should complete the final line.");
        }

        [Test]
        public void ClearTrigger_WithoutCandidate_CompletesShellFallbackImmediately()
        {
            using var context = CreateContext(withBoundStageBridge: true);
            context.DialogueCatalog.Entries = new[]
            {
                CreateStageStartEntry(),
            };

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.ClearPresentation);
            SetPrivateField(context.Shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.ClearPresentation);
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Shell, "_pendingClearResult", new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 10f,
            });
            SetPrivateField(context.Shell, "_hasPendingClearResult", true);

            InvokeHandlePreResultClearPresentationRequested(context.Bridge, new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 10f,
            });

            Assert.That(context.Bridge.IsDialogueActive, Is.False);
            Assert.That(context.Shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.AwaitingClearCompleted));
        }

        [Test]
        public void ClearTrigger_WithCandidate_CompletesShellExactlyOnce()
        {
            using var context = CreateContext(withBoundStageBridge: true);

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.ClearPresentation);
            SetPrivateField(context.Shell, "_currentStagePlayPhase", DemoShellStagePlayPhaseId.ClearPresentation);
            SetPrivateField(context.Shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(context.Shell, "_currentStageIndex", 0);
            SetPrivateField(context.Shell, "_pendingClearResult", new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 10f,
            });
            SetPrivateField(context.Shell, "_hasPendingClearResult", true);

            InvokeHandlePreResultClearPresentationRequested(context.Bridge, new DemoShellStageResultMetrics
            {
                StageId = 1,
                Outcome = DemoShellStageOutcomeId.Clear,
                ElapsedSec = 10f,
            });

            Assert.That(context.Bridge.IsDialogueActive, Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageClear));
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.True);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.True);
            Assert.That(context.Bridge.Skip(), Is.True);
            Assert.That(context.Shell.CurrentStagePlayPhase, Is.EqualTo(DemoShellStagePlayPhaseId.AwaitingClearCompleted));
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.False);
            Assert.That(context.Shell.NotifyPreResultClearPresentationCompleted(), Is.False, "Shell completion must only be sent once by bridge.");
        }

        [Test]
        public void StageStartGateIntro_AcquiresAndReleasesPauseHandle_OnSkip()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);

            Assert.That(context.Bridge.IsDialogueActive, Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageStart));
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.True);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.True);
            Assert.That(context.PauseController.CurrentSnapshot.IsPauseMenuOpenBlocked, Is.True);

            Assert.That(context.Bridge.Skip(), Is.True);
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.False);
        }

        [Test]
        public void StageStartGateIntro_ReleasesPauseHandle_OnComplete()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);

            Assert.That(context.Bridge.IsDialogueActive, Is.True);
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.True);

            InvokeTick(context.Bridge, 0.25f);
            Assert.That(context.Bridge.Advance(), Is.True);
            InvokeTick(context.Bridge, 0.11f);

            Assert.That(context.Bridge.IsDialogueActive, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.False);
        }

        [Test]
        public void StageStartOverlay_DoesNotAcquirePauseHandle()
        {
            using var context = CreateContext();
            context.DialogueCatalog.Entries = new[]
            {
                CreateStageStartEntry(InWorldDialogueBlockingMode.OverlayOnly),
                CreateStageClearEntry(),
                CreateThemeTransitionEntry(),
                CreateInterventionCarryFullEntry(),
                CreateInterventionFirstHitEntry(),
            };

            DemoShellSessionStaging.IncrementDialogueStageAttempt(1);
            SetShellStagePlay(context.Shell, stageIndex: 0, DemoShellStagePlayPhaseId.Running);
            InvokeUpdateStartTriggerState(context.Bridge);

            Assert.That(context.Bridge.IsDialogueActive, Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.StageStart));
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.False);
        }

        [Test]
        public void TryStartStagePlayIntervention_OverlayOnly_DoesNotAcquirePauseHandle()
        {
            using var context = CreateContext();

            Assert.That(context.Bridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionCarryFull, 1), Is.True);
            Assert.That(context.Bridge.CurrentPresentation.Trigger, Is.EqualTo(InWorldDialogueTriggerId.InterventionCarryFull));
            Assert.That(context.PauseController.CurrentSnapshot.IsSimulationPaused, Is.False);
            Assert.That(context.PauseController.CurrentSnapshot.IsGameplayInputBlocked, Is.False);
        }

        [Test]
        public void InterventionCarryFull_CompleteAndSkip_MarkRunSeen()
        {
            using var context = CreateContext();

            DemoShellSessionStaging.BeginDialogueStageRun(1);
            Assert.That(context.Bridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionCarryFull, 1), Is.True);
            Assert.That(DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(1, InWorldDialogueTriggerId.InterventionCarryFull), Is.False);
            Assert.That(context.Bridge.Skip(), Is.True);
            Assert.That(DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(1, InWorldDialogueTriggerId.InterventionCarryFull), Is.True);

            DemoShellSessionStaging.BeginDialogueStageRun(1);
            Assert.That(context.Bridge.TryStartStagePlayIntervention(InWorldDialogueTriggerId.InterventionCarryFull, 1), Is.True);
            InvokeTick(context.Bridge, 0.11f);
            Assert.That(context.Bridge.IsDialogueActive, Is.False);
            Assert.That(DemoShellSessionStaging.HasSeenDialogueTriggerThisRun(1, InWorldDialogueTriggerId.InterventionCarryFull), Is.True);
        }

        [Test]
        public void HiddenPresentation_DefaultsToInvisibleSnapshot()
        {
            using var context = CreateContext();

            Assert.That(context.Bridge.IsDialogueActive, Is.False);
            Assert.That(context.Bridge.CurrentPresentation.Visible, Is.False);
            Assert.That(context.Bridge.CurrentPresentation.EntryKey, Is.EqualTo(string.Empty));
            Assert.That(context.Bridge.CurrentPresentation.BodyText, Is.EqualTo(string.Empty));
            Assert.That(context.Bridge.CurrentPresentation.LineCount, Is.EqualTo(0));
        }

        private static void InvokeUpdateStartTriggerState(DemoShellDialogueBridge bridge)
        {
            Assert.That(UpdateStartTriggerStateMethod, Is.Not.Null, "DemoShellDialogueBridge.UpdateStartTriggerState method not found.");
            UpdateStartTriggerStateMethod.Invoke(bridge, null);
        }

        private static void InvokeTick(DemoShellDialogueBridge bridge, float deltaSec)
        {
            Assert.That(TickMethod, Is.Not.Null, "DemoShellDialogueBridge.Tick method not found.");
            TickMethod.Invoke(bridge, new object[] { deltaSec });
        }

        private static void InvokeHandlePreResultClearPresentationRequested(
            DemoShellDialogueBridge bridge,
            DemoShellStageResultMetrics result)
        {
            Assert.That(HandlePreResultClearPresentationRequestedMethod, Is.Not.Null, "DemoShellDialogueBridge.HandlePreResultClearPresentationRequested method not found.");
            HandlePreResultClearPresentationRequestedMethod.Invoke(bridge, new object[] { result });
        }

        private static void SetShellStagePlay(DemoShellFlowController shell, int stageIndex, DemoShellStagePlayPhaseId phase)
        {
            SetPrivateField(shell, "_currentScreen", DemoShellScreenId.StagePlay);
            SetPrivateField(shell, "_currentStageIndex", stageIndex);
            SetPrivateField(shell, "_currentStagePlayPhase", phase);
        }

        private static void SetPrivateField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }

        private static TestContextData CreateContext(bool withBoundStageBridge = false)
        {
            var previousDefaultWorld = World.DefaultGameObjectInjectionWorld;
            World world = null;
            if (withBoundStageBridge)
            {
                world = new World("DemoShellDialogueBridge_TestWorld");
                World.DefaultGameObjectInjectionWorld = world;
                var em = world.EntityManager;
                em.CreateEntity(typeof(RunDirectorStageRequestComponent));
                em.CreateEntity(typeof(RunDirectorStageGateComponent));
                em.CreateEntity(typeof(RunDirectorStageSignalComponent));
            }

            var shellGo = new GameObject("DemoShellDialogueBridge_Test");
            shellGo.SetActive(false);

            var shell = shellGo.AddComponent<DemoShellFlowController>();
            shell.StageBridge = shellGo.GetComponent<RunDirectorStageBridge>();
            shell.TopologyBridge = shellGo.GetComponent<StageTopologyBridge>();
            shell.StageProfiles = new[]
            {
                new DemoShellStageProfile { StageId = 1, DisplayName = "Stage 1", StageTimeLimitSec = 60f },
                new DemoShellStageProfile { StageId = 2, DisplayName = "Stage 2", StageTimeLimitSec = 60f },
            };

            var runtimeUiGo = new GameObject("RuntimeUiRoot_Test");
            var runtimeUiRoot = runtimeUiGo.AddComponent<RuntimeUiRoot>();
            var pauseController = shellGo.AddComponent<DemoShellGameplayPauseController>();
            pauseController.LogBindWarnings = false;

            var dialogueCatalog = ScriptableObject.CreateInstance<InWorldDialogueCatalogSO>();
            dialogueCatalog.Entries = new[]
            {
                CreateStageStartEntry(),
                CreateStageClearEntry(),
                CreateThemeTransitionEntry(),
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
                    Portrait = null,
                    PortraitSide = DialoguePortraitSide.Left,
                },
            };

            var bridge = shellGo.AddComponent<DemoShellDialogueBridge>();
            bridge.DemoShell = shell;
            bridge.RuntimeUiRoot = runtimeUiRoot;
            bridge.PauseController = pauseController;
            bridge.DialogueCatalog = dialogueCatalog;
            bridge.SpeakerCatalog = speakerCatalog;
            bridge.LogBindWarnings = false;

            return new TestContextData(shellGo, runtimeUiGo, shell, pauseController, bridge, dialogueCatalog, speakerCatalog, world, previousDefaultWorld);
        }

        private static InWorldDialogueCatalogEntry CreateStageStartEntry(InWorldDialogueBlockingMode blockingMode = InWorldDialogueBlockingMode.GateIntro)
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "stage1_start",
                Trigger = InWorldDialogueTriggerId.StageStart,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = 1,
                Priority = 10,
                BlockingMode = blockingMode,
                RetryPolicy = InWorldDialogueRetryPolicy.ShortOnRetry,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Full intro",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.LowerCenter,
                            },
                            MinHoldSec = 0.2f,
                        },
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Second line",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.LeftActor,
                            },
                            AutoAdvanceSec = 0.1f,
                        },
                    },
                },
                RetryVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Retry intro",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.RightActor,
                            },
                        },
                    },
                },
            };
        }

        private static InWorldDialogueCatalogEntry CreateStageClearEntry()
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "stage1_clear",
                Trigger = InWorldDialogueTriggerId.StageClear,
                TargetKind = InWorldDialogueTargetKind.Stage,
                StageId = 1,
                Priority = 20,
                BlockingMode = InWorldDialogueBlockingMode.GateClear,
                RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Clear line",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.ScreenAnchor,
                                ScreenAnchor = InWorldDialogueScreenAnchorId.Center,
                            },
                        },
                    },
                },
            };
        }

        private static InWorldDialogueCatalogEntry CreateThemeTransitionEntry()
        {
            return new InWorldDialogueCatalogEntry
            {
                Enabled = true,
                EntryKey = "theme_forest_once",
                Trigger = InWorldDialogueTriggerId.ThemeTransition,
                TargetKind = InWorldDialogueTargetKind.Theme,
                ThemeKey = "forest",
                Priority = 5,
                BlockingMode = InWorldDialogueBlockingMode.ShellOverlay,
                RetryPolicy = InWorldDialogueRetryPolicy.OncePerSession,
                FullVariant = new InWorldDialogueSequenceVariant
                {
                    Lines = new[]
                    {
                        new InWorldDialogueLine
                        {
                            SpeakerKey = "hero",
                            Text = "Forest theme",
                            Anchor = new InWorldDialogueAnchorRef
                            {
                                Kind = InWorldDialogueAnchorKind.None,
                            },
                        },
                    },
                },
            };
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
                Priority = 30,
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
                            AutoAdvanceSec = 0.1f,
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
                Priority = 40,
                BlockingMode = InWorldDialogueBlockingMode.OverlayOnly,
                RetryPolicy = InWorldDialogueRetryPolicy.AlwaysFull,
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
                            AutoAdvanceSec = 0.1f,
                        },
                    },
                },
            };
        }

        private readonly struct TestContextData : System.IDisposable
        {
            private readonly GameObject _shellGo;
            private readonly GameObject _runtimeUiGo;
            private readonly World _world;
            private readonly World _previousDefaultWorld;
            public TestContextData(
                GameObject shellGo,
                GameObject runtimeUiGo,
                DemoShellFlowController shell,
                DemoShellGameplayPauseController pauseController,
                DemoShellDialogueBridge bridge,
                InWorldDialogueCatalogSO dialogueCatalog,
                InWorldDialogueSpeakerCatalogSO speakerCatalog,
                World world,
                World previousDefaultWorld)
            {
                _shellGo = shellGo;
                _runtimeUiGo = runtimeUiGo;
                _world = world;
                _previousDefaultWorld = previousDefaultWorld;
                Shell = shell;
                PauseController = pauseController;
                Bridge = bridge;
                DialogueCatalog = dialogueCatalog;
                SpeakerCatalog = speakerCatalog;
            }

            public DemoShellFlowController Shell { get; }
            public DemoShellGameplayPauseController PauseController { get; }
            public DemoShellDialogueBridge Bridge { get; }
            public InWorldDialogueCatalogSO DialogueCatalog { get; }
            public InWorldDialogueSpeakerCatalogSO SpeakerCatalog { get; }

            public void Dispose()
            {
                if (_shellGo != null)
                    UnityEngine.Object.DestroyImmediate(_shellGo);
                if (_runtimeUiGo != null)
                    UnityEngine.Object.DestroyImmediate(_runtimeUiGo);
                if (DialogueCatalog != null)
                    UnityEngine.Object.DestroyImmediate(DialogueCatalog);
                if (SpeakerCatalog != null)
                    UnityEngine.Object.DestroyImmediate(SpeakerCatalog);
                _world?.Dispose();
                World.DefaultGameObjectInjectionWorld = _previousDefaultWorld;
            }
        }
    }
}
