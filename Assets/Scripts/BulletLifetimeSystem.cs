using Unity.Burst;
using Unity.Entities;

namespace SweepNDodge.ECS
{
    [BurstCompile]
    public partial struct BulletLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var ecb = SystemAPI
                .GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            var job = new LifetimeJob
            {
                DeltaTime = dt,
                ECB = ecb.AsParallelWriter()
            };

            job.ScheduleParallel();
        }

        [BurstCompile]
        partial struct LifetimeJob : IJobEntity
        {
            public float DeltaTime;
            public EntityCommandBuffer.ParallelWriter ECB;

            public void Execute(Entity entity, [EntityIndexInQuery] int entityInQueryIndex, ref LifetimeComponent lifetime, in BulletTag _)
            {
                lifetime.Value -= DeltaTime;
                if (lifetime.Value <= 0f)
                {
                    ECB.DestroyEntity(entityInQueryIndex, entity);
                }
            }
        }
    }
}
