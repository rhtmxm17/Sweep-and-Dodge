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
            return CreateSourceTemplate(em, null);
        }

        public static Entity CreateSourceTemplate(EntityManager em, StageTopologyPrefabCatalogSO catalog)
        {
            var entity = CreateDefaultSourceTemplate(em);
            if (catalog != null && catalog.SourceTemplatePrefab != null)
                TryAttachHazardHierarchyFromCatalogPrefab(em, entity, catalog.SourceTemplatePrefab);

            return entity;
        }

        private static Entity CreateDefaultSourceTemplate(EntityManager em)
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
            em.AddBuffer<SourceHazardActorRefBuffer>(entity).Clear();
            var linkedEntities = em.AddBuffer<LinkedEntityGroup>(entity);
            linkedEntities.Add(entity);
            return entity;
        }

        private static bool TryAttachHazardHierarchyFromCatalogPrefab(
            EntityManager em,
            Entity sourceEntity,
            GameObject sourceTemplatePrefab)
        {
            if (sourceEntity == Entity.Null || !em.Exists(sourceEntity) || sourceTemplatePrefab == null)
                return false;

            var sourceAuthoring = sourceTemplatePrefab.GetComponentInChildren<SourceRuntimeTemplateAuthoringBase>(true);
            if (sourceAuthoring == null)
                return false;

            if (!em.HasBuffer<SourceHazardActorRefBuffer>(sourceEntity))
                em.AddBuffer<SourceHazardActorRefBuffer>(sourceEntity);
            if (!em.HasBuffer<LinkedEntityGroup>(sourceEntity))
            {
                var linked = em.AddBuffer<LinkedEntityGroup>(sourceEntity);
                linked.Add(sourceEntity);
            }

            em.GetBuffer<SourceHazardActorRefBuffer>(sourceEntity).Clear();
            var sourceLinkedGroup = em.GetBuffer<LinkedEntityGroup>(sourceEntity);
            if (sourceLinkedGroup.Length == 0)
                sourceLinkedGroup.Add(sourceEntity);

            var actors = sourceAuthoring.GetComponentsInChildren<HazardActorAuthoring>(true);
            for (int i = 0; i < actors.Length; i++)
            {
                var actorAuthoring = actors[i];
                if (actorAuthoring == null)
                    continue;

                var parentSource = actorAuthoring.GetComponentInParent<SourceRuntimeTemplateAuthoringBase>(true);
                if (parentSource != sourceAuthoring)
                    continue;

                var actorEntity = em.CreateEntity(
                    typeof(Prefab),
                    typeof(HazardActorComponent),
                    typeof(HazardActorAppliedConfigBaselineComponent),
                    typeof(HazardActorAppliedConfigComponent),
                    typeof(HazardActorRuntimeBaselineComponent),
                    typeof(HazardActorRuntimeStateComponent),
                    typeof(HazardActorPatternSelectorStateComponent));

                int actorId = math.max(1, actorAuthoring.ActorId);
                byte actorEnabled = actorAuthoring.Enabled ? (byte)1 : (byte)0;
                byte actorSuppressed = actorAuthoring.StartSuppressed ? (byte)1 : (byte)0;

                em.SetComponentData(actorEntity, new HazardActorComponent
                {
                    ActorId = actorId,
                    SourceEntity = sourceEntity,
                });
                em.SetComponentData(actorEntity, new HazardActorAppliedConfigBaselineComponent
                {
                    IsEnabled = actorEnabled,
                    IsSuppressed = actorSuppressed,
                });
                em.SetComponentData(actorEntity, new HazardActorAppliedConfigComponent
                {
                    IsEnabled = actorEnabled,
                    IsSuppressed = actorSuppressed,
                });
                em.AddComponentData(actorEntity, new HazardActorPresencePolicyComponent
                {
                    ActivationTrigger = HazardActorPresenceTriggerMode.Immediate,
                    ActivationDurationSec = 0f,
                    RetireTrigger = HazardActorPresenceTriggerMode.None,
                    RetireDurationSec = 0f,
                });
                em.SetComponentData(actorEntity, new HazardActorRuntimeBaselineComponent
                {
                    InitialPresenceState = actorAuthoring.InitialPresenceState,
                });
                em.SetComponentData(actorEntity, new HazardActorRuntimeStateComponent
                {
                    PresenceState = actorAuthoring.InitialPresenceState,
                    StateElapsedSec = 0f,
                });
                em.SetComponentData(actorEntity, new HazardActorPatternSelectorStateComponent
                {
                    TargetEmitterId = -1,
                    CurrentPatternSlotId = -1,
                    LastPatternSlotId = -1,
                    SelectionSequence = 0u,
                });
                em.AddBuffer<HazardActorEmitterRefBuffer>(actorEntity).Clear();

                em.GetBuffer<SourceHazardActorRefBuffer>(sourceEntity).Add(new SourceHazardActorRefBuffer
                {
                    ActorEntity = actorEntity,
                    ActorId = actorId,
                });
                em.GetBuffer<LinkedEntityGroup>(sourceEntity).Add(actorEntity);

                var emitters = actorAuthoring.GetComponentsInChildren<HazardEmitterAuthoring>(true);
                for (int emitterIndex = 0; emitterIndex < emitters.Length; emitterIndex++)
                {
                    var emitterAuthoring = emitters[emitterIndex];
                    if (emitterAuthoring == null)
                        continue;

                    var parentActor = emitterAuthoring.GetComponentInParent<HazardActorAuthoring>(true);
                    if (parentActor != actorAuthoring)
                        continue;

                    if (!HazardEmitterProfileResolver.TryResolve(emitterAuthoring.EmissionProfile, out var resolvedEmission, out var error))
                    {
                        Debug.LogWarning($"[StageTopologyTemplateFactory] Failed to resolve HazardEmitter profile while creating runtime template. emitter={emitterAuthoring.name}, error={error}", emitterAuthoring);
                        continue;
                    }

                    int telegraphProfileRefId = emitterAuthoring.TelegraphProfile != null ? emitterAuthoring.TelegraphProfile.GetInstanceID() : 0;
                    int emissionProfileRefId = emitterAuthoring.EmissionProfile != null ? emitterAuthoring.EmissionProfile.GetInstanceID() : 0;
                    byte emitterEnabled = emitterAuthoring.IsEnabled ? (byte)1 : (byte)0;
                    byte emitterSuppressed = emitterAuthoring.StartSuppressed ? (byte)1 : (byte)0;
                    int emitterId = math.max(1, emitterAuthoring.EmitterId);

                    var emitterEntity = em.CreateEntity(
                        typeof(Prefab),
                        typeof(HazardEmitterComponent),
                        typeof(HazardEmitterAppliedConfigBaselineComponent),
                        typeof(HazardEmitterAppliedConfigComponent),
                        typeof(HazardEmitterTelegraphProfileBaselineComponent),
                        typeof(HazardEmitterTelegraphProfileComponent),
                        typeof(HazardEmitterEmissionProfileBaselineComponent),
                        typeof(HazardEmitterEmissionProfileComponent),
                        typeof(HazardEmitterRuntimeStateComponent));

                    var baselineConfig = new HazardEmitterAppliedConfigBaselineComponent
                    {
                        IsEnabled = emitterEnabled,
                        IsSuppressed = emitterSuppressed,
                        LocalOffset = emitterAuthoring.LocalOffset,
                        TelegraphProfileRefId = telegraphProfileRefId,
                        EmissionProfileRefId = emissionProfileRefId,
                    };
                    var baselineTelegraph = new HazardEmitterTelegraphProfileBaselineComponent
                    {
                        ProfileId = telegraphProfileRefId,
                        TelegraphDurationSec = emitterAuthoring.TelegraphProfile != null
                            ? math.max(0f, emitterAuthoring.TelegraphProfile.TelegraphDurationSec)
                            : 0f,
                    };
                    var baselineEmission = new HazardEmitterEmissionProfileBaselineComponent
                    {
                        ProfileId = emissionProfileRefId,
                        BulletTypeKey = resolvedEmission.Bullet.DefinitionId,
                        PositionPatternMode = resolvedEmission.PositionPatternMode,
                        SpawnOffset = resolvedEmission.SpawnOffset,
                        LineStart = resolvedEmission.LineStart,
                        LineEnd = resolvedEmission.LineEnd,
                        SampleSpacing = resolvedEmission.SampleSpacing,
                        PointSetCount = resolvedEmission.PointSetCount,
                        Point0 = resolvedEmission.Point0,
                        Point1 = resolvedEmission.Point1,
                        Point2 = resolvedEmission.Point2,
                        Point3 = resolvedEmission.Point3,
                        AimMode = resolvedEmission.AimMode,
                        AimSnapshotTiming = resolvedEmission.AimSnapshotTiming,
                        BaseAngleDeg = resolvedEmission.BaseAngleDeg,
                        AimAngleOffsetDeg = resolvedEmission.AimAngleOffsetDeg,
                        LineNormalSide = resolvedEmission.LineNormalSide,
                        LineNormalAngleOffsetDeg = resolvedEmission.LineNormalAngleOffsetDeg,
                        SpiralStepDeg = resolvedEmission.SpiralStepDeg,
                        ShotPatternMode = resolvedEmission.ShotPatternMode,
                        ShotCount = resolvedEmission.ShotCount,
                        NWayAngleSpacingDeg = resolvedEmission.NWayAngleSpacingDeg,
                        EventShotSchedule = resolvedEmission.EventShotSchedule,
                        EventShotIntervalSec = resolvedEmission.EventShotIntervalSec,
                        EventRepeatCount = resolvedEmission.EventRepeatCount,
                        CooldownSec = resolvedEmission.CooldownSec,
                    };

                    em.SetComponentData(emitterEntity, new HazardEmitterComponent
                    {
                        EmitterId = emitterId,
                        ActorEntity = actorEntity,
                        ActivationPolicy = emitterAuthoring.ActivationPolicy,
                        InitialLifecycleState = HazardEmitterLifecycleStateId.Dormant,
                        AnchorKind = emitterAuthoring.AnchorKind,
                        Mobility = emitterAuthoring.Mobility,
                    });
                    em.SetComponentData(emitterEntity, baselineConfig);
                    em.SetComponentData(emitterEntity, new HazardEmitterAppliedConfigComponent
                    {
                        IsEnabled = baselineConfig.IsEnabled,
                        IsSuppressed = baselineConfig.IsSuppressed,
                        LocalOffset = baselineConfig.LocalOffset,
                        TelegraphProfileRefId = baselineConfig.TelegraphProfileRefId,
                        EmissionProfileRefId = baselineConfig.EmissionProfileRefId,
                    });
                    em.SetComponentData(emitterEntity, baselineTelegraph);
                    em.SetComponentData(emitterEntity, new HazardEmitterTelegraphProfileComponent
                    {
                        ProfileId = baselineTelegraph.ProfileId,
                        TelegraphDurationSec = baselineTelegraph.TelegraphDurationSec,
                    });
                    em.SetComponentData(emitterEntity, baselineEmission);
                    em.SetComponentData(emitterEntity, new HazardEmitterEmissionProfileComponent
                    {
                        ProfileId = baselineEmission.ProfileId,
                        BulletTypeKey = baselineEmission.BulletTypeKey,
                        PositionPatternMode = baselineEmission.PositionPatternMode,
                        SpawnOffset = baselineEmission.SpawnOffset,
                        LineStart = baselineEmission.LineStart,
                        LineEnd = baselineEmission.LineEnd,
                        SampleSpacing = baselineEmission.SampleSpacing,
                        PointSetCount = baselineEmission.PointSetCount,
                        Point0 = baselineEmission.Point0,
                        Point1 = baselineEmission.Point1,
                        Point2 = baselineEmission.Point2,
                        Point3 = baselineEmission.Point3,
                        AimMode = baselineEmission.AimMode,
                        AimSnapshotTiming = baselineEmission.AimSnapshotTiming,
                        BaseAngleDeg = baselineEmission.BaseAngleDeg,
                        AimAngleOffsetDeg = baselineEmission.AimAngleOffsetDeg,
                        LineNormalSide = baselineEmission.LineNormalSide,
                        LineNormalAngleOffsetDeg = baselineEmission.LineNormalAngleOffsetDeg,
                        SpiralStepDeg = baselineEmission.SpiralStepDeg,
                        ShotPatternMode = baselineEmission.ShotPatternMode,
                        ShotCount = baselineEmission.ShotCount,
                        NWayAngleSpacingDeg = baselineEmission.NWayAngleSpacingDeg,
                        EventShotSchedule = baselineEmission.EventShotSchedule,
                        EventShotIntervalSec = baselineEmission.EventShotIntervalSec,
                        EventRepeatCount = baselineEmission.EventRepeatCount,
                        CooldownSec = baselineEmission.CooldownSec,
                    });
                    em.SetComponentData(emitterEntity, new HazardEmitterRuntimeStateComponent
                    {
                        LifecycleState = HazardEmitterLifecycleStateId.Dormant,
                        StateElapsedSec = 0f,
                    });

                    em.GetBuffer<HazardActorEmitterRefBuffer>(actorEntity).Add(new HazardActorEmitterRefBuffer
                    {
                        EmitterEntity = emitterEntity,
                        EmitterId = emitterId,
                    });
                    em.GetBuffer<LinkedEntityGroup>(sourceEntity).Add(emitterEntity);
                }
            }

            return em.GetBuffer<SourceHazardActorRefBuffer>(sourceEntity).Length > 0;
        }

    }
}
