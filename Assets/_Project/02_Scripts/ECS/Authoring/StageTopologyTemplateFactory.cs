using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    internal static class StageTopologyTemplateFactory
    {
        public static Entity CreateSourceTemplate(EntityManager em)
        {
            var entity = em.CreateEntity(
                typeof(Prefab),
                typeof(SourceStableIdComponent),
                typeof(SourceSpawnComponent),
                typeof(SourceSpawnRuntimeComponent),
                typeof(SourceAnchorComponent),
                typeof(BulletFieldAreaComponent),
                typeof(Shape2DComponent),
                typeof(SourceShapeDerivedComponent),
                typeof(SourcePollutionConfigComponent),
                typeof(SourcePollutionGridComponent),
                typeof(SourceSustainRuntimeComponent),
                typeof(SourceEventRuntimeComponent),
                typeof(SourceRunDirectorStateComponent),
                typeof(LocalTransform));

            em.SetComponentData(entity, new SourceStableIdComponent { Value = 1u });
            em.SetComponentData(entity, new SourceSpawnComponent
            {
                ThresholdWeakened = 2000,
                ThresholdDepleted = 4000,
                CollectedCount = 0,
                State = SourceStateId.Normal,
            });
            em.SetComponentData(entity, new SourceSpawnRuntimeComponent { SpawnSequence = 1u });
            em.SetComponentData(entity, new SourceAnchorComponent { Position = float3.zero });
            var sourceShape = new Shape2DComponent
            {
                Kind = Shape2DKind.Circle,
                Radius = 8f,
                Size = new float2(12f, 8f),
            };
            var sourceDerived = default(SourceShapeDerivedComponent);
            SourceRuntimeApplyUtility.RefreshSourceShapeDerived(in sourceShape, ref sourceDerived);
            em.SetComponentData(entity, sourceShape);
            em.SetComponentData(entity, sourceDerived);
            em.SetComponentData(entity, new SourcePollutionConfigComponent
            {
                MinValue = 0f,
                MaxValue = 1f,
                RegenPerSec = 0.08f,
                DropPerCollect = 0.12f,
                TopKSampleCount = 6,
                ActiveRatioThreshold = 0.35f,
                RecoveryCooldownFrames = 45u,
                RecoveryWaveSeedCount = 2,
                RecoveryWaveClusterSize = 4,
                RecoveryWaveRestoreValue = 0.4f,
                RecoveryRecentCleanBiasFrames = 90u,
            });
            em.SetComponentData(entity, new SourcePollutionGridComponent
            {
                CellSize = 2f,
                InvCellSize = 0.5f,
                HalfExtents = sourceDerived.HalfExtents,
                OriginX = -sourceDerived.HalfExtents.x,
                OriginZ = -sourceDerived.HalfExtents.y,
                Cols = 1,
                Rows = 1,
            });
            em.SetComponentData(entity, new SourceSustainRuntimeComponent { ActiveState = SourceStateId.Normal });
            em.SetComponentData(entity, new SourceEventRuntimeComponent
            {
                IsPlaying = 0,
                ActiveEventClipId = 0,
                TriggerState = SourceStateId.Normal,
                ElapsedSec = 0f,
                SelectionSequence = 1u,
            });
            em.SetComponentData(entity, new SourceRunDirectorStateComponent
            {
                State = RunDirectorSourceStateId.Baseline,
                SelectedClipState = SourceStateId.Normal,
                PressureOccupancySec = 0f,
                DensityScale = 1f,
                Version = 1u,
            });
            em.SetComponentData(entity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));

            em.AddBuffer<SourceSpawnRequestBuffer>(entity).Clear();
            em.AddBuffer<SourceClipPatternBuffer>(entity).Clear();
            em.AddBuffer<SourceSustainSlotCandidateBuffer>(entity).Clear();
            em.AddBuffer<SourceSustainRuntimeLaneBuffer>(entity).Clear();
            em.AddBuffer<SourceEventQueueBuffer>(entity).Clear();
            em.AddBuffer<SourceActiveBulletCountBuffer>(entity).Clear();
            var pressureInputs = em.AddBuffer<SourceDirectorPressureInputBuffer>(entity);
            SourceRuntimeApplyUtility.ResetPressureInputs(pressureInputs);
            em.AddBuffer<SourcePollutionCellBuffer>(entity).Clear();
            em.AddBuffer<SourcePollutionDropRequestBuffer>(entity).Clear();
            em.AddBuffer<SourcePollutionValidCellIndexBuffer>(entity).Clear();
            em.AddBuffer<SourceRegionCellIndexBuffer>(entity).Clear();
            return entity;
        }

    }
}
