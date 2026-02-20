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
        public BulletSourceProfileSO SpawnProfile;

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
                BakeSpawnProfile(authoring.SpawnProfile, patternBuffer, activeCountBuffer);
                spawnRequestBuffer.Clear();

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

            private void BakeSpawnProfile(
                BulletSourceProfileSO profile,
                DynamicBuffer<SourceSpawnPatternBuffer> patternBuffer,
                DynamicBuffer<SourceActiveBulletCountBuffer> activeCountBuffer)
            {
                if (profile == null || profile.States == null)
                    return;

                var activeCountKeys = new System.Collections.Generic.HashSet<int>();

                for (int i = 0; i < profile.States.Length; i++)
                {
                    var stateConfig = profile.States[i];
                    var entries = stateConfig.Entries;
                    if (entries == null)
                        continue;

                    for (int j = 0; j < entries.Length; j++)
                    {
                        var entry = entries[j];
                        if (entry.Bullet == null)
                            continue;

                        int typeKey = entry.Bullet.DefinitionId;
                        patternBuffer.Add(new SourceSpawnPatternBuffer
                        {
                            State = stateConfig.State,
                            BulletTypeKey = typeKey,
                            SpawnMode = entry.SpawnMode,
                            SpawnDensityPerSecPerArea = Mathf.Max(0f, entry.SpawnDensityPerSecPerArea),
                            MaxActiveDensityPerArea = Mathf.Max(0f, entry.MaxActiveDensityPerArea),
                            SpawnAccumulator = 0f
                        });

                        if (activeCountKeys.Add(typeKey))
                        {
                            activeCountBuffer.Add(new SourceActiveBulletCountBuffer
                            {
                                BulletTypeKey = typeKey,
                                ActiveCount = 0
                            });
                        }
                    }
                }
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
