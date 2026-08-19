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

        public static bool CanPromoteLifecycleRequest(
            BulletLifecycleReasonId reason,
            bool requestEnabled,
            in BulletLifecycleRequestComponent currentRequest)
        {
            if (reason == BulletLifecycleReasonId.None)
                return false;

            return !requestEnabled || ResolvePriority(reason) > currentRequest.Priority;
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
            if (!entityManager.HasComponent<BulletDespawnRequestTag>(bullet))
                return false;
            if (!entityManager.HasComponent<BulletLifecycleRequestComponent>(bullet)
                || !entityManager.HasComponent<BulletLifecycleContactComponent>(bullet))
                return false;

            bool requestEnabled = entityManager.IsComponentEnabled<BulletDespawnRequestTag>(bullet);
            var currentRequest = entityManager.GetComponentData<BulletLifecycleRequestComponent>(bullet);
            if (!CanPromoteLifecycleRequest(reason, requestEnabled, in currentRequest))
                return false;

            entityManager.SetComponentData(bullet, CreateRequest(reason, relatedEntity, frame));
            entityManager.SetComponentData(bullet, CreateContact(positionXZ, directionXZ));
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
            if (!despawnRequestLookup.HasComponent(bullet))
                return false;
            if (!requestLookup.HasComponent(bullet) || !contactLookup.HasComponent(bullet))
                return false;

            bool requestEnabled = despawnRequestLookup.IsComponentEnabled(bullet);
            var currentRequest = requestLookup[bullet];
            if (!CanPromoteLifecycleRequest(reason, requestEnabled, in currentRequest))
                return false;

            requestLookup[bullet] = CreateRequest(reason, relatedEntity, frame);
            contactLookup[bullet] = CreateContact(positionXZ, directionXZ);
            despawnRequestLookup.SetComponentEnabled(bullet, true);
            return true;
        }

        public static bool TryPromoteLifecycleRequest(
            BulletLifecycleReasonId reason,
            Entity relatedEntity,
            uint frame,
            float2 positionXZ,
            float2 directionXZ,
            EnabledRefRW<BulletDespawnRequestTag> despawnRequest,
            ref BulletLifecycleRequestComponent request,
            ref BulletLifecycleContactComponent contact)
        {
            if (!CanPromoteLifecycleRequest(reason, despawnRequest.ValueRO, in request))
                return false;

            request = CreateRequest(reason, relatedEntity, frame);
            contact = CreateContact(positionXZ, directionXZ);
            despawnRequest.ValueRW = true;
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

        private static BulletLifecycleRequestComponent CreateRequest(
            BulletLifecycleReasonId reason,
            Entity relatedEntity,
            uint frame)
        {
            return new BulletLifecycleRequestComponent
            {
                Reason = reason,
                Priority = ResolvePriority(reason),
                RelatedEntity = relatedEntity,
                Frame = frame,
            };
        }

        private static BulletLifecycleContactComponent CreateContact(float2 positionXZ, float2 directionXZ)
        {
            return new BulletLifecycleContactComponent
            {
                PositionXZ = positionXZ,
                DirectionXZ = ResolveDirectionXZ(directionXZ),
            };
        }
    }
}
