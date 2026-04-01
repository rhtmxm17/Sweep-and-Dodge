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

            uint currentFrame = 0u;
            if (SystemAPI.TryGetSingleton<BulletFrameCounterComponent>(out var frameCounter))
                currentFrame = FrameSequenceUtility.GetCurrentFrame(in frameCounter);

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var sourceRefLookup = SystemAPI.GetComponentLookup<BulletSourceRefComponent>(true);
            var explodeReactionLookup = SystemAPI.GetComponentLookup<BulletOnMotionCompletedExplodeReactionComponent>(true);
            var cleanupRemovedReactionLookup = SystemAPI.GetComponentLookup<BulletOnCleanupRemovedSpawnSecondaryReactionComponent>(true);
            txLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
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
                    hasSecondaryChannel,
                    secondaryRequests,
                    ref txLookup,
                    ref sourceRefLookup,
                    ref explodeReactionLookup,
                    ref cleanupRemovedReactionLookup);
            }
        }

        private static void DispatchLifecycleReaction(
            Entity bullet,
            in BulletLifecycleRequestComponent lifecycleRequest,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
            bool hasSecondaryChannel,
            DynamicBuffer<BulletSecondarySpawnRequestBuffer> secondaryRequests,
            ref ComponentLookup<LocalTransform> txLookup,
            ref ComponentLookup<BulletSourceRefComponent> sourceRefLookup,
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
                        hasSecondaryChannel,
                        secondaryRequests,
                        ref txLookup,
                        ref sourceRefLookup,
                        ref cleanupRemovedReactionLookup);
                    break;
                case BulletLifecycleReasonId.MotionCompleted:
                    TryAppendMotionCompletedExplodeRequest(
                        bullet,
                        in lifecycleContact,
                        currentFrame,
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

        private static void TryAppendMotionCompletedExplodeRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
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
                in lifecycleContact,
                currentFrame,
                secondaryRequests,
                ref txLookup,
                ref sourceRefLookup);
        }

        private static void TryAppendCleanupRemovedSecondarySpawnRequest(
            Entity bullet,
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
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
                in lifecycleContact,
                currentFrame,
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
            in BulletLifecycleContactComponent lifecycleContact,
            uint currentFrame,
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
                Sequence = 0u,
            });
        }
    }
}
