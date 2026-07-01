using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public struct ResolvedEmissionCore
    {
        public int ProfileRefId;
        public BulletDefinitionSO Bullet;
        public int BulletTypeKey;

        public bool HasSpeedOverride;
        public float SpeedOverride;
        public bool HasLifetimeOverride;
        public float LifetimeOverride;

        public bool HasMovementOverride;
        public BulletMovementFamilyId MovementFamily;
        public BulletDampedLinearDefinition DampedLinear;
        public BulletHomingLiteDefinition HomingLite;

        public WavePositionPatternModeId PositionPatternMode;
        public Vector2 SpawnOffset;
        public Vector2 LineStart;
        public Vector2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public Vector2 Point0;
        public Vector2 Point1;
        public Vector2 Point2;
        public Vector2 Point3;

        public WaveAimModeId AimMode;
        public WaveAimSnapshotTimingId AimSnapshotTiming;
        public float BaseAngleDeg;
        public float AimAngleOffsetDeg;
        public WaveLineNormalSideId LineNormalSide;
        public float LineNormalAngleOffsetDeg;
        public float SpiralStepDeg;

        public WaveShotPatternModeId ShotPatternMode;
        public int ShotCount;
        public float NWayAngleSpacingDeg;

        public bool HasMotionCompletedTrigger;
        public EmissionProfileSO MotionCompletedTargetProfile;
        public int MotionCompletedTargetProfileRefId;
        public EmissionTriggerOriginBindingId MotionCompletedOriginPosition;
        public EmissionTriggerDirectionBindingId MotionCompletedForwardDirection;
        public EmissionTriggerSourceBindingId MotionCompletedSourceEntity;
        public EmissionTriggerCauserBindingId MotionCompletedCauserEntity;
        public float MotionCompletedDelaySec;
    }

    public static class EmissionProfileResolver
    {
        public static bool TryResolve(
            EmissionProfileSO profile,
            out ResolvedEmissionCore core,
            out string error)
        {
            core = default;
            error = string.Empty;

            if (profile == null)
            {
                error = "Emission profile is null.";
                return false;
            }

            if (!TryResolveInline(
                    profile.Bullet,
                    profile.PositionPattern,
                    profile.Aim,
                    profile.ShotPattern,
                    profile.GetInstanceID(),
                    out core,
                    out error))
            {
                error = $"Emission profile '{profile.name}' is invalid. {error}";
                return false;
            }

            ApplySpawnTuning(ref core, profile.SpawnTuning);
            ApplyMovementTuning(ref core, profile.MovementTuning);
            ApplyLifecycleTriggers(ref core, profile.LifecycleTriggers);
            return true;
        }

        public static bool TryResolveInline(
            BulletDefinitionSO bullet,
            WavePositionPatternAuthoringBase positionPattern,
            WaveAimAuthoringBase aim,
            WaveShotPatternAuthoringBase shotPattern,
            int profileRefId,
            out ResolvedEmissionCore core,
            out string error,
            bool requirePositiveDefinitionId = true)
        {
            core = CreateDefaultCore(profileRefId);
            error = string.Empty;

            if (bullet == null)
            {
                error = "Emission core is missing Bullet.";
                return false;
            }

            if (requirePositiveDefinitionId && bullet.DefinitionId <= 0)
            {
                error = $"Emission core references invalid DefinitionId {bullet.DefinitionId}.";
                return false;
            }

            if (positionPattern == null)
            {
                error = "Emission core is missing PositionPattern.";
                return false;
            }

            if (aim == null)
            {
                error = "Emission core is missing Aim.";
                return false;
            }

            if (shotPattern == null)
            {
                error = "Emission core is missing ShotPattern.";
                return false;
            }

            core.Bullet = bullet;
            core.BulletTypeKey = bullet.DefinitionId;
            ApplyPositionPattern(ref core, positionPattern);
            ApplyAim(ref core, aim);
            ApplyShotPattern(ref core, shotPattern);
            return true;
        }

        private static ResolvedEmissionCore CreateDefaultCore(int profileRefId)
        {
            return new ResolvedEmissionCore
            {
                ProfileRefId = profileRefId,
                Bullet = null,
                BulletTypeKey = 0,
                HasSpeedOverride = false,
                SpeedOverride = 0f,
                HasLifetimeOverride = false,
                LifetimeOverride = 0f,
                HasMovementOverride = false,
                MovementFamily = BulletMovementFamilyId.Linear,
                DampedLinear = default,
                HomingLite = default,
                PositionPatternMode = WavePositionPatternModeId.SinglePoint,
                SpawnOffset = Vector2.zero,
                LineStart = Vector2.zero,
                LineEnd = Vector2.zero,
                SampleSpacing = 1f,
                PointSetCount = 0,
                Point0 = Vector2.zero,
                Point1 = Vector2.zero,
                Point2 = Vector2.zero,
                Point3 = Vector2.zero,
                AimMode = WaveAimModeId.Random,
                AimSnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                BaseAngleDeg = 0f,
                AimAngleOffsetDeg = 0f,
                LineNormalSide = WaveLineNormalSideId.Left,
                LineNormalAngleOffsetDeg = 0f,
                SpiralStepDeg = 0f,
                ShotPatternMode = WaveShotPatternModeId.Single,
                ShotCount = 1,
                NWayAngleSpacingDeg = 0f,
                HasMotionCompletedTrigger = false,
                MotionCompletedTargetProfile = null,
                MotionCompletedTargetProfileRefId = 0,
                MotionCompletedOriginPosition = EmissionTriggerOriginBindingId.LifecycleContactPosition,
                MotionCompletedForwardDirection = EmissionTriggerDirectionBindingId.LifecycleContactDirection,
                MotionCompletedSourceEntity = EmissionTriggerSourceBindingId.CauserSourceEntity,
                MotionCompletedCauserEntity = EmissionTriggerCauserBindingId.CompletedBullet,
                MotionCompletedDelaySec = 0f,
            };
        }

        private static void ApplySpawnTuning(ref ResolvedEmissionCore core, EmissionSpawnTuningAuthoring spawnTuning)
        {
            if (spawnTuning == null)
                return;

            core.HasSpeedOverride = spawnTuning.OverrideSpeed;
            core.SpeedOverride = Mathf.Max(0.001f, spawnTuning.SpeedOverride);
            core.HasLifetimeOverride = spawnTuning.OverrideLifetime;
            core.LifetimeOverride = Mathf.Max(0.001f, spawnTuning.LifetimeOverride);
        }

        private static void ApplyMovementTuning(ref ResolvedEmissionCore core, EmissionMovementTuningAuthoring movementTuning)
        {
            if (movementTuning == null)
                return;

            core.HasMovementOverride = movementTuning.OverrideMovement;
            core.MovementFamily = movementTuning.Family;
            core.DampedLinear = movementTuning.DampedLinear;
            core.HomingLite = movementTuning.HomingLite;
        }

        private static void ApplyLifecycleTriggers(ref ResolvedEmissionCore core, EmissionLifecycleTriggersAuthoring lifecycleTriggers)
        {
            var motionCompleted = lifecycleTriggers?.MotionCompleted;
            if (motionCompleted == null || !motionCompleted.Enabled)
                return;

            core.HasMotionCompletedTrigger = true;
            core.MotionCompletedTargetProfile = motionCompleted.TargetProfile;
            core.MotionCompletedTargetProfileRefId = motionCompleted.TargetProfile != null
                ? motionCompleted.TargetProfile.GetInstanceID()
                : 0;
            core.MotionCompletedOriginPosition = motionCompleted.OriginPosition;
            core.MotionCompletedForwardDirection = motionCompleted.ForwardDirection;
            core.MotionCompletedSourceEntity = motionCompleted.SourceEntity;
            core.MotionCompletedCauserEntity = motionCompleted.CauserEntity;
            core.MotionCompletedDelaySec = Mathf.Max(0f, motionCompleted.DelaySec);
        }

        private static void ApplyPositionPattern(
            ref ResolvedEmissionCore core,
            WavePositionPatternAuthoringBase positionPattern)
        {
            core.PositionPatternMode = positionPattern.PositionPatternMode;

            switch (positionPattern)
            {
                case LineEvenPositionPatternAuthoring lineEven:
                    core.LineStart = lineEven.LineStart;
                    core.LineEnd = lineEven.LineEnd;
                    core.SampleSpacing = lineEven.SampleSpacing > 0f ? lineEven.SampleSpacing : 1f;
                    break;

                case PointSetPositionPatternAuthoring pointSet:
                    int pointCount = Mathf.Clamp(pointSet.Points?.Length ?? 0, 0, PointSetPositionPatternAuthoring.MaxPointCount);
                    core.PointSetCount = pointCount;
                    core.Point0 = GetPoint(pointSet.Points, 0);
                    core.Point1 = GetPoint(pointSet.Points, 1);
                    core.Point2 = GetPoint(pointSet.Points, 2);
                    core.Point3 = GetPoint(pointSet.Points, 3);
                    break;
            }
        }

        private static void ApplyAim(ref ResolvedEmissionCore core, WaveAimAuthoringBase aim)
        {
            core.AimMode = aim.AimMode;

            switch (aim)
            {
                case FixedAimAuthoring fixedAim:
                    core.BaseAngleDeg = fixedAim.BaseAngleDeg;
                    break;

                case SpiralAimAuthoring spiralAim:
                    core.BaseAngleDeg = spiralAim.BaseAngleDeg;
                    core.SpiralStepDeg = spiralAim.SpiralStepDeg;
                    break;

                case PlayerPositionAimAuthoring playerAim:
                    core.AimAngleOffsetDeg = playerAim.AngleOffsetDeg;
                    core.AimSnapshotTiming = playerAim.SnapshotTiming;
                    break;

                case LineNormalAimAuthoring lineNormalAim:
                    core.LineNormalSide = lineNormalAim.NormalSide;
                    core.LineNormalAngleOffsetDeg = lineNormalAim.AngleOffsetDeg;
                    break;
            }
        }

        private static void ApplyShotPattern(ref ResolvedEmissionCore core, WaveShotPatternAuthoringBase shotPattern)
        {
            core.ShotPatternMode = shotPattern.ShotPatternMode;

            switch (shotPattern)
            {
                case NWayShotPatternAuthoring nWay:
                    core.ShotCount = nWay.ShotCount > 0 ? nWay.ShotCount : 1;
                    core.NWayAngleSpacingDeg = nWay.AngleSpacingDeg;
                    break;

                case RadialShotPatternAuthoring radial:
                    core.ShotCount = radial.ShotCount > 0 ? radial.ShotCount : 1;
                    core.NWayAngleSpacingDeg = 0f;
                    break;

                default:
                    core.ShotCount = 1;
                    core.NWayAngleSpacingDeg = 0f;
                    break;
            }
        }

        private static Vector2 GetPoint(Vector2[] points, int index)
        {
            if (points == null || index < 0 || index >= points.Length)
                return Vector2.zero;

            return points[index];
        }
    }
}
