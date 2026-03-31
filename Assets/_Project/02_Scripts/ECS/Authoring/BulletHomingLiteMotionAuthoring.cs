using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletHomingLiteMotionAuthoring : MonoBehaviour
    {
        [Min(0f)] public float TurnRateDegPerSec = 90f;
        [Min(0f)] public float MaxAcquireDistance = 10f;
        [Min(0f)] public float MinRetargetDistance = 0.25f;

        private sealed class Baker : Baker<BulletHomingLiteMotionAuthoring>
        {
            public override void Bake(BulletHomingLiteMotionAuthoring authoring)
            {
                var root = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(root, new BulletHomingLiteMotionComponent
                {
                    TurnRateDegPerSec = authoring.TurnRateDegPerSec,
                    MaxAcquireDistance = authoring.MaxAcquireDistance,
                    MinRetargetDistance = authoring.MinRetargetDistance,
                });
            }
        }
    }
}
