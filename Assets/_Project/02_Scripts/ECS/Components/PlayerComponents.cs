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
        public byte CleanupActionRequested; // 1이면 이번 프레임 행동 전환 요청
        public byte RequestedCleanupActionSlot; // PlayerCleanupActionSlotId(byte) 직렬화 값
    }

    public struct PlayerInputIntentComponent : IComponentData
    {
        public float2 MoveAxis;
        public float2 AimWorldXZ;
        public byte HasAimWorldPoint;
        public byte VacuumRequested; // 1이면 이번 프레임 요청
        public byte CleanupActionRequested; // 1이면 이번 프레임 행동 전환 요청
        public byte RequestedCleanupActionSlot; // PlayerCleanupActionSlotId(byte) 직렬화 값
        public uint Sequence;
    }

    public struct PlayerRadiusComponent : IComponentData
    {
        public float Value;
    }

    public struct VacuumActivationConfigComponent : IComponentData
    {
        public float CaptureActiveTime;
        public float CaptureCooldown;
        public float ActiveTime;
        public float Cooldown;
    }

    public struct VacuumRuntimeStateComponent : IComponentData
    {
        public float CaptureActiveTimer;
        public float CaptureCooldownTimer;
        public float ActiveTimer;
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
        public float HitImpulseMagnitude;
    }

    public struct PlayerHazardPenaltyStateComponent : IComponentData
    {
        public float IFrameTimer;
        public float VacuumLockTimer;
    }

    // Deposit 접촉이 감지되었음을 알리는 요청 태그.
    // Request 단계에서 enable, Execution 단계에서 consume(disable)한다.
    public struct PlayerCarryBinDepositRequestTag : IComponentData, IEnableableComponent { }

    // Deposit 요청 컨텍스트(payload).
    public struct PlayerCarryBinDepositContextComponent : IComponentData
    {
        public Entity DepositEntity;
    }

    // 위험탄 충돌이 감지되었음을 알리는 요청 태그.
    // Request 단계에서 enable, Execution 단계에서 consume(disable)한다.
    public struct PlayerHazardHitRequestTag : IComponentData, IEnableableComponent { }

    // 위험탄 피격 요청에 대한 컨텍스트(payload).
    // - SourceEntity: 피격을 유발한 위험탄의 소스
    public struct PlayerHazardHitContextComponent : IComponentData
    {
        public Entity SourceEntity;
        public float HitDirX;
        public float HitDirZ;
    }

    public enum PlayerUiFeedbackEventType : byte
    {
        None = 0,
        VacuumStartBlocked = 1,
        SourceStateChanged = 2,
        PlayerHazardHit = 3,
        HazardCaptured = 4,
        HazardRemoved = 5,
    }

    public enum PlayerUiFeedbackReasonId : byte
    {
        None = 0,
        CarryBinFull = 1,
        VacuumLocked = 2,
        CooldownActive = 3,
        SourceToWeakened = 4,
        SourceToDepleted = 5,
        Default = 255,
    }

    public enum PlayerImpulseReasonId : byte
    {
        None = 0,
        Default = 1,
    }

    [InternalBufferCapacity(16)]
    public struct PlayerUiFeedbackEventBufferElement : IBufferElementData
    {
        public PlayerUiFeedbackEventType Type;
        public byte Reason;
        public int Value;
        public Entity RelatedEntity;
        public uint Frame;
        public uint Sequence;
    }

    [InternalBufferCapacity(8)]
    public struct PlayerImpulseEventBufferElement : IBufferElementData
    {
        public byte Reason;
        public float DirX;
        public float DirZ;
        public float Magnitude;
        public uint Frame;
        public uint Sequence;
    }

    public struct PlayerUiFeedbackPresentationSnapshotComponent : IComponentData
    {
        public uint Version;
        public PlayerUiFeedbackEventType Type;
        public byte Reason;
        public int Value;
        public Entity RelatedEntity;
        public uint Frame;
        public float RemainingSec;
        public float ClockSec;
        public float NextAllowedVacuumBlockedSec;
        public float NextAllowedSourceStateChangedSec;
        public float NextAllowedHazardCapturedSec;
        public float NextAllowedHazardRemovedSec;
        public float NextAllowedHitSec;
    }

    public struct PlayerImpulsePresentationSnapshotComponent : IComponentData
    {
        public uint Version;
        public byte Reason;
        public float DirX;
        public float DirZ;
        public float Magnitude;
        public uint Frame;
        public int MergedEventCount;
    }
}
