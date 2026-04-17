using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine.TestTools;

namespace SweepNDodge.DotsBullets.Tests
{
    public class HazardEmitterPlayModeTests : PlayModeTestBase
    {
        [UnityTest]
        public IEnumerator HazardActorEmit_SelectedSlot_AppendsDiscreteEmitRequest()
        {
            using var world = new World("PlayMode_HazardActorEmit_Request");
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
            selector.CurrentPatternSlotId = 1;
            selector.CurrentCandidateOrder = 0;
            em.SetComponentData(actor, selector);

            var executionSlots = em.GetBuffer<HazardActorPatternExecutionSlotBuffer>(actor);
            executionSlots.Add(new HazardActorPatternExecutionSlotBuffer
            {
                PatternSlotId = 1,
                TelegraphProfileRefId = 10,
                EmissionProfileRefId = 20,
                TelegraphDurationSec = 0f,
                LocalOffset = new float3(1f, 0f, 0f),
                BulletTypeKey = 7001,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                AimMode = WaveAimModeId.Fixed,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                EventRepeatCount = 1,
                CooldownSec = 1f,
            });

            world.GetOrCreateSystem<HazardActorEmitSystem>().Update(world.Unmanaged);
            world.GetOrCreateSystem<HazardActorEmitSystem>().Update(world.Unmanaged);
            var requests = em.GetBuffer<DiscreteEmitRequestBuffer>(channel);

            Assert.That(requests.Length, Is.EqualTo(1));
            Assert.That(requests[0].BulletTypeKey, Is.EqualTo(7001));
            Assert.That(requests[0].EmissionId, Is.EqualTo(1));

            var emitState = em.GetComponentData<HazardActorEmitStateComponent>(actor);
            Assert.That(emitState.LifecycleState, Is.EqualTo(HazardActorEmitLifecycleStateId.Cooldown));
            yield break;
        }

        [UnityTest]
        public IEnumerator HazardActorPatternSelector_OrderedCycle_AdvancesOnActorCycleSignal()
        {
            using var world = new World("PlayMode_HazardActorSelector_Cycle");
            var em = world.EntityManager;

            SetSingleton(em, new RunDirectorStageStateComponent
            {
                State = RunDirectorStageStateId.Running,
            });

            var actor = CreateActor(em, Entity.Null);
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
            yield break;
        }

        private static Entity CreateActor(EntityManager em, Entity sourceEntity)
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
