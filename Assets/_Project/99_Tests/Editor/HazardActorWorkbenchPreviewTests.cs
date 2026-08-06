using System.Linq;
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
                Object.DestroyImmediate(Root);
                Object.DestroyImmediate(Bullet);
                Object.DestroyImmediate(Telegraph);
                Object.DestroyImmediate(ProfileA);
                Object.DestroyImmediate(ProfileB);
            }
        }
    }
}
