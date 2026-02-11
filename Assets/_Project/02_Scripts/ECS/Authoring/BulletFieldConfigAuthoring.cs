using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletFieldConfigAuthoring : MonoBehaviour
    {
        [Header("Pool / Target")]
        public int PoolSize = 120_000;
        public int MaxActiveTarget = 100_000;

        [Header("Spatial Hash")]
        public float CellSize = 1.6f;

        [Header("Bullets")]
        public float BulletLifetime = 4.0f;

        private class Baker : Baker<BulletFieldConfigAuthoring>
        {
            public override void Bake(BulletFieldConfigAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);

                AddComponent(e, new BulletFieldConfigComponent
                {
                    PoolSize = authoring.PoolSize,
                    MaxActiveTarget = authoring.MaxActiveTarget,

                    CellSize = authoring.CellSize,
                    InvCellSize = authoring.CellSize > 0f ? 1f / authoring.CellSize : 1f,

                    BulletLifetime = authoring.BulletLifetime
                });

                // 점수 싱글톤도 동일 엔티티에 추가
                AddComponent(e, new ScoreComponent { Value = 0 });
            }
        }
    }
}
