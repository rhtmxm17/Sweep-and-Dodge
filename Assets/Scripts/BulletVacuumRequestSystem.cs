using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Vacuum 제거 행동: 활성 시간 동안 Range 내 탄환(Trash)을 즉시 디스폰 요청.
    /// - 실제 비활성/풀 반납은 BulletExecutionGroup의 BulletDespawnExecutionSystem이 단일 책임으로 수행
    /// - LocalTransform 타입 충돌 방지: 메인 스레드에서 LocalTransform을 직접 읽지 않고 Job으로 스케줄
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(BulletRequestGroup))]
    public partial struct BulletVacuumRequestSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BulletFieldConfigComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var dt = SystemAPI.Time.DeltaTime;
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();

            // Vacuum 상태 갱신(플레이어 단일)
            var vacuumRW = SystemAPI.GetComponentRW<VacuumBurstComponent>(playerEntity);
            UpdateVacuumState(ref vacuumRW.ValueRW, dt);

            if (vacuumRW.ValueRO.IsActive == 0)
                return;

            var txLookup = SystemAPI.GetComponentLookup<LocalTransform>(isReadOnly: true);
            var kindLookup = SystemAPI.GetComponentLookup<BulletKindComponent>(isReadOnly: true);
            txLookup.Update(ref state);
            kindLookup.Update(ref state);

            state.Dependency = new VacuumRequestJob
            {
                PlayerEntity = playerEntity,
                RangeSq = vacuumRW.ValueRO.Range * vacuumRW.ValueRO.Range,
                TxLookup = txLookup,
                KindLookup = kindLookup,
            }.ScheduleParallel(state.Dependency);
        }

        private static void UpdateVacuumState(ref VacuumBurstComponent v, float dt)
        {
            if (v.CooldownTimer > 0f)
                v.CooldownTimer = math.max(0f, v.CooldownTimer - dt);

            if (v.IsActive != 0)
            {
                v.ActiveTimer = math.max(0f, v.ActiveTimer - dt);
                if (v.ActiveTimer <= 0f)
                {
                    v.IsActive = 0;
                    v.CooldownTimer = v.Cooldown;
                }
                return;
            }

            if (v.ActivateRequested != 0 && v.CooldownTimer <= 0f)
            {
                v.ActivateRequested = 0;
                v.IsActive = 1;
                v.ActiveTimer = v.ActiveTime;
            }
            else
            {
                // 선입력 버림(쿨타임 중 요청은 폐기)
                v.ActivateRequested = 0;
            }
        }

        [BurstCompile]
        private partial struct VacuumRequestJob : IJobEntity
        {
            public Entity PlayerEntity;
            public float RangeSq;

            [ReadOnly] public ComponentLookup<LocalTransform> TxLookup;
            [ReadOnly] public ComponentLookup<BulletKindComponent> KindLookup;

            private void Execute(
                Entity e,
                EnabledRefRO<BulletActiveTag> active,
                EnabledRefRW<BulletDespawnRequestTag> request)
            {
                if (!active.ValueRO)
                    return;

                // 이미 요청된 탄은 스킵(중복 작업/중복 점수 방지용)
                if (request.ValueRO)
                    return;

                if (!TxLookup.HasComponent(PlayerEntity) || !TxLookup.HasComponent(e))
                    return;
                if (!KindLookup.HasComponent(e) || KindLookup[e].Value != BulletKindId.Trash)
                    return;

                // 메인 스레드에서 Player LocalTransform을 읽지 않기 위해, Job 안에서 lookup으로 참조
                var playerPos = TxLookup[PlayerEntity].Position;
                var p = TxLookup[e].Position;
                float dx = p.x - playerPos.x;
                float dz = p.z - playerPos.z;
                float distSq = dx * dx + dz * dz;

                if (distSq <= RangeSq)
                    request.ValueRW = true;
            }
        }
    }
}
