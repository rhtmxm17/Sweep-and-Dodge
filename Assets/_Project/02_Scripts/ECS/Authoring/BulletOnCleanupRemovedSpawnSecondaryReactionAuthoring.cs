using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletOnCleanupRemovedSpawnSecondaryReactionAuthoring : MonoBehaviour
    {
        public int SecondaryBulletTypeKey = -1;
        [Min(0)] public int SpawnCount = 0;
        public BulletSecondarySpawnShapeId Shape = BulletSecondarySpawnShapeId.PointBurst;
        public float SpreadAngleDeg = 90f;
        [Min(0f)] public float SpawnRadius = 0f;

        private sealed class Baker : Baker<BulletOnCleanupRemovedSpawnSecondaryReactionAuthoring>
        {
            public override void Bake(BulletOnCleanupRemovedSpawnSecondaryReactionAuthoring authoring)
            {
                var root = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(root, new BulletOnCleanupRemovedSpawnSecondaryReactionComponent
                {
                    SecondaryBulletTypeKey = authoring.SecondaryBulletTypeKey,
                    SpawnCount = authoring.SpawnCount,
                    Shape = authoring.Shape,
                    SpreadAngleDeg = authoring.SpreadAngleDeg,
                    SpawnRadius = authoring.SpawnRadius,
                });
            }
        }
    }
}
