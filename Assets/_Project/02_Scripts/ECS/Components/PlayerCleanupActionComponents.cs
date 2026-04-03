using System.Text;
using Unity.Collections;
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
        public FixedString64Bytes SelectedProfileKey;
        public FixedString64Bytes PendingProfileKey;
        public uint Version;
    }

    public struct PlayerCleanupActionSelectionConfigComponent : IComponentData
    {
        public FixedString64Bytes DefaultProfileKey;
    }

    public struct PlayerCleanupActionSlotMapComponent : IComponentData
    {
        public FixedString64Bytes PrimaryProfileKey;
        public FixedString64Bytes SecondaryProfileKey;
    }

    public struct PlayerCleanupResolvedProfileComponent : IComponentData
    {
        public FixedString64Bytes ProfileKey;
        public PlayerCleanupActionId ActionKind;
        public float CaptureActiveTime;
        public float CaptureCooldown;
        public float ActiveTime;
        public float Cooldown;
        public byte LockFacingWhileActive;
        public float ActiveMoveSpeedScale;
        public float TrashRange;
        public float TrashFanHalfAngleDeg;
        public float HazardRingRadius;
        public float HazardRingWidth;
        public float HazardLineLength;
        public float HazardLineHalfWidth;
        public float TrashSweepInnerRadius;
        public float TrashSweepOuterRadius;
        public float TrashSweepHalfAngleDeg;
        public float TrashSweepStartAngleDeg;
        public float TrashSweepEndAngleDeg;
        public float HazardRectLength;
        public float HazardRectHalfWidth;
        public float HazardForwardWindowAngleDeg;
        public uint Version;
    }

    public struct PlayerCleanupSweepRuntimeStateComponent : IComponentData
    {
        public sbyte NextSweepDirectionSign;
        public sbyte ActiveSweepDirectionSign;
        public float2 LockedFacingXZ;
        public byte HasLockedFacing;
        public uint ActivationFrame;
    }

    [InternalBufferCapacity(4)]
    public struct PlayerCleanupActionProfileBufferElement : IBufferElementData
    {
        public FixedString64Bytes ProfileKey;
        public PlayerCleanupActionId ActionId;

        // action timing profile
        public float CaptureActiveTime;
        public float CaptureCooldown;
        public float ActiveTime;
        public float Cooldown;
        public byte LockFacingWhileActive;
        public float ActiveMoveSpeedScale;

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
        public static bool IsValidProfileKey(string profileKey, out string reason)
        {
            if (string.IsNullOrEmpty(profileKey))
            {
                reason = "ProfileKey is empty.";
                return false;
            }

            int byteCount = Encoding.UTF8.GetByteCount(profileKey);
            if (byteCount > 64)
            {
                reason = $"ProfileKey exceeds 64 UTF-8 bytes ({byteCount}).";
                return false;
            }

            for (int i = 0; i < profileKey.Length; i++)
            {
                char c = profileKey[i];
                bool isLowerAlpha = c >= 'a' && c <= 'z';
                bool isDigit = c >= '0' && c <= '9';
                bool isAllowedPunctuation = c == '_' || c == '.' || c == '/' || c == '-';
                if (!isLowerAlpha && !isDigit && !isAllowedPunctuation)
                {
                    reason = $"ProfileKey contains invalid character '{c}'.";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        public static bool TryConvertProfileKey(string profileKey, out FixedString64Bytes fixedKey)
        {
            fixedKey = default;
            if (!IsValidProfileKey(profileKey, out _))
                return false;

            fixedKey = profileKey;
            return true;
        }

        public static bool IsEmptyProfileKey(in FixedString64Bytes profileKey)
        {
            return profileKey.Length <= 0;
        }

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
            sanitized.LockFacingWhileActive = profile.LockFacingWhileActive != 0 ? (byte)1 : (byte)0;
            sanitized.ActiveMoveSpeedScale = math.max(0f, profile.ActiveMoveSpeedScale);
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

        public static PlayerCleanupResolvedProfileComponent CreateResolvedProfile(
            in PlayerCleanupActionProfileBufferElement profile,
            uint version)
        {
            var sanitized = SanitizeProfile(profile);
            return new PlayerCleanupResolvedProfileComponent
            {
                ProfileKey = sanitized.ProfileKey,
                ActionKind = sanitized.ActionId,
                CaptureActiveTime = sanitized.CaptureActiveTime,
                CaptureCooldown = sanitized.CaptureCooldown,
                ActiveTime = sanitized.ActiveTime,
                Cooldown = sanitized.Cooldown,
                LockFacingWhileActive = sanitized.LockFacingWhileActive,
                ActiveMoveSpeedScale = sanitized.ActiveMoveSpeedScale,
                TrashRange = sanitized.TrashRange,
                TrashFanHalfAngleDeg = sanitized.TrashFanHalfAngleDeg,
                HazardRingRadius = sanitized.HazardRingRadius,
                HazardRingWidth = sanitized.HazardRingWidth,
                HazardLineLength = sanitized.HazardLineLength,
                HazardLineHalfWidth = sanitized.HazardLineHalfWidth,
                TrashSweepInnerRadius = sanitized.TrashSweepInnerRadius,
                TrashSweepOuterRadius = sanitized.TrashSweepOuterRadius,
                TrashSweepHalfAngleDeg = sanitized.TrashSweepHalfAngleDeg,
                TrashSweepStartAngleDeg = sanitized.TrashSweepStartAngleDeg,
                TrashSweepEndAngleDeg = sanitized.TrashSweepEndAngleDeg,
                HazardRectLength = sanitized.HazardRectLength,
                HazardRectHalfWidth = sanitized.HazardRectHalfWidth,
                HazardForwardWindowAngleDeg = sanitized.HazardForwardWindowAngleDeg,
                Version = version,
            };
        }

        public static PlayerCleanupActionProfileBufferElement CreateFallbackBroomSweepProfile(
            string profileKey,
            float range,
            float captureRingRadius,
            float captureRingWidth,
            float captureActiveTime = 0.20f,
            float captureCooldown = 0f,
            float activeTime = 0.22f,
            float cooldown = 1.8f,
            bool lockFacingWhileActive = true,
            float activeMoveSpeedScale = 0.5f)
        {
            float safeRange = math.max(0f, range);
            FixedString64Bytes fixedProfileKey = default;
            if (!TryConvertProfileKey(profileKey, out fixedProfileKey))
                fixedProfileKey = default;

            return SanitizeProfile(new PlayerCleanupActionProfileBufferElement
            {
                ProfileKey = fixedProfileKey,
                ActionId = PlayerCleanupActionId.BroomSweep,
                CaptureActiveTime = captureActiveTime,
                CaptureCooldown = captureCooldown,
                ActiveTime = activeTime,
                Cooldown = cooldown,
                LockFacingWhileActive = lockFacingWhileActive ? (byte)1 : (byte)0,
                ActiveMoveSpeedScale = activeMoveSpeedScale,

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
