using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct PrefabReferenceComponent : IComponentData
    {
        public EntityPrefabReference Value;
    }

    public class PrefabReferenceAuthoring : MonoBehaviour
    {
        public GameObject Prefab;

#if UNITY_EDITOR
        class Baker : Baker<PrefabReferenceAuthoring>
        {
            public override void Bake(PrefabReferenceAuthoring authoring)
            {
                var entityPrefab = new EntityPrefabReference(authoring.Prefab);
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new PrefabReferenceComponent
                {
                    Value = entityPrefab
                });
            }
        }
#endif
    }
}
