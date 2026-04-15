using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [Serializable]
    public struct HazardEmitterPatternSlotAuthoring
    {
        [Min(1)] public int PatternSlotId;
        public HazardEmitterTelegraphProfileSO TelegraphProfile;
        public HazardEmitterEmissionProfileSO EmissionProfile;
        [Min(0f)] public float BaseWeight;
        public uint AvailabilityFlags;
    }

    public readonly struct ResolvedHazardEmitterPatternSlotAuthoring
    {
        public readonly HazardEmitterPatternSlotBuffer Metadata;
        public readonly HazardEmitterPatternExecutionSlotBuffer Execution;

        public ResolvedHazardEmitterPatternSlotAuthoring(
            in HazardEmitterPatternSlotBuffer metadata,
            in HazardEmitterPatternExecutionSlotBuffer execution)
        {
            Metadata = metadata;
            Execution = execution;
        }
    }

    public static class HazardEmitterPatternSlotAuthoringUtility
    {
        public static bool TryResolveSlots(
            HazardEmitterPatternSlotAuthoring[] authoringSlots,
            out ResolvedHazardEmitterPatternSlotAuthoring[] resolvedSlots,
            out string error)
        {
            resolvedSlots = Array.Empty<ResolvedHazardEmitterPatternSlotAuthoring>();
            error = string.Empty;

            if (authoringSlots == null || authoringSlots.Length <= 0)
            {
                error = "HazardEmitterAuthoring requires at least one pattern slot.";
                return false;
            }

            var collected = new List<ResolvedHazardEmitterPatternSlotAuthoring>(authoringSlots.Length);
            var usedSlotIds = new HashSet<int>();

            for (int i = 0; i < authoringSlots.Length; i++)
            {
                var slot = authoringSlots[i];
                if (slot.PatternSlotId < 1)
                {
                    error = $"HazardEmitter pattern slot requires PatternSlotId >= 1. index={i}, current={slot.PatternSlotId}.";
                    return false;
                }

                if (!usedSlotIds.Add(slot.PatternSlotId))
                {
                    error = $"HazardEmitter pattern slot contains duplicate PatternSlotId {slot.PatternSlotId}.";
                    return false;
                }

                if (slot.TelegraphProfile == null)
                {
                    error = $"HazardEmitter pattern slot is missing TelegraphProfile. slotId={slot.PatternSlotId}.";
                    return false;
                }

                if (slot.EmissionProfile == null)
                {
                    error = $"HazardEmitter pattern slot is missing EmissionProfile. slotId={slot.PatternSlotId}.";
                    return false;
                }

                if (slot.BaseWeight < 0f)
                {
                    error = $"HazardEmitter pattern slot requires BaseWeight >= 0. slotId={slot.PatternSlotId}, current={slot.BaseWeight}.";
                    return false;
                }

                if (!HazardEmitterProfileResolver.TryResolve(slot.TelegraphProfile, out var resolvedTelegraph))
                {
                    error = $"HazardEmitter pattern slot failed to resolve TelegraphProfile. slotId={slot.PatternSlotId}.";
                    return false;
                }

                if (!HazardEmitterProfileResolver.TryResolve(slot.EmissionProfile, out var resolvedEmission, out error))
                {
                    error = $"HazardEmitter pattern slot failed to resolve EmissionProfile. slotId={slot.PatternSlotId}. {error}";
                    return false;
                }

                var metadata = new HazardEmitterPatternSlotBuffer
                {
                    PatternSlotId = slot.PatternSlotId,
                    TelegraphProfileRefId = resolvedTelegraph.ProfileId,
                    EmissionProfileRefId = slot.EmissionProfile.GetInstanceID(),
                    BaseWeight = math.max(0f, slot.BaseWeight),
                    AvailabilityFlags = slot.AvailabilityFlags,
                };
                var execution = CreateExecutionSlot(
                    slot.PatternSlotId,
                    slot.EmissionProfile.GetInstanceID(),
                    resolvedTelegraph,
                    resolvedEmission);
                collected.Add(new ResolvedHazardEmitterPatternSlotAuthoring(in metadata, in execution));
            }

            collected.Sort(static (a, b) => a.Metadata.PatternSlotId.CompareTo(b.Metadata.PatternSlotId));
            resolvedSlots = collected.ToArray();
            return true;
        }

        public static void ApplyResolvedSlotsToBuffers(
            ResolvedHazardEmitterPatternSlotAuthoring[] resolvedSlots,
            ref DynamicBuffer<HazardEmitterPatternSlotBuffer> metadataSlots,
            ref DynamicBuffer<HazardEmitterPatternExecutionSlotBuffer> executionSlots)
        {
            metadataSlots.Clear();
            executionSlots.Clear();

            for (int i = 0; i < resolvedSlots.Length; i++)
            {
                metadataSlots.Add(resolvedSlots[i].Metadata);
                executionSlots.Add(resolvedSlots[i].Execution);
            }
        }

        public static HazardEmitterTelegraphProfileBaselineComponent CreateBaselineTelegraph(
            ResolvedHazardEmitterPatternSlotAuthoring[] resolvedSlots)
        {
            var first = resolvedSlots[0].Execution;
            return new HazardEmitterTelegraphProfileBaselineComponent
            {
                ProfileId = first.TelegraphProfileRefId,
                TelegraphDurationSec = first.TelegraphDurationSec,
            };
        }

        public static HazardEmitterEmissionProfileBaselineComponent CreateBaselineEmission(
            ResolvedHazardEmitterPatternSlotAuthoring[] resolvedSlots)
        {
            var first = resolvedSlots[0].Execution;
            return new HazardEmitterEmissionProfileBaselineComponent
            {
                ProfileId = first.EmissionProfileRefId,
                BulletTypeKey = first.BulletTypeKey,
                PositionPatternMode = first.PositionPatternMode,
                SpawnOffset = first.SpawnOffset,
                LineStart = first.LineStart,
                LineEnd = first.LineEnd,
                SampleSpacing = first.SampleSpacing,
                PointSetCount = first.PointSetCount,
                Point0 = first.Point0,
                Point1 = first.Point1,
                Point2 = first.Point2,
                Point3 = first.Point3,
                AimMode = first.AimMode,
                AimSnapshotTiming = first.AimSnapshotTiming,
                BaseAngleDeg = first.BaseAngleDeg,
                AimAngleOffsetDeg = first.AimAngleOffsetDeg,
                LineNormalSide = first.LineNormalSide,
                LineNormalAngleOffsetDeg = first.LineNormalAngleOffsetDeg,
                SpiralStepDeg = first.SpiralStepDeg,
                ShotPatternMode = first.ShotPatternMode,
                ShotCount = first.ShotCount,
                NWayAngleSpacingDeg = first.NWayAngleSpacingDeg,
                EventShotSchedule = first.EventShotSchedule,
                EventShotIntervalSec = first.EventShotIntervalSec,
                EventRepeatCount = first.EventRepeatCount,
                CooldownSec = first.CooldownSec,
            };
        }

        public static HazardEmitterPatternExecutionSlotBuffer CreateExecutionSlot(
            int patternSlotId,
            int emissionProfileRefId,
            in ResolvedHazardEmitterTelegraphProfileSnapshot telegraph,
            in ResolvedHazardEmitterEmissionProfileSnapshot emission)
        {
            return new HazardEmitterPatternExecutionSlotBuffer
            {
                PatternSlotId = patternSlotId,
                TelegraphProfileRefId = telegraph.ProfileId,
                EmissionProfileRefId = emissionProfileRefId,
                TelegraphDurationSec = telegraph.TelegraphDurationSec,
                BulletTypeKey = emission.Bullet != null ? emission.Bullet.DefinitionId : 0,
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
