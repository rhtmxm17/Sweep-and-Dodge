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

            bool hasSecondaryChannel = SystemAPI.TryGetSingletonEntity<BulletSecondarySpawnChannelSingletonTag>(out var channelEntity);
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests = default;
            if (hasSecondaryChannel)
                secondaryRequests = state.EntityManager.GetBuffer<BulletSecondarySpawnRequestBuffer>(channelEntity);
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
            var explodeReactionLookup = SystemAPI.GetComponentLookup<BulletOnMotionCompletedExplodeReactionComponent>(true);
            var cleanupRemovedReactionLookup = SystemAPI.GetComponentLookup<BulletOnCleanupRemovedSpawnSecondaryReactionComponent>(true);
            txLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
            emissionProfileRefLookup.Update(ref state);
            explodeReactionLookup.Update(ref state);
            cleanupRemovedReactionLookup.Update(ref state);

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
                    hasSecondaryChannel,
                    secondaryRequests,
                    hasDiscreteChannel,
                    discreteRequests,
                    hasRegistry,
                    registry,
                    ref txLookup,
                    ref sourceRefLookup,
                    ref emissionProfileRefLookup,
                    ref explodeReactionLookup,
                    ref cleanupRemovedReactionLookup);
            }
        }

        private static void DispatchLifecycleReaction(
            Entity bullet,
            in BulletLifecycleRequestComponent lifecycleRequest,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasSecondaryChannel,
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests,
            bool hasDiscreteChannel,
            DynamicBuffer<DiscreteEmitRequestBuffer> discreteRequests,
            bool hasRegistry,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletEmissionProfileRefComponent> emissionProfileRefLookup,
            ref ComponentLookup<BulletOnMotionCompletedExplodeReactionComponent> explodeReactionLookup,
            ref ComponentLookup<BulletOnCleanupRemovedSpawnSecondaryReactionComponent> cleanupRemovedReactionLookup)
        {
            switch (lifecycleRequest.Reason)
            {
                case BulletLifecycleReasonId.LifetimeExpired:
                case BulletLifecycleReasonId.StageBlocked:
                case BulletLifecycleReasonId.PlayerHit:
                    break;
                case BulletLifecycleReasonId.VacuumCollected:
                case BulletLifecycleReasonId.CarryFullRemoved:
                    TryAppendCleanupRemovedSecondarySpawnRequest(
                        bullet,
                        in lifecycleContact,
                        currentFrame,
                        hasFixedTickRuntime,
                        in fixedTickRuntime,
                        hasSecondaryChannel,
                        secondaryRequests,
                        ref txLookup,
                        ref sourceRefLookup,
                        ref cleanupRemovedReactionLookup);
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

                    TryAppendMotionCompletedExplodeRequest(
                        bullet,
                        in lifecycleContact,
                        currentFrame,
                        hasFixedTickRuntime,
                        in fixedTickRuntime,
                        hasSecondaryChannel,
                        secondaryRequests,
                        ref txLookup,
                        ref sourceRefLookup,
                        ref explodeReactionLookup);
                    break;
                case BulletLifecycleReasonId.None:
                default:
                    break;
            }
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

        private static void TryAppendMotionCompletedExplodeRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasSecondaryChannel,
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletOnMotionCompletedExplodeReactionComponent> explodeReactionLookup)
        {
            if (!hasSecondaryChannel || !explodeReactionLookup.HasComponent(bullet))
                return;

            var reaction = explodeReactionLookup[bullet];
            TryAppendSecondarySpawnRequest(
                bullet,
                reaction.SecondaryBulletTypeKey,
                reaction.SpawnCount,
                reaction.Shape,
                reaction.SpreadAngleDeg,
                reaction.SpawnRadius,
                reaction.SpawnDelaySec,
                in lifecycleContact,
                currentFrame,
                hasFixedTickRuntime,
                in fixedTickRuntime,
                secondaryRequests,
                ref txLookup,
                ref sourceRefLookup);
        }

        private static void TryAppendCleanupRemovedSecondarySpawnRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            bool hasSecondaryChannel,
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
            ref ComponentLookup<BulletOnCleanupRemovedSpawnSecondaryReactionComponent> cleanupRemovedReactionLookup)
        {
            if (!hasSecondaryChannel || !cleanupRemovedReactionLookup.HasComponent(bullet))
                return;

            var reaction = cleanupRemovedReactionLookup[bullet];
            TryAppendSecondarySpawnRequest(
                bullet,
                reaction.SecondaryBulletTypeKey,
                reaction.SpawnCount,
                reaction.Shape,
                reaction.SpreadAngleDeg,
                reaction.SpawnRadius,
                reaction.SpawnDelaySec,
                in lifecycleContact,
                currentFrame,
                hasFixedTickRuntime,
                in fixedTickRuntime,
                secondaryRequests,
                ref txLookup,
                ref sourceRefLookup);
        }

        private static void TryAppendSecondarySpawnRequest(
            Entity bullet,
            int secondaryBulletTypeKey,
            int spawnCount,
            BulletSecondarySpawnShapeId shape,
            float spreadAngleDeg,
            float spawnRadius,
            float spawnDelaySec,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasFixedTickRuntime,
            in FixedTickStepRuntimeComponent fixedTickRuntime,
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup)
        {
            if (secondaryBulletTypeKey < 0 || spawnCount <= 0)
                return;

            Entity sourceEntity = sourceRefLookup.HasComponent(bullet)
                ? sourceRefLookup[bullet].Value
                : Entity.Null;
            float originY = txLookup.HasComponent(bullet)
                ? txLookup[bullet].Position.y
                : 0f;
            uint readyFrame = ResolveReadyFrame(
                currentFrame,
                spawnDelaySec,
                hasFixedTickRuntime,
                in fixedTickRuntime);

            secondaryRequests.Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = secondaryBulletTypeKey,
                Count = spawnCount,
                Priority = 0,
                SourceEntity = sourceEntity,
                CauserEntity = bullet,
                OriginPosition = new float3(lifecycleContact.PositionXZ.x, originY, lifecycleContact.PositionXZ.y),
                BaseDirection = math.normalizesafe(lifecycleContact.DirectionXZ, new float2(1f, 0f)),
                SpreadAngleDeg = spreadAngleDeg,
                SpawnRadius = spawnRadius,
                Shape = shape,
                OldestFrame = currentFrame,
                ReadyFrame = readyFrame,
                Sequence = 0u,
            });
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
