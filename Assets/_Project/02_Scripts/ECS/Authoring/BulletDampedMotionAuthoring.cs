using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletDampedMotionAuthoring : MonoBehaviour
    {
        [Min(0f)] public float DampingPerSec = 1f;
        [Min(0f)] public float StopSpeedThreshold = 0.1f;

        private sealed class Baker : Baker<BulletDampedMotionAuthoring>
        {
            public override void Bake(BulletDampedMotionAuthoring authoring)
            {
                var root = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(root, new BulletDampedMotionComponent
                {
                    DampingPerSec = authoring.DampingPerSec,
                    StopSpeedThreshold = authoring.StopSpeedThreshold,
                });
            }
        }
    }
}
