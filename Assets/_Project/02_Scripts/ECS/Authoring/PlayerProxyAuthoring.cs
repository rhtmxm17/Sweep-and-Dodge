using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class PlayerProxyAuthoring : MonoBehaviour
    {
        [Header("Proxy Radius")]
        public float PlayerRadius = 0.35f;

        [Header("Vacuum Fallback(CleanupActionSet이 없을 때)")]
        public float Range = 3.2f;
        public float CaptureActiveTime = 0.20f;
        public float CaptureRingRadius = 2.88f;
        public float CaptureRingWidth = 0.8f;
        public float CaptureCooldown = 0.0f;

        public float ActiveTime = 0.22f;
        public float Cooldown = 1.8f;

        [Header("Cleanup Action Set")]
        public PlayerCleanupActionSetSO CleanupActionSet;

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

                AddComponent(e, new VacuumActivationConfigComponent
                {
                    CaptureActiveTime = Mathf.Max(0f, authoring.CaptureActiveTime),
                    CaptureCooldown = Mathf.Max(0f, authoring.CaptureCooldown),
                    ActiveTime = authoring.ActiveTime,
                    Cooldown = authoring.Cooldown,
                });

                AddComponent(e, new VacuumRuntimeStateComponent
                {
                    CaptureActiveTimer = 0f,
                    CaptureCooldownTimer = 0f,
                    ActiveTimer = 0f,
                    CooldownTimer = 0f,
                    IsActive = 0,
                    ActivateRequested = 0
                });

                AddComponent(e, new PlayerGoSyncComponent
                {
                    Position = authoring.transform.position,
                    Rotation = authoring.transform.rotation,
                    SyncRotation = 1,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None
                });

                AddComponent(e, new PlayerInputIntentComponent
                {
                    MoveAxis = float2.zero,
                    AimWorldXZ = float2.zero,
                    HasAimWorldPoint = 0,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                    Sequence = 0u,
                });

                AddComponent(e, new PlayerCleanupActionStateComponent
                {
                    SelectedActionId = ResolveInitialSelectedAction(authoring),
                    PendingActionId = PlayerCleanupActionId.None,
                    Version = 0,
                });

                AddComponent(e, new PlayerCleanupActionSlotMapComponent
                {
                    PrimaryActionId = ResolvePrimarySlotAction(authoring),
                    SecondaryActionId = ResolveSecondarySlotAction(authoring),
                });

                var actionProfileBuffer = AddBuffer<PlayerCleanupActionProfileBufferElement>(e);
                actionProfileBuffer.EnsureCapacity(4);
                BakeCleanupActionProfiles(authoring, actionProfileBuffer);

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

                AddComponent<PlayerCarryBinDepositRequestTag>(e);
                SetComponentEnabled<PlayerCarryBinDepositRequestTag>(e, false);

                AddComponent(e, new PlayerCarryBinDepositContextComponent
                {
                    DepositEntity = Entity.Null
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

            private static void BakeCleanupActionProfiles(
                PlayerProxyAuthoring authoring,
                DynamicBuffer<PlayerCleanupActionProfileBufferElement> actionProfileBuffer)
            {
                var actionSet = authoring.CleanupActionSet;
                if (actionSet != null && actionSet.Profiles != null && actionSet.Profiles.Length > 0)
                {
                    for (int i = 0; i < actionSet.Profiles.Length; i++)
                    {
                        var profile = actionSet.Profiles[i];
                        if (profile.ActionId == PlayerCleanupActionId.None)
                            continue;

                        actionProfileBuffer.Add(new PlayerCleanupActionProfileBufferElement
                        {
                            ActionId = profile.ActionId,
                            TrashRange = Mathf.Max(0f, profile.TrashRange),
                            TrashFanHalfAngleDeg = Mathf.Clamp(profile.TrashFanHalfAngleDeg, 0f, 180f),
                            HazardRingRadius = Mathf.Max(0f, profile.HazardRingRadius),
                            HazardRingWidth = Mathf.Max(0f, profile.HazardRingWidth),
                            HazardLineLength = Mathf.Max(0f, profile.HazardLineLength),
                            HazardLineHalfWidth = Mathf.Max(0f, profile.HazardLineHalfWidth),
                        });
                    }
                }

                if (actionProfileBuffer.Length > 0)
                    return;

                // fallback default 설정 추가
                actionProfileBuffer.Add(new PlayerCleanupActionProfileBufferElement
                {
                    ActionId = PlayerCleanupActionId.RadialRing,
                    TrashRange = Mathf.Max(0f, authoring.Range),
                    TrashFanHalfAngleDeg = 180f,
                    HazardRingRadius = Mathf.Max(0f, authoring.CaptureRingRadius),
                    HazardRingWidth = Mathf.Max(0f, authoring.CaptureRingWidth),
                    HazardLineLength = 0f,
                    HazardLineHalfWidth = 0f,
                });
                actionProfileBuffer.Add(new PlayerCleanupActionProfileBufferElement
                {
                    ActionId = PlayerCleanupActionId.ForwardFanLine,
                    TrashRange = Mathf.Max(0f, authoring.Range),
                    TrashFanHalfAngleDeg = 40f,
                    HazardRingRadius = 0f,
                    HazardRingWidth = 0f,
                    HazardLineLength = Mathf.Max(0f, authoring.Range),
                    HazardLineHalfWidth = 0.55f,
                });
            }

            private static PlayerCleanupActionId ResolveInitialSelectedAction(PlayerProxyAuthoring authoring)
            {
                return authoring.CleanupActionSet != null
                    ? NormalizeActionId(authoring.CleanupActionSet.InitialSelectedAction)
                    : PlayerCleanupActionId.RadialRing;
            }

            private static PlayerCleanupActionId ResolvePrimarySlotAction(PlayerProxyAuthoring authoring)
            {
                return authoring.CleanupActionSet != null
                    ? NormalizeActionId(authoring.CleanupActionSet.PrimarySlotAction)
                    : PlayerCleanupActionId.RadialRing;
            }

            private static PlayerCleanupActionId ResolveSecondarySlotAction(PlayerProxyAuthoring authoring)
            {
                return authoring.CleanupActionSet != null
                    ? NormalizeActionId(authoring.CleanupActionSet.SecondarySlotAction)
                    : PlayerCleanupActionId.ForwardFanLine;
            }

            private static PlayerCleanupActionId NormalizeActionId(PlayerCleanupActionId actionId)
            {
                return actionId switch
                {
                    PlayerCleanupActionId.RadialRing => PlayerCleanupActionId.RadialRing,
                    PlayerCleanupActionId.ForwardFanLine => PlayerCleanupActionId.ForwardFanLine,
                    _ => PlayerCleanupActionId.RadialRing,
                };
            }
        }
    }
}
