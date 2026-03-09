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
                if (segment.EndSec <= segment.StartSec || segment.Entries == null)
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

                for (int e = 0; e < segment.Entries.Length; e++)
                {
                    var entry = segment.Entries[e];
                    var bullet = entry.ResolveBullet();
                    if (bullet == null)
                        continue;

                    int typeKey = bullet.DefinitionId;
                    var fixedPoint = entry.ResolveFixedPoint();
                    var spawnOffset = entry.ResolveSpawnOffset();
                    var lineStart = entry.ResolveLineStart();
                    var lineEnd = entry.ResolveLineEnd();
                    int pointSetCount = entry.ResolvePointSetCount();
                    var point0 = entry.ResolvePointSetPoint(0);
                    var point1 = entry.ResolvePointSetPoint(1);
                    var point2 = entry.ResolvePointSetPoint(2);
                    var point3 = entry.ResolvePointSetPoint(3);
                    clipPatternBuffer.Add(new SourceClipPatternBuffer
                    {
                        DirectiveId = nextDirectiveId++,
                        ClipId = clip.ClipId,
                        Phase = clip.Phase,
                        Lane = lane,
                        TriggerState = triggerState,
                        LocalStartSec = startSec,
                        LocalEndSec = endSec,
                        BulletTypeKey = typeKey,
                        EmissionMode = entry.ResolveEmissionMode(),
                        SpawnMode = entry.ResolveSpawnMode(),
                        SamplingMode = entry.ResolveSamplingMode(),
                        CenterMode = entry.ResolveCenterMode(),
                        DirectionMode = entry.ResolveDirectionMode(),
                        FixedPoint = new float2(fixedPoint.x, fixedPoint.y),
                        SpawnOffset = new float2(spawnOffset.x, spawnOffset.y),
                        LineStart = new float2(lineStart.x, lineStart.y),
                        LineEnd = new float2(lineEnd.x, lineEnd.y),
                        SampleSpacing = Mathf.Max(0.001f, entry.ResolveSampleSpacing()),
                        PointSetCount = Mathf.Clamp(pointSetCount, 0, WaveClipSO.SpawnSamplingProfile.PointSetMaxCount),
                        Point0 = new float2(point0.x, point0.y),
                        Point1 = new float2(point1.x, point1.y),
                        Point2 = new float2(point2.x, point2.y),
                        Point3 = new float2(point3.x, point3.y),
                        SpawnSampleBudget = Mathf.Max(1, entry.ResolveSpawnSampleBudget()),
                        PlayerNoSpawnRadius = Mathf.Max(0f, entry.ResolvePlayerNoSpawnRadius()),
                        BaseAngleDeg = entry.ResolveBaseAngleDeg(),
                        NWayCount = Mathf.Max(1, entry.ResolveNWayCount()),
                        SpiralStepDeg = entry.ResolveSpiralStepDeg(),
                        SpawnDensityPerSecPerArea = Mathf.Max(0f, entry.ResolveRatePerSecPerArea()),
                        MeanEventsPerSec = Mathf.Max(0f, entry.ResolveMeanEventsPerSec()),
                        BurstRepeatCount = entry.ResolveBurstRepeatCount(),
                        BurstIntervalSec = Mathf.Max(0.001f, entry.ResolveBurstIntervalSec()),
                        BurstShotsPerEvent = Mathf.Max(1, entry.ResolveBurstShotsPerEvent()),
                        EventShotSchedule = entry.ResolveEventShotSchedule(),
                        EventShotIntervalSec = Mathf.Max(0f, entry.ResolveEventShotIntervalSec()),
                        LanePriority = lanePriority,
                        MaxActiveDensityPerArea = Mathf.Max(0f, entry.ResolveMaxActiveDensityPerArea()),
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
                pollutionCells[centerIndex] = cell;
                pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
                {
                    Value = centerIndex,
                });
            }
        }
    }
}
