using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Diagnostics;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public sealed class HazardActorWorkbenchPreviewTests
    {
        [Test]
        public void SnapshotBuilder_UsesAuthoringResolverExecutionData()
        {
            using (var setup = CreateActorSetup())
            {
                Assert.That(HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot), Is.True);
                Assert.That(snapshot.PatternSlots, Has.Length.EqualTo(2));

                Assert.That(HazardActorPatternSlotAuthoringUtility.TryResolveSlots(
                    setup.Actor.PatternSlots,
                    out var resolved,
                    out string error), Is.True, error);

                var expected = resolved.Single(x => x.Metadata.PatternSlotId == 1).Execution;
                var actual = snapshot.PatternSlots.Single(x => x.PatternSlotId == 1).Execution;
                Assert.That(actual.EmissionProfileRefId, Is.EqualTo(expected.EmissionProfileRefId));
                Assert.That(actual.PositionPatternMode, Is.EqualTo(expected.PositionPatternMode));
                Assert.That(actual.AimMode, Is.EqualTo(expected.AimMode));
                Assert.That(actual.ShotPatternMode, Is.EqualTo(expected.ShotPatternMode));
            }
        }

        [Test]
        public void WorkbenchCommands_EditPhasePatternAndProtectReferences()
        {
            using (var setup = CreateActorSetup())
            {
                Assert.That(HazardActorWorkbenchCommandUtility.AddPhase(setup.Actor, out int phaseId), Is.True);
                Assert.That(setup.Actor.PhaseSelectorPolicies.Any(x => x.PhaseId == phaseId), Is.True);
                Assert.That(HazardActorWorkbenchCommandUtility.DuplicatePattern(setup.Actor, 1, out int duplicatedPattern), Is.True);
                Assert.That(setup.Actor.PatternSlots.Any(x => x.PatternSlotId == duplicatedPattern), Is.True);
                Assert.That(HazardActorWorkbenchCommandUtility.RemovePattern(setup.Actor, 1, out string removeError), Is.False);
                Assert.That(removeError, Does.Contain("selector candidates reference"));
            }
        }

        [Test]
        public void PreviewSimulator_ReplaysPresenceSelectorTelegraphEmitCooldownDeterministically()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Actor,
                    SourceProgress01 = 0f,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                session.Step();
                Assert.That(session.Frame.Presence, Is.EqualTo(HazardActorPresenceStateId.Active));
                Assert.That(session.Frame.PatternSlotId, Is.EqualTo(1));
                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(0));

                session.Step();
                Assert.That(session.Frame.Lifecycle, Is.EqualTo(HazardActorEmitLifecycleStateId.Dormant));
                Assert.That(session.Frame.ActiveGhostCount, Is.GreaterThan(0));

                float firstTime = session.TimeSec;
                int firstCount = session.Frame.ActiveGhostCount;
                session.Restart();
                session.Step();
                session.Step();
                Assert.That(session.TimeSec, Is.EqualTo(firstTime));
                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(firstCount));
            }
        }

        [Test]
        public void PreviewCoordinator_AdvancesByWallClockInsteadOfCallbackCount()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Actor,
                    SourceProgress01 = 0f,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                double start = UnityEditor.EditorApplication.timeSinceStartup;
                HazardActorPreviewCoordinator.SetActiveSession(session);
                session.Play();
                HazardActorPreviewCoordinator.EvaluatePlaybackForTests(start);
                HazardActorPreviewCoordinator.EvaluatePlaybackForTests(start + 5.0);

                Assert.That(session.TimeSec, Is.InRange(4.9f, 5.1f));
                HazardActorPreviewCoordinator.Shutdown();
            }
        }

        [Test]
        public void PreviewCoordinator_ProducesSameStateAcrossCallbackCadence()
        {
            var low = RunCadencePreview(8);
            var high = RunCadencePreview(60);

            Assert.That(low.time, Is.EqualTo(high.time));
            Assert.That(low.ghostCount, Is.EqualTo(high.ghostCount));
            Assert.That(low.firstGhost, Is.EqualTo(high.firstGhost));
        }

        [Test]
        public void PreviewSimulator_UsesRepeatScheduleAndInterval()
        {
            using (var setup = CreateActorSetup())
            {
                setup.Actor.PatternSlots[0].Emission.EventRepeatCount = 3;
                setup.Actor.PatternSlots[0].Emission.EventShotSchedule = SourceSpawnEventShotScheduleId.Timed;
                setup.Actor.PatternSlots[0].Emission.EventShotIntervalSec = 0.1f;
                setup.Actor.PatternSlots[0].Emission.CooldownSec = 10f;
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Pattern,
                    ForcedPatternSlotId = 1,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                session.EvaluateAt(0.12f);
                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(1));
                session.EvaluateAt(0.30f);
                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(3));
            }
        }

        [Test]
        public void PreviewSimulator_LineEvenSpawnCountIsIndependentFromTrajectorySampleBudget()
        {
            using (var setup = CreateActorSetup())
            {
                setup.ProfileA.PositionPattern = new LineEvenPositionPatternAuthoring
                {
                    LineStart = Vector2.zero,
                    LineEnd = new Vector2(20f, 0f),
                    SampleSpacing = 1f,
                };
                setup.Actor.PatternSlots[0].Emission.CooldownSec = 10f;
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Pattern,
                    ForcedPatternSlotId = 1,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                session.EvaluateAt(0.10f);

                Assert.That(session.Frame.ActiveGhostCount, Is.GreaterThan(HazardActorPreviewSession.TrajectorySamplesPerBranch));
                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(21));
            }
        }

        [Test]
        public void PreviewSimulator_HandlesPhaseProgressAndOrderedCycle()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Actor,
                    SourceProgress01 = 1f,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                for (int i = 0; i < 6; i++)
                    session.Step();

                Assert.That(session.Frame.PhaseId, Is.EqualTo(2));
                Assert.That(session.Frame.PatternSlotId, Is.EqualTo(2));
            }
        }

        [Test]
        public void PreviewSimulator_AppliesMovementFamiliesAndGhostCap()
        {
            using (var setup = CreateActorSetup())
            {
                setup.ProfileA.MovementTuning.OverrideMovement = true;
                setup.ProfileA.MovementTuning.Family = BulletMovementFamilyId.HomingLite;
                setup.ProfileA.SpawnTuning.OverrideLifetime = true;
                setup.ProfileA.SpawnTuning.LifetimeOverride = 2f;
                setup.ProfileA.ShotPattern = new RadialShotPatternAuthoring { ShotCount = HazardActorPreviewSession.ActorGhostCap + 100 };

                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Pattern,
                    ForcedPatternSlotId = 1,
                    TargetWorldPosition = new Vector3(10f, 0f, 0f),
                    SpawnAtStart = true,
                });
                session.Step();
                session.Step();

                Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(HazardActorPreviewSession.ActorGhostCap));
                Assert.That(session.Frame.SuppressedGhostCount, Is.GreaterThan(0));
                Assert.That(session.Frame.Warning, Does.Contain("Ghost cap"));
                Assert.That(session.Ghosts[0].MovementFamily, Is.EqualTo(BulletMovementFamilyId.HomingLite));
            }
        }

        [Test]
        public void PreviewSimulator_SteadyStepDoesNotAllocateManagedMemory()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Actor,
                    SourceProgress01 = 0f,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                for (int i = 0; i < 12; i++)
                    session.Step();

                Assert.That(session.MeasureSteadyStepManagedAllocation(), Is.EqualTo(0L));
            }
        }

        [Test]
        public void EncounterPreviewSession_AggregatesMultipleActorsUnderSharedCap()
        {
            using (var setupA = CreateActorSetup())
            using (var setupB = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setupA.Root, out var snapshotA);
                HazardActorPreviewSnapshotBuilder.TryBuild(setupB.Root, out var snapshotB);
                var encounter = new HazardActorEncounterPreviewSession();
                encounter.AddActor(snapshotA, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Encounter,
                    GhostCapOverride = HazardActorPreviewSession.EncounterGhostCap / 2,
                    ActorWorldPosition = Vector3.zero,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });
                encounter.AddActor(snapshotB, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Encounter,
                    GhostCapOverride = HazardActorPreviewSession.EncounterGhostCap / 2,
                    ActorWorldPosition = new Vector3(2f, 0f, 0f),
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                encounter.Step();
                encounter.Step();

                Assert.That(encounter.ActiveActorCount, Is.EqualTo(2));
                Assert.That(encounter.Frame.ActiveGhostCount, Is.GreaterThan(0));
                Assert.That(encounter.Frame.ActiveGhostCount, Is.LessThanOrEqualTo(HazardActorPreviewSession.EncounterGhostCap));
                encounter.Dispose();
                Assert.That(encounter.IsDisposed, Is.True);
            }
        }

        [Test]
        public void EncounterPreviewSession_ReevaluatesSpawnPhaseSetRetireWhenProgressScrubsBackward()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var encounter = new HazardActorEncounterPreviewSession();
                encounter.AddActorPlan(
                    7,
                    snapshot,
                    new HazardActorPreviewInput
                    {
                        Scope = HazardActorPreviewScope.Encounter,
                        GhostCapOverride = HazardActorPreviewSession.EncounterGhostCap,
                        ActorWorldPosition = Vector3.zero,
                        TargetWorldPosition = new Vector3(0f, 0f, 5f),
                        SpawnAtStart = true,
                    },
                    new[]
                    {
                        new HazardActorEncounterRulePreview(1, 7, HazardActorOrchestrationActionId.Spawn, HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove, 0.2f, 0),
                        new HazardActorEncounterRulePreview(2, 7, HazardActorOrchestrationActionId.PhaseSet, HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove, 0.5f, 2),
                        new HazardActorEncounterRulePreview(3, 7, HazardActorOrchestrationActionId.Retire, HazardActorOrchestrationTriggerId.OnSourceProgressAtOrAbove, 0.8f, 0),
                    });

                encounter.SetSourceProgress(0f);
                Assert.That(encounter.ActiveActorCount, Is.EqualTo(0));

                encounter.SetSourceProgress(0.3f);
                encounter.EvaluateAt(0.2f);
                Assert.That(encounter.ActiveActorCount, Is.EqualTo(1));
                Assert.That(encounter.Actors[0].Frame.PhaseId, Is.EqualTo(1));

                encounter.SetSourceProgress(0.6f);
                encounter.EvaluateAt(0.2f);
                Assert.That(encounter.ActiveActorCount, Is.EqualTo(1));
                Assert.That(encounter.Actors[0].Frame.PhaseId, Is.EqualTo(2));

                encounter.SetSourceProgress(0.9f);
                Assert.That(encounter.ActiveActorCount, Is.EqualTo(0));

                encounter.SetSourceProgress(0.6f);
                encounter.EvaluateAt(0.2f);
                Assert.That(encounter.ActiveActorCount, Is.EqualTo(1));
                Assert.That(encounter.Actors[0].Frame.PhaseId, Is.EqualTo(2));
            }
        }

        [Test]
        public void PreviewRenderer_DoesNotUsePerGhostImmediateDrawPaths()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string core = File.ReadAllText(
                Path.Combine(root, "Assets/_Project/02_Scripts/ECS/Editor/HazardActorPreviewCore.cs"),
                Encoding.UTF8);
            string window = File.ReadAllText(
                Path.Combine(root, "Assets/_Project/02_Scripts/ECS/Editor/HazardActorWorkbenchWindow.cs"),
                Encoding.UTF8);

            Assert.That(core, Does.Not.Contain("Handles.DrawSolidDisc(ghosts"));
            Assert.That(core, Does.Not.Contain("Handles.DrawLine(ghosts"));
            Assert.That(window, Does.Not.Contain("GUI.DrawTexture"));
            Assert.That(window, Does.Not.Contain("_previewDensity"));
            Assert.That(window, Does.Contain("new HazardActorWorkbenchPreviewElement"));
        }

        [Test]
        public void WorkbenchPreviewProjection_PreservesExactPositionsAndRejectsOutOfViewPoints()
        {
            var rect = new Rect(0f, 0f, 320f, 160f);
            Assert.That(HazardActorWorkbenchPreviewElement.TryProjectWorldToPreview(
                new Vector3(-4f, 0f, -2f),
                Vector2.zero,
                8f,
                rect,
                out Vector2 first), Is.True);
            Assert.That(HazardActorWorkbenchPreviewElement.TryProjectWorldToPreview(
                new Vector3(4f, 0f, 2f),
                Vector2.zero,
                8f,
                rect,
                out Vector2 second), Is.True);

            Assert.That(first, Is.EqualTo(new Vector2(120f, 100f)));
            Assert.That(second, Is.EqualTo(new Vector2(200f, 60f)));
            Assert.That(Vector2.Distance(first, second), Is.GreaterThan(2f));
            Assert.That(HazardActorWorkbenchPreviewElement.TryProjectWorldToPreview(
                new Vector3(100f, 0f, 0f),
                Vector2.zero,
                8f,
                rect,
                out _), Is.False, "Out-of-view bullets must be clipped instead of clamped into an edge density cell.");

            var ghosts = new[]
            {
                new HazardActorPreviewGhost { Position = Vector3.zero },
                new HazardActorPreviewGhost { Position = new Vector3(100f, 0f, 0f) },
            };
            Assert.That(
                HazardActorWorkbenchPreviewElement.CountVisibleGhosts(ghosts, Vector2.zero, 8f, rect),
                Is.EqualTo(1));
        }

        [Test]
        public void WorkbenchPreview_DefaultsToExactAndUsesDensityOnlyWhenExplicitlySelected()
        {
            var session = new HazardActorPreviewSession();
            try
            {
                var preview = new HazardActorWorkbenchPreviewElement(session);
                Assert.That(preview.DisplayMode, Is.EqualTo(HazardActorPreviewDisplayMode.Exact));

                preview.DisplayMode = HazardActorPreviewDisplayMode.Density;
                Assert.That(preview.DisplayMode, Is.EqualTo(HazardActorPreviewDisplayMode.Density));
            }
            finally
            {
                session.Dispose();
            }
        }

        [Test]
        public void PreviewRenderer_SubmissionBudgetStaysWithinActorAndEncounterLimits()
        {
            Assert.That(
                HazardActorPreviewRendererUtility.EstimateDrawSubmissions(HazardActorPreviewSession.ActorGhostCap, drawAggregate: false),
                Is.LessThanOrEqualTo(3));
            Assert.That(
                HazardActorPreviewRendererUtility.EstimateDrawSubmissions(HazardActorPreviewSession.EncounterGhostCap, drawAggregate: false),
                Is.LessThanOrEqualTo(8));
            Assert.That(
                HazardActorPreviewRendererUtility.EstimateDrawSubmissions(HazardActorPreviewSession.EncounterGhostCap, drawAggregate: true),
                Is.LessThanOrEqualTo(8));
        }

        [Test]
        public void PreviewRenderer_ShutdownReleasesEditorResources()
        {
            using (var setup = CreateActorSetup())
            {
                var session = CreateHighCountSession(setup, HazardActorPreviewScope.Actor, 8);
                HazardActorPreviewRendererUtility.DrawGhostInstances(session.Ghosts, 0.08f, drawAggregate: true);
                Assert.That(HazardActorPreviewRendererUtility.HasAllocatedResources, Is.True);

                HazardActorPreviewCoordinator.SetActiveSession(session);
                HazardActorPreviewCoordinator.Shutdown();

                Assert.That(HazardActorPreviewRendererUtility.HasAllocatedResources, Is.False);
                Assert.That(HazardActorPreviewCoordinator.ActiveCallbackCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void PreviewRenderer_MeetsMeasuredActorAndEncounterFrameBudgets()
        {
            using (var actorSetup = CreateActorSetup())
            using (var encounterSetup = CreateActorSetup())
            {
                var actorSession = CreateHighCountSession(actorSetup, HazardActorPreviewScope.Actor, HazardActorPreviewSession.ActorGhostCap);
                var encounterSession = CreateHighCountSession(encounterSetup, HazardActorPreviewScope.Encounter, HazardActorPreviewSession.EncounterGhostCap);

                var actor = MeasurePreviewFrame(actorSession, 80, drawAggregate: false);
                var encounter = MeasurePreviewFrame(encounterSession, 80, drawAggregate: false);

                string actorSummary = $"Actor preview p95={actor.p95Ms:0.###}ms submissions={actor.maxSubmissions} gc={actor.allocationBytes}B";
                string encounterSummary = $"Encounter preview p95={encounter.p95Ms:0.###}ms submissions={encounter.maxSubmissions} gc={encounter.allocationBytes}B";
                TestContext.WriteLine(actorSummary);
                TestContext.WriteLine(encounterSummary);
                UnityEngine.Debug.Log($"[HazardActorPreviewPerformance] {actorSummary}; {encounterSummary}");
                Assert.That(actor.p95Ms, Is.LessThanOrEqualTo(8d));
                Assert.That(actor.maxSubmissions, Is.LessThanOrEqualTo(3));
                Assert.That(actor.allocationBytes, Is.EqualTo(0L));
                Assert.That(encounter.p95Ms, Is.LessThanOrEqualTo(16d));
                Assert.That(encounter.maxSubmissions, Is.LessThanOrEqualTo(8));
                Assert.That(encounter.allocationBytes, Is.EqualTo(0L));
            }
        }

        [Test]
        public void PreviewCoordinator_CleansCallbacksAndSessionState()
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput { Scope = HazardActorPreviewScope.Actor, SpawnAtStart = true });
                HazardActorPreviewCoordinator.SetActiveSession(session);
                Assert.That(HazardActorPreviewCoordinator.ActiveCallbackCount, Is.EqualTo(2));

                HazardActorPreviewCoordinator.ClearActiveSession(session);
                Assert.That(session.IsDisposed, Is.True);
                Assert.That(HazardActorPreviewCoordinator.ActiveCallbackCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void WorkbenchVisibility_UsesPopupArchetypePickerInsteadOfPersistentLibraryPanel()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string window = File.ReadAllText(
                Path.Combine(root, "Assets/_Project/02_Scripts/ECS/Editor/HazardActorWorkbenchWindow.cs"),
                Encoding.UTF8);

            Assert.That(window, Does.Contain("Change Archetype"));
            Assert.That(window, Does.Contain("PopupWindowContent"));
            Assert.That(window, Does.Contain("BuildArchetypeSummary"));
            Assert.That(window, Does.Not.Contain("Panel(\"Archetype Library\""));
            Assert.That(window, Does.Not.Contain("private ScrollView _library"));
            Assert.That(window, Does.Not.Contain("new UnityEditor.UIElements.ObjectField"));
        }

        [Test]
        public void WorkbenchVisibility_SummarizesArchetypePhaseAndPatternAsSeparatedFields()
        {
            using (var setup = CreateActorSetup())
            {
                var archetype = HazardActorWorkbenchWindow.BuildArchetypeSummary(setup.Root, setup.Root);
                Assert.That(archetype.Name, Is.EqualTo(setup.Root.name));
                Assert.That(archetype.PhaseCount, Is.EqualTo(2));
                Assert.That(archetype.PatternCount, Is.EqualTo(2));
                Assert.That(archetype.ProfileCount, Is.EqualTo(3));
                Assert.That(archetype.IssueLabel, Is.EqualTo("OK"));
                Assert.That(archetype.IsActive, Is.True);

                var phaseIssue = new HazardActorWorkbenchIssue(
                    ContentValidationSeverity.Warning,
                    "TEST_PHASE",
                    "phase warning",
                    HazardActorWorkbenchSelection.ForPhase(setup.Root, 1));
                var phase = HazardActorWorkbenchWindow.BuildPhaseRowSummary(
                    setup.Actor,
                    setup.Actor.PhaseSelectorPolicies[0],
                    new[] { phaseIssue });
                Assert.That(phase.PhaseLabel, Is.EqualTo("Phase 1"));
                Assert.That(phase.SelectorLabel, Is.EqualTo(HazardActorSelectionModeId.OrderedPriority.ToString()));
                Assert.That(phase.CandidatesLabel, Does.Contain("P1"));
                Assert.That(phase.TransitionLabel, Does.Contain("Phase 2"));
                Assert.That(phase.IssueLabel, Is.EqualTo("1 warn"));

                var patternIssue = new HazardActorWorkbenchIssue(
                    ContentValidationSeverity.Error,
                    "TEST_PATTERN",
                    "pattern error",
                    HazardActorWorkbenchSelection.ForPattern(setup.Root, 1));
                var pattern = HazardActorWorkbenchWindow.BuildPatternRowSummary(
                    setup.Actor.PatternSlots[0],
                    new[] { patternIssue });
                Assert.That(pattern.PatternLabel, Is.EqualTo("Pattern 1"));
                Assert.That(pattern.TelegraphLabel, Is.EqualTo("0s"));
                Assert.That(pattern.EmissionLabel, Is.EqualTo(setup.ProfileA.name));
                Assert.That(pattern.ScheduleLabel, Does.Contain("repeat x1"));
                Assert.That(pattern.MovementLabel, Is.Not.Empty);
                Assert.That(pattern.IssueLabel, Is.EqualTo("1 error"));
            }
        }

        private static (float time, int ghostCount, Vector3 firstGhost) RunCadencePreview(int hz)
        {
            using (var setup = CreateActorSetup())
            {
                HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
                var session = new HazardActorPreviewSession();
                session.Load(snapshot, new HazardActorPreviewInput
                {
                    Scope = HazardActorPreviewScope.Actor,
                    SourceProgress01 = 0f,
                    TargetWorldPosition = new Vector3(0f, 0f, 5f),
                    SpawnAtStart = true,
                });

                double start = UnityEditor.EditorApplication.timeSinceStartup;
                HazardActorPreviewCoordinator.SetActiveSession(session);
                session.Play();
                HazardActorPreviewCoordinator.EvaluatePlaybackForTests(start);
                int callbacks = Mathf.CeilToInt(hz * 1f);
                for (int i = 1; i <= callbacks; i++)
                    HazardActorPreviewCoordinator.EvaluatePlaybackForTests(start + ((double)i / hz));

                Vector3 first = session.Ghosts.Count > 0 ? session.Ghosts[0].Position : Vector3.zero;
                var result = (session.TimeSec, session.Frame.ActiveGhostCount, first);
                HazardActorPreviewCoordinator.Shutdown();
                return result;
            }
        }

        private static HazardActorPreviewSession CreateHighCountSession(
            ActorSetup setup,
            HazardActorPreviewScope scope,
            int ghostCount)
        {
            setup.ProfileA.SpawnTuning.OverrideLifetime = true;
            setup.ProfileA.SpawnTuning.LifetimeOverride = 20f;
            setup.ProfileA.ShotPattern = new RadialShotPatternAuthoring { ShotCount = ghostCount };
            setup.Actor.PatternSlots[0].Emission.CooldownSec = 20f;
            HazardActorPreviewSnapshotBuilder.TryBuild(setup.Root, out var snapshot);
            var session = new HazardActorPreviewSession();
            session.Load(snapshot, new HazardActorPreviewInput
            {
                Scope = scope,
                ForcedPatternSlotId = 1,
                TargetWorldPosition = new Vector3(0f, 0f, 5f),
                SpawnAtStart = true,
            });
            session.EvaluateAt(0.1f);
            Assert.That(session.Frame.ActiveGhostCount, Is.EqualTo(ghostCount));
            return session;
        }

        private static (double p95Ms, int maxSubmissions, long allocationBytes) MeasurePreviewFrame(
            HazardActorPreviewSession session,
            int samples,
            bool drawAggregate)
        {
            for (int i = 0; i < 16; i++)
            {
                session.Step();
                HazardActorPreviewRendererUtility.PrepareGhostBatchesForMeasurement(session.Ghosts, 0.08f, drawAggregate);
            }

            var timings = new double[samples];
            int maxSubmissions = 0;
            var stopwatch = new Stopwatch();
            for (int i = 0; i < samples; i++)
            {
                stopwatch.Restart();
                session.Step();
                HazardActorPreviewRendererUtility.PrepareGhostBatchesForMeasurement(session.Ghosts, 0.08f, drawAggregate);
                stopwatch.Stop();
                timings[i] = stopwatch.Elapsed.TotalMilliseconds;
                maxSubmissions = Mathf.Max(maxSubmissions, HazardActorPreviewRendererUtility.LastDrawSubmissions);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            session.Step();
            HazardActorPreviewRendererUtility.PrepareGhostBatchesForMeasurement(session.Ghosts, 0.08f, drawAggregate);
            long allocationBytes = GC.GetAllocatedBytesForCurrentThread() - before;

            Array.Sort(timings);
            int p95Index = Mathf.Clamp(Mathf.CeilToInt(samples * 0.95f) - 1, 0, samples - 1);
            return (timings[p95Index], maxSubmissions, allocationBytes);
        }

        private static ActorSetup CreateActorSetup()
        {
            var root = new GameObject("hazard_actor_prefab");
            var actor = root.AddComponent<HazardActorAuthoring>();
            actor.InitialPresenceState = HazardActorPresenceStateId.Active;
            actor.InitialPhaseId = 1;
            actor.PhaseSelectorPolicies = new[]
            {
                new HazardActorPhaseSelectorPolicyAuthoring
                {
                    PhaseId = 1,
                    SelectionMode = HazardActorSelectionModeId.OrderedPriority,
                    Candidates = new[] { new HazardActorPhaseSelectorCandidateAuthoring { PatternSlotId = 1 } },
                },
                new HazardActorPhaseSelectorPolicyAuthoring
                {
                    PhaseId = 2,
                    SelectionMode = HazardActorSelectionModeId.OrderedCycle,
                    Candidates = new[] { new HazardActorPhaseSelectorCandidateAuthoring { PatternSlotId = 2 } },
                },
            };
            actor.PhaseProgressTransitions = new[]
            {
                new HazardActorPhaseProgressTransitionAuthoring
                {
                    FromPhaseId = 1,
                    ToPhaseId = 2,
                    ProgressThresholdNormalized = 0.5f,
                    TransitionLeadInSec = 0f,
                }
            };

            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(11);
            bullet.Speed = 1f;
            bullet.Lifetime = 1f;
            var telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            telegraph.TelegraphDurationSec = 0f;
            var profileA = CreateProfile("profile_a", bullet, WaveAimModeId.Fixed);
            var profileB = CreateProfile("profile_b", bullet, WaveAimModeId.PlayerPosition);
            actor.PatternSlots = new[]
            {
                new HazardActorPatternSlotAuthoring
                {
                    PatternSlotId = 1,
                    TelegraphProfile = telegraph,
                    Emission = new HazardActorEmissionAuthoring
                    {
                        Profile = profileA,
                        EventRepeatCount = 1,
                        EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                        CooldownSec = 0f,
                    },
                    BaseWeight = 1f,
                },
                new HazardActorPatternSlotAuthoring
                {
                    PatternSlotId = 2,
                    TelegraphProfile = telegraph,
                    Emission = new HazardActorEmissionAuthoring
                    {
                        Profile = profileB,
                        EventRepeatCount = 1,
                        EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                        CooldownSec = 0f,
                    },
                    BaseWeight = 1f,
                },
            };
            return new ActorSetup(root, actor, bullet, telegraph, profileA, profileB);
        }

        private static EmissionProfileSO CreateProfile(string name, BulletDefinitionSO bullet, WaveAimModeId aimMode)
        {
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
            profile.name = name;
            profile.Bullet = bullet;
            profile.PositionPattern = new SinglePointPositionPatternAuthoring();
            profile.Aim = aimMode == WaveAimModeId.PlayerPosition
                ? (WaveAimAuthoringBase)new PlayerPositionAimAuthoring()
                : new FixedAimAuthoring();
            profile.ShotPattern = new SingleShotPatternAuthoring();
            return profile;
        }

        private readonly struct ActorSetup : System.IDisposable
        {
            public ActorSetup(
                GameObject root,
                HazardActorAuthoring actor,
                BulletDefinitionSO bullet,
                HazardEmitterTelegraphProfileSO telegraph,
                EmissionProfileSO profileA,
                EmissionProfileSO profileB)
            {
                Root = root;
                Actor = actor;
                Bullet = bullet;
                Telegraph = telegraph;
                ProfileA = profileA;
                ProfileB = profileB;
            }

            public GameObject Root { get; }
            public HazardActorAuthoring Actor { get; }
            public BulletDefinitionSO Bullet { get; }
            public HazardEmitterTelegraphProfileSO Telegraph { get; }
            public EmissionProfileSO ProfileA { get; }
            public EmissionProfileSO ProfileB { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Bullet);
                UnityEngine.Object.DestroyImmediate(Telegraph);
                UnityEngine.Object.DestroyImmediate(ProfileA);
                UnityEngine.Object.DestroyImmediate(ProfileB);
            }
        }
    }
}
