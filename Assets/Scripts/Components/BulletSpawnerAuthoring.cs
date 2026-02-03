using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.ECS
{
    // BulletSpawner 프리팹에 붙여서 ECS 변환 시 BulletSpawnerComponent 등 컴포넌트 추가
    public class BulletSpawnerAuthoring : MonoBehaviour
    {
        [Header("Prefab (entity)")]
        public GameObject BulletPrefab;

        [Header("Spawner Settings")]
        public BulletPatternType Pattern = 0;
        public float FireInterval = 0.5f;
        public int BulletsPerShot = 1;
        public float BulletSpeed = 5f;
        public float StartAngleDeg = 0f;
        public float AngularSpeedDeg = 0f;

        public class Baker : Baker<BulletSpawnerAuthoring>
        {
            public override void Bake(BulletSpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent(entity, new BulletSpawnerComponent
                {
                    BulletPrefab = GetEntity(authoring.BulletPrefab, TransformUsageFlags.Dynamic),

                    FireInterval = math.max(0.001f, authoring.FireInterval),
                    Timer = 0f,
                    BulletsPerShot = math.max(1, authoring.BulletsPerShot),
                    BulletSpeed = authoring.BulletSpeed,
                    StartAngleRad = math.radians(authoring.StartAngleDeg),     //
                    AngularSpeedRad = math.radians(authoring.AngularSpeedDeg), //
                    CurrentAngleRad = math.radians(authoring.StartAngleDeg),   // 라디안 변환
                    Pattern = (int)authoring.Pattern
                });
            }
        }
    }
}