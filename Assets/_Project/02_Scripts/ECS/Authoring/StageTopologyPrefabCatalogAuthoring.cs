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
                Entity depositTemplate = Entity.Null;
                Entity obstacleTemplate = Entity.Null;

                if (authoring.Catalog != null)
                {
                    if (authoring.Catalog.SourceTemplatePrefab != null)
                        sourceTemplate = GetEntity(authoring.Catalog.SourceTemplatePrefab, TransformUsageFlags.Dynamic);
                    if (authoring.Catalog.DepositTemplatePrefab != null)
                        depositTemplate = GetEntity(authoring.Catalog.DepositTemplatePrefab, TransformUsageFlags.Dynamic);
                    if (authoring.Catalog.ObstacleTemplatePrefab != null)
                        obstacleTemplate = GetEntity(authoring.Catalog.ObstacleTemplatePrefab, TransformUsageFlags.Dynamic);
                }

                AddComponent(entity, new StageTopologyPrefabCatalogComponent
                {
                    SourceTemplate = sourceTemplate,
                    DepositTemplate = depositTemplate,
                    ObstacleTemplate = obstacleTemplate,
                });
            }
        }
    }
}
