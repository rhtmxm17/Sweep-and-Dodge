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
            sourceLookup.Update(ref state);

            var em = state.EntityManager;
            foreach (var (actor, applied, policy, runtime) in SystemAPI.Query<
                RefRO<HazardActorComponent>,
                RefRO<HazardActorAppliedConfigComponent>,
                RefRO<HazardActorPresencePolicyComponent>,
                RefRW<HazardActorRuntimeStateComponent>>())
            {
                ref readonly var actorConfig = ref actor.ValueRO;
                ref readonly var appliedConfig = ref applied.ValueRO;
                ref readonly var presencePolicy = ref policy.ValueRO;
                ref var runtimeState = ref runtime.ValueRW;

                switch (runtimeState.PresenceState)
                {
                    case HazardActorPresenceStateId.Hidden:
                    {
                        runtimeState.StateElapsedSec = 0f;
                        if (appliedConfig.IsEnabled == 0 || appliedConfig.IsSuppressed != 0)
                            break;

                        if (!IsPresenceTriggerSatisfied(
                                presencePolicy.ActivationTrigger,
                                actorConfig.SourceEntity,
                                em,
                                sourceLookup))
                        {
                            break;
                        }

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
                        if (!IsPresenceTriggerSatisfied(
                                presencePolicy.RetireTrigger,
                                actorConfig.SourceEntity,
                                em,
                                sourceLookup))
                        {
                            break;
                        }

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
                        break;
                }
            }
        }

        private static bool IsPresenceTriggerSatisfied(
            HazardActorPresenceTriggerMode trigger,
            Entity sourceEntity,
            EntityManager em,
            ComponentLookup<SourceSpawnComponent> sourceLookup)
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

                default:
                    return false;
            }
        }
    }
}
