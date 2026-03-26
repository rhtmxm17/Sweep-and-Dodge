using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageTopologyPrefabCatalogAuthoring : MonoBehaviour
    {
        public StageTopologyPrefabCatalogSO Catalog;

        private sealed class Baker : Baker<StageTopologyPrefabCatalogAuthoring>
        {
            public override void Bake(StageTopologyPrefabCatalogAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                Entity sourceTemplate = Entity.Null;

                if (authoring.Catalog != null)
                {
                    if (authoring.Catalog.SourceTemplatePrefab != null)
                        sourceTemplate = GetEntity(authoring.Catalog.SourceTemplatePrefab, TransformUsageFlags.Dynamic);
                }

                AddComponent(entity, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = sourceTemplate,
                });
            }
        }
    }
}
