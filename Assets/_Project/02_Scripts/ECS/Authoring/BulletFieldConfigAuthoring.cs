using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletFieldConfigAuthoring : MonoBehaviour
    {
        [Header("Pool")]
        public int PoolSize = 120_000;

        [Header("Spatial Hash")]
        public float CellSize = 1.6f;

        private class Baker : Baker<BulletFieldConfigAuthoring>
        {
            public override void Bake(BulletFieldConfigAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);

                AddComponent(e, new BulletFieldConfigComponent
                {
                    PoolSize = authoring.PoolSize,
                    InvCellSize = authoring.CellSize > 0f ? 1f / authoring.CellSize : 1f,
                });

                AddComponent(e, new MetaScrapComponent { Value = 0 });
            }
        }
    }
}
