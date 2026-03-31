using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletOnCollectedSpawnSecondaryReactionAuthoring : MonoBehaviour
    {
        public int SecondaryBulletTypeKey = -1;
        [Min(0)] public int SpawnCount = 0;
        public BulletSecondarySpawnShapeId Shape = BulletSecondarySpawnShapeId.PointBurst;
        public float SpreadAngleDeg = 90f;
        [Min(0f)] public float SpawnRadius = 0f;

        private sealed class Baker : Baker<BulletOnCollectedSpawnSecondaryReactionAuthoring>
        {
            public override void Bake(BulletOnCollectedSpawnSecondaryReactionAuthoring authoring)
            {
                var root = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(root, new BulletOnCollectedSpawnSecondaryReactionComponent
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
