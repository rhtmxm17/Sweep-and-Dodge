using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// terminal lifecycle reaction execute owner.
    /// - ExecutionEnd에서 pending lifecycle request를 먼저 읽는다.
    /// - 이번 slice에서는 reason dispatch만 수행하고 실제 반응은 하지 않는다.
    /// - request consume / render toggle / pool enqueue는 계속 BulletDespawnExecutionSystem 단일 책임이다.
    /// </summary>
    [UpdateInGroup(typeof(BulletExecutionEndGroup))]
    [UpdateAfter(typeof(PlayerHazardRiskResolveSystem))]
    [UpdateBefore(typeof(BulletDespawnExecutionSystem))]
    [UpdateBefore(typeof(CombatEventChannelConsumeSystem))]
    public partial struct BulletLifecycleReactionExecutionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            state.Dependency = default;

            bool hasDiscreteChannel = SystemAPI.TryGetSingletonEntity<DiscreteEmitChannelSingletonTag>(out var discreteChannelEntity);
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests = default;
            if (hasDiscreteChannel)
                discreteRequests = state.EntityManager.GetBuffer<DiscreteEmitRequestBuffer>(discreteChannelEntity);
            bool hasRegistry = SystemAPI.TryGetSingletonEntity<EmissionProfileRuntimeRegistryTag>(out var registryEntity);
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry = default;
            if (hasRegistry && state.EntityManager.HasBuffer<EmissionProfileRuntimeRegistryBuffer>(registryEntity))
                registry = state.EntityManager.GetBuffer<EmissionProfileRuntimeRegistryBuffer>(registryEntity);
            else
                hasRegistry = false;

            uint currentFrame = 0u;
            if (SystemAPI.TryGetSingleton<BulletFrameCounterComponent>(out var frameCounter))
                currentFrame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);
            bool hasFixedTickRuntime = SystemAPI.TryGetSingleton<FixedTickStepRuntimeComponent>(out var fixedTickRuntime);

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var sourceRefLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(true);
            var emissionProfileRefLookup = SystemAPI.GetComponentLookup<BulletEmissionProfileRefComponent>(true);
            txLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
            emissionProfileRefLookup.Update(ref state);

            foreach (var (despawnRequest, lifecycleRequest, lifecycleContact, entity) in SystemAPI
                         .Query<EnabledRefRO<BulletDespawnRequestTag>, RefRO<BulletLifecycleRequestComponent>, RefRO<BulletLifecycleContactComponent>>()
                         .WithEntityAccess())
            {
                if (!despawnRequest.ValueRO)
                    continue;

                DispatchLifecycleReaction(
                    entity,
                    in lifecycleRequest.ValueRO,
                    in lifecycleContact.ValueRO,
                    currentFrame,
                    hasFixedTickRuntime,
                    in fixedTickRuntime,
                    hasDiscreteChannel,
                    discreteRequests,
                    hasRegistry,
                    registry,
                    ref txLookup,
                    ref sourceRefLookup,
                    ref emissionProfileRefLookup);
            }
        }

        private static void DispatchLifecycleReaction(
            Entity bullet,
            in BulletLifecycleRequestComponent lifecycleRequest,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasDiscreteChannel,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            bool hasRegistry,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletEmissionProfileRefComponent> emissionProfileRefLookup)
        {
            switch (lifecycleRequest.Reason)
            {
                case BulletLifecycleReasonId.LifetimeExpired:
                case BulletLifecycleReasonId.StageBlocked:
                case BulletLifecycleReasonId.PlayerHit:
                    break;
                case BulletLifecycleReasonId.VacuumCollected:
                case BulletLifecycleReasonId.CarryFullRemoved:
                    TryAppendCleanupRemovedTriggeredEmissionRequest(
                        bullet,
                        in lifecycleContact,
                        currentFrame,
                        hasFixedTickRuntime,
                        in fixedTickRuntime,
                        hasDiscreteChannel,
                        discreteRequests,
                        hasRegistry,
                        registry,
                        ref txLookup,
                        ref sourceRefLookup,
                        ref emissionProfileRefLookup);
                    break;
                case BulletLifecycleReasonId.MotionCompleted:
                    if (TryAppendMotionCompletedTriggeredEmissionRequest(
                            bullet,
                            in lifecycleContact,
                            currentFrame,
                            hasFixedTickRuntime,
                            in fixedTickRuntime,
                            hasDiscreteChannel,
                            discreteRequests,
                            hasRegistry,
                            registry,
                            ref txLookup,
                            ref sourceRefLookup,
                            ref emissionProfileRefLookup))
                    {
                        break;
                    }
                    break;
                case BulletLifecycleReasonId.None:
                default:
                    break;
            }
        }

        private static bool TryAppendCleanupRemovedTriggeredEmissionRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasDiscreteChannel,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            bool hasRegistry,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletEmissionProfileRefComponent> emissionProfileRefLookup)
        {
            if (!hasDiscreteChannel || !hasRegistry || !emissionProfileRefLookup.HasComponent(bullet))
                return false;

            int sourceProfileRefId = emissionProfileRefLookup[bullet].ProfileRefId;
            if (sourceProfileRefId == 0)
                return false;

            if (!EmissionProfileRuntimeRegistryUtility.TryFind(registry, sourceProfileRefId, out var sourceProfile)
                || sourceProfile.HasCleanupRemovedTrigger == 0
                || sourceProfile.CleanupRemovedTargetProfileRefId == 0)
            {
                return false;
            }

            if (!EmissionProfileRuntimeRegistryUtility.TryFind(
                    registry,
                    sourceProfile.CleanupRemovedTargetProfileRefId,
                    out var targetProfile))
            {
                return false;
            }

            Entity sourceEntity = ResolveTriggerSourceEntity(
                bullet,
                sourceProfile.CleanupRemovedSourceEntity,
                ref sourceRefLookup);
            Entity causerEntity = ResolveTriggerCauserEntity(
                bullet,
                sourceProfile.CleanupRemovedCauserEntity);
            float originY = txLookup.HasComponent(bullet)
                ? txLookup[bullet].Position.y
                : 0f;
            float3 anchorPosition = new(
                lifecycleContact.PositionXZ.x,
                originY,
                lifecycleContact.PositionXZ.y);
            float2 contextDirection = math.normalizesafe(lifecycleContact.DirectionXZ, new float2(1f, 0f));
            uint readyFrame = ResolveReadyFrame(
                currentFrame,
                sourceProfile.CleanupRemovedDelaySec,
                hasFixedTickRuntime,
                in fixedTickRuntime);

            var seed = DiscreteEmitRequestUtility.BuildDiscreteEmitSeedFromTriggeredRegistry(
                sourceEntity,
                causerEntity,
                sourceProfile.ProfileRefId,
                in targetProfile,
                anchorPosition,
                contextDirection,
                readyFrame,
                priority: 0);
            discreteRequests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, currentFrame));
            return true;
        }

        private static bool TryAppendMotionCompletedTriggeredEmissionRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasDiscreteChannel,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            bool hasRegistry,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletEmissionProfileRefComponent> emissionProfileRefLookup)
        {
            if (!hasDiscreteChannel || !hasRegistry || !emissionProfileRefLookup.HasComponent(bullet))
                return false;

            int sourceProfileRefId = emissionProfileRefLookup[bullet].ProfileRefId;
            if (sourceProfileRefId == 0)
                return false;

            if (!EmissionProfileRuntimeRegistryUtility.TryFind(registry, sourceProfileRefId, out var sourceProfile)
                || sourceProfile.HasMotionCompletedTrigger == 0
                || sourceProfile.MotionCompletedTargetProfileRefId == 0)
            {
                return false;
            }

            if (!EmissionProfileRuntimeRegistryUtility.TryFind(
                    registry,
                    sourceProfile.MotionCompletedTargetProfileRefId,
                    out var targetProfile))
            {
                return false;
            }

            Entity sourceEntity = ResolveTriggerSourceEntity(
                bullet,
                sourceProfile.MotionCompletedSourceEntity,
                ref sourceRefLookup);
            Entity causerEntity = ResolveTriggerCauserEntity(
                bullet,
                sourceProfile.MotionCompletedCauserEntity);
            float originY = txLookup.HasComponent(bullet)
                ? txLookup[bullet].Position.y
                : 0f;
            float3 anchorPosition = new(
                lifecycleContact.PositionXZ.x,
                originY,
                lifecycleContact.PositionXZ.y);
            float2 contextDirection = math.normalizesafe(lifecycleContact.DirectionXZ, new float2(1f, 0f));
            uint readyFrame = ResolveReadyFrame(
                currentFrame,
                sourceProfile.MotionCompletedDelaySec,
                hasFixedTickRuntime,
                in fixedTickRuntime);

            var seed = DiscreteEmitRequestUtility.BuildDiscreteEmitSeedFromTriggeredRegistry(
                sourceEntity,
                causerEntity,
                sourceProfile.ProfileRefId,
                in targetProfile,
                anchorPosition,
                contextDirection,
                readyFrame,
                priority: 0);
            discreteRequests.Add(DiscreteEmitRequestUtility.CreateDiscreteEmitRequest(seed, currentFrame));
            return true;
        }

        private static Entity ResolveTriggerSourceEntity(
            Entity bullet,
            EmissionTriggerSourceBindingId binding,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup)
        {
            return binding switch
            {
                EmissionTriggerSourceBindingId.CauserSourceEntity => sourceRefLookup.HasComponent(bullet)
                    ? sourceRefLookup[bullet].Value
                    : Entity.Null,
                _ => Entity.Null,
            };
        }

        private static Entity ResolveTriggerCauserEntity(
            Entity bullet,
            EmissionTriggerCauserBindingId binding)
        {
            return binding switch
            {
                EmissionTriggerCauserBindingId.CompletedBullet => bullet,
                _ => Entity.Null,
            };
        }

        private static uint ResolveReadyFrame(
            uint currentFrame,
            float spawnDelaySec,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime)
        {
            int delayFrames = 1;
            if (hasFixedTickRuntime
                && FixedTickTimeUtility.TryResolveLogicDeltaTime(in fixedTickRuntime, out float deltaTime)
                && deltaTime > 0f)
            {
                delayFrames = math.max(1, (int)math.ceil(math.max(0f, spawnDelaySec) / deltaTime));
            }

            return currentFrame + (uint)delayFrames;
        }
    }
}
