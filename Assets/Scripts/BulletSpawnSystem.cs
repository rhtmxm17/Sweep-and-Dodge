using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.ECS
{

    [BurstCompile]
    public partial struct BulletSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BulletSpawnerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var elapsed = (float)SystemAPI.Time.ElapsedTime;

            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (spawnerRW, spawnerTransform) in
                     SystemAPI.Query<RefRW<BulletSpawnerComponent>, RefRO<LocalTransform>>())
            {
                ref var sp = ref spawnerRW.ValueRW;

                sp.Timer -= dt;
                if (sp.Timer > 0f)
                    continue;

                // Timer가 0 이하로 내려간 초과분(overflow) 계산
                float overflow = -sp.Timer;
                sp.Timer += sp.FireInterval;

                var origin = spawnerTransform.ValueRO.Position;
                origin.z = 0f;

                switch ((BulletPatternType)sp.Pattern)
                {
                    case BulletPatternType.Ring:
                        SpawnRing(ecb, sp, origin, elapsed, overflow);
                        break;

                    case BulletPatternType.Spiral:
                        SpawnSpiral(ecb, ref sp, origin, overflow);
                        break;

                    case BulletPatternType.Aimed:
                        // TODO: 타게팅 로직 추가 필요
                        //   현재는 정면 발사로 대체
                        SpawnForward(ecb, ref sp, origin, overflow);
                        break;
                }
            }
        }

        static void SpawnRing(EntityCommandBuffer ecb, in BulletSpawnerComponent sp, float3 origin, float elapsed, float overflow)
        {
            int count = sp.BulletsPerShot;
            float step = math.PI * 2f / count;

            float baseAngle = sp.StartAngleRad + sp.AngularSpeedRad * elapsed;

            for (int i = 0; i < count; i++)
            {
                float angle = baseAngle + step * i;
                float2 dir = new float2(math.cos(angle), math.sin(angle));

                var bullet = ecb.Instantiate(sp.BulletPrefab);

                // 위치 보정: velocity * overflow
                float3 correctedPos = origin + new float3(dir * sp.BulletSpeed * overflow, 0f);

                ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(
                    correctedPos, quaternion.identity, 1f));

                ecb.SetComponent(bullet, new Velocity2DComponent { Value = dir * sp.BulletSpeed });
            }
        }

        static void SpawnSpiral(EntityCommandBuffer ecb, ref BulletSpawnerComponent sp, float3 origin, float overflow)
        {
            // One bullet per tick, angle advances each shot.
            float angle = sp.CurrentAngleRad;
            sp.CurrentAngleRad += sp.AngularSpeedRad * sp.FireInterval;

            float2 dir = new float2(math.cos(angle), math.sin(angle));

            var bullet = ecb.Instantiate(sp.BulletPrefab);

            // 위치 보정: velocity * overflow
            float3 correctedPos = origin + new float3(dir * sp.BulletSpeed * overflow, 0f);

            ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(correctedPos, quaternion.identity, 1f));
            ecb.SetComponent(bullet, new Velocity2DComponent { Value = dir * sp.BulletSpeed });
        }

        static void SpawnForward(EntityCommandBuffer ecb, ref BulletSpawnerComponent sp, float3 origin, float overflow)
        {
            float2 dir = new float2(0f, 1f);

            var bullet = ecb.Instantiate(sp.BulletPrefab);

            // 위치 보정: velocity * overflow
            float3 correctedPos = origin + new float3(dir * sp.BulletSpeed * overflow, 0f);

            ecb.SetComponent(bullet, LocalTransform.FromPositionRotationScale(correctedPos, quaternion.identity, 1f));
            ecb.SetComponent(bullet, new Velocity2DComponent { Value = dir * sp.BulletSpeed });
        }
    }
}