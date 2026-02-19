using Unity.Entities;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class PlayerProxyAuthoring : MonoBehaviour
    {
        [Header("Proxy Radius")]
        public float PlayerRadius = 0.35f;

        [Header("Vacuum")]
        public float Range = 3.2f;
        public float Strength = 75f;
        public float CollectRadius = 0.35f;
        public float CaptureActiveTime = 0.20f;
        public float CaptureRingRadius = 2.88f;
        public float CaptureRingWidth = 0.8f;
        public float CaptureCooldown = 0.0f;

        public float ActiveTime = 0.22f;
        public float Cooldown = 1.8f;

        [Header("CarryBin")]
        public int CarryCapacity = 300;

        [Header("Hazard Penalty")]
        [Range(0f, 1f)] public float CarryLossFrac = 0.15f;
        public int CarryLossMin = 5;
        public int CarryLossMax = 30;
        public float IFrameTime = 0.7f;
        public float VacuumLockTime = 0.7f;
        public float HitImpulseMagnitude = 1.0f;

        private class PlayerProxyBaker : Baker<PlayerProxyAuthoring>
        {
            public override void Bake(PlayerProxyAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                AddComponent<PlayerTag>(e);

                AddComponent(e, new PlayerRadiusComponent
                {
                    Value = authoring.PlayerRadius
                });

                AddComponent(e, new VacuumBurstComponent
                {
                    Range = authoring.Range,
                    Strength = authoring.Strength,
                    CollectRadius = authoring.CollectRadius,
                    CaptureActiveTime = Mathf.Max(0f, authoring.CaptureActiveTime),
                    CaptureActiveTimer = 0f,
                    CaptureRingRadius = Mathf.Max(0f, authoring.CaptureRingRadius),
                    CaptureRingWidth = Mathf.Max(0f, authoring.CaptureRingWidth),
                    CaptureCooldown = Mathf.Max(0f, authoring.CaptureCooldown),
                    CaptureCooldownTimer = 0f,

                    ActiveTime = authoring.ActiveTime,
                    ActiveTimer = 0f,

                    Cooldown = authoring.Cooldown,
                    CooldownTimer = 0f,

                    IsActive = 0,
                    ActivateRequested = 0
                });

                AddComponent(e, new PlayerGoSyncComponent
                {
                    Position = authoring.transform.position,
                    Rotation = authoring.transform.rotation,
                    SyncRotation = 1,
                    VacuumRequested = 0
                });

                AddComponent(e, new PlayerCarryBinComponent
                {
                    Load = 0,
                    Capacity = Mathf.Max(0, authoring.CarryCapacity)
                });

                AddComponent(e, new PlayerHazardPenaltyConfigComponent
                {
                    CarryLossFrac = Mathf.Clamp01(authoring.CarryLossFrac),
                    CarryLossMin = Mathf.Max(0, authoring.CarryLossMin),
                    CarryLossMax = Mathf.Max(0, authoring.CarryLossMax),
                    IFrameTime = Mathf.Max(0f, authoring.IFrameTime),
                    VacuumLockTime = Mathf.Max(0f, authoring.VacuumLockTime),
                    HitImpulseMagnitude = Mathf.Max(0f, authoring.HitImpulseMagnitude)
                });

                AddComponent(e, new PlayerHazardPenaltyStateComponent
                {
                    IFrameTimer = 0f,
                    VacuumLockTimer = 0f
                });

                AddComponent<PlayerHazardHitRequestTag>(e);
                SetComponentEnabled<PlayerHazardHitRequestTag>(e, false);

                AddComponent(e, new PlayerHazardHitContextComponent
                {
                    SourceEntity = Entity.Null,
                    HitDirX = 0f,
                    HitDirZ = 0f
                });

                var uiFeedbackBuffer = AddBuffer<PlayerUiFeedbackEventBufferElement>(e);
                uiFeedbackBuffer.EnsureCapacity(16);

                var impulseBuffer = AddBuffer<PlayerImpulseEventBufferElement>(e);
                impulseBuffer.EnsureCapacity(8);
            }
        }
    }
}
