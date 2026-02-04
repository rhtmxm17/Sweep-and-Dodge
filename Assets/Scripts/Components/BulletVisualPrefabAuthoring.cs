using Unity.Entities;
using UnityEngine;

namespace SweepnDodge.DotsBullets
{
    /// <summary>
    /// Bullet 렌더 프리펩(GameObject)을 Entity Prefab으로 베이크한 참조를 저장
    /// </summary>
    public class BulletVisualPrefabAuthoring : MonoBehaviour
    {
        [Header("Bullet Render Prefab (GameObject)")]
        public GameObject BulletPrefab;

        private class BulletVisualPrefabBaker : Baker<BulletVisualPrefabAuthoring>
        {
            public override void Bake(BulletVisualPrefabAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);

                var prefabEntity = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Renderable);
                AddComponent(e, new BulletVisualPrefabComponent { Value = prefabEntity });
            }
        }
    }
}
