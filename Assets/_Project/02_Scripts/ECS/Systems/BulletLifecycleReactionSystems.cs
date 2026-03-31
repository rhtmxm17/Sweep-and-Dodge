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
            txLookup.Update(ref state);
            sourceRefLookup.Update(ref state);
            explodeReactionLookup.Update(ref state);

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
                    ref explodeReactionLookup);
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
            ref ComponentLookup<BulletOnMotionCompletedExplodeReactionComponent> explodeReactionLookup)
        {
            switch (lifecycleRequest.Reason)
            {
                case BulletLifecycleReasonId.LifetimeExpired:
                case BulletLifecycleReasonId.StageBlocked:
                case BulletLifecycleReasonId.VacuumCollected:
                case BulletLifecycleReasonId.CarryFullRemoved:
                case BulletLifecycleReasonId.PlayerHit:
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
            if (reaction.SecondaryBulletTypeKey < 0 || reaction.SpawnCount <= 0)
                return;

            Entity sourceEntity = sourceRefLookup.HasComponent(bullet)
                ? sourceRefLookup[bullet].Value
                : Entity.Null;
            float originY = txLookup.HasComponent(bullet)
                ? txLookup[bullet].Position.y
                : 0f;

            secondaryRequests.Add(new BulletSecondarySpawnRequestBuffer
            {
                BulletTypeKey = reaction.SecondaryBulletTypeKey,
                Count = reaction.SpawnCount,
                Priority = 0,
                SourceEntity = sourceEntity,
                CauserEntity = bullet,
                OriginPosition = new float3(lifecycleContact.PositionXZ.x, originY, lifecycleContact.PositionXZ.y),
                BaseDirection = math.normalizesafe(lifecycleContact.DirectionXZ, new float2(1f, 0f)),
                SpreadAngleDeg = reaction.SpreadAngleDeg,
                SpawnRadius = reaction.SpawnRadius,
                Shape = reaction.Shape,
                OldestFrame = currentFrame,
                Sequence = 0u,
            });
        }
    }
}
