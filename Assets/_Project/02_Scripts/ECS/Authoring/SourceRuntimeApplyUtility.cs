using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public static class SourceRuntimeApplyUtility
    {
        public static void RebuildClipBindingsFromStageDefinition(
            in StageSourceBinding binding,
            DynamicBuffer<SourceClipPatternBuffer> clipPatternBuffer,
            DynamicBuffer<SourceSustainSlotCandidateBuffer> sustainSlotCandidateBuffer,
            DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainRuntimeLaneBuffer,
            DynamicBuffer<SourceEventQueueBuffer> eventQueueBuffer,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer)
        {
            clipPatternBuffer.Clear();
            sustainSlotCandidateBuffer.Clear();
            sustainRuntimeLaneBuffer.Clear();
            eventQueueBuffer.Clear();
            activeCountBuffer.Clear();

            int nextDirectiveId = 1;
            var sustainSlots = binding.SustainSlots;
            if (sustainSlots != null)
            {
                for (int i = 0; i < sustainSlots.Length; i++)
                {
                    var slot = sustainSlots[i];
                    EnsureSustainLaneRuntimeEntry(sustainRuntimeLaneBuffer, slot.Lane);

                    if (slot.Clips == null)
                        continue;

                    for (int c = 0; c < slot.Clips.Length; c++)
                    {
                        var clip = slot.Clips[c];
                        if (clip == null)
                            continue;

                        if (clip.Phase != SourceWavePhaseId.Sustain)
                        {
                            Debug.LogWarning(
                                $"[StageDefinitionApply] Sustain slot references non-sustain clip. stableId={binding.SourceStableId}, clip={clip.name}, phase={clip.Phase}",
                                clip);
                            continue;
                        }

                        sustainSlotCandidateBuffer.Add(new SourceSustainSlotCandidateBuffer
                        {
                            State = slot.State,
                            Lane = slot.Lane,
                            ClipId = clip.ClipId,
                            Weight = ResolveClipWeight(slot.Weights, c),
                        });

                        AppendClipToPatternBuffer(
                            clip,
                            slot.State,
                            slot.Lane,
                            clipPatternBuffer,
                            activeCountBuffer,
                            ref nextDirectiveId);
                    }
                }
            }

            var eventSlots = binding.EventSlots;
            if (eventSlots == null)
                return;

            for (int i = 0; i < eventSlots.Length; i++)
            {
                var slot = eventSlots[i];
                if (slot.EventClips == null)
                    continue;

                for (int c = 0; c < slot.EventClips.Length; c++)
                {
                    var clip = slot.EventClips[c];
                    if (clip == null)
                        continue;

                    if (clip.Phase != SourceWavePhaseId.OnStateEnterOnce)
                    {
                        Debug.LogWarning(
                            $"[StageDefinitionApply] Event slot references non-event clip. stableId={binding.SourceStableId}, clip={clip.name}, phase={clip.Phase}",
                            clip);
                        continue;
                    }

                    AppendClipToPatternBuffer(
                        clip,
                        slot.TriggerState,
                        clip.Lane,
                        clipPatternBuffer,
                        activeCountBuffer,
                        ref nextDirectiveId);
                }
            }
        }

        public static void ResetPressureInputs(DynamicBuffer<SourceDirectorPressureInputBuffer> pressureInputs)
        {
            pressureInputs.Clear();
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                Value = 0f,
            });
            pressureInputs.Add(new SourceDirectorPressureInputBuffer
            {
                Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                Value = 0f,
            });
        }

        public static void RefreshSourceShapeDerived(
            in Shape2DComponent shape,
            ref SourceShapeDerivedComponent derived)
        {
            var normalized = Shape2DUtility.Normalize(in shape);
            derived.ComputedArea = Shape2DUtility.ComputeArea(in normalized);
            derived.HalfExtents = Shape2DUtility.ComputeHalfExtents(in normalized);
        }

        public static void RebuildPollutionGrid(
            in Shape2DComponent shape,
            in SourceShapeDerivedComponent derived,
            in SourcePollutionConfigComponent config,
            ref SourcePollutionGridComponent grid,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops,
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndices)
        {
            float cellSize = math.max(0.1f, grid.CellSize);
            var normalized = Shape2DUtility.Normalize(in shape);
            float2 halfExtents = math.max(float2.zero, derived.HalfExtents);
            int cols = math.max(1, Mathf.CeilToInt((halfExtents.x * 2f) / cellSize));
            int rows = math.max(1, Mathf.CeilToInt((halfExtents.y * 2f) / cellSize));

            grid.Cols = cols;
            grid.Rows = rows;
            grid.CellSize = cellSize;
            grid.InvCellSize = 1f / cellSize;
            grid.HalfExtents = halfExtents;
            grid.OriginX = -halfExtents.x;
            grid.OriginZ = -halfExtents.y;

            BakePollutionGrid(
                pollutionCells,
                pollutionDrops,
                pollutionValidCellIndices,
                cols,
                rows,
                cellSize,
                halfExtents,
                normalized.Kind,
                normalized.Radius,
                math.max(config.MinValue, config.MaxValue));
        }

        public static void ResetPollutionRuntimeState(
            in SourcePollutionConfigComponent config,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops)
        {
            float maxValue = math.max(config.MinValue, config.MaxValue);
            for (int i = 0; i < pollutionCells.Length; i++)
            {
                var cell = pollutionCells[i];
                cell.Value = maxValue;
                cell.IsActive = cell.IsValid != 0 ? (byte)1 : (byte)0;
                cell.LastDropFrame = 0u;
                cell.CooldownUntilFrame = 0u;
                pollutionCells[i] = cell;
            }

            pollutionDrops.Clear();
        }

        public static void RebuildPollutionGridFromRegionBounds(
            int minCellX,
            int minCellY,
            int maxCellX,
            int maxCellY,
            int stageWidth,
            float stageCellSize,
            float stageOriginX,
            float stageOriginZ,
            in SourcePollutionConfigComponent config,
            DynamicBuffer<SourceRegionCellIndexBuffer> ownedStageCells,
            ref SourcePollutionGridComponent grid,
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops,
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndices)
        {
            int cols = math.max(1, (maxCellX - minCellX) + 1);
            int rows = math.max(1, (maxCellY - minCellY) + 1);
            float cellSize = math.max(0.1f, stageCellSize);
            float2 halfExtents = new float2(cols * cellSize * 0.5f, rows * cellSize * 0.5f);

            grid.Cols = cols;
            grid.Rows = rows;
            grid.CellSize = cellSize;
            grid.InvCellSize = 1f / cellSize;
            grid.HalfExtents = halfExtents;
            grid.OriginX = stageOriginX + (minCellX * cellSize);
            grid.OriginZ = stageOriginZ + (minCellY * cellSize);

            BakePollutionGridFromOwnedCells(
                pollutionCells,
                pollutionDrops,
                pollutionValidCellIndices,
                cols,
                rows,
                stageWidth,
                minCellX,
                minCellY,
                in ownedStageCells,
                math.max(config.MinValue, config.MaxValue));
        }

        public static float ComputeArea(Shape2DKind kind, float radius, Vector2 size)
        {
            var shape = new Shape2DComponent
            {
                Kind = kind,
                Radius = radius,
                Size = new float2(size.x, size.y),
            };
            return Shape2DUtility.ComputeArea(in shape);
        }

        public static float2 ComputeHalfExtents(Shape2DKind kind, float radius, Vector2 size)
        {
            return ComputeHalfExtents(kind, radius, new float2(size.x, size.y));
        }

        public static float2 ComputeHalfExtents(Shape2DKind kind, float radius, float2 size)
        {
            var shape = new Shape2DComponent
            {
                Kind = kind,
                Radius = radius,
                Size = size,
            };
            return Shape2DUtility.ComputeHalfExtents(in shape);
        }

        public static int ResolveCollectedCount(SourceStateId state, int thresholdWeakened, int thresholdDepleted)
        {
            return state switch
            {
                SourceStateId.Weakened => math.max(0, thresholdWeakened),
                SourceStateId.Depleted => math.max(math.max(0, thresholdWeakened), thresholdDepleted),
                _ => 0,
            };
        }

        private static void AppendClipToPatternBuffer(
            WaveClipSO clip,
            SourceStateId triggerState,
            SourceSpawnLaneId lane,
            DynamicBuffer<SourceClipPatternBuffer> clipPatternBuffer,
            DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer,
            ref int nextDirectiveId)
        {
            if (clip == null || clip.Segments == null)
                return;

            float clipDuration = Mathf.Max(0f, clip.DurationSec);
            int lanePriority = SourceSpawnLanePriorityUtility.ResolvePriority(lane);

            for (int s = 0; s < clip.Segments.Length; s++)
            {
                var segment = clip.Segments[s];
                int entryCount = segment.Directives?.Length ?? 0;
                if (segment.EndSec <= segment.StartSec || entryCount <= 0)
                    continue;

                float startSec = Mathf.Max(0f, segment.StartSec);
                float endSec = Mathf.Max(startSec, segment.EndSec);
                if (clipDuration > 0f)
                {
                    startSec = Mathf.Min(startSec, clipDuration);
                    endSec = Mathf.Min(endSec, clipDuration);
                    if (endSec <= startSec)
                        continue;
                }

                for (int e = 0; e < entryCount; e++)
                {
                    if (!WaveClipAuthoringResolver.TryResolveTypedEntry(segment.Directives[e], out var snapshot, out string error))
                    {
                        throw new InvalidOperationException(
                            $"{nameof(WaveClipSO)} '{clip.name}' has invalid typed directive at segmentIndex={s}, entryIndex={e}. {error}");
                    }

                    var bullet = snapshot.Bullet;
                    if (bullet == null)
                        continue;

                    int typeKey = bullet.DefinitionId;
                    clipPatternBuffer.Add(new SourceClipPatternBuffer
                    {
                        DirectiveId = nextDirectiveId++,
                        ClipId = clip.ClipId,
                        ClipDurationSec = clipDuration,
                        Phase = clip.Phase,
                        Lane = lane,
                        TriggerState = triggerState,
                        LocalStartSec = startSec,
                        LocalEndSec = endSec,
                        BulletTypeKey = typeKey,
                        EmissionMode = snapshot.EmissionMode,
                        SpawnMode = snapshot.SpawnMode,
                        SamplingAnchorMode = snapshot.SamplingAnchorMode,
                        AreaSamplerMode = snapshot.AreaSamplerMode,
                        PositionPatternMode = snapshot.PositionPatternMode,
                        AimMode = snapshot.AimMode,
                        AimSnapshotTiming = snapshot.AimSnapshotTiming,
                        AimAngleOffsetDeg = snapshot.AimAngleOffsetDeg,
                        ShotPatternMode = snapshot.ShotPatternMode,
                        ShotCount = Mathf.Max(1, snapshot.ShotCount),
                        EventRepeatCount = Mathf.Max(1, snapshot.EventRepeatCount),
                        FixedPoint = new float2(snapshot.FixedPoint.x, snapshot.FixedPoint.y),
                        SpawnOffset = new float2(snapshot.SpawnOffset.x, snapshot.SpawnOffset.y),
                        LineStart = new float2(snapshot.LineStart.x, snapshot.LineStart.y),
                        LineEnd = new float2(snapshot.LineEnd.x, snapshot.LineEnd.y),
                        SampleSpacing = Mathf.Max(0.001f, snapshot.SampleSpacing),
                        PointSetCount = Mathf.Clamp(snapshot.PointSetCount, 0, PointSetPositionPatternAuthoring.MaxPointCount),
                        Point0 = new float2(snapshot.Point0.x, snapshot.Point0.y),
                        Point1 = new float2(snapshot.Point1.x, snapshot.Point1.y),
                        Point2 = new float2(snapshot.Point2.x, snapshot.Point2.y),
                        Point3 = new float2(snapshot.Point3.x, snapshot.Point3.y),
                        SpawnSampleBudget = Mathf.Max(1, snapshot.SpawnSampleBudget),
                        PlayerNoSpawnRadius = Mathf.Max(0f, snapshot.PlayerNoSpawnRadius),
                        BaseAngleDeg = snapshot.BaseAngleDeg,
                        SpiralStepDeg = snapshot.SpiralStepDeg,
                        SpawnDensityPerSecPerArea = Mathf.Max(0f, snapshot.RatePerSecPerArea),
                        MeanEventsPerSec = Mathf.Max(0f, snapshot.MeanEventsPerSec),
                        BurstRepeatCount = snapshot.BurstRepeatCount,
                        BurstIntervalSec = Mathf.Max(0.001f, snapshot.BurstIntervalSec),
                        EventShotSchedule = snapshot.EventShotSchedule,
                        EventShotIntervalSec = Mathf.Max(0f, snapshot.EventShotIntervalSec),
                        LanePriority = lanePriority,
                        MaxActiveDensityPerArea = Mathf.Max(0f, snapshot.MaxActiveDensityPerArea),
                        SpawnAccumulator = 0f,
                        BurstEventsEmitted = 0,
                    });

                    EnsureActiveCountEntry(activeCountBuffer, typeKey);
                }
            }
        }

        private static float ResolveClipWeight(float[] weights, int index)
        {
            if (weights == null || index < 0 || index >= weights.Length)
                return 1f;

            return weights[index] > 0f ? weights[index] : 1f;
        }

        private static void EnsureSustainLaneRuntimeEntry(
            DynamicBuffer<SourceSustainRuntimeLaneBuffer> runtimeByLaneBuffer,
            SourceSpawnLaneId lane)
        {
            for (int i = 0; i < runtimeByLaneBuffer.Length; i++)
            {
                if (runtimeByLaneBuffer[i].Lane == lane)
                    return;
            }

            runtimeByLaneBuffer.Add(new SourceSustainRuntimeLaneBuffer
            {
                Lane = lane,
                ActiveClipId = 0,
                ElapsedSec = 0f,
                LastClipId = 0,
                SelectionSequence = 1u,
                LastMissingLogFrame = 0u,
            });
        }

        private static void EnsureActiveCountEntry(DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer, int typeKey)
        {
            for (int i = 0; i < activeCountBuffer.Length; i++)
            {
                if (activeCountBuffer[i].BulletTypeKey == typeKey)
                    return;
            }

            activeCountBuffer.Add(new SourceActiveBulletCountBuffer
            {
                BulletTypeKey = typeKey,
                ActiveCount = 0,
            });
        }

        private static void BakePollutionGrid(
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops,
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndices,
            int cols,
            int rows,
            float cellSize,
            float2 halfExtents,
            Shape2DKind shape,
            float fieldRadius,
            float maxValue)
        {
            int cellCount = Mathf.Max(1, cols * rows);
            pollutionCells.Clear();
            if (pollutionCells.Capacity < cellCount)
                pollutionCells.Capacity = cellCount;

            pollutionDrops.Clear();
            pollutionValidCellIndices.Clear();
            if (pollutionValidCellIndices.Capacity < cellCount)
                pollutionValidCellIndices.Capacity = cellCount;

            float safeCellSize = Mathf.Max(0.001f, cellSize);
            float safeRadius = Mathf.Max(0f, fieldRadius);
            float radiusSq = safeRadius * safeRadius;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < cols; x++)
                {
                    int index = y * cols + x;

                    float centerX = -halfExtents.x + (x + 0.5f) * safeCellSize;
                    float centerZ = -halfExtents.y + (y + 0.5f) * safeCellSize;
                    bool isValid = shape == Shape2DKind.Rectangle
                        || (centerX * centerX + centerZ * centerZ) <= radiusSq;

                    pollutionCells.Add(new SourcePollutionCellBuffer
                    {
                        Value = maxValue,
                        IsValid = isValid ? (byte)1 : (byte)0,
                        IsActive = isValid ? (byte)1 : (byte)0,
                        LastDropFrame = 0u,
                        CooldownUntilFrame = 0u,
                    });

                    if (isValid)
                    {
                        pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
                        {
                            Value = index,
                        });
                    }
                }
            }

            if (pollutionValidCellIndices.Length <= 0)
            {
                int centerIndex = Mathf.Clamp((rows / 2) * cols + (cols / 2), 0, cellCount - 1);
                var cell = pollutionCells[centerIndex];
                cell.IsValid = 1;
                cell.IsActive = 1;
                pollutionCells[centerIndex] = cell;
                pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
                {
                    Value = centerIndex,
                });
            }
        }

        private static void BakePollutionGridFromOwnedCells(
            DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
            DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops,
            DynamicBuffer<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndices,
            int cols,
            int rows,
            int stageWidth,
            int minCellX,
            int minCellY,
            in DynamicBuffer<SourceRegionCellIndexBuffer> ownedStageCells,
            float maxValue)
        {
            int cellCount = Mathf.Max(1, cols * rows);
            pollutionCells.Clear();
            if (pollutionCells.Capacity < cellCount)
                pollutionCells.Capacity = cellCount;

            pollutionDrops.Clear();
            pollutionValidCellIndices.Clear();
            if (pollutionValidCellIndices.Capacity < cellCount)
                pollutionValidCellIndices.Capacity = cellCount;

            for (int i = 0; i < cellCount; i++)
            {
                pollutionCells.Add(new SourcePollutionCellBuffer
                {
                    Value = maxValue,
                    IsValid = 0,
                    IsActive = 0,
                    LastDropFrame = 0u,
                    CooldownUntilFrame = 0u,
                });
            }

            for (int i = 0; i < ownedStageCells.Length; i++)
            {
                int globalIndex = ownedStageCells[i].Value;
                if (globalIndex < 0 || stageWidth <= 0)
                    continue;

                int globalX = globalIndex % stageWidth;
                int globalY = globalIndex / stageWidth;
                int localX = globalX - minCellX;
                int localY = globalY - minCellY;
                if ((uint)localX >= (uint)cols || (uint)localY >= (uint)rows)
                    continue;

                int localIndex = localY * cols + localX;
                if ((uint)localIndex >= (uint)pollutionCells.Length)
                    continue;

                var cell = pollutionCells[localIndex];
                cell.IsValid = 1;
                cell.IsActive = 1;
                pollutionCells[localIndex] = cell;
                pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
                {
                    Value = localIndex,
                });
            }

            if (pollutionValidCellIndices.Length > 0)
                return;

            int fallbackIndex = Mathf.Clamp((rows / 2) * cols + (cols / 2), 0, cellCount - 1);
            var fallbackCell = pollutionCells[fallbackIndex];
            fallbackCell.IsValid = 1;
            fallbackCell.IsActive = 1;
            pollutionCells[fallbackIndex] = fallbackCell;
            pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
            {
                Value = fallbackIndex,
            });
        }
    }
}
