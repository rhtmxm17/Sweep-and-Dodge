using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Text;

namespace SweepNDodge.DotsBullets
{
    public class BulletSourceAuthoring : MonoBehaviour
    {
        [System.Serializable]
        public struct SustainClipSlotAuthoring
        {
            public SourceStateId State;
            public SourceSpawnLaneId Lane;
            public WaveClipSO[] Clips;
            public float[] Weights;
        }

        [System.Serializable]
        public struct EventClipSlotAuthoring
        {
            public SourceStateId TriggerState;
            public WaveClipSO[] EventClips;
        }

        [Header("Source Field")]
        public BulletFieldShapeId FieldShape = BulletFieldShapeId.Circle;
        public float FieldRadius = 8f;
        public Vector2 FieldSize = new Vector2(12f, 8f);

        [Header("V3 Wave Clips (Experimental)")]
        public SustainClipSlotAuthoring[] SustainClipSlots;
        public EventClipSlotAuthoring[] EventClipSlots;

        [Header("Depletion Threshold (externally injectable)")]
        public int ThresholdWeakened = 2000;
        public int ThresholdDepleted = 4000;
        public int InitialCollectedCount = 0;
        public SourceStateId InitialState = SourceStateId.Normal;

        [Header("Cleaning Trail (Pollution Grid)")]
        public float PollutionCellSize = 2.0f;
        public float PollutionMin = 0f;
        public float PollutionMax = 1f;
        public float PollutionRegenPerSec = 0.08f;
        public float PollutionDropPerCollect = 0.12f;
        public int PollutionTopKSampleCount = 6;

        [Header("Debug")]
        public bool DrawGizmo = true;
        public bool DrawGizmoWhenNotSelected = false;

        private class Baker : Baker<BulletSourceAuthoring>
        {
            public override void Bake(BulletSourceAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.Dynamic);

                int thresholdWeakened = Mathf.Max(0, authoring.ThresholdWeakened);
                int thresholdDepleted = Mathf.Max(thresholdWeakened, authoring.ThresholdDepleted);

                AddComponent(e, new SourceSpawnComponent
                {
                    ThresholdWeakened = thresholdWeakened,
                    ThresholdDepleted = thresholdDepleted,
                    CollectedCount = Mathf.Max(0, authoring.InitialCollectedCount),
                    State = authoring.InitialState
                });

                AddComponent(e, new SourceStableIdComponent
                {
                    Value = ComputeStableSourceId(authoring.transform, authoring.transform.position),
                });

                AddComponent(e, new BulletFieldAreaComponent
                {
                    Shape = authoring.FieldShape,
                    Radius = Mathf.Max(0f, authoring.FieldRadius),
                    Size = new float2(Mathf.Max(0f, authoring.FieldSize.x), Mathf.Max(0f, authoring.FieldSize.y)),
                    ComputedArea = ComputeArea(authoring.FieldShape, authoring.FieldRadius, authoring.FieldSize)
                });

                AddComponent(e, new SourceSpawnRuntimeComponent
                {
                    SpawnSequence = 1u
                });

                var halfExtents = ComputeHalfExtents(authoring.FieldShape, authoring.FieldRadius, authoring.FieldSize);
                float cellSize = Mathf.Max(0.1f, authoring.PollutionCellSize);
                int cols = Mathf.Max(1, Mathf.CeilToInt((halfExtents.x * 2f) / cellSize));
                int rows = Mathf.Max(1, Mathf.CeilToInt((halfExtents.y * 2f) / cellSize));
                float minValue = Mathf.Max(0f, authoring.PollutionMin);
                float maxValue = Mathf.Max(minValue, authoring.PollutionMax);

                AddComponent(e, new SourcePollutionConfigComponent
                {
                    MinValue = minValue,
                    MaxValue = maxValue,
                    RegenPerSec = Mathf.Max(0f, authoring.PollutionRegenPerSec),
                    DropPerCollect = Mathf.Max(0f, authoring.PollutionDropPerCollect),
                    TopKSampleCount = Mathf.Max(1, authoring.PollutionTopKSampleCount),
                });

                AddComponent(e, new SourcePollutionGridComponent
                {
                    Cols = cols,
                    Rows = rows,
                    CellSize = cellSize,
                    InvCellSize = 1f / cellSize,
                    HalfExtents = halfExtents,
                });

                var activeCountBuffer = AddBuffer<SourceActiveBulletCountBuffer>(e);
                var spawnRequestBuffer = AddBuffer<SourceSpawnRequestBuffer>(e);
                int nextDirectiveId = 1;
                spawnRequestBuffer.Clear();

