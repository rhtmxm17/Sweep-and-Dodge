using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    [UpdateInGroup(typeof(BulletRequestGroup))]
    [UpdateAfter(typeof(PlayerCarryBinDepositRequestSystem))]
    [UpdateAfter(typeof(RunProgressDirectorSystem))]
    [UpdateAfter(typeof(SourcePollutionUpdateSystem))]
    [UpdateBefore(typeof(HazardEmitterEmitBuildSystem))]
    [UpdateBefore(typeof(BulletRequestFencePublishSystem))]
    public partial struct HazardEmitterCoordinatorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RunDirectorStageStateComponent>();
            state.RequireForUpdate<HazardEmitterComponent>();
            state.RequireForUpdate<HazardEmitterAppliedConfigComponent>();
            state.RequireForUpdate<HazardEmitterCoordinatorStateComponent>();
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

            var em = state.EntityManager;

            var pressureGateLookup = SystemAPI.GetComponentLookup<HazardEmitterSourcePressureGateComponent>(true);
            var distanceGateLookup = SystemAPI.GetComponentLookup<HazardEmitterPlayerDistanceGateComponent>(true);
            var progressGateLookup = SystemAPI.GetComponentLookup<HazardEmitterSourceProgressGateComponent>(true);
            var actorLookup = SystemAPI.GetComponentLookup<HazardActorComponent>(true);
            var actorAppliedLookup = SystemAPI.GetComponentLookup<HazardActorAppliedConfigComponent>(true);
            var actorRuntimeLookup = SystemAPI.GetComponentLookup<HazardActorRuntimeStateComponent>(true);
            var sourceLookup = SystemAPI.GetComponentLookup<SourceSpawnComponent>(true);
            var directorLookup = SystemAPI.GetComponentLookup<SourceRunDirectorStateComponent>(true);
            var playerSyncLookup = SystemAPI.GetComponentLookup<PlayerGoSyncComponent>(true);
            var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var localToWorldLookup = SystemAPI.GetComponentLookup<LocalToWorld>(true);

            pressureGateLookup.Update(ref state);
            distanceGateLookup.Update(ref state);
            progressGateLookup.Update(ref state);
            actorLookup.Update(ref state);
            actorAppliedLookup.Update(ref state);
            actorRuntimeLookup.Update(ref state);
            sourceLookup.Update(ref state);
            directorLookup.Update(ref state);
            playerSyncLookup.Update(ref state);
            localTransformLookup.Update(ref state);
            localToWorldLookup.Update(ref state);

            Entity playerEntity = SystemAPI.TryGetSingletonEntity<PlayerTag>(out var resolvedPlayer) ? resolvedPlayer : Entity.Null;
            bool hasPlayer = TryResolvePlayerPosition(playerEntity, playerSyncLookup, localTransformLookup, out float3 playerPosition);

            foreach (var (emitter, applied, coordinatorState, entity) in SystemAPI.Query<
                RefRO<HazardEmitterComponent>,
                RefRO<HazardEmitterAppliedConfigComponent>,
                RefRW<HazardEmitterCoordinatorStateComponent>>().WithEntityAccess())
            {
                ref readonly var emitterConfig = ref emitter.ValueRO;
                ref readonly var appliedConfig = ref applied.ValueRO;
                uint reasonMask = 0u;
                float lastPlayerDistanceSq = float.MaxValue;

                if (appliedConfig.IsEnabled == 0)
                    reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.DisabledByAppliedConfig;
                if (appliedConfig.IsSuppressed != 0)
                    reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.SuppressedByAppliedConfig;

                bool hasPressureGate = pressureGateLookup.HasComponent(entity) && pressureGateLookup[entity].Enabled != 0;
                bool hasProgressGate = progressGateLookup.HasComponent(entity) && progressGateLookup[entity].Enabled != 0;
                bool hasDistanceGate = distanceGateLookup.HasComponent(entity) && distanceGateLookup[entity].Enabled != 0;

                bool hasActor = emitterConfig.ActorEntity != Entity.Null
                    && em.Exists(emitterConfig.ActorEntity)
                    && actorLookup.HasComponent(emitterConfig.ActorEntity);
                if (!hasActor)
                {
                    reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.MissingActor;
                }

                Entity sourceEntity = Entity.Null;
                bool hasSource = false;
                if (hasActor)
                {
                    if (actorAppliedLookup.HasComponent(emitterConfig.ActorEntity))
                    {
                        var actorApplied = actorAppliedLookup[emitterConfig.ActorEntity];
                        if (actorApplied.IsEnabled == 0)
                            reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.DisabledByActorConfig;
                        if (actorApplied.IsSuppressed != 0)
                            reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.SuppressedByActorConfig;
                    }

                    if (actorRuntimeLookup.HasComponent(emitterConfig.ActorEntity))
                    {
                        var actorRuntime = actorRuntimeLookup[emitterConfig.ActorEntity];
                        switch (actorRuntime.PresenceState)
                        {
                            case HazardActorPresenceStateId.Hidden:
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceHidden;
                                break;
                            case HazardActorPresenceStateId.Activating:
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceActivating;
                                break;
                            case HazardActorPresenceStateId.Retiring:
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.ActorPresenceRetiring;
                                break;
                        }
                    }

                    sourceEntity = actorLookup[emitterConfig.ActorEntity].SourceEntity;
                    hasSource = sourceEntity != Entity.Null
                        && em.Exists(sourceEntity);
                }

                bool needsSource = hasPressureGate || hasProgressGate;
                if (hasActor && needsSource && !hasSource)
                {
                    reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.MissingSource;
                }
                else if (hasActor && hasSource)
                {
                    if (hasPressureGate)
                    {
                        var pressureGate = pressureGateLookup[entity];
                        if (!directorLookup.HasComponent(sourceEntity))
                        {
                            reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.MissingSource;
                        }
                        else
                        {
                            var director = directorLookup[sourceEntity];
                            if (pressureGate.RequirePressureState != 0
                                && director.State != RunDirectorSourceStateId.Pressure)
                            {
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.SourcePressureBlocked;
                            }

                            if (director.PressureOccupancySec < math.max(0f, pressureGate.MinPressureOccupancySec))
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.SourcePressureBlocked;
                        }
                    }

                    if (hasProgressGate)
                    {
                        var progressGate = progressGateLookup[entity];
                        if (!sourceLookup.HasComponent(sourceEntity))
                        {
                            reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.MissingSource;
                        }
                        else
                        {
                            var source = sourceLookup[sourceEntity];
                            float progress01 = math.saturate((float)source.CollectedCount / math.max(1, source.ThresholdDepleted));
                            if (progress01 < progressGate.MinProgress01 || progress01 > progressGate.MaxProgress01)
                                reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.SourceProgressBlocked;
                        }
                    }
                }

                if (hasActor && hasDistanceGate)
                {
                    if (!hasPlayer)
                    {
                        reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.MissingPlayer;
                    }
                    else
                    {
                        var distanceGate = distanceGateLookup[entity];
                        float3 emitterPosition = ResolveWorldAnchorPosition(
                            entity,
                            appliedConfig.LocalOffset,
                            localTransformLookup,
                            localToWorldLookup);
                        lastPlayerDistanceSq = math.lengthsq(playerPosition - emitterPosition);
                        if (lastPlayerDistanceSq < distanceGate.MinDistanceSq
                            || lastPlayerDistanceSq > distanceGate.MaxDistanceSq)
                        {
                            reasonMask |= (uint)HazardEmitterSuppressionReasonFlags.PlayerDistanceBlocked;
                        }
                    }
                }

                coordinatorState.ValueRW = new HazardEmitterCoordinatorStateComponent
                {
                    ActivationAllowed = reasonMask == 0u ? (byte)1 : (byte)0,
                    SuppressionReasonMask = reasonMask,
                    LastPlayerDistanceSq = lastPlayerDistanceSq,
                };
            }
        }

        private static bool TryResolvePlayerPosition(
            Entity player,
            ComponentLookup<PlayerGoSyncComponent> playerSyncLookup,
            ComponentLookup<LocalTransform> localTransformLookup,
            out float3 playerPosition)
        {
            if (player == Entity.Null)
            {
                playerPosition = float3.zero;
                return false;
            }

            if (playerSyncLookup.HasComponent(player))
            {
                playerPosition = playerSyncLookup[player].Position;
                return true;
            }

            if (localTransformLookup.HasComponent(player))
            {
                playerPosition = localTransformLookup[player].Position;
                return true;
            }

            playerPosition = float3.zero;
            return false;
        }

        private static float3 ResolveWorldAnchorPosition(
            Entity emitterEntity,
            float3 localOffset,
            ComponentLookup<LocalTransform> localTransformLookup,
            ComponentLookup<LocalToWorld> localToWorldLookup)
        {
            if (localToWorldLookup.HasComponent(emitterEntity))
                return math.transform(localToWorldLookup[emitterEntity].Value, localOffset);

            if (localTransformLookup.HasComponent(emitterEntity))
                return localTransformLookup[emitterEntity].Position + localOffset;

            return localOffset;
        }
    }
}
