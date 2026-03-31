using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public static class BulletLifecycleRequestUtility
    {
        public static byte ResolvePriority(BulletLifecycleReasonId reason)
        {
            return reason switch
            {
                BulletLifecycleReasonId.PlayerHit => 100,
                BulletLifecycleReasonId.VacuumCollected => 80,
                BulletLifecycleReasonId.CarryFullRemoved => 80,
                BulletLifecycleReasonId.MotionCompleted => 60,
                BulletLifecycleReasonId.StageBlocked => 40,
                BulletLifecycleReasonId.LifetimeExpired => 20,
                _ => 0,
            };
        }

        public static float2 ResolveDirectionXZ(float2 velocity)
        {
            if (math.lengthsq(velocity) < 1e-8f)
                return float2.zero;

            return math.normalize(velocity);
        }

        public static bool TryPromoteLifecycleRequest(
            EntityManager entityManager,
            Entity bullet,
            BulletLifecycleReasonId reason,
            Entity relatedEntity,
            uint frame,
            float2 positionXZ,
            float2 directionXZ)
        {
            if (reason == BulletLifecycleReasonId.None)
                return false;
            if (!entityManager.HasComponent<BulletDespawnRequestTag>(bullet))
                return false;
            if (!entityManager.HasComponent<BulletLifecycleRequestComponent>(bullet)
                || !entityManager.HasComponent<BulletLifecycleContactComponent>(bullet))
                return false;

            byte priority = ResolvePriority(reason);
            if (entityManager.IsComponentEnabled<BulletDespawnRequestTag>(bullet))
            {
                var current = entityManager.GetComponentData<BulletLifecycleRequestComponent>(bullet);
                if (priority <= current.Priority)
                    return false;
            }

            entityManager.SetComponentData(bullet, new BulletLifecycleRequestComponent
            {
                Reason = reason,
                Priority = priority,
                RelatedEntity = relatedEntity,
                Frame = frame,
            });
            entityManager.SetComponentData(bullet, new BulletLifecycleContactComponent
            {
                PositionXZ = positionXZ,
                DirectionXZ = ResolveDirectionXZ(directionXZ),
            });
            entityManager.SetComponentEnabled<BulletDespawnRequestTag>(bullet, true);
            return true;
        }

        public static bool TryPromoteLifecycleRequest(
            Entity bullet,
            BulletLifecycleReasonId reason,
            Entity relatedEntity,
            uint frame,
            float2 positionXZ,
            float2 directionXZ,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref ComponentLookup<BulletLifecycleRequestComponent> requestLookup,
            ref ComponentLookup<BulletLifecycleContactComponent> contactLookup)
        {
            if (reason == BulletLifecycleReasonId.None)
                return false;
            if (!despawnRequestLookup.HasComponent(bullet))
                return false;
            if (!requestLookup.HasComponent(bullet) || !contactLookup.HasComponent(bullet))
                return false;

            byte priority = ResolvePriority(reason);
            if (despawnRequestLookup.IsComponentEnabled(bullet))
            {
                var current = requestLookup[bullet];
                if (priority <= current.Priority)
                    return false;
            }

            requestLookup[bullet] = new BulletLifecycleRequestComponent
            {
                Reason = reason,
                Priority = priority,
                RelatedEntity = relatedEntity,
                Frame = frame,
            };
            contactLookup[bullet] = new BulletLifecycleContactComponent
            {
                PositionXZ = positionXZ,
                DirectionXZ = ResolveDirectionXZ(directionXZ),
            };
            despawnRequestLookup.SetComponentEnabled(bullet, true);
            return true;
        }

        public static void ResetLifecycleRequestState(EntityManager entityManager, Entity bullet)
        {
            if (entityManager.HasComponent<BulletLifecycleRequestComponent>(bullet))
            {
                entityManager.SetComponentData(bullet, new BulletLifecycleRequestComponent
                {
                    Reason = BulletLifecycleReasonId.None,
                    Priority = 0,
                    RelatedEntity = Entity.Null,
                    Frame = 0u,
                });
            }

            if (entityManager.HasComponent<BulletLifecycleContactComponent>(bullet))
                entityManager.SetComponentData(bullet, default(BulletLifecycleContactComponent));

            if (entityManager.HasComponent<BulletDespawnRequestTag>(bullet))
                entityManager.SetComponentEnabled<BulletDespawnRequestTag>(bullet, false);
        }

        public static void ResetLifecycleRequestState(
            Entity bullet,
            ref ComponentLookup<BulletDespawnRequestTag> despawnRequestLookup,
            ref ComponentLookup<BulletLifecycleRequestComponent> requestLookup,
            ref ComponentLookup<BulletLifecycleContactComponent> contactLookup)
        {
            if (requestLookup.HasComponent(bullet))
            {
                requestLookup[bullet] = new BulletLifecycleRequestComponent
                {
                    Reason = BulletLifecycleReasonId.None,
                    Priority = 0,
                    RelatedEntity = Entity.Null,
                    Frame = 0u,
                };
            }

            if (contactLookup.HasComponent(bullet))
                contactLookup[bullet] = default;

            if (despawnRequestLookup.HasComponent(bullet))
                despawnRequestLookup.SetComponentEnabled(bullet, false);
        }
    }
}
