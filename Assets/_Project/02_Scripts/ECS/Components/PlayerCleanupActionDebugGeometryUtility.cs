using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public struct BroomSweepFrameGeometry
    {
        public byte CaptureReady;
        public float2 LockedForwardXZ;
        public float2 LockedRightXZ;
        public float Progress01;
        public float CurrentSweepCenterAngleDeg;
        public byte HazardWindowActive;
        public float SearchRadius;
    }

    public static class PlayerCleanupActionDebugGeometryUtility
    {
        private const float FallbackRadialTrashRange = 3.2f;
        private const float FallbackRadialHazardRingRadius = 2.88f;
        private const float FallbackRadialHazardRingWidth = 0.8f;
        private const float FallbackForwardTrashRange = 3.2f;
        private const float FallbackForwardTrashHalfAngleDeg = 40f;
        private const float FallbackForwardHazardLineLength = 3.2f;
        private const float FallbackForwardHazardLineHalfWidth = 0.5f;

        public static PlayerCleanupActionProfileBufferElement ResolveActionProfile(
            DynamicBuffer<PlayerCleanupActionProfileBufferElement> profiles,
            PlayerCleanupActionId actionId)
        {
            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i].ActionId == actionId)
                    return PlayerCleanupActionContractUtility.SanitizeProfile(profiles[i]);
            }

            if (actionId == PlayerCleanupActionId.ForwardFanLine)
            {
                return PlayerCleanupActionContractUtility.SanitizeProfile(new PlayerCleanupActionProfileBufferElement
                {
                    ActionId = PlayerCleanupActionId.ForwardFanLine,
                    CaptureActiveTime = 0.20f,
                    CaptureCooldown = 0f,
                    ActiveTime = 0.22f,
                    Cooldown = 1.8f,
                    TrashRange = FallbackForwardTrashRange,
                    TrashFanHalfAngleDeg = FallbackForwardTrashHalfAngleDeg,
                    HazardRingRadius = 0f,
                    HazardRingWidth = 0f,
                    HazardLineLength = FallbackForwardHazardLineLength,
                    HazardLineHalfWidth = FallbackForwardHazardLineHalfWidth,
                });
            }

            if (actionId == PlayerCleanupActionId.BroomSweep)
            {
                return PlayerCleanupActionContractUtility.CreateFallbackBroomSweepProfile(
                    FallbackRadialTrashRange,
                    FallbackRadialHazardRingRadius,
                    FallbackRadialHazardRingWidth);
            }

            return PlayerCleanupActionContractUtility.SanitizeProfile(new PlayerCleanupActionProfileBufferElement
            {
                ActionId = PlayerCleanupActionId.RadialRing,
                CaptureActiveTime = 0.20f,
                CaptureCooldown = 0f,
                ActiveTime = 0.22f,
                Cooldown = 1.8f,
                TrashRange = FallbackRadialTrashRange,
                TrashFanHalfAngleDeg = 180f,
                HazardRingRadius = FallbackRadialHazardRingRadius,
                HazardRingWidth = FallbackRadialHazardRingWidth,
                HazardLineLength = 0f,
                HazardLineHalfWidth = 0f,
            });
        }

        public static BroomSweepFrameGeometry ResolveBroomSweepFrameGeometry(
            PlayerCleanupActionId actionId,
            in VacuumRuntimeStateComponent vacuumState,
            in PlayerCleanupSweepRuntimeStateComponent sweepRuntimeState,
            in PlayerCleanupActionProfileBufferElement profile)
        {
            var geometry = default(BroomSweepFrameGeometry);
            geometry.SearchRadius = ComputeSearchRange(actionId, in profile);

            if (actionId != PlayerCleanupActionId.BroomSweep)
                return geometry;

            if (sweepRuntimeState.HasLockedFacing == 0 || sweepRuntimeState.ActiveSweepDirectionSign == 0)
                return geometry;

            float2 facingXZ = sweepRuntimeState.LockedFacingXZ;
            if (math.lengthsq(facingXZ) <= 1e-8f)
                return geometry;

            float safeActiveTime = math.max(1e-5f, profile.ActiveTime);
            geometry.Progress01 = math.saturate(1f - (math.max(0f, vacuumState.ActiveTimer) / safeActiveTime));

            float startAngleDeg;
            float endAngleDeg;
            if (sweepRuntimeState.ActiveSweepDirectionSign > 0)
            {
                startAngleDeg = profile.TrashSweepStartAngleDeg;
                endAngleDeg = profile.TrashSweepEndAngleDeg;
            }
            else
            {
                startAngleDeg = -profile.TrashSweepStartAngleDeg;
                endAngleDeg = -profile.TrashSweepEndAngleDeg;
            }

            geometry.LockedForwardXZ = math.normalize(facingXZ);
            geometry.LockedRightXZ = new float2(geometry.LockedForwardXZ.y, -geometry.LockedForwardXZ.x);
            geometry.CurrentSweepCenterAngleDeg = math.lerp(startAngleDeg, endAngleDeg, geometry.Progress01);
            geometry.HazardWindowActive = (byte)(vacuumState.CaptureActiveTimer > 0f
                && math.abs(geometry.CurrentSweepCenterAngleDeg) <= math.max(0f, profile.HazardForwardWindowAngleDeg)
                    ? 1
                    : 0);
            geometry.CaptureReady = 1;
            return geometry;
        }

        public static float ComputeSearchRange(PlayerCleanupActionId actionId, in PlayerCleanupActionProfileBufferElement profile)
        {
            if (actionId == PlayerCleanupActionId.BroomSweep)
            {
                float hazardDiagonal = math.sqrt(
                    profile.HazardRectLength * profile.HazardRectLength
                    + profile.HazardRectHalfWidth * profile.HazardRectHalfWidth);
                return math.max(0f, math.max(profile.TrashSweepOuterRadius, hazardDiagonal));
            }

            float radialOuter = GetHazardRingOuter(in profile);
            float forwardRange = math.max(profile.TrashRange, profile.HazardLineLength + profile.HazardLineHalfWidth);
            return math.max(0f, math.max(radialOuter, forwardRange));
        }

        public static bool EvaluateBroomTrashCapture(
            float distSq,
            float dxp,
            float dzp,
            float bulletRadius,
            in PlayerCleanupActionProfileBufferElement profile,
            in BroomSweepFrameGeometry geometry)
        {
            if (geometry.CaptureReady == 0)
                return false;

            float innerRadius = math.max(0f, profile.TrashSweepInnerRadius - bulletRadius);
            float outerRadius = math.max(innerRadius, profile.TrashSweepOuterRadius + bulletRadius);
            if (distSq < innerRadius * innerRadius || distSq > outerRadius * outerRadius)
                return false;

            float relativeAngleDeg = ComputeSignedAngleDeg(dxp, dzp, geometry.LockedForwardXZ, geometry.LockedRightXZ);
            float deltaAngleDeg = ComputeDeltaAngleDeg(relativeAngleDeg, geometry.CurrentSweepCenterAngleDeg);
            return math.abs(deltaAngleDeg) <= profile.TrashSweepHalfAngleDeg;
        }

        public static bool EvaluateBroomHazardCapture(
            float dxp,
            float dzp,
            float bulletRadius,
            in PlayerCleanupActionProfileBufferElement profile,
            in BroomSweepFrameGeometry geometry)
        {
            if (geometry.CaptureReady == 0 || geometry.HazardWindowActive == 0)
                return false;

            float forwardProjection = dxp * geometry.LockedForwardXZ.x + dzp * geometry.LockedForwardXZ.y;
            if (forwardProjection < -bulletRadius)
                return false;

            float maxForward = math.max(0f, profile.HazardRectLength + bulletRadius);
            if (forwardProjection > maxForward)
                return false;

            float lateral = math.abs(dxp * geometry.LockedRightXZ.x + dzp * geometry.LockedRightXZ.y);
            float maxLateral = math.max(0f, profile.HazardRectHalfWidth + bulletRadius);
            return lateral <= maxLateral;
        }

        public static float ComputeSignedAngleDeg(
            float dxp,
            float dzp,
            float2 forwardXZ,
            float2 rightXZ)
        {
            float forwardProjection = dxp * forwardXZ.x + dzp * forwardXZ.y;
            float rightProjection = dxp * rightXZ.x + dzp * rightXZ.y;
            return math.degrees(math.atan2(rightProjection, forwardProjection));
        }

        public static float ComputeDeltaAngleDeg(float angleDeg, float referenceDeg)
        {
            float deltaRad = math.atan2(
                math.sin(math.radians(angleDeg - referenceDeg)),
                math.cos(math.radians(angleDeg - referenceDeg)));
            return math.degrees(deltaRad);
        }

        public static float GetHazardRingInner(in PlayerCleanupActionProfileBufferElement profile)
        {
            float halfWidth = math.max(0f, profile.HazardRingWidth * 0.5f);
            return math.max(0f, profile.HazardRingRadius - halfWidth);
        }

        public static float GetHazardRingOuter(in PlayerCleanupActionProfileBufferElement profile)
        {
            float halfWidth = math.max(0f, profile.HazardRingWidth * 0.5f);
            float inner = math.max(0f, profile.HazardRingRadius - halfWidth);
            return math.max(inner, profile.HazardRingRadius + halfWidth);
        }
    }
}
