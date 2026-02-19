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

    public enum VacuumStartBlockReasonId : byte
    {
        None = 0,
        CarryBinFull = 1,
    }

    // 흡입 시작 실패 피드백(프레임 이벤트).
    // - HasBlockEvent: 이번 프레임에 시작 거부 이벤트가 발생했는지 여부
    // - Reason: 시작 거부 사유
    public struct PlayerVacuumStartBlockFeedbackComponent : IComponentData
    {
        public byte HasBlockEvent;
        public VacuumStartBlockReasonId Reason;
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

    // 위험탄 피격 요청에 대한 컨텍스트(payload).
    // - SourceEntity: 피격을 유발한 위험탄의 소스
    public struct PlayerHazardHitContextComponent : IComponentData
    {
        public Entity SourceEntity;
    }
}
