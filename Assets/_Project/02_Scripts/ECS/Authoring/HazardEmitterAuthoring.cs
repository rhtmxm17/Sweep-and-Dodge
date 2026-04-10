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

        [Header("Pattern Slots")]
        public HazardEmitterPatternSlotAuthoring[] Slots =
        {
            new HazardEmitterPatternSlotAuthoring
            {
                PatternSlotId = HazardEmitterPatternSetCompatibilityUtility.CompatibilityPatternSlotId,
                BaseWeight = HazardEmitterPatternSetCompatibilityUtility.CompatibilityBaseWeight,
                AvailabilityFlags = HazardEmitterPatternSetCompatibilityUtility.CompatibilityAvailabilityFlags,
            }
        };

        private sealed class Baker : Baker<HazardEmitterAuthoring>
        {
            public override void Bake(HazardEmitterAuthoring authoring)
            {
                if (!HazardEmitterAuthoringValidationUtility.TryValidate(authoring, out var actorAuthoring, out _, out var error))
                {
                    Debug.LogError($"[HazardEmitterAuthoring] {error}", authoring);
                    return;
                }

                var emitterEntity = GetEntity(TransformUsageFlags.Dynamic);
                var actorEntity = GetEntity(actorAuthoring.gameObject, TransformUsageFlags.Dynamic);
                if (!HazardEmitterPatternSlotAuthoringUtility.TryResolveSlots(authoring.Slots, out var resolvedSlots, out error))
                {
                    Debug.LogError($"[HazardEmitterAuthoring] {error}", authoring);
                    return;
                }

                byte isEnabled = authoring.IsEnabled ? (byte)1 : (byte)0;
                byte isSuppressed = authoring.StartSuppressed ? (byte)1 : (byte)0;
                int emitterId = math.max(1, authoring.EmitterId);
                var firstSlot = resolvedSlots[0];
                var baselineTelegraph = HazardEmitterPatternSlotAuthoringUtility.CreateBaselineTelegraph(resolvedSlots);
                var baselineEmission = HazardEmitterPatternSlotAuthoringUtility.CreateBaselineEmission(resolvedSlots);

                var baselineConfig = new HazardEmitterAppliedConfigBaselineComponent
                {
                    IsEnabled = isEnabled,
                    IsSuppressed = isSuppressed,
                    LocalOffset = authoring.LocalOffset,
                    TelegraphProfileRefId = firstSlot.Metadata.TelegraphProfileRefId,
                    EmissionProfileRefId = firstSlot.Metadata.EmissionProfileRefId,
                };

                AddComponent(emitterEntity, new HazardEmitterComponent
                {
                    EmitterId = emitterId,
                    ActorEntity = actorEntity,
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
                AddComponent(emitterEntity, new HazardEmitterSelectedPatternRuntimeComponent
                {
                    AppliedPatternSlotId = HazardEmitterPatternSetCompatibilityUtility.InvalidPatternSlotId,
                });
                AddComponent(emitterEntity, new HazardEmitterCycleSignalComponent
                {
                    CompletedVersion = 0u,
                });
                AddBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
                AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
                var patternSlots = SetBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
                var executionSlots = SetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
                HazardEmitterPatternSlotAuthoringUtility.ApplyResolvedSlotsToBuffers(
                    resolvedSlots,
                    ref patternSlots,
                    ref executionSlots);
            }
        }
    }
}
