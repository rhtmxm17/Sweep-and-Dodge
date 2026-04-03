using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum PlayerCleanupActionId : byte
    {
        None = 0,
        RadialRing = 1,
        ForwardFanLine = 2,
        BroomSweep = 3,
    }

    public enum PlayerCleanupActionSlotId : byte
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
    }

    public struct PlayerCleanupActionStateComponent : IComponentData
    {
        public PlayerCleanupActionId SelectedActionId;
        public PlayerCleanupActionId PendingActionId;
        public uint Version;
    }

    public struct PlayerCleanupActionSlotMapComponent : IComponentData
    {
        public PlayerCleanupActionId PrimaryActionId;
        public PlayerCleanupActionId SecondaryActionId;
    }

    public struct PlayerCleanupSweepRuntimeStateComponent : IComponentData
    {
        public sbyte NextSweepDirectionSign;
        public sbyte ActiveSweepDirectionSign;
        public float2 LockedFacingXZ;
        public byte HasLockedFacing;
        public uint ActivationFrame;
    }

    public struct PlayerCleanupMotionConstraintConfigComponent : IComponentData
    {
        public byte LockFacingWhileActive;
        public float ActiveMoveSpeedScale;
    }

    [InternalBufferCapacity(4)]
    public struct PlayerCleanupActionProfileBufferElement : IBufferElementData
    {
        public PlayerCleanupActionId ActionId;

        // action timing profile
        public float CaptureActiveTime;
        public float CaptureCooldown;
        public float ActiveTime;
        public float Cooldown;

        // legacy compatibility fields for non-default compatibility actions
        public float TrashRange;
        public float TrashFanHalfAngleDeg;

        // legacy compatibility fields for non-default compatibility actions
        public float HazardRingRadius;
        public float HazardRingWidth;
        public float HazardLineLength;
        public float HazardLineHalfWidth;

        // BroomSweep trash profile
        public float TrashSweepInnerRadius;
        public float TrashSweepOuterRadius;
        public float TrashSweepHalfAngleDeg;
        public float TrashSweepStartAngleDeg;
        public float TrashSweepEndAngleDeg;

        // BroomSweep hazard profile
        public float HazardRectLength;
        public float HazardRectHalfWidth;
        public float HazardForwardWindowAngleDeg;
    }

    public static class PlayerCleanupActionContractUtility
    {
        public static PlayerCleanupActionId NormalizeConfiguredActionId(PlayerCleanupActionId actionId)
        {
            return actionId switch
            {
                PlayerCleanupActionId.RadialRing => PlayerCleanupActionId.RadialRing,
                PlayerCleanupActionId.ForwardFanLine => PlayerCleanupActionId.ForwardFanLine,
                PlayerCleanupActionId.BroomSweep => PlayerCleanupActionId.BroomSweep,
                _ => PlayerCleanupActionId.BroomSweep,
            };
        }

        public static PlayerCleanupActionId NormalizeRuntimeActionId(PlayerCleanupActionId actionId, bool allowNone = false)
        {
            if (allowNone && actionId == PlayerCleanupActionId.None)
                return PlayerCleanupActionId.None;

            return NormalizeConfiguredActionId(actionId);
        }

        public static PlayerCleanupActionProfileBufferElement SanitizeProfile(in PlayerCleanupActionProfileBufferElement profile)
        {
            var sanitized = profile;
            sanitized.ActionId = NormalizeConfiguredActionId(profile.ActionId);
            sanitized.CaptureActiveTime = math.max(0f, profile.CaptureActiveTime);
            sanitized.CaptureCooldown = math.max(0f, profile.CaptureCooldown);
            sanitized.ActiveTime = math.max(0f, profile.ActiveTime);
            sanitized.Cooldown = math.max(0f, profile.Cooldown);
            sanitized.TrashRange = math.max(0f, profile.TrashRange);
            sanitized.TrashFanHalfAngleDeg = math.clamp(profile.TrashFanHalfAngleDeg, 0f, 180f);
            sanitized.HazardRingRadius = math.max(0f, profile.HazardRingRadius);
            sanitized.HazardRingWidth = math.max(0f, profile.HazardRingWidth);
            sanitized.HazardLineLength = math.max(0f, profile.HazardLineLength);
            sanitized.HazardLineHalfWidth = math.max(0f, profile.HazardLineHalfWidth);
            sanitized.TrashSweepInnerRadius = math.max(0f, profile.TrashSweepInnerRadius);
            sanitized.TrashSweepOuterRadius = math.max(sanitized.TrashSweepInnerRadius, profile.TrashSweepOuterRadius);
            sanitized.TrashSweepHalfAngleDeg = math.clamp(profile.TrashSweepHalfAngleDeg, 0f, 180f);
            sanitized.HazardRectLength = math.max(0f, profile.HazardRectLength);
            sanitized.HazardRectHalfWidth = math.max(0f, profile.HazardRectHalfWidth);
            sanitized.HazardForwardWindowAngleDeg = math.clamp(profile.HazardForwardWindowAngleDeg, 0f, 180f);
            return sanitized;
        }

        public static PlayerCleanupActionProfileBufferElement CreateFallbackBroomSweepProfile(
            float range,
            float captureRingRadius,
            float captureRingWidth,
            float captureActiveTime = 0.20f,
            float captureCooldown = 0f,
            float activeTime = 0.22f,
            float cooldown = 1.8f)
        {
            float safeRange = math.max(0f, range);
            return SanitizeProfile(new PlayerCleanupActionProfileBufferElement
            {
                ActionId = PlayerCleanupActionId.BroomSweep,
                CaptureActiveTime = captureActiveTime,
                CaptureCooldown = captureCooldown,
                ActiveTime = activeTime,
                Cooldown = cooldown,

                // Legacy compatibility values retained for explicit RadialRing/ForwardFanLine fixtures.
                TrashRange = safeRange,
                TrashFanHalfAngleDeg = 180f,
                HazardRingRadius = math.max(0f, captureRingRadius),
                HazardRingWidth = math.max(0f, captureRingWidth),
                HazardLineLength = 0f,
                HazardLineHalfWidth = 0f,

                TrashSweepInnerRadius = math.min(1.0f, safeRange),
                TrashSweepOuterRadius = safeRange,
                TrashSweepHalfAngleDeg = 12f,
                TrashSweepStartAngleDeg = -20f,
                TrashSweepEndAngleDeg = 80f,
                HazardRectLength = safeRange,
                HazardRectHalfWidth = 0.55f,
                HazardForwardWindowAngleDeg = 7f,
            });
        }
    }
}
