using System.Text;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public abstract class SourceRuntimeTemplateAuthoringBase : MonoBehaviour
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

        [Header("Identity")]
        [Min(0)] public uint StableIdOverride = 0;

        [Header("Source Field")]
        public Shape2DKind Shape = Shape2DKind.Circle;
        public float Radius = 8f;
        public Vector2 Size = new Vector2(12f, 8f);

        [Header("Source Definition Seed (Deprecated)")]
        public SustainClipSlotAuthoring[] SustainClipSlots;
        public EventClipSlotAuthoring[] EventClipSlots;
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
        [Range(0f, 1f)] public float PollutionActiveRatioThreshold = 0.35f;
        [Min(0)] public int PollutionRecoveryCooldownFrames = 45;
        [Min(1)] public int PollutionRecoveryWaveSeedCount = 2;
        [Min(1)] public int PollutionRecoveryWaveClusterSize = 4;
        public float PollutionRecoveryRestoreValue = 0.4f;
        [Min(0)] public int PollutionRecoveryRecentCleanBiasFrames = 90;

        [Header("Debug")]
        public bool DrawGizmo = true;
        public bool DrawGizmoWhenNotSelected = false;

        protected static void BakeRuntimeTemplate<TAuthoring>(Baker<TAuthoring> baker, SourceRuntimeTemplateAuthoringBase authoring)
            where TAuthoring : SourceRuntimeTemplateAuthoringBase
        {
            var e = baker.GetEntity(TransformUsageFlags.Dynamic);

            baker.AddComponent(e, new SourceSpawnComponent
            {
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });

            uint stableId = authoring.StableIdOverride > 0
                ? authoring.StableIdOverride
                : ComputeStableSourceId(authoring.transform, authoring.transform.position);
            baker.AddComponent(e, new SourceStableIdComponent { Value = stableId });

            var fieldShape = new Shape2DComponent
            {
                Kind = authoring.Shape,
                Radius = Mathf.Max(0f, authoring.Radius),
                Size = new float2(Mathf.Max(0f, authoring.Size.x), Mathf.Max(0f, authoring.Size.y)),
            };
            var derived = default(SourceShapeDerivedComponent);
            SourceRuntimeApplyUtility.RefreshSourceShapeDerived(in fieldShape, ref derived);
            baker.AddComponent<BulletFieldAreaComponent>(e);
            baker.AddComponent(e, fieldShape);
            baker.AddComponent(e, derived);

            baker.AddComponent(e, new SourceSpawnRuntimeComponent { SpawnSequence = 1u });

            float cellSize = Mathf.Max(0.1f, authoring.PollutionCellSize);
            float minValue = Mathf.Max(0f, authoring.PollutionMin);
            float maxValue = Mathf.Max(minValue, authoring.PollutionMax);

            var pollutionConfig = new SourcePollutionConfigComponent
            {
                MinValue = minValue,
                MaxValue = maxValue,
                RegenPerSec = Mathf.Max(0f, authoring.PollutionRegenPerSec),
                DropPerCollect = Mathf.Max(0f, authoring.PollutionDropPerCollect),
                TopKSampleCount = Mathf.Max(1, authoring.PollutionTopKSampleCount),
                ActiveRatioThreshold = Mathf.Clamp01(authoring.PollutionActiveRatioThreshold),
                RecoveryCooldownFrames = (uint)Mathf.Max(0, authoring.PollutionRecoveryCooldownFrames),
                RecoveryWaveSeedCount = Mathf.Max(1, authoring.PollutionRecoveryWaveSeedCount),
                RecoveryWaveClusterSize = Mathf.Max(1, authoring.PollutionRecoveryWaveClusterSize),
                RecoveryWaveRestoreValue = Mathf.Clamp(authoring.PollutionRecoveryRestoreValue, minValue, maxValue),
                RecoveryRecentCleanBiasFrames = (uint)Mathf.Max(0, authoring.PollutionRecoveryRecentCleanBiasFrames),
            };
            baker.AddComponent(e, pollutionConfig);

            var pollutionGrid = new SourcePollutionGridComponent
            {
                CellSize = cellSize,
                HalfExtents = derived.HalfExtents,
                OriginX = -derived.HalfExtents.x,
                OriginZ = -derived.HalfExtents.y,
            };

            baker.AddBuffer<SourceActiveBulletCountBuffer>(e).Clear();
            baker.AddBuffer<SourceSpawnRequestBuffer>(e).Clear();
            baker.AddBuffer<SourceClipPatternBuffer>(e).Clear();
            baker.AddBuffer<SourceSustainSlotCandidateBuffer>(e).Clear();
            baker.AddBuffer<SourceSustainRuntimeLaneBuffer>(e).Clear();
            baker.AddBuffer<SourceEventQueueBuffer>(e).Clear();
            baker.AddBuffer<SourceHazardActorRefBuffer>(e).Clear();

            baker.AddComponent(e, new SourceSustainRuntimeComponent
            {
                ActiveState = SourceStateId.Normal,
            });

            baker.AddComponent(e, new SourceEventRuntimeComponent
            {
                IsPlaying = 0,
                ActiveEventClipId = 0,
                TriggerState = SourceStateId.Normal,
                ElapsedSec = 0f,
                SelectionSequence = 1u,
            });

            baker.AddComponent(e, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Baseline,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 1u,
            });
            var pressureInputs = baker.AddBuffer<SourceDirectorPressureInputBuffer>(e);
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);

            var pollutionCells = baker.AddBuffer<SourcePollutionCellBuffer>(e);
            var pollutionDrops = baker.AddBuffer<SourcePollutionDropRequestBuffer>(e);
            var pollutionValidCellIndices = baker.AddBuffer<SourcePollutionValidCellIndexBuffer>(e);
            baker.AddBuffer<SourceRegionCellIndexBuffer>(e).Clear();
            SourceRuntimeApplyUtility.RebuildPollutionGrid(
                in fieldShape,
                in derived,
                in pollutionConfig,
                ref pollutionGrid,
                pollutionCells,
                pollutionDrops,
                pollutionValidCellIndices);
            pollutionGrid.OriginX = authoring.transform.position.x - pollutionGrid.HalfExtents.x;
            pollutionGrid.OriginZ = authoring.transform.position.z - pollutionGrid.HalfExtents.y;
            baker.AddComponent(e, pollutionGrid);

            baker.AddComponent(e, new SourceAnchorComponent
            {
                Position = (float3)authoring.transform.position,
            });
        }

        private static uint ComputeStableSourceId(Transform sourceTransform, Vector3 sourcePosition)
        {
            if (sourceTransform == null)
                return 1u;

            var sb = new StringBuilder(128);
            AppendHierarchyPath(sb, sourceTransform);

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
            if (Shape == Shape2DKind.Rectangle)
            {
                var size = new Vector3(Mathf.Max(0f, Size.x), 0f, Mathf.Max(0f, Size.y));
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = prevMatrix;
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, Radius));
            }

            Gizmos.color = prev;
            Gizmos.matrix = prevMatrix;
        }
    }
}
