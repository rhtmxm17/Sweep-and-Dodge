using Unity.Entities;
using Unity.Mathematics;
using System;

namespace SweepNDodge.DotsBullets
{
    public enum HazardEmitterActivationPolicyId : byte
    {
        AlwaysCycle = 0,
        ProgressReactive = 1,
        TriggerReactive = 2,
        RotatingSet = 3,
    }

    public enum HazardEmitterLifecycleStateId : byte
    {
        Dormant = 0,
        Telegraph = 1,
        Emit = 2,
        Cooldown = 3,
    }

    public enum HazardEmitterAnchorKindId : byte
    {
        ObjectBound = 0,
        PointBound = 1,
    }

    public enum HazardEmitterMobilityId : byte
    {
        Static = 0,
        Dynamic = 1,
    }

    public struct HazardEmitterComponent : IComponentData
    {
        public int EmitterId;
        public Entity ActorEntity;
        public HazardEmitterActivationPolicyId ActivationPolicy;
        public HazardEmitterLifecycleStateId InitialLifecycleState;
        public HazardEmitterAnchorKindId AnchorKind;
        public HazardEmitterMobilityId Mobility;
    }

    public struct HazardEmitterAppliedConfigBaselineComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
        public float3 LocalOffset;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
    }

    public struct HazardEmitterAppliedConfigComponent : IComponentData
    {
        public byte IsEnabled;
        public byte IsSuppressed;
        public float3 LocalOffset;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
    }

    public struct HazardEmitterTelegraphProfileBaselineComponent : IComponentData
    {
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct HazardEmitterTelegraphProfileComponent : IComponentData
    {
        public int ProfileId;
        public float TelegraphDurationSec;
    }

    public struct HazardEmitterEmissionProfileBaselineComponent : IComponentData
    {
        public int ProfileId;
        public int BulletTypeKey;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

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

        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public struct HazardEmitterEmissionProfileComponent : IComponentData
    {
        public int ProfileId;
        public int BulletTypeKey;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

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

        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public struct HazardEmitterRuntimeStateComponent : IComponentData
    {
        public HazardEmitterLifecycleStateId LifecycleState;
        public float StateElapsedSec;
    }

    [InternalBufferCapacity(1)]
    public struct HazardEmitterPatternSlotBuffer : IBufferElementData
    {
        public int PatternSlotId;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
        public float BaseWeight;
        public uint AvailabilityFlags;
    }

    [InternalBufferCapacity(1)]
    public struct HazardEmitterPatternExecutionSlotBuffer : IBufferElementData
    {
        public int PatternSlotId;
        public int TelegraphProfileRefId;
        public int EmissionProfileRefId;
        public float TelegraphDurationSec;

        public int BulletTypeKey;

        public WavePositionPatternModeId PositionPatternMode;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int PointSetCount;
        public float2 Point0;
        public float2 Point1;
        public float2 Point2;
        public float2 Point3;

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

        public SourceSpawnEventShotScheduleId EventShotSchedule;
        public float EventShotIntervalSec;
        public int EventRepeatCount;
        public float CooldownSec;
    }

    public struct HazardEmitterSelectedPatternRuntimeComponent : IComponentData
    {
        public int AppliedPatternSlotId;
    }

    public struct HazardEmitterCycleSignalComponent : IComponentData
    {
        public uint CompletedVersion;
    }

    [Flags]
    public enum HazardEmitterSuppressionReasonFlags : uint
    {
        None = 0,
        DisabledByActorConfig = 1u << 0,
        SuppressedByActorConfig = 1u << 1,
        MissingActor = 1u << 2,
        ActorPresenceHidden = 1u << 3,
        ActorPresenceActivating = 1u << 4,
        ActorPresenceRetiring = 1u << 5,
        DisabledByAppliedConfig = 1u << 6,
        SuppressedByAppliedConfig = 1u << 7,
        MissingSource = 1u << 8,
        SourcePressureBlocked = 1u << 9,
        SourceProgressBlocked = 1u << 10,
        PlayerDistanceBlocked = 1u << 11,
        MissingPlayer = 1u << 12,
        GroupSuppressed = 1u << 13,
    }

    public struct HazardEmitterCoordinatorStateComponent : IComponentData
    {
        public byte ActivationAllowed;
        public uint SuppressionReasonMask;
        public float LastPlayerDistanceSq;
    }

    public struct HazardEmitterSourcePressureGateComponent : IComponentData
    {
        public byte Enabled;
        public byte RequirePressureState;
        public float MinPressureOccupancySec;
    }

    public struct HazardEmitterPlayerDistanceGateComponent : IComponentData
    {
        public byte Enabled;
        public float MinDistanceSq;
        public float MaxDistanceSq;
    }

    public struct HazardEmitterSourceProgressGateComponent : IComponentData
    {
        public byte Enabled;
        public float MinProgress01;
        public float MaxProgress01;
    }

    public static class HazardEmitterPatternSetCompatibilityUtility
    {
        public const int InvalidPatternSlotId = -1;
        public const int CompatibilityPatternSlotId = 1;
        public const float CompatibilityBaseWeight = 1f;
        public const uint CompatibilityAvailabilityFlags = 0u;

        public static void SetSingleSlotBuffers(
            ref DynamicBuffer<HazardEmitterPatternSlotBuffer> slots,
            ref DynamicBuffer<HazardEmitterPatternExecutionSlotBuffer> executionSlots,
            int patternSlotId,
            float baseWeight,
            uint availabilityFlags,
            int telegraphProfileRefId,
            float telegraphDurationSec,
            in HazardEmitterEmissionProfileComponent emission)
        {
            slots.Clear();
            slots.Add(new HazardEmitterPatternSlotBuffer
            {
                PatternSlotId = patternSlotId,
                TelegraphProfileRefId = telegraphProfileRefId,
                EmissionProfileRefId = emission.ProfileId,
                BaseWeight = baseWeight,
                AvailabilityFlags = availabilityFlags,
            });
            executionSlots.Clear();
            executionSlots.Add(CreateExecutionSlot(
                patternSlotId,
                telegraphProfileRefId,
                telegraphDurationSec,
                in emission));
        }

        public static void ReseedSingleCompatibilitySlot(
            ref DynamicBuffer<HazardEmitterPatternSlotBuffer> slots,
            ref DynamicBuffer<HazardEmitterPatternExecutionSlotBuffer> executionSlots,
            int telegraphProfileRefId,
            float telegraphDurationSec,
            in HazardEmitterEmissionProfileComponent emission)
        {
            SetSingleSlotBuffers(
                ref slots,
                ref executionSlots,
                CompatibilityPatternSlotId,
                CompatibilityBaseWeight,
                CompatibilityAvailabilityFlags,
                telegraphProfileRefId,
                telegraphDurationSec,
                in emission);
        }

        public static void ReseedSingleCompatibilitySlot(EntityManager em, Entity emitterEntity)
        {
            if (emitterEntity == Entity.Null || !em.Exists(emitterEntity) || !em.HasComponent<HazardEmitterAppliedConfigComponent>(emitterEntity))
                return;

            if (!em.HasBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity))
                em.AddBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
            if (!em.HasBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity))
                em.AddBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);

            float telegraphDurationSec = 0f;
            if (em.HasComponent<HazardEmitterTelegraphProfileComponent>(emitterEntity))
                telegraphDurationSec = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitterEntity).TelegraphDurationSec;

            var slots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
            var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitterEntity);
            ReseedSingleCompatibilitySlot(
                ref slots,
                ref executionSlots,
                emission.ProfileId == 0 && em.HasComponent<HazardEmitterAppliedConfigComponent>(emitterEntity)
                    ? em.GetComponentData<HazardEmitterAppliedConfigComponent>(emitterEntity).TelegraphProfileRefId
                    : em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitterEntity).ProfileId,
                telegraphDurationSec,
                in emission);
        }

        public static bool TryMirrorCurrentProfilesIntoSingleExistingSlot(EntityManager em, Entity emitterEntity)
        {
            if (emitterEntity == Entity.Null
                || !em.Exists(emitterEntity)
                || !em.HasBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity)
                || !em.HasBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity)
                || !em.HasComponent<HazardEmitterTelegraphProfileComponent>(emitterEntity)
                || !em.HasComponent<HazardEmitterEmissionProfileComponent>(emitterEntity))
            {
                return false;
            }

            var slots = em.GetBuffer<HazardEmitterPatternSlotBuffer>(emitterEntity);
            if (slots.Length != 1)
                return false;

            var telegraph = em.GetComponentData<HazardEmitterTelegraphProfileComponent>(emitterEntity);
            var emission = em.GetComponentData<HazardEmitterEmissionProfileComponent>(emitterEntity);
            var existingSlot = slots[0];
            var executionSlots = em.GetBuffer<HazardEmitterPatternExecutionSlotBuffer>(emitterEntity);
            SetSingleSlotBuffers(
                ref slots,
                ref executionSlots,
                existingSlot.PatternSlotId,
                existingSlot.BaseWeight,
                existingSlot.AvailabilityFlags,
                telegraph.ProfileId,
                telegraph.TelegraphDurationSec,
                in emission);
            return true;
        }

        public static void ApplyExecutionSlotToRuntime(
            in HazardEmitterPatternExecutionSlotBuffer slot,
            ref HazardEmitterTelegraphProfileComponent telegraph,
            ref HazardEmitterEmissionProfileComponent emission)
        {
            telegraph.ProfileId = slot.TelegraphProfileRefId;
            telegraph.TelegraphDurationSec = slot.TelegraphDurationSec;

            emission.ProfileId = slot.EmissionProfileRefId;
            emission.BulletTypeKey = slot.BulletTypeKey;
            emission.PositionPatternMode = slot.PositionPatternMode;
            emission.SpawnOffset = slot.SpawnOffset;
            emission.LineStart = slot.LineStart;
            emission.LineEnd = slot.LineEnd;
            emission.SampleSpacing = slot.SampleSpacing;
            emission.PointSetCount = slot.PointSetCount;
            emission.Point0 = slot.Point0;
            emission.Point1 = slot.Point1;
            emission.Point2 = slot.Point2;
            emission.Point3 = slot.Point3;
            emission.AimMode = slot.AimMode;
            emission.AimSnapshotTiming = slot.AimSnapshotTiming;
            emission.BaseAngleDeg = slot.BaseAngleDeg;
            emission.AimAngleOffsetDeg = slot.AimAngleOffsetDeg;
            emission.LineNormalSide = slot.LineNormalSide;
            emission.LineNormalAngleOffsetDeg = slot.LineNormalAngleOffsetDeg;
            emission.SpiralStepDeg = slot.SpiralStepDeg;
            emission.ShotPatternMode = slot.ShotPatternMode;
            emission.ShotCount = slot.ShotCount;
            emission.NWayAngleSpacingDeg = slot.NWayAngleSpacingDeg;
            emission.EventShotSchedule = slot.EventShotSchedule;
            emission.EventShotIntervalSec = slot.EventShotIntervalSec;
            emission.EventRepeatCount = slot.EventRepeatCount;
            emission.CooldownSec = slot.CooldownSec;
        }

        public static HazardEmitterPatternExecutionSlotBuffer CreateExecutionSlot(
            int patternSlotId,
            int telegraphProfileRefId,
            float telegraphDurationSec,
            in HazardEmitterEmissionProfileComponent emission)
        {
            return new HazardEmitterPatternExecutionSlotBuffer
            {
                PatternSlotId = patternSlotId,
                TelegraphProfileRefId = telegraphProfileRefId,
                EmissionProfileRefId = emission.ProfileId,
                TelegraphDurationSec = telegraphDurationSec,
                BulletTypeKey = emission.BulletTypeKey,
                PositionPatternMode = emission.PositionPatternMode,
                SpawnOffset = emission.SpawnOffset,
                LineStart = emission.LineStart,
                LineEnd = emission.LineEnd,
                SampleSpacing = emission.SampleSpacing,
                PointSetCount = emission.PointSetCount,
                Point0 = emission.Point0,
                Point1 = emission.Point1,
                Point2 = emission.Point2,
                Point3 = emission.Point3,
                AimMode = emission.AimMode,
                AimSnapshotTiming = emission.AimSnapshotTiming,
                BaseAngleDeg = emission.BaseAngleDeg,
                AimAngleOffsetDeg = emission.AimAngleOffsetDeg,
                LineNormalSide = emission.LineNormalSide,
                LineNormalAngleOffsetDeg = emission.LineNormalAngleOffsetDeg,
                SpiralStepDeg = emission.SpiralStepDeg,
                ShotPatternMode = emission.ShotPatternMode,
                ShotCount = emission.ShotCount,
                NWayAngleSpacingDeg = emission.NWayAngleSpacingDeg,
                EventShotSchedule = emission.EventShotSchedule,
                EventShotIntervalSec = emission.EventShotIntervalSec,
                EventRepeatCount = emission.EventRepeatCount,
                CooldownSec = emission.CooldownSec,
            };
        }
    }

}
