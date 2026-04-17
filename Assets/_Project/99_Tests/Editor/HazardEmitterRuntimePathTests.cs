using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterRuntimePathTests
    {
        [Test]
        public void HazardActorAuthoringValidation_RejectsDuplicatePatternSlotIds()
        {
            var root = new GameObject("actor-root");
            var actor = root.AddComponent<HazardActorAuthoring>();
            actor.ActorId = 1;
            actor.PatternSlots = new[]
            {
                CreatePatternSlot(1),
                CreatePatternSlot(1),
            };

            try
            {
                bool ok = HazardActorAuthoringValidationUtility.TryValidateStandalone(
                    actor,
                    out _,
                    out _,
                    out _,
                    out var error);

                Assert.That(ok, Is.False);
                Assert.That(error, Does.Contain("duplicate PatternSlotId"));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HazardActorPatternSelector_OrderedCycle_AdvancesOnActorCycleSignal()
        {
            using var world = new World("EditMode_HazardActorSelector_Cycle");
            var em = world.EntityManager;

            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
            });

            var actor = CreateActor(em);
            em.AddBuffer<HazardActorPhaseSelectorPolicyBuffer>(actor).Add(new HazardActorPhaseSelectorPolicyBuffer
            {
                PhaseId = 1,
                SelectionMode = HazardActorSelectionModeId.OrderedCycle,
            });

            var candidates = em.AddBuffer<HazardActorPhaseSelectorCandidateBuffer>(actor);
            candidates.Add(new HazardActorPhaseSelectorCandidateBuffer { PhaseId = 1, OrderIndex = 0, PatternSlotId = 1 });
            candidates.Add(new HazardActorPhaseSelectorCandidateBuffer { PhaseId = 1, OrderIndex = 1, PatternSlotId = 2 });

            var slots = em.AddBuffer<HazardActorPatternSlotBuffer>(actor);
            slots.Add(new HazardActorPatternSlotBuffer { PatternSlotId = 1 });
            slots.Add(new HazardActorPatternSlotBuffer { PatternSlotId = 2 });

            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);
            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(1));

            em.SetComponentData(actor, new HazardActorEmitCycleSignalComponent { CompletedVersion = 1u });
            world.GetOrCreateSystem<HazardActorPatternSelectorSystem>().Update(world.Unmanaged);

            selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            Assert.That(selector.CurrentPatternSlotId, Is.EqualTo(2));
            Assert.That(selector.LastConsumedCycleVersion, Is.EqualTo(1u));
        }

        [Test]
        public void HazardActorEmitSystem_SelectedSlot_ProducesDiscreteEmitRequest()
        {
            using var world = new World("EditMode_HazardActorEmit_Request");
            var em = world.EntityManager;

            SetSingleton(em, new BulletFrameCounterComponent { Value = 1u });
            SetSingleton(em, new FixedTickStepRuntimeComponent
            {
                FrameDeltaTime = 1f / 60f,
                LogicDeltaTime = 1f / 60f,
                LogicStepCount = 1,
                HasStep = 1,
                UsingFixedTick = 0,
            });
            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
            });

            var channel = em.CreateEntity(
                typeof(DiscreteEmitChannelSingletonTag),
                typeof(DiscreteEmitPolicyComponent),
                typeof(DiscreteEmitBacklogMetricsComponent));
            em.SetComponentData(channel, new DiscreteEmitPolicyComponent
            {
                BudgetPerFrame = 8,
                MaxPendingCount = 32,
                MaxPendingAgeFrames = 120u,
            });
            em.AddBuffer<DiscreteEmitRequestBuffer>(channel);

            var source = em.CreateEntity();
            var actor = CreateActor(em, source);

            var selector = em.GetComponentData<HazardActorPatternSelectorStateComponent>(actor);
            selector.CurrentPatternSlotId = 7;
            selector.CurrentCandidateOrder = 0;
            em.SetComponentData(actor, selector);

            var executionSlots = em.GetBuffer<HazardActorPatternExecutionSlotBuffer>(actor);
            executionSlots.Add(new HazardActorPatternExecutionSlotBuffer
            {
                PatternSlotId = 7,
                TelegraphProfileRefId = 10,
                EmissionProfileRefId = 20,
                TelegraphDurationSec = 0f,
                LocalOffset = new float3(2f, 0f, 0f),
                BulletTypeKey = 9001,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventRepeatCount = 1,
                CooldownSec = 0.5f,
            });

            world.GetOrCreateSystem<HazardActorEmitSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorEmitSystem>().Update(world.Unmanaged);
            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);

            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].BulletTypeKey, Is.EqualTo(9001));
            Assert.That(requests[0].EmissionId, Is.EqualTo(7));

            var emitState = em.GetComponentData<HazardActorEmitStateComponent>(actor);
            Assert.That(emitState.LifecycleState, Is.EqualTo(HazardActorEmitLifecycleStateId.Cooldown));
        }

        private static HazardActorPatternSlotAuthoring CreatePatternSlot(int slotId)
        {
            var telegraph = ScriptableObject.CreateInstance<HazardEmitterTelegraphProfileSO>();
            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(1000 + slotId);
            var emission = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();
            emission.Bullet = bullet;
            emission.PositionPattern = new SinglePointPositionPatternAuthoring();
            emission.Aim = new FixedAimAuthoring();
            emission.ShotPattern = new SingleShotPatternAuthoring();

            return new HazardActorPatternSlotAuthoring
            {
                PatternSlotId = slotId,
                TelegraphProfile = telegraph,
                EmissionProfile = emission,
                BaseWeight = 1f,
            };
        }

        private static Entity CreateActor(EntityManager em, Entity sourceEntity = default)
        {
            var actor = em.CreateEntity(
                typeof(LocalTransform),
                typeof(LocalToWorld),
                typeof(HazardActorComponent),
                typeof(HazardActorAppliedConfigComponent),
                typeof(HazardActorRuntimeStateComponent),
                typeof(HazardActorBehaviorPhaseStateComponent),
                typeof(HazardActorPhaseTransitionRuntimeComponent),
                typeof(HazardActorPatternSelectorStateComponent),
                typeof(HazardActorEmitStateComponent),
                typeof(HazardActorEmitActiveTelegraphComponent),
                typeof(HazardActorEmitActiveEmissionComponent),
                typeof(HazardActorEmitCycleSignalComponent));

            em.SetComponentData(actor, LocalTransform.FromPosition(float3.zero));
            em.SetComponentData(actor, new LocalToWorld { Value = float4x4.identity });
            em.SetComponentData(actor, new HazardActorComponent { ActorId = 1, SourceEntity = sourceEntity });
            em.SetComponentData(actor, new HazardActorAppliedConfigComponent { IsEnabled = 1, IsSuppressed = 0 });
            em.SetComponentData(actor, new HazardActorRuntimeStateComponent { PresenceState = HazardActorPresenceStateId.Active });
            em.SetComponentData(actor, new HazardActorBehaviorPhaseStateComponent { CurrentPhaseId = 1, PreviousPhaseId = 1, PhaseVersion = 1u });
            em.SetComponentData(actor, new HazardActorPhaseTransitionRuntimeComponent { State = HazardActorPhaseTransitionStateId.Idle });
            em.SetComponentData(actor, new HazardActorPatternSelectorStateComponent
            {
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                CurrentCandidateOrder = -1,
            });
            em.SetComponentData(actor, new HazardActorEmitStateComponent
            {
                LifecycleState = HazardActorEmitLifecycleStateId.Dormant,
                StateElapsedSec = 0f,
            });
            em.SetComponentData(actor, new HazardActorEmitActiveTelegraphComponent
            {
                AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId,
            });
            em.SetComponentData(actor, new HazardActorEmitActiveEmissionComponent
            {
                AppliedPatternSlotId = HazardActorPatternRuntimeUtility.InvalidPatternSlotId,
            });
            em.SetComponentData(actor, new HazardActorEmitCycleSignalComponent { CompletedVersion = 0u });
            em.AddBuffer<HazardActorPatternExecutionSlotBuffer>(actor);
            return actor;
        }

        private static void SetSingleton<T>(EntityManager em, in T value)
            where T : unmanaged, IComponentData
        {
            var entity = em.CreateEntity(typeof(T));
            em.SetComponentData(entity, value);
        }
    }
}