                var clipPatternBuffer = AddBuffer<SourceClipPatternBuffer>(e);
                var sustainSlotCandidateBuffer = AddBuffer<SourceSustainSlotCandidateBuffer>(e);
                var sustainRuntimeLaneBuffer = AddBuffer<SourceSustainRuntimeLaneBuffer>(e);
                var eventQueueBuffer = AddBuffer<SourceEventQueueBuffer>(e);

                AddComponent(e, new SourceSustainRuntimeComponent
                {
                    ActiveState = authoring.InitialState,
                });

                AddComponent(e, new SourceEventRuntimeComponent
                {
                    IsPlaying = 0,
                    ActiveEventClipId = 0,
                    TriggerState = authoring.InitialState,
                    ElapsedSec = 0f,
                    SelectionSequence = 1u,
                });

                AddComponent(e, new SourceRunDirectorStateComponent
                {
                    State = authoring.InitialState == SourceStateId.Depleted
                        ? RunDirectorSourceStateId.Finish
                        : RunDirectorSourceStateId.Baseline,
                    SelectedClipState = authoring.InitialState,
                    PressureOccupancySec = 0f,
                    DensityScale = 1f,
                    Version = 1u,
                });
                var pressureInputs = AddBuffer<SourceDirectorPressureInputBuffer>(e);
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

                BakeV3ClipBindings(
                    authoring,
                    clipPatternBuffer,
                    sustainSlotCandidateBuffer,
                    sustainRuntimeLaneBuffer,
                    eventQueueBuffer,
                    activeCountBuffer,
                    ref nextDirectiveId);

                var pollutionCells = AddBuffer<SourcePollutionCellBuffer>(e);
                var pollutionDrops = AddBuffer<SourcePollutionDropRequestBuffer>(e);
                var pollutionValidCellIndices = AddBuffer<SourcePollutionValidCellIndexBuffer>(e);
                BakePollutionGrid(
                    pollutionCells,
                    pollutionDrops,
                    pollutionValidCellIndices,
                    cols,
                    rows,
                    cellSize,
                    halfExtents,
                    authoring.FieldShape,
                    authoring.FieldRadius,
                    maxValue);

                AddComponent(e, new SourceAnchorComponent
                {
                    Position = (float3)authoring.transform.position
                });
            }

