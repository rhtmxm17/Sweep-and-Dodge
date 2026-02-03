using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.ECS
{
    // Velocity에 기반한 Bullet 이동 시스템
    public partial struct BulletMoveSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // MoveJob을 생성하고 실행
            var moveJob = new MoveJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime
            };

            moveJob.ScheduleParallel();
        }

        // Bullet 이동을 정의하는 Job
        [BurstCompile]
        partial struct MoveJob : IJobEntity
        {
            public float DeltaTime;

            public void Execute(ref LocalTransform transform, in Velocity2DComponent velocity, in BulletTag _)
            {
                var p = transform.Position;
                p.x += velocity.Value.x * DeltaTime;
                p.y += velocity.Value.y * DeltaTime;
                transform.Position = p;
            }
        }
    }
}
