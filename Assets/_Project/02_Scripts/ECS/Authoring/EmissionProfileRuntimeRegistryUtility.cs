using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public static class EmissionProfileRuntimeRegistryUtility
    {
        public static void RebuildFromStageDefinition(
            EntityManager em,
            StageDefinitionSO definition,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry)
        {
            registry.Clear();
            if (definition == null || definition.SourceBindings == null)
                return;

            var visited = new HashSet<int>();
            for (int i = 0; i < definition.SourceBindings.Length; i++)
            {
                var binding = definition.SourceBindings[i];
                CollectSourceProfiles(binding.SustainSlots, registry, visited);
                CollectEventProfiles(binding.EventSlots, registry, visited);
                CollectHazardProfiles(binding.HazardActorPlacements, registry, visited);
            }
        }

        public static bool TryFind(
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            int profileRefId,
            out EmissionProfileRuntimeRegistryBuffer entry)
        {
            for (int i = 0; i < registry.Length; i++)
            {
                var candidate = registry[i];
                if (candidate.ProfileRefId != profileRefId)
                    continue;

                entry = candidate;
                return true;
            }

            entry = default;
            return false;
        }

        private static void CollectSourceProfiles(
            SustainSlotBinding[] slots,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            HashSet<int> visited)
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                var clips = slots[i].Clips;
                if (clips == null)
                    continue;

                for (int c = 0; c < clips.Length; c++)
                    CollectWaveClipProfiles(clips[c], registry, visited);
            }
        }

        private static void CollectEventProfiles(
            EventSlotBinding[] slots,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            HashSet<int> visited)
        {
            if (slots == null)
                return;

            for (int i = 0; i < slots.Length; i++)
            {
                var clips = slots[i].EventClips;
                if (clips == null)
                    continue;

                for (int c = 0; c < clips.Length; c++)
                    CollectWaveClipProfiles(clips[c], registry, visited);
            }
        }

        private static void CollectWaveClipProfiles(
            WaveClipSO clip,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            HashSet<int> visited)
        {
            if (clip == null || clip.Segments == null)
                return;

            for (int s = 0; s < clip.Segments.Length; s++)
            {
                var directives = clip.Segments[s].Directives;
                if (directives == null)
                    continue;

                for (int d = 0; d < directives.Length; d++)
                    CollectProfileRecursive(directives[d]?.Profile, registry, visited);
            }
        }

        private static void CollectHazardProfiles(
            HazardActorPlacementBinding[] placements,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            HashSet<int> visited)
        {
            if (placements == null)
                return;

            for (int i = 0; i < placements.Length; i++)
            {
                var prefab = placements[i].ActorArchetypePrefab;
                if (prefab == null)
                    continue;

                var actor = prefab.GetComponentInChildren<HazardActorAuthoring>(true);
                var slots = actor != null ? actor.PatternSlots : null;
                if (slots == null)
                    continue;

                for (int s = 0; s < slots.Length; s++)
                    CollectProfileRecursive(slots[s].Emission.Profile, registry, visited);
            }
        }

        private static void CollectProfileRecursive(
            EmissionProfileSO profile,
            DynamicBuffer<EmissionProfileRuntimeRegistryBuffer> registry,
            HashSet<int> visited)
        {
            if (profile == null)
                return;

            int profileRefId = profile.GetInstanceID();
            if (profileRefId == 0 || !visited.Add(profileRefId))
                return;

            if (!EmissionProfileResolver.TryResolve(profile, out var core, out _))
                return;

            registry.Add(CreateEntry(in core));
            if (core.HasMotionCompletedTrigger)
                CollectProfileRecursive(core.MotionCompletedTargetProfile, registry, visited);
            if (core.HasCleanupRemovedTrigger)
                CollectProfileRecursive(core.CleanupRemovedTargetProfile, registry, visited);
        }

        private static EmissionProfileRuntimeRegistryBuffer CreateEntry(in ResolvedEmissionCore core)
        {
            return new EmissionProfileRuntimeRegistryBuffer
            {
                ProfileRefId = core.ProfileRefId,
                BulletTypeKey = core.BulletTypeKey,
                HasSpeedOverride = core.HasSpeedOverride ? (byte)1 : (byte)0,
                SpeedOverride = math.max(0.001f, core.SpeedOverride),
                HasLifetimeOverride = core.HasLifetimeOverride ? (byte)1 : (byte)0,
                LifetimeOverride = math.max(0.001f, core.LifetimeOverride),
                HasMovementOverride = core.HasMovementOverride ? (byte)1 : (byte)0,
                MovementFamily = core.MovementFamily,
                DampedLinear = core.DampedLinear,
                HomingLite = core.HomingLite,
                PositionPatternMode = core.PositionPatternMode,
                SpawnOffset = new float2(core.SpawnOffset.x, core.SpawnOffset.y),
                LineStart = new float2(core.LineStart.x, core.LineStart.y),
                LineEnd = new float2(core.LineEnd.x, core.LineEnd.y),
                SampleSpacing = math.max(0.001f, core.SampleSpacing),
                PointSetCount = math.clamp(core.PointSetCount, 0, PointSetPositionPatternAuthoring.MaxPointCount),
                Point0 = new float2(core.Point0.x, core.Point0.y),
                Point1 = new float2(core.Point1.x, core.Point1.y),
                Point2 = new float2(core.Point2.x, core.Point2.y),
                Point3 = new float2(core.Point3.x, core.Point3.y),
                AimMode = core.AimMode,
                AimSnapshotTiming = core.AimSnapshotTiming,
                BaseAngleDeg = core.BaseAngleDeg,
                AimAngleOffsetDeg = core.AimAngleOffsetDeg,
                LineNormalSide = core.LineNormalSide,
                LineNormalAngleOffsetDeg = core.LineNormalAngleOffsetDeg,
                SpiralStepDeg = core.SpiralStepDeg,
                ShotPatternMode = core.ShotPatternMode,
                ShotCount = math.max(1, core.ShotCount),
                NWayAngleSpacingDeg = core.ShotPatternMode == WaveShotPatternModeId.NWay
                    ? core.NWayAngleSpacingDeg
                    : 0f,
                HasMotionCompletedTrigger = core.HasMotionCompletedTrigger ? (byte)1 : (byte)0,
                MotionCompletedTargetProfileRefId = core.MotionCompletedTargetProfileRefId,
                MotionCompletedOriginPosition = core.MotionCompletedOriginPosition,
                MotionCompletedForwardDirection = core.MotionCompletedForwardDirection,
                MotionCompletedSourceEntity = core.MotionCompletedSourceEntity,
                MotionCompletedCauserEntity = core.MotionCompletedCauserEntity,
                MotionCompletedDelaySec = math.max(0f, core.MotionCompletedDelaySec),
                HasCleanupRemovedTrigger = core.HasCleanupRemovedTrigger ? (byte)1 : (byte)0,
                CleanupRemovedTargetProfileRefId = core.CleanupRemovedTargetProfileRefId,
                CleanupRemovedOriginPosition = core.CleanupRemovedOriginPosition,
                CleanupRemovedForwardDirection = core.CleanupRemovedForwardDirection,
                CleanupRemovedSourceEntity = core.CleanupRemovedSourceEntity,
                CleanupRemovedCauserEntity = core.CleanupRemovedCauserEntity,
                CleanupRemovedDelaySec = math.max(0f, core.CleanupRemovedDelaySec),
            };
        }
    }
}
