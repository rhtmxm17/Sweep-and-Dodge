using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public class BulletSourceAuthoring : MonoBehaviour
    {
        [Header("Source Field")]
        public BulletFieldShapeId FieldShape = BulletFieldShapeId.Circle;
        public float FieldRadius = 8f;
        public Vector2 FieldSize = new Vector2(12f, 8f);
        public WaveTimelineSO WaveTimeline;

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
                    SamplingMode = SourcePollutionSamplingModeId.TopK,
                });

                AddComponent(e, new SourcePollutionGridComponent
                {
                    Cols = cols,
                    Rows = rows,
                    CellSize = cellSize,
                    InvCellSize = 1f / cellSize,
                    HalfExtents = halfExtents,
                });

                var patternBuffer = AddBuffer<SourceSpawnPatternBuffer>(e);
                var activeCountBuffer = AddBuffer<SourceActiveBulletCountBuffer>(e);
                var spawnRequestBuffer = AddBuffer<SourceSpawnRequestBuffer>(e);
                int nextDirectiveId = 1;
                BakeSustainFromWaveTimeline(authoring.WaveTimeline, patternBuffer, activeCountBuffer, ref nextDirectiveId);
                spawnRequestBuffer.Clear();

                var openingWaveBuffer = AddBuffer<SourceOpeningWavePatternBuffer>(e);
                BakeOpeningWaveFromTimeline(authoring.WaveTimeline, openingWaveBuffer, activeCountBuffer, ref nextDirectiveId);
                AddComponent(e, new SourceOpeningWaveRuntimeComponent
                {
                    LastState = authoring.InitialState,
                    ActiveTriggerState = SourceStateId.Normal,
                    IsPlaying = 0,
                    ElapsedSec = 0f
                });

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

            private void BakeSustainFromWaveTimeline(
                WaveTimelineSO timeline,
                DynamicBuffer<SourceSpawnPatternBuffer> patternBuffer,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer,
                ref int nextDirectiveId)
            {
                if (timeline == null || timeline.Segments == null)
                    return;

                for (int s = 0; s < timeline.Segments.Length; s++)
                {
                    var segment = timeline.Segments[s];
                    if (segment.Phase != SourceWavePhaseId.Sustain || segment.Entries == null)
                        continue;

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
                        patternBuffer.Add(new SourceSpawnPatternBuffer
                        {
                            DirectiveId = nextDirectiveId++,
                            State = segment.TargetState,
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
                            SpawnPriority = entry.ResolveSpawnPriority(),
                            MaxActiveDensityPerArea = Mathf.Max(0f, entry.ResolveMaxActiveDensityPerArea()),
                            SpawnAccumulator = 0f,
                            BurstEventsEmitted = 0
                        });

                        EnsureActiveCountEntry(activeCountBuffer, typeKey);
                    }
                }
            }

            private void BakeOpeningWaveFromTimeline(
                WaveTimelineSO timeline,
                DynamicBuffer<SourceOpeningWavePatternBuffer> openingWaveBuffer,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer,
                ref int nextDirectiveId)
            {
                if (timeline == null || timeline.Segments == null)
                    return;

                var activeCountKeys = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < activeCountBuffer.Length; i++)
                    activeCountKeys.Add(activeCountBuffer[i].BulletTypeKey);

                for (int s = 0; s < timeline.Segments.Length; s++)
                {
                    var segment = timeline.Segments[s];
                    if (segment.Phase != SourceWavePhaseId.OnStateEnterOnce || segment.EndSec <= segment.StartSec || segment.Entries == null)
                        continue;

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
                        openingWaveBuffer.Add(new SourceOpeningWavePatternBuffer
                        {
                            DirectiveId = nextDirectiveId++,
                            TriggerState = segment.TargetState,
                            StartSec = Mathf.Max(0f, segment.StartSec),
                            EndSec = Mathf.Max(segment.StartSec, segment.EndSec),
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
                            SpawnPriority = entry.ResolveSpawnPriority(),
                            MaxActiveDensityPerArea = Mathf.Max(0f, entry.ResolveMaxActiveDensityPerArea()),
                            SpawnAccumulator = 0f,
                            BurstEventsEmitted = 0
                        });

                        EnsureActiveCountEntry(activeCountBuffer, typeKey, activeCountKeys);
                    }
                }
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
