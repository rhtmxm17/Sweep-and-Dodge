using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(HazardActorPresenceSystem))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(HazardEmitterCoordinatorSystem))]
    [UpdateBefore(typeof(HazardActorPatternSelectorSystem))]
    public partial struct HazardActorPhaseTransitionSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardActorComponent>();
            state.RequireForUpdate<HazardActorRuntimeStateComponent>();
            state.RequireForUpdate<HazardActorBehaviorPhaseStateComponent>();
            state.RequireForUpdate<HazardActorPhaseTransitionRuntimeComponent>();
            state.RequireForUpdate<HazardActorPhaseTransitionSignalComponent>();
            state.RequireForUpdate<HazardActorPhaseProgressTransitionBuffer>();
            state.RequireForUpdate<SourceSpawnComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
            {
                return;
            }

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            float deltaTime = 0f;
            if (SystemAPI.TryGetSingleton<FixedTickStepRuntimeComponent>(out var fixedTick)
                && FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTick, out float logicDeltaTime))
            {
                deltaTime = logicDeltaTime;
            }

            var actorLookup = SystemAPI.GetComponentLookup<HazardActorComponent>(true);
            var actorRuntimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var phaseStateLookup = SystemAPI.GetComponentLookup<HazardActorBehaviorPhaseStateComponent>(false);
            var transitionRuntimeLookup = SystemAPI.GetComponentLookup<HazardActorPhaseTransitionRuntimeComponent>(false);
            var transitionSignalLookup = SystemAPI.GetComponentLookup<HazardActorPhaseTransitionSignalComponent>(false);
            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var transitionLookup = SystemAPI.GetBufferLookup<HazardActorPhaseProgressTransitionBuffer>(true);

            actorLookup.Update(ref state);
            actorRuntimeLookup.Update(ref state);
            phaseStateLookup.Update(ref state);
            transitionRuntimeLookup.Update(ref state);
            transitionSignalLookup.Update(ref state);
            sourceLookup.Update(ref state);
            transitionLookup.Update(ref state);

            using var actorQuery = state.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HazardActorComponent>(),
                ComponentType.ReadOnly<HazardActorRuntimeStateComponent>(),
                ComponentType.ReadWrite<HazardActorBehaviorPhaseStateComponent>(),
                ComponentType.ReadWrite<HazardActorPhaseTransitionRuntimeComponent>(),
                ComponentType.ReadWrite<HazardActorPhaseTransitionSignalComponent>());
            using var actorEntities = actorQuery.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int actorIndex = 0; actorIndex < actorEntities.Length; actorIndex++)
            {
                Entity actorEntity = actorEntities[actorIndex];
                if (!actorLookup.HasComponent(actorEntity)
                    || !actorRuntimeLookup.HasComponent(actorEntity)
                    || !phaseStateLookup.HasComponent(actorEntity)
                    || !transitionRuntimeLookup.HasComponent(actorEntity)
                    || !transitionSignalLookup.HasComponent(actorEntity)
                    || !transitionLookup.HasBuffer(actorEntity))
                {
                    continue;
                }

                ref var phaseState = ref phaseStateLookup.GetRefRW(actorEntity).ValueRW;
                ref var transitionRuntime = ref transitionRuntimeLookup.GetRefRW(actorEntity).ValueRW;
                ref var transitionSignal = ref transitionSignalLookup.GetRefRW(actorEntity).ValueRW;
                var actorRuntime = actorRuntimeLookup[actorEntity];

                if (actorRuntime.PresenceState != HazardActorPresenceStateId.Active)
                {
                    if (transitionRuntime.State == HazardActorPhaseTransitionStateId.Preparing)
                        SetRuntimeIdle(ref transitionRuntime, keepVersion: true);

                    continue;
                }

                switch (transitionRuntime.State)
                {
                    case HazardActorPhaseTransitionStateId.Idle:
                    {
                        var actor = actorLookup[actorEntity];
                        if (actor.SourceEntity == Entity.Null
                            || !sourceLookup.HasComponent(actor.SourceEntity))
                        {
                            break;
                        }

                        var transitions = transitionLookup[actorEntity];
                        if (!TryFindTransitionRule(transitions, phaseState.CurrentPhaseId, out var transition))
                            break;

                        var source = sourceLookup[actor.SourceEntity];
                        float progress01 = math.saturate((float)source.CollectedCount / math.max(1, source.ThresholdDepleted));
                        if (progress01 < transition.ProgressThresholdNormalized)
                            break;

                        transitionRuntime.State = HazardActorPhaseTransitionStateId.Preparing;
                        transitionRuntime.PendingFromPhaseId = phaseState.CurrentPhaseId;
                        transitionRuntime.PendingToPhaseId = transition.ToPhaseId;
                        transitionRuntime.ElapsedSec = 0f;
                        transitionRuntime.DurationSec = math.max(0f, transition.TransitionLeadInSec);
                        transitionRuntime.TransitionVersion = NextVersion(transitionRuntime.TransitionVersion);

                        transitionSignal.Version = NextVersion(transitionSignal.Version);
                        transitionSignal.Cue = HazardActorPhaseTransitionSignalCueId.PreparingStarted;
                        transitionSignal.Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold;
                        transitionSignal.PreviousPhaseId = phaseState.CurrentPhaseId;
                        transitionSignal.CurrentPhaseId = phaseState.CurrentPhaseId;
                        transitionSignal.PendingToPhaseId = transition.ToPhaseId;

                        if (transitionRuntime.DurationSec <= 0f)
                            CommitTransition(ref phaseState, ref transitionRuntime, ref transitionSignal);

                        break;
                    }

                    case HazardActorPhaseTransitionStateId.Preparing:
                    {
                        transitionRuntime.ElapsedSec += math.max(0f, deltaTime);
                        if (transitionRuntime.ElapsedSec < transitionRuntime.DurationSec)
                            break;

                        CommitTransition(ref phaseState, ref transitionRuntime, ref transitionSignal);
                        break;
                    }
                }
            }
        }

        private static bool TryFindTransitionRule(
            DynamicBuffer<HazardActorPhaseProgressTransitionBuffer> transitions,
            int fromPhaseId,
            out HazardActorPhaseProgressTransitionBuffer transition)
        {
            for (int i = 0; i < transitions.Length; i++)
            {
                if (transitions[i].FromPhaseId != fromPhaseId)
                    continue;

                transition = transitions[i];
                return true;
            }

            transition = default;
            return false;
        }

        private static void CommitTransition(
            ref HazardActorBehaviorPhaseStateComponent phaseState,
            ref HazardActorPhaseTransitionRuntimeComponent transitionRuntime,
            ref HazardActorPhaseTransitionSignalComponent transitionSignal)
        {
            int previousPhaseId = phaseState.CurrentPhaseId;
            int committedPhaseId = math.max(1, transitionRuntime.PendingToPhaseId);

            phaseState.PreviousPhaseId = previousPhaseId;
            phaseState.CurrentPhaseId = committedPhaseId;
            phaseState.PhaseVersion = NextVersion(phaseState.PhaseVersion);

            transitionSignal.Version = NextVersion(transitionSignal.Version);
            transitionSignal.Cue = HazardActorPhaseTransitionSignalCueId.PhaseCommitted;
            transitionSignal.Reason = HazardActorPhaseTransitionReasonId.ProgressThreshold;
            transitionSignal.PreviousPhaseId = previousPhaseId;
            transitionSignal.CurrentPhaseId = committedPhaseId;
            transitionSignal.PendingToPhaseId = committedPhaseId;

            SetRuntimeIdle(ref transitionRuntime, keepVersion: true);
        }

        private static void SetRuntimeIdle(
            ref HazardActorPhaseTransitionRuntimeComponent transitionRuntime,
            bool keepVersion)
        {
            uint version = keepVersion ? transitionRuntime.TransitionVersion : 0u;
            transitionRuntime = new HazardActorPhaseTransitionRuntimeComponent
            {
                State = HazardActorPhaseTransitionStateId.Idle,
                PendingFromPhaseId = -1,
                PendingToPhaseId = -1,
                ElapsedSec = 0f,
                DurationSec = 0f,
                TransitionVersion = version,
            };
        }

        private static uint NextVersion(uint value)
        {
            return value >= uint.MaxValue ? 1u : value + 1u;
        }
    }
}
