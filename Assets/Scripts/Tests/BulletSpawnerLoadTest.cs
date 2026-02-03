using System.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Scenes;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.ECS.Tests
{
    public class BulletSpawnerLoadTest : MonoBehaviour
    {
        [Header("Spawner Entity Prefab")]
        public EntityPrefabReference BulletSpawnerPrefab;

        [Header("테스트 설정")]
        public int SpawnerCount = 1000;
        public Vector2 SpawnAreaMin = new Vector2(-50, -50);
        public Vector2 SpawnAreaMax = new Vector2(50, 50);

        [Header("랜덤 시드")]
        public bool UseDeterministicSeed = true;
        public uint Seed = 1;

        // BulletSpawnerPrefab으로부터 SpawnerCount만큼 Entity 생성
        IEnumerator Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager = world.EntityManager;

            // 로드 요청 엔티티 생성 + RequestEntityPrefabLoaded 추가
            var requestEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(requestEntity, new RequestEntityPrefabLoaded
            {
                Prefab = BulletSpawnerPrefab
            });

            // 로드 완료까지 대기 (PrefabLoadResult가 붙으면 완료)
            while (!entityManager.HasComponent<PrefabLoadResult>(requestEntity))
                yield return null;

            var loadResult = entityManager.GetComponentData<PrefabLoadResult>(requestEntity);
            var spawnerEntityPrefab = loadResult.PrefabRoot;

            if (spawnerEntityPrefab == Entity.Null)
            {
                Debug.LogError("[LoadTest] 엔티티 프리펩 로드 실패. 프리펩이 없거나 엔티티 프리펩으로 구울 수 없습니다.");
                yield break;
            }

            // 스포너 엔티티 생성 및 배치
            var rng = new Unity.Mathematics.Random(UseDeterministicSeed ? math.max(1u, Seed)
                                                                        : (uint)System.Environment.TickCount);
            
            float2 min = SpawnAreaMin;
            float2 max = SpawnAreaMax;

            // 간단 루프(안전/호환성 우선). 필요하면 나중에 배치 Instantiate로 최적화하세요.
            for (int i = 0; i < SpawnerCount; i++)
            {
                var e = entityManager.Instantiate(spawnerEntityPrefab);

                float x = math.lerp(min.x, max.x, rng.NextFloat());
                float y = math.lerp(min.y, max.y, rng.NextFloat());

                var t = LocalTransform.FromPosition(new float3(x, y, 0f));

                entityManager.SetComponentData(e, t);
            }

            Debug.Log($"[LoadTest] {SpawnerCount} 개의 스포너 생성됨 [{SpawnAreaMin} ~ {SpawnAreaMax}].");
        }
    }
}