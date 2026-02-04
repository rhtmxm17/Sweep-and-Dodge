using Unity.Entities;
using UnityEngine;

namespace SweepnDodge.DotsBullets
{
    public class PlayerProxyAuthoring : MonoBehaviour
    {
        [Header("Proxy Radius")]
        public float PlayerRadius = 0.35f;

        [Header("Vacuum")]
        public float Range = 3.2f;
        public float Strength = 75f;
        public float CollectRadius = 0.35f;

        public float ActiveTime = 0.22f;
        public float Cooldown = 1.8f;

        private class PlayerProxyBaker : Baker<PlayerProxyAuthoring>
        {
            public override void Bake(PlayerProxyAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<PlayerTag>(e);

                AddComponent(e, new PlayerRadiusComponent
                {
                    Value = authoring.PlayerRadius
                });

                AddComponent(e, new VacuumBurstComponent
                {
                    Range = authoring.Range,
                    Strength = authoring.Strength,
                    CollectRadius = authoring.CollectRadius,

                    ActiveTime = authoring.ActiveTime,
                    ActiveTimer = 0f,

                    Cooldown = authoring.Cooldown,
                    CooldownTimer = 0f,

                    IsActive = 0,
                    ActivateRequested = 0
                });
            }
        }
    }
}
