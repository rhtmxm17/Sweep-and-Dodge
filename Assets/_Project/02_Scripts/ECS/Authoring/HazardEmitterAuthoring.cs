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

                AddComponent(emitterEntity, new HazardEmitterComponent
                {
                    EmitterId = math.max(1, authoring.EmitterId),
                    SourceEntity = sourceEntity,
                    ActivationPolicy = authoring.ActivationPolicy,
                    InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    AnchorKind = authoring.AnchorKind,
                    Mobility = authoring.Mobility,
                    IsEnabled = authoring.IsEnabled ? (byte)1 : (byte)0,
                    IsSuppressed = authoring.StartSuppressed ? (byte)1 : (byte)0,
                    LocalOffset = authoring.LocalOffset,
                    TelegraphProfileRefId = authoring.TelegraphProfile != null ? authoring.TelegraphProfile.GetInstanceID() : 0,
                    EmissionProfileRefId = authoring.EmissionProfile != null ? authoring.EmissionProfile.GetInstanceID() : 0,
                });
                AddComponent(emitterEntity, new HazardEmitterTelegraphProfileComponent
                {
                    ProfileId = authoring.TelegraphProfile != null ? authoring.TelegraphProfile.GetInstanceID() : 0,
                    TelegraphDurationSec = math.max(0f, authoring.TelegraphProfile.TelegraphDurationSec),
                });
                AddComponent(emitterEntity, new HazardEmitterEmissionProfileComponent
                {
                    ProfileId = authoring.EmissionProfile != null ? authoring.EmissionProfile.GetInstanceID() : 0,
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
                });
                AddComponent(emitterEntity, new HazardEmitterRuntimeStateComponent
                {
                    LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                    StateElapsedSec = 0f,
                });
            }
        }
    }
}
