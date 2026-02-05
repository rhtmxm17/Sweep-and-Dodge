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

        public float ActiveTime;
        public float ActiveTimer;

        public float Cooldown;
        public float CooldownTimer;

        public byte IsActive;           // 0/1
        public byte ActivateRequested;  // 0/1 (입력 시스템이 1로 세팅 → 이 시스템이 소모)
    }

}