using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(HazardEmitterCoordinatorSystem))]
    [UpdateBefore(typeof(HazardEmitterEmitBuildSystem))]
    public partial struct HazardActorPresenceSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<FixedTickStepRuntimeComponent>();
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardActorComponent>();
            state.RequireForUpdate<HazardActorAppliedConfigComponent>();
            state.RequireForUpdate<HazardActorPresencePolicyComponent>();
            state.RequireForUpdate<HazardActorRuntimeStateComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            bool hasTopologyState = SystemAPI.TryGetSingleton<StageTopologyStateComponent>(out var topologyState);
            var stageState = SystemAPI.GetSingleton<RunDirectorStageStateComponent>();
            if (hasTopologyState
                && !StageTopologyRuntimeGateUtility.ShouldRunGameplay(in topologyState, in stageState))
                return;

            if (stageState.State != RunDirectorStageStateId.Running)
                return;

            var fixedTickRuntime = SystemAPI.GetSingleton<FixedTickStepRuntimeComponent>();
            if (!FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime))
                return;

            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var pressureInputLookup = SystemAPI.GetBufferLookup<SourceDirectorPressureInputBuffer>(true);
            var placementLookup = SystemAPI.GetComponentLookup<HazardActorPlacementComponent>(true);
            var orchestrationSignalLookup = SystemAPI.GetComponentLookup<HazardActorOrchestrationRequestSignalComponent>(true);
            var orchestrationConsumptionLookup = SystemAPI.GetComponentLookup<HazardActorOrchestrationRequestConsumptionComponent>(false);
            sourceLookup.Update(ref state);
            pressureInputLookup.Update(ref state);
            placementLookup.Update(ref state);
            orchestrationSignalLookup.Update(ref state);
            orchestrationConsumptionLookup.Update(ref state);

            var em = state.EntityManager;
            foreach (var (actor, applied, policy, runtime, actorEntity) in SystemAPI.Query<
                RefRO<HazardActorComponent>,
                RefRO<HazardActorAppliedConfigComponent>,
                RefRO<HazardActorPresencePolicyComponent>,
                RefRW<HazardActorRuntimeStateComponent>>().WithEntityAccess())
            {
                ref readonly var actorConfig = ref actor.ValueRO;
                ref readonly var appliedConfig = ref applied.ValueRO;
                ref readonly var presencePolicy = ref policy.ValueRO;
                ref var runtimeState = ref runtime.ValueRW;
                bool isPlacementDelivered = placementLookup.HasComponent(actorEntity);

                if (appliedConfig.IsEnabled == 0 || appliedConfig.IsSuppressed != 0)
                {
                    runtimeState.PresenceState = HazardActorPresenceStateId.Hidden;
                    runtimeState.StateElapsedSec = 0f;
                    ResetSelectorState(em, actorEntity);
                    continue;
                }

                switch (runtimeState.PresenceState)
                {
                    case HazardActorPresenceStateId.Hidden:
                    {
                        runtimeState.StateElapsedSec = 0f;
                        ResetSelectorState(em, actorEntity);

                        bool shouldActivate = isPlacementDelivered
                            ? TryConsumePresenceRequest(
                                actorEntity,
                                HazardActorOrchestrationActionId.Spawn,
                                orchestrationSignalLookup,
                                orchestrationConsumptionLookup)
                            : IsPresenceTriggerSatisfied(
                                presencePolicy.ActivationTrigger,
                                actorConfig.SourceEntity,
                                em,
                                sourceLookup,
                                pressureInputLookup);
                        if (!shouldActivate)
                            break;

                        EmitPresenceSignal(em, actorEntity, HazardActorPresencePresentationCueId.ActivationStarted);
                        runtimeState.PresenceState = HazardActorPresenceStateId.Activating;
                        runtimeState.StateElapsedSec = 0f;
                        if (presencePolicy.ActivationDurationSec <= 0f)
                        {
                            runtimeState.PresenceState = HazardActorPresenceStateId.Active;
                            runtimeState.StateElapsedSec = 0f;
                        }

                        break;
                    }

                    case HazardActorPresenceStateId.Activating:
                    {
                        ResetSelectorState(em, actorEntity);
                        runtimeState.StateElapsedSec = math.max(0f, runtimeState.StateElapsedSec + deltaTime);
                        if (runtimeState.StateElapsedSec >= math.max(0f, presencePolicy.ActivationDurationSec))
                        {
                            runtimeState.PresenceState = HazardActorPresenceStateId.Active;
                            runtimeState.StateElapsedSec = 0f;
                        }

                        break;
                    }

                    case HazardActorPresenceStateId.Active:
                    {
                        runtimeState.StateElapsedSec = 0f;
                        bool shouldRetire = isPlacementDelivered
                            ? TryConsumePresenceRequest(
                                actorEntity,
                                HazardActorOrchestrationActionId.Retire,
                                orchestrationSignalLookup,
                                orchestrationConsumptionLookup)
                            : IsPresenceTriggerSatisfied(
                                presencePolicy.RetireTrigger,
                                actorConfig.SourceEntity,
                                em,
                                sourceLookup,
                                pressureInputLookup);
                        if (!shouldRetire)
                            break;

                        EmitPresenceSignal(em, actorEntity, HazardActorPresencePresentationCueId.RetireStarted);
                        runtimeState.PresenceState = HazardActorPresenceStateId.Retiring;
                        runtimeState.StateElapsedSec = 0f;
                        if (presencePolicy.RetireDurationSec <= 0f)
                        {
                            runtimeState.PresenceState = HazardActorPresenceStateId.Hidden;
                            runtimeState.StateElapsedSec = 0f;
                        }

                        break;
                    }

                    case HazardActorPresenceStateId.Retiring:
                    {
                        ResetSelectorState(em, actorEntity);
                        runtimeState.StateElapsedSec = math.max(0f, runtimeState.StateElapsedSec + deltaTime);
                        if (runtimeState.StateElapsedSec >= math.max(0f, presencePolicy.RetireDurationSec))
                        {
                            runtimeState.PresenceState = HazardActorPresenceStateId.Hidden;
                            runtimeState.StateElapsedSec = 0f;
                        }

                        break;
                    }

                    default:
                        runtimeState.PresenceState = HazardActorPresenceStateId.Hidden;
                        runtimeState.StateElapsedSec = 0f;
                        ResetSelectorState(em, actorEntity);
                        break;
                }
            }
        }

        private static void EmitPresenceSignal(
            EntityManager em,
            Entity actorEntity,
            HazardActorPresencePresentationCueId cue)
        {
            if (cue == HazardActorPresencePresentationCueId.None
                || actorEntity == Entity.Null
                || !em.Exists(actorEntity)
                || !em.HasComponent<HazardActorPresencePresentationSignalComponent>(actorEntity))
            {
                return;
            }

            var signal = em.GetComponentData<HazardActorPresencePresentationSignalComponent>(actorEntity);
            signal.Version = signal.Version >= uint.MaxValue ? 1u : signal.Version + 1u;
            signal.Cue = cue;
            em.SetComponentData(actorEntity, signal);
        }

        private static void ResetSelectorState(EntityManager em, Entity actorEntity)
        {
            if (actorEntity == Entity.Null || !em.Exists(actorEntity) || !em.HasComponent<HazardActorPatternSelectorStateComponent>(actorEntity))
                return;

            em.SetComponentData(actorEntity, new HazardActorPatternSelectorStateComponent
            {
                TargetEmitterId = -1,
                CurrentPatternSlotId = -1,
                LastPatternSlotId = -1,
                SelectionSequence = 0u,
                CurrentCandidateOrder = -1,
                LastResolvedPhaseVersion = 0u,
                LastConsumedCycleVersion = 0u,
            });
        }

        private static bool TryConsumePresenceRequest(
            Entity actorEntity,
            HazardActorOrchestrationActionId expectedAction,
            ComponentLookup<HazardActorOrchestrationRequestSignalComponent> signalLookup,
            ComponentLookup<HazardActorOrchestrationRequestConsumptionComponent> consumptionLookup)
        {
            if (!signalLookup.HasComponent(actorEntity) || !consumptionLookup.HasComponent(actorEntity))
                return false;

            var signal = signalLookup[actorEntity];
            ref var consumption = ref consumptionLookup.GetRefRW(actorEntity).ValueRW;
            if (signal.Version == 0u || signal.Version == consumption.LastPresenceRequestVersion)
                return false;

            consumption.LastPresenceRequestVersion = signal.Version;
            return signal.ActionType == expectedAction;
        }

        private static bool IsPresenceTriggerSatisfied(
            HazardActorPresenceTriggerMode trigger,
            Entity sourceEntity,
            EntityManager em,
            ComponentLookup<SourceSpawnComponent> sourceLookup,
            BufferLookup<SourceDirectorPressureInputBuffer> pressureInputLookup)
        {
            switch (trigger)
            {
                case HazardActorPresenceTriggerMode.None:
                    return false;

                case HazardActorPresenceTriggerMode.Immediate:
                    return true;

                case HazardActorPresenceTriggerMode.SourceAvailable:
                    return sourceEntity != Entity.Null && em.Exists(sourceEntity);

                case HazardActorPresenceTriggerMode.SourceDepleted:
                    if (sourceEntity == Entity.Null || !em.Exists(sourceEntity) || !sourceLookup.HasComponent(sourceEntity))
                        return false;

                    var source = sourceLookup[sourceEntity];
                    return source.State == SourceStateId.Depleted
                        || source.CollectedCount >= math.max(1, source.ThresholdDepleted);

                case HazardActorPresenceTriggerMode.SourceOccupied:
                    if (sourceEntity == Entity.Null || !em.Exists(sourceEntity) || !pressureInputLookup.HasBuffer(sourceEntity))
                        return false;

                    var pressureInputs = pressureInputLookup[sourceEntity];
                    for (int i = 0; i < pressureInputs.Length; i++)
                    {
                        if (pressureInputs[i].Slot != RunDirectorPressureInputSlotId.InfluenceOccupancy)
                            continue;

                        return pressureInputs[i].Value > 0.5f;
                    }

                    return false;

                default:
                    return false;
            }
        }
    }
}
