using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace SweepNDodge.DotsBullets
{
    public class PlayerProxyAuthoring : MonoBehaviour
    {
        [Header("Proxy Radius")]
        public float PlayerRadius = 0.35f;

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

        [Header("Hazard Risk")]
        public int HazardStackMax = 5;
        public float HazardBonusRate = 0.05f;

        public static PlayerCleanupActionSetSO RequireCleanupActionSet(PlayerCleanupActionSetSO cleanupActionSet)
        {
            if (cleanupActionSet != null)
                return cleanupActionSet;

            throw new InvalidOperationException(
                $"{nameof(PlayerProxyAuthoring)} requires a {nameof(PlayerCleanupActionSetSO)} reference.");
        }

        private class PlayerProxyBaker : Baker<PlayerProxyAuthoring>
        {
            public override void Bake(PlayerProxyAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);
                var cleanupActionSet = RequireCleanupActionSet(authoring.CleanupActionSet);

                AddComponent<PlayerTag>(e);

                AddComponent(e, new PlayerRadiusComponent
                {
                    Value = authoring.PlayerRadius
                });

                AddComponent(e, new PlayerPreviousPositionComponent
                {
                    Position = authoring.transform.position
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

                AddComponent(e, new PlayerResolvedInputSnapshotComponent
                {
                    MoveAxis = float2.zero,
                    AimWorldXZ = float2.zero,
                    HasAimWorldPoint = 0,
                    VacuumRequested = 0,
                    CleanupActionRequested = 0,
                    RequestedCleanupActionSlot = (byte)PlayerCleanupActionSlotId.None,
                    Sequence = 0u,
                });

                AddComponent(e, new PlayerStageEntryApplyStateComponent
                {
                    LastAppliedVersion = 0u,
                });

                AddComponent(e, new PlayerCleanupActionStateComponent
                {
                    SelectedActionId = ResolveInitialSelectedAction(cleanupActionSet),
                    PendingActionId = PlayerCleanupActionId.None,
                    Version = 0,
                });

                AddComponent(e, new PlayerCleanupActionSlotMapComponent
                {
                    PrimaryActionId = ResolvePrimarySlotAction(cleanupActionSet),
                    SecondaryActionId = ResolveSecondarySlotAction(cleanupActionSet),
                });

                AddComponent(e, new PlayerCleanupSweepRuntimeStateComponent
                {
                    NextSweepDirectionSign = 1,
                    ActiveSweepDirectionSign = 0,
                    LockedFacingXZ = float2.zero,
                    HasLockedFacing = 0,
                    ActivationFrame = 0u,
                });

                AddComponent(e, new PlayerCleanupMotionConstraintConfigComponent
                {
                    LockFacingWhileActive = cleanupActionSet.LockFacingWhileActive ? (byte)1 : (byte)0,
                    ActiveMoveSpeedScale = Mathf.Max(0f, cleanupActionSet.ActiveMoveSpeedScale),
                });

                var actionProfileBuffer = AddBuffer<PlayerCleanupActionProfileBufferElement>(e);
                actionProfileBuffer.EnsureCapacity(4);
                BakeCleanupActionProfiles(cleanupActionSet, actionProfileBuffer);

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

                AddComponent(e, new PlayerHazardRiskConfigComponent
                {
                    HazardStackMax = Mathf.Max(0, authoring.HazardStackMax),
                    HazardBonusRate = Mathf.Max(0f, authoring.HazardBonusRate),
                });

                AddComponent(e, new PlayerHazardRiskStateComponent
                {
                    HazardStack = 0,
                });

                AddComponent(e, new PlayerHazardRiskRequestComponent
                {
                    PendingHazardCapturedCount = 0,
                    ResetRequested = 0,
                });

                AddComponent<PlayerCarryBinDepositRequestTag>(e);
                SetComponentEnabled<PlayerCarryBinDepositRequestTag>(e, false);

                AddComponent(e, new PlayerCarryBinDepositContextComponent
                {
                    DepositRegionId = 0u
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

                AddComponent(e, new PlayerUiFeedbackPresentationSnapshotComponent
                {
                    Version = 0u,
                    Type = PlayerUiFeedbackEventType.None,
                    Reason = (byte)PlayerUiFeedbackReasonId.None,
                    Value = 0,
                    RelatedEntity = Entity.Null,
                    Frame = 0u,
                    RemainingSec = 0f,
                    ClockSec = 0f,
                    NextAllowedVacuumBlockedSec = 0f,
                    NextAllowedSourceStateChangedSec = 0f,
                    NextAllowedHazardCapturedSec = 0f,
                    NextAllowedHazardRemovedSec = 0f,
                    NextAllowedHitSec = 0f,
                });

                AddComponent(e, new PlayerImpulsePresentationSnapshotComponent
                {
                    Version = 0u,
                    Reason = (byte)PlayerImpulseReasonId.None,
                    DirX = 0f,
                    DirZ = 0f,
                    Magnitude = 0f,
                    Frame = 0u,
                    MergedEventCount = 0,
                });
            }

            private static void BakeCleanupActionProfiles(
                PlayerCleanupActionSetSO actionSet,
                DynamicBuffer<PlayerCleanupActionProfileBufferElement> actionProfileBuffer)
            {
                if (actionSet.Profiles != null && actionSet.Profiles.Length > 0)
                {
                    for (int i = 0; i < actionSet.Profiles.Length; i++)
                    {
                        var profile = actionSet.Profiles[i];
                        if (profile.ActionId == PlayerCleanupActionId.None)
                            continue;

                        actionProfileBuffer.Add(PlayerCleanupActionContractUtility.SanitizeProfile(new PlayerCleanupActionProfileBufferElement
                        {
                            ActionId = profile.ActionId,
                            CaptureActiveTime = profile.CaptureActiveTime,
                            CaptureCooldown = profile.CaptureCooldown,
                            ActiveTime = profile.ActiveTime,
                            Cooldown = profile.Cooldown,
                            TrashRange = profile.TrashRange,
                            TrashFanHalfAngleDeg = profile.TrashFanHalfAngleDeg,
                            HazardRingRadius = profile.HazardRingRadius,
                            HazardRingWidth = profile.HazardRingWidth,
                            HazardLineLength = profile.HazardLineLength,
                            HazardLineHalfWidth = profile.HazardLineHalfWidth,
                            TrashSweepInnerRadius = profile.TrashSweepInnerRadius,
                            TrashSweepOuterRadius = profile.TrashSweepOuterRadius,
                            TrashSweepHalfAngleDeg = profile.TrashSweepHalfAngleDeg,
                            TrashSweepStartAngleDeg = profile.TrashSweepStartAngleDeg,
                            TrashSweepEndAngleDeg = profile.TrashSweepEndAngleDeg,
                            HazardRectLength = profile.HazardRectLength,
                            HazardRectHalfWidth = profile.HazardRectHalfWidth,
                            HazardForwardWindowAngleDeg = profile.HazardForwardWindowAngleDeg,
                        }));
                    }
                }

                if (actionProfileBuffer.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"{nameof(PlayerCleanupActionSetSO)} must contain at least one cleanup action profile.");
                }
            }

            private static PlayerCleanupActionId ResolveInitialSelectedAction(PlayerCleanupActionSetSO actionSet)
            {
                return NormalizeActionId(actionSet.InitialSelectedAction);
            }

            private static PlayerCleanupActionId ResolvePrimarySlotAction(PlayerCleanupActionSetSO actionSet)
            {
                return NormalizeActionId(actionSet.PrimarySlotAction);
            }

            private static PlayerCleanupActionId ResolveSecondarySlotAction(PlayerCleanupActionSetSO actionSet)
            {
                return NormalizeActionId(actionSet.SecondarySlotAction);
            }

            private static PlayerCleanupActionId NormalizeActionId(PlayerCleanupActionId actionId)
            {
                return PlayerCleanupActionContractUtility.NormalizeConfiguredActionId(actionId);
            }
        }
    }
}