            private void BakeV3ClipBindings(
                BulletSourceAuthoring authoring,
                DynamicBuffer<SourceClipPatternBuffer> clipPatternBuffer,
                DynamicBuffer<SourceSustainSlotCandidateBuffer> sustainSlotCandidateBuffer,
                DynamicBuffer<SourceSustainRuntimeLaneBuffer> sustainRuntimeLaneBuffer,
                DynamicBuffer<SourceEventQueueBuffer> eventQueueBuffer,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer,
                ref int nextDirectiveId)
            {
                clipPatternBuffer.Clear();
                sustainSlotCandidateBuffer.Clear();
                sustainRuntimeLaneBuffer.Clear();
                eventQueueBuffer.Clear();

                if (authoring.SustainClipSlots != null)
                {
                    for (int i = 0; i < authoring.SustainClipSlots.Length; i++)
                    {
                        var slot = authoring.SustainClipSlots[i];
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
                                    $"[WaveClipBake] Sustain slot references non-sustain clip. clip={clip.name}, phase={clip.Phase}, source={authoring.name}",
                                    clip);
                                continue;
                            }

                            sustainSlotCandidateBuffer.Add(new SourceSustainSlotCandidateBuffer
                            {
                                State = slot.State,
                                Lane = slot.Lane,
                                ClipId = clip.ClipId,
                                Weight = ResolveClipWeight(slot.Weights, c)
                            });

                            BakeWaveClipToPatternBuffer(
                                clip,
                                slot.State,
                                slot.Lane,
                                clipPatternBuffer,
                                activeCountBuffer,
                                ref nextDirectiveId);
                        }
                    }
                }

                if (authoring.EventClipSlots == null)
                    return;

                for (int i = 0; i < authoring.EventClipSlots.Length; i++)
                {
                    var slot = authoring.EventClipSlots[i];
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
                                $"[WaveClipBake] Event slot references non-event clip. clip={clip.name}, phase={clip.Phase}, source={authoring.name}",
                                clip);
                            continue;
                        }

                        BakeWaveClipToPatternBuffer(
                            clip,
                            slot.TriggerState,
                            clip.Lane,
                            clipPatternBuffer,
                            activeCountBuffer,
                            ref nextDirectiveId);
                    }
                }
            }

            private static void BakeWaveClipToPatternBuffer(
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
                            BurstEventsEmitted = 0
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

            private static void EnsureActiveCountEntry(
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer,
                int typeKey,
                System.Collections.Generic.HashSet<int> knownKeys = null)
            {
                if (knownKeys != null)
                {
                    if (!knownKeys.Add(typeKey))
                        return;

                    activeCountBuffer.Add(new SourceActiveBulletCountBuffer
                    {
                        BulletTypeKey = typeKey,
                        ActiveCount = 0
                    });
                    return;
                }

                for (int i = 0; i < activeCountBuffer.Length; i++)
                {
                    if (activeCountBuffer[i].BulletTypeKey == typeKey)
                        return;
                }

                activeCountBuffer.Add(new SourceActiveBulletCountBuffer
                {
                    BulletTypeKey = typeKey,
                    ActiveCount = 0
                });
            }

            private static uint ComputeStableSourceId(Transform sourceTransform, Vector3 sourcePosition)
            {
                if (sourceTransform == null)
                    return 1u;

                var sb = new StringBuilder(128);
                AppendHierarchyPath(sb, sourceTransform);

                // Quantized position is included to reduce collisions for same-name siblings.
                int px = Mathf.RoundToInt(sourcePosition.x * 100f);
                int py = Mathf.RoundToInt(sourcePosition.y * 100f);
                int pz = Mathf.RoundToInt(sourcePosition.z * 100f);
                sb.Append('|').Append(px).Append(',').Append(py).Append(',').Append(pz);

                uint hash = 2166136261u;
                for (int i = 0; i < sb.Length; i++)
                {
                    hash ^= sb[i];
                    hash *= 16777619u;
                }

                return math.max(1u, hash);
            }

            private static void AppendHierarchyPath(StringBuilder sb, Transform t)
            {
                if (t.parent != null)
                {
                    AppendHierarchyPath(sb, t.parent);
                    sb.Append('/');
                }

                sb.Append(t.name);
            }

            private static void BakePollutionGrid(
                DynamicBuffer<SourcePollutionCellBuffer> pollutionCells,
                DynamicBuffer<SourcePollutionDropRequestBuffer> pollutionDrops,
                DynamicBuffer<SourcePollutionValidCellIndexBuffer> pollutionValidCellIndices,
                int cols,
                int rows,
                float cellSize,
                float2 halfExtents,
                BulletFieldShapeId shape,
                float fieldRadius,
                float maxValue)
            {
                int cellCount = Mathf.Max(1, cols * rows);
                pollutionCells.ResizeUninitialized(cellCount);
                pollutionValidCellIndices.Clear();

                float safeCellSize = Mathf.Max(0.001f, cellSize);
                float safeRadius = Mathf.Max(0f, fieldRadius);
                float radiusSq = safeRadius * safeRadius;

                for (int y = 0; y < rows; y++)
                {
                    for (int x = 0; x < cols; x++)
                    {
                        int i = y * cols + x;

                        float centerX = -halfExtents.x + (x + 0.5f) * safeCellSize;
                        float centerZ = -halfExtents.y + (y + 0.5f) * safeCellSize;
                        bool isValid = shape == BulletFieldShapeId.Rectangle
                            || (centerX * centerX + centerZ * centerZ) <= radiusSq;

                        pollutionCells[i] = new SourcePollutionCellBuffer
                        {
                            Value = maxValue,
                            IsValid = isValid ? (byte)1 : (byte)0,
                        };

                        if (isValid)
                        {
                            pollutionValidCellIndices.Add(new SourcePollutionValidCellIndexBuffer
                            {
                                Value = i,
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

                pollutionDrops.Clear();
            }
        }

        private static float ComputeArea(BulletFieldShapeId shape, float radius, Vector2 size)
        {
            if (shape == BulletFieldShapeId.Rectangle)
                return Mathf.Max(0f, size.x) * Mathf.Max(0f, size.y);

            float r = Mathf.Max(0f, radius);
            return Mathf.PI * r * r;
        }

        private static float2 ComputeHalfExtents(BulletFieldShapeId shape, float radius, Vector2 size)
        {
            if (shape == BulletFieldShapeId.Rectangle)
            {
                float2 safe = new float2(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y));
                return safe * 0.5f;
            }

            float r = Mathf.Max(0f, radius);
            return new float2(r, r);
        }

        private void OnDrawGizmos()
        {
            if (!DrawGizmo || !DrawGizmoWhenNotSelected)
                return;
            DrawSourceGizmo();
        }

        private void OnDrawGizmosSelected()
        {
            if (!DrawGizmo || DrawGizmoWhenNotSelected)
                return;
            DrawSourceGizmo();
        }

        private void DrawSourceGizmo()
        {
            var prevMatrix = Gizmos.matrix;
            var prev = Gizmos.color;
            Gizmos.color = new Color(0.2f, 0.9f, 0.3f, 1f);
            if (FieldShape == BulletFieldShapeId.Rectangle)
            {
                var size = new Vector3(Mathf.Max(0f, FieldSize.x), 0f, Mathf.Max(0f, FieldSize.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = prevMatrix;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, FieldRadius));
            }
            Gizmos.color = prev;
            Gizmos.matrix = prevMatrix;
        }
    }
}
