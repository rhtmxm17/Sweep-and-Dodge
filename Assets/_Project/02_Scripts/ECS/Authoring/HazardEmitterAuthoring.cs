using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class HazardEmitterAuthoring : MonoBehaviour
    {
        [Header("Identity")]
        [Min(1)] public int EmitterId = 1;

        [Header("Contract")]
        public HazardEmitterActivationPolicyId ActivationPolicy = HazardEmitterActivationPolicyId.AlwaysCycle;
        public HazardEmitterAnchorKindId AnchorKind = HazardEmitterAnchorKindId.ObjectBound;
        public HazardEmitterMobilityId Mobility = HazardEmitterMobilityId.Static;
        public bool IsEnabled = true;
        public bool StartSuppressed = false;
        public Vector3 LocalOffset = Vector3.zero;

        [Header("Profiles")]
        public HazardEmitterTelegraphProfileSO TelegraphProfile;
        public HazardEmitterEmissionProfileSO EmissionProfile;

        private sealed class Baker : Baker<HazardEmitterAuthoring>
        {
            public override void Bake(HazardEmitterAuthoring authoring)
            {
                if (!HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var sourceAuthoring, out var error))
                {
                    Debug.LogError($"[HazardEmitterAuthoring] {error}", authoring);
                    return;
                }

                if (!HazardEmitterProfileResolver.TryResolve(authoring.EmissionProfile, out var resolvedEmission, out error))
                {
                    Debug.LogError($"[HazardEmitterAuthoring] {error}", authoring);
                    return;
                }

                var emitterEntity = GetEntity(TransformUsageFlags.Dynamic);
                var sourceEntity = GetEntity(sourceAuthoring.gameObject, TransformUsageFlags.Dynamic);

                int telegraphProfileRefId = authoring.TelegraphProfile != null ? authoring.TelegraphProfile.GetInstanceID() : 0;
                int emissionProfileRefId = authoring.EmissionProfile != null ? authoring.EmissionProfile.GetInstanceID() : 0;
                byte isEnabled = authoring.IsEnabled ? (byte)1 : (byte)0;
                byte isSuppressed = authoring.StartSuppressed ? (byte)1 : (byte)0;
                int emitterId = math.max(1, authoring.EmitterId);

                var baselineConfig = new HazardEmitterAppliedConfigBaselineComponent
                {
                    IsEnabled = isEnabled,
                    IsSuppressed = isSuppressed,
                    LocalOffset = authoring.LocalOffset,
                    TelegraphProfileRefId = telegraphProfileRefId,
                    EmissionProfileRefId = emissionProfileRefId,
                };
                var baselineTelegraph = new HazardEmitterTelegraphProfileBaselineComponent
                {
                    ProfileId = telegraphProfileRefId,
                    TelegraphDurationSec = math.max(0f, authoring.TelegraphProfile.TelegraphDurationSec),
                };
                var baselineEmission = new HazardEmitterEmissionProfileBaselineComponent
                {
                    ProfileId = emissionProfileRefId,
                    BulletTypeKey = resolvedEmission.Bullet.DefinitionId,
                    PositionPatternMode = resolvedEmission.PositionPatternMode,
                    SpawnOffset = resolvedEmission.SpawnOffset,
                    LineStart = resolvedEmission.LineStart,
                    LineEnd = resolvedEmission.LineEnd,
                    SampleSpacing = resolvedEmission.SampleSpacing,
                    PointSetCount = resolvedEmission.PointSetCount,
                    Point0 = resolvedEmission.Point0,
                    Point1 = resolvedEmission.Point1,
                    Point2 = resolvedEmission.Point2,
                    Point3 = resolvedEmission.Point3,
                    AimMode = resolvedEmission.AimMode,
                    AimSnapshotTiming = resolvedEmission.AimSnapshotTiming,
                    BaseAngleDeg = resolvedEmission.BaseAngleDeg,
                    AimAngleOffsetDeg = resolvedEmission.AimAngleOffsetDeg,
                    LineNormalSide = resolvedEmission.LineNormalSide,
                    LineNormalAngleOffsetDeg = resolvedEmission.LineNormalAngleOffsetDeg,
                    SpiralStepDeg = resolvedEmission.SpiralStepDeg,
                    ShotPatternMode = resolvedEmission.ShotPatternMode,
                    ShotCount = resolvedEmission.ShotCount,
                    NWayAngleSpacingDeg = resolvedEmission.NWayAngleSpacingDeg,
                    EventShotSchedule = resolvedEmission.EventShotSchedule,
                    EventShotIntervalSec = resolvedEmission.EventShotIntervalSec,
                    EventRepeatCount = resolvedEmission.EventRepeatCount,
                    CooldownSec = resolvedEmission.CooldownSec,
                };

                AddComponent(emitterEntity, new HazardEmitterComponent
                {
                    EmitterId = emitterId,
                    SourceEntity = sourceEntity,
                    ActivationPolicy = authoring.ActivationPolicy,
                    InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    AnchorKind = authoring.AnchorKind,
                    Mobility = authoring.Mobility,
                });
                AddComponent(emitterEntity, baselineConfig);
                AddComponent(emitterEntity, new HazardEmitterAppliedConfigComponent
                {
                    IsEnabled = baselineConfig.IsEnabled,
                    IsSuppressed = baselineConfig.IsSuppressed,
                    LocalOffset = baselineConfig.LocalOffset,
                    TelegraphProfileRefId = baselineConfig.TelegraphProfileRefId,
                    EmissionProfileRefId = baselineConfig.EmissionProfileRefId,
                });
                AddComponent(emitterEntity, baselineTelegraph);
                AddComponent(emitterEntity, new HazardEmitterTelegraphProfileComponent
                {
                    ProfileId = baselineTelegraph.ProfileId,
                    TelegraphDurationSec = baselineTelegraph.TelegraphDurationSec,
                });
                AddComponent(emitterEntity, baselineEmission);
                AddComponent(emitterEntity, new HazardEmitterEmissionProfileComponent
                {
                    ProfileId = baselineEmission.ProfileId,
                    BulletTypeKey = baselineEmission.BulletTypeKey,
                    PositionPatternMode = baselineEmission.PositionPatternMode,
                    SpawnOffset = baselineEmission.SpawnOffset,
                    LineStart = baselineEmission.LineStart,
                    LineEnd = baselineEmission.LineEnd,
                    SampleSpacing = baselineEmission.SampleSpacing,
                    PointSetCount = baselineEmission.PointSetCount,
                    Point0 = baselineEmission.Point0,
                    Point1 = baselineEmission.Point1,
                    Point2 = baselineEmission.Point2,
                    Point3 = baselineEmission.Point3,
                    AimMode = baselineEmission.AimMode,
                    AimSnapshotTiming = baselineEmission.AimSnapshotTiming,
                    BaseAngleDeg = baselineEmission.BaseAngleDeg,
                    AimAngleOffsetDeg = baselineEmission.AimAngleOffsetDeg,
                    LineNormalSide = baselineEmission.LineNormalSide,
                    LineNormalAngleOffsetDeg = baselineEmission.LineNormalAngleOffsetDeg,
                    SpiralStepDeg = baselineEmission.SpiralStepDeg,
                    ShotPatternMode = baselineEmission.ShotPatternMode,
                    ShotCount = baselineEmission.ShotCount,
                    NWayAngleSpacingDeg = baselineEmission.NWayAngleSpacingDeg,
                    EventShotSchedule = baselineEmission.EventShotSchedule,
                    EventShotIntervalSec = baselineEmission.EventShotIntervalSec,
                    EventRepeatCount = baselineEmission.EventRepeatCount,
                    CooldownSec = baselineEmission.CooldownSec,
                });
                AddComponent(emitterEntity, new HazardEmitterRuntimeStateComponent
                {
                    LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    StateElapsedSec = 0f,
                });
                AppendToBuffer(sourceEntity, new SourceHazardEmitterRefBuffer
                {
                    EmitterEntity = emitterEntity,
                    EmitterId = emitterId,
                });
            }
        }
    }
}
