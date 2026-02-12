using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    // Player
    public struct PlayerTag : IComponentData { }

    public struct PlayerGoSyncComponent : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
        public byte SyncRotation;
        public byte VacuumRequested; // 1이면 이번 프레임 요청
    }

    public struct PlayerRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct VacuumBurstComponent : IComponentData
    {
        public float Range;
        public float Strength;
        public float CollectRadius;
        public float CaptureActiveTime;
        public float CaptureActiveTimer;
        public float CaptureRingRadius;
        public float CaptureRingWidth;
        public float CaptureCooldown;
        public float CaptureCooldownTimer;

        public float ActiveTime;
        public float ActiveTimer;

        public float Cooldown;
        public float CooldownTimer;

        public byte IsActive;           // 0/1
        public byte ActivateRequested;  // 0/1 (입력 시스템이 1로 세팅 → 이 시스템이 소모)
    }

    public struct PlayerCarryBinComponent : IComponentData
    {
        public int Load;
        public int Capacity;
    }

    public struct PlayerHazardPenaltyConfigComponent : IComponentData
    {
        public float CarryLossFrac;
        public int CarryLossMin;
        public int CarryLossMax;
        public float IFrameTime;
        public float VacuumLockTime;
    }

    public struct PlayerHazardPenaltyStateComponent : IComponentData
    {
        public float IFrameTimer;
        public float VacuumLockTimer;
    }

    // 위험탄 충돌이 감지되었음을 알리는 요청 태그.
    // Request 단계에서 enable, Execution 단계에서 consume(disable)한다.
    public struct PlayerHazardHitRequestTag : IComponentData, IEnableableComponent { }
}
