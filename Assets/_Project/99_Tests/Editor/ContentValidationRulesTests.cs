using System.Collections.Generic;
using System.Linq;
using System;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SweepNDodge.DotsBullets.Tests
{
    public class ContentValidationRulesTests
    {
        [Test]
        public void DuplicateDefinitionId_IsTreatedAsError()
        {
            var defA = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var defB = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                defA.Editor_SetDefinitionId(1001);
                defB.Editor_SetDefinitionId(1001);
                defA.Prefab = prefab;
                defB.Prefab = prefab;

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(defA, "defA"),
                        new ContentValidationRecord<BulletDefinitionSO>(defB, "defB"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var duplicateErrors = issues.Where(i => i.Code == "CV001").ToArray();

                Assert.That(duplicateErrors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(duplicateErrors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(defA);
                Object.DestroyImmediate(defB);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void StageTopologyPrefabCatalog_WithMissingSourceTemplate_IsError()
        {
            var catalog = ScriptableObject.CreateInstance<StageTopologyPrefabCatalogSO>();

            try
            {
                var input = new ContentValidationInput(
                    null,
                    null,
                    new List<ContentValidationRecord<StageTopologyPrefabCatalogSO>>
                    {
                        new ContentValidationRecord<StageTopologyPrefabCatalogSO>(catalog, "topology_catalog"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV030").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void StageTopologyPrefabCatalog_WithOnlySourceTemplate_IsAllowed()
        {
            var catalog = ScriptableObject.CreateInstance<StageTopologyPrefabCatalogSO>();
            catalog.SourceTemplatePrefab = new GameObject("source_template");

            try
            {
                var input = new ContentValidationInput(
                    null,
                    null,
                    new List<ContentValidationRecord<StageTopologyPrefabCatalogSO>>
                    {
                        new ContentValidationRecord<StageTopologyPrefabCatalogSO>(catalog, "topology_catalog"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV030"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog.SourceTemplatePrefab);
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AutoCorrectionInputs_AreReportedAsWarningsOnly()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<SourceRuntimeTemplateAuthoring>();

            try
            {
                def.Editor_SetDefinitionId(2001);
                def.Prefab = new GameObject("bullet_prefab");
                def.PoolSize = -1;
                def.Speed = -2f;
                def.Lifetime = -3f;
                def.Radius = -0.5f;
                def.ScoreValue = -7;

                sourceAuthoring.Radius = -4f;
                sourceAuthoring.Size = new Vector2(-5f, -6f);
                sourceAuthoring.PollutionCellSize = 0.01f;
                sourceAuthoring.PollutionMin = -1f;
                sourceAuthoring.PollutionMax = -2f;
                sourceAuthoring.PollutionRegenPerSec = -3f;
                sourceAuthoring.PollutionDropPerCollect = -4f;
                sourceAuthoring.PollutionTopKSampleCount = 0;
                sourceAuthoring.PollutionActiveRatioThreshold = 2f;
                sourceAuthoring.PollutionRecoveryCooldownFrames = -1;
                sourceAuthoring.PollutionRecoveryWaveSeedCount = 0;
                sourceAuthoring.PollutionRecoveryWaveClusterSize = 0;
                sourceAuthoring.PollutionRecoveryRestoreValue = -1f;
                sourceAuthoring.PollutionRecoveryRecentCleanBiasFrames = -1;

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    new List<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>>
                    {
                        new ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>(sourceAuthoring, "source"),
                    },
                    null);

                var issues = ContentValidationRules.Validate(input);
                var warnings = issues.Where(i => i.Code.StartsWith("CVW")).ToArray();
                var warningErrors = warnings.Where(i => i.Severity == ContentValidationSeverity.Error).ToArray();

                Assert.That(warnings.Length, Is.GreaterThan(0));
                Assert.That(warningErrors.Length, Is.EqualTo(0));
            }
            finally
            {
                if (def.Prefab != null)
                    Object.DestroyImmediate(def.Prefab);
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(sourceRoot);
            }
        }

        [Test]
        public void Definition_DampedLinearWithNegativeParameters_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2201);
                def.Prefab = prefab;
                def.MovementFamily = BulletMovementFamilyId.DampedLinear;
                def.DampedLinear = new BulletDampedLinearDefinition
                {
                    DampingPerSec = -1f,
                    StopSpeedThreshold = -0.1f,
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV031"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Definition_HomingLiteWithInvalidRange_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2202);
                def.Prefab = prefab;
                def.MovementFamily = BulletMovementFamilyId.HomingLite;
                def.HomingLite = new BulletHomingLiteDefinition
                {
                    TurnRateDegPerSec = 45f,
                    MaxAcquireDistance = 1f,
                    MinRetargetDistance = 2f,
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV032"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Definition_ReactionWithUnknownSecondaryBulletReference_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var secondary = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");
            var secondaryPrefab = new GameObject("secondary_bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2203);
                def.Prefab = prefab;
                secondary.Editor_SetDefinitionId(9999);
                secondary.Prefab = secondaryPrefab;
                def.OnCleanupRemovedSpawnSecondary = new BulletSecondarySpawnReactionDefinition
                {
                    Enabled = true,
                    SecondaryBullet = secondary,
                    SpawnCount = 1,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    SpreadAngleDeg = 45f,
                    SpawnRadius = 1f,
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV035"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(secondary);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(secondaryPrefab);
            }
        }

        [Test]
        public void Definition_ReactionWithNullSecondaryBullet_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2205);
                def.Prefab = prefab;
                def.OnCleanupRemovedSpawnSecondary = new BulletSecondarySpawnReactionDefinition
                {
                    Enabled = true,
                    SecondaryBullet = null,
                    SpawnCount = 1,
                    Shape = BulletSecondarySpawnShapeId.PointBurst,
                    SpreadAngleDeg = 45f,
                    SpawnRadius = 1f,
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV033"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Definition_PrefabWithForbiddenOptionalBehaviorAuthoring_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2204);
                def.Prefab = prefab;
                prefab.AddComponent<BulletDampedMotionAuthoring>();

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV034"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Definition_ReactionWithKnownSecondaryBulletReference_IsAllowed()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var secondary = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");
            var secondaryPrefab = new GameObject("secondary_bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(2206);
                def.Prefab = prefab;
                secondary.Editor_SetDefinitionId(2207);
                secondary.Prefab = secondaryPrefab;
                def.OnMotionCompletedExplode = new BulletSecondarySpawnReactionDefinition
                {
                    Enabled = true,
                    SecondaryBullet = secondary,
                    SpawnCount = 2,
                    Shape = BulletSecondarySpawnShapeId.ForwardSpread,
                    SpreadAngleDeg = 60f,
                    SpawnRadius = 0.5f,
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                        new ContentValidationRecord<BulletDefinitionSO>(secondary, "secondary"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV033" || i.Code == "CV035"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(secondary);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(secondaryPrefab);
            }
        }

        [Test]
        public void SourceAuthoring_WithoutAnyWaveClipBinding_IsNotAnError()
        {
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<SourceRuntimeTemplateAuthoring>();

            try
            {
                sourceAuthoring.SustainClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.SustainClipSlotAuthoring>();
                sourceAuthoring.EventClipSlots = System.Array.Empty<SourceRuntimeTemplateAuthoringBase.EventClipSlotAuthoring>();
                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>>
                    {
                        new ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>(sourceAuthoring, "source"),
                    },
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV006").ToArray();
                Assert.That(errors.Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(sourceRoot);
            }
        }

        [Test]
        public void BulletAuthoring_WithoutRenderParts_IsError()
        {
            var root = new GameObject("bullet_root");
            var bullet = root.AddComponent<BulletAuthoring>();

            try
            {
                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<BulletAuthoring>>
                    {
                        new ContentValidationRecord<BulletAuthoring>(bullet, "bullet"),
                    });

                var issues = ContentValidationRules.Validate(input);
                var renderErrors = issues.Where(i => i.Code == "CV007").ToArray();
                Assert.That(renderErrors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(renderErrors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WaveClip_WithNegativeSpawnDensity_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3001);
                def.Prefab = prefab;

                var entry = CreateDefaultTypedEntry(def);
                ((RateFieldEmissionAuthoring)entry.Emission).RatePerSecPerArea = -1f;

                clip.ClipId = 11;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV015").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_PoissonWithNonPositiveEventRepeatCount_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3002);
                def.Prefab = prefab;

                var entry = CreateDefaultTypedEntry(def);
                entry.Emission = new PoissonEmissionAuthoring
                {
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    MaxActiveDensityPerArea = 0f,
                    MeanEventsPerSec = 1f,
                    EventRepeatCount = 0,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                };

                clip.ClipId = 12;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV022"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_SpawnSampleBudgetZero_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3003);
                def.Prefab = prefab;

                var entry = CreateDefaultTypedEntry(def);
                entry.Sampling.SpawnSampleBudget = 0;

                clip.ClipId = 13;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV018"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_NWayShotPatternCountTooLow_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3004);
                def.Prefab = prefab;

                var entry = CreateDefaultTypedEntry(def);
                entry.Aim = new FixedAimAuthoring { BaseAngleDeg = 15f };
                entry.ShotPattern = new NWayShotPatternAuthoring { ShotCount = 1 };

                clip.ClipId = 14;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV023"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_PlayerPositionAimWithUnsupportedTiming_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3005);
                def.Prefab = prefab;

                var entry = CreateDefaultTypedEntry(def);
                entry.Aim = new PlayerPositionAimAuthoring
                {
                    AngleOffsetDeg = 10f,
                    SnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                };

                clip.ClipId = 15;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV041"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClipResolver_PlayerPositionAimEventStart_ResolvesCanonicalSnapshot()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();

            try
            {
                def.Editor_SetDefinitionId(3006);
                var entry = CreateDefaultTypedEntry(def);
                entry.Emission = new EventBurstEmissionAuthoring
                {
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    MaxActiveDensityPerArea = 0f,
                    BurstRepeatCount = 3,
                    BurstIntervalSec = 0.5f,
                    EventRepeatCount = 2,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.15f,
                };
                entry.Sampling = new WaveSamplingAuthoring
                {
                    SpawnSampleBudget = 12,
                    PlayerNoSpawnRadius = 1.25f,
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new CenterPointAreaSamplerAuthoring(),
                };
                entry.PositionPattern = new PointSetPositionPatternAuthoring
                {
                    Points = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one, new Vector2(-1f, 0f) }
                };
                entry.Aim = new PlayerPositionAimAuthoring
                {
                    AngleOffsetDeg = 22f,
                    SnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                };
                entry.ShotPattern = new RadialShotPatternAuthoring
                {
                    ShotCount = 6,
                };

                Assert.That(WaveClipAuthoringResolver.TryResolveTypedEntry(entry, out var snapshot, out var error), Is.True, error);
                Assert.That(snapshot.EmissionMode, Is.EqualTo(SourceSpawnEmissionModeId.EventBurst));
                Assert.That(snapshot.EventRepeatCount, Is.EqualTo(2));
                Assert.That(snapshot.PositionPatternMode, Is.EqualTo(WavePositionPatternModeId.PointSet));
                Assert.That(snapshot.PointSetCount, Is.EqualTo(4));
                Assert.That(snapshot.AimMode, Is.EqualTo(WaveAimModeId.PlayerPosition));
                Assert.That(snapshot.AimSnapshotTiming, Is.EqualTo(WaveAimSnapshotTimingId.EventStart));
                Assert.That(snapshot.ShotPatternMode, Is.EqualTo(WaveShotPatternModeId.Radial));
                Assert.That(snapshot.ShotCount, Is.EqualTo(6));
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void WaveClip_OverlappingSegments_IsAllowed()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(4001);
                def.Prefab = prefab;
                var entry = CreateDefaultTypedEntry(def);

                clip.ClipId = 21;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 5f,
                        Directives = new[] { entry }
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 4f,
                        EndSec = 8f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV011").ToArray();
                Assert.That(errors.Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_BoundaryTouch_IsNotOverlapError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(4002);
                def.Prefab = prefab;
                var entry = CreateDefaultTypedEntry(def);

                clip.ClipId = 22;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 5f,
                        Directives = new[] { entry }
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 5f,
                        EndSec = 9f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV011").ToArray();
                Assert.That(errors.Length, Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_WithMissingSegmentsBuffer_IsError()
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();

            try
            {
                clip.Segments = null;
                var input = new ContentValidationInput(
                    null,
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV008").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void WaveClip_WithDuplicateClipId_IsError()
        {
            var clipA = ScriptableObject.CreateInstance<WaveClipSO>();
            var clipB = ScriptableObject.CreateInstance<WaveClipSO>();

            try
            {
                clipA.ClipId = 777;
                clipB.ClipId = 777;
                clipA.Segments = new[] { new WaveClipSO.ClipSegment { StartSec = 0f, EndSec = 1f, Directives = Array.Empty<WaveSpawnEntryAuthoring>() } };
                clipB.Segments = new[] { new WaveClipSO.ClipSegment { StartSec = 0f, EndSec = 1f, Directives = Array.Empty<WaveSpawnEntryAuthoring>() } };

                var input = new ContentValidationInput(
                    null,
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clipA, "clipA"),
                        new ContentValidationRecord<WaveClipSO>(clipB, "clipB"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV009").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(clipA);
                Object.DestroyImmediate(clipB);
            }
        }

        [Test]
        public void WaveClip_WithNonPositiveDefinitionId_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(0);
                def.Prefab = prefab;
                var entry = CreateDefaultTypedEntry(def);

                clip.ClipId = 91;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV027").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_PointSetWithNonPositivePointCount_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(9101);
                def.Prefab = prefab;
                var entry = CreatePointSetTypedEntry(def, Array.Empty<Vector2>());

                clip.ClipId = 191;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV028").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveClip_PointSetCountExceedsMax_IsWarning()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(9102);
                def.Prefab = prefab;
                var entry = CreatePointSetTypedEntry(
                    def,
                    new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                        Vector2.up,
                        Vector2.one,
                        new Vector2(-1f, 0f),
                    });

                clip.ClipId = 192;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var warnings = issues.Where(i => i.Code == "CVW033").ToArray();
                Assert.That(warnings.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(warnings.All(i => i.Severity == ContentValidationSeverity.Warning), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CleanupActionSet_DuplicateProfileKey_IsError()
        {
            var actionSet = ScriptableObject.CreateInstance<PlayerCleanupActionSetSO>();
            PlayerCleanupActionProfileDefinitionSO[] cleanupProfiles = null;

            try
            {
                actionSet.InitialSelectedProfileKey = "broom_default";
                actionSet.PrimarySlotProfileKey = "broom_default";
                actionSet.SecondarySlotProfileKey = "broom_default";
                cleanupProfiles = new PlayerCleanupActionProfileDefinitionSO[]
                {
                    CreateCleanupProfile("broom_default", PlayerCleanupActionId.BroomSweep),
                    CreateCleanupProfile("broom_default", PlayerCleanupActionId.BroomSweep),
                };
                actionSet.Profiles = cleanupProfiles;

                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<PlayerCleanupActionSetSO>>
                    {
                        new ContentValidationRecord<PlayerCleanupActionSetSO>(actionSet, "cleanup_set"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV037"), Is.True);
            }
            finally
            {
                DestroyCleanupProfiles(cleanupProfiles);
                Object.DestroyImmediate(actionSet);
            }
        }

        [Test]
        public void CleanupActionSet_InvalidProfileKey_IsError()
        {
            var actionSet = ScriptableObject.CreateInstance<PlayerCleanupActionSetSO>();
            PlayerCleanupActionProfileDefinitionSO[] cleanupProfiles = null;

            try
            {
                actionSet.InitialSelectedProfileKey = "broom_default";
                actionSet.PrimarySlotProfileKey = "broom_default";
                actionSet.SecondarySlotProfileKey = "broom_default";
                cleanupProfiles = new PlayerCleanupActionProfileDefinitionSO[]
                {
                    CreateCleanupProfile("BROOM_DEFAULT", PlayerCleanupActionId.BroomSweep),
                };
                actionSet.Profiles = cleanupProfiles;

                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<PlayerCleanupActionSetSO>>
                    {
                        new ContentValidationRecord<PlayerCleanupActionSetSO>(actionSet, "cleanup_set"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV037"), Is.True);
            }
            finally
            {
                DestroyCleanupProfiles(cleanupProfiles);
                Object.DestroyImmediate(actionSet);
            }
        }

        [Test]
        public void CleanupActionSet_MissingInitialOrSlotProfileKey_IsError()
        {
            var actionSet = ScriptableObject.CreateInstance<PlayerCleanupActionSetSO>();
            PlayerCleanupActionProfileDefinitionSO[] cleanupProfiles = null;

            try
            {
                actionSet.InitialSelectedProfileKey = "missing_profile";
                actionSet.PrimarySlotProfileKey = "broom_default";
                actionSet.SecondarySlotProfileKey = "missing_profile";
                cleanupProfiles = new PlayerCleanupActionProfileDefinitionSO[]
                {
                    CreateCleanupProfile("broom_default", PlayerCleanupActionId.BroomSweep),
                };
                actionSet.Profiles = cleanupProfiles;

                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<PlayerCleanupActionSetSO>>
                    {
                        new ContentValidationRecord<PlayerCleanupActionSetSO>(actionSet, "cleanup_set"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV038"), Is.True);
            }
            finally
            {
                DestroyCleanupProfiles(cleanupProfiles);
                Object.DestroyImmediate(actionSet);
            }
        }

        [Test]
        public void CleanupActionSet_InvalidProfilePayload_IsError()
        {
            var actionSet = ScriptableObject.CreateInstance<PlayerCleanupActionSetSO>();
            PlayerCleanupActionProfileDefinitionSO[] cleanupProfiles = null;

            try
            {
                var invalidProfile = CreateCleanupProfile("broom_default", PlayerCleanupActionId.BroomSweep) as BroomSweepCleanupActionProfileSO;
                Assert.That(invalidProfile, Is.Not.Null);
                invalidProfile.ActiveTime = -1f;
                invalidProfile.TrashSweepOuterRadius = 0f;

                actionSet.InitialSelectedProfileKey = "broom_default";
                actionSet.PrimarySlotProfileKey = "broom_default";
                actionSet.SecondarySlotProfileKey = "broom_default";
                cleanupProfiles = new PlayerCleanupActionProfileDefinitionSO[] { invalidProfile };
                actionSet.Profiles = cleanupProfiles;

                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<PlayerCleanupActionSetSO>>
                    {
                        new ContentValidationRecord<PlayerCleanupActionSetSO>(actionSet, "cleanup_set"),
                    },
                    null,
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV039"), Is.True);
            }
            finally
            {
                DestroyCleanupProfiles(cleanupProfiles);
                Object.DestroyImmediate(actionSet);
            }
        }

        [Test]
        public void PlayerProxyAuthoring_WithoutCleanupActionSet_IsError()
        {
            var root = new GameObject("player_proxy");
            var authoring = root.AddComponent<PlayerProxyAuthoring>();

            try
            {
                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<PlayerProxyAuthoring>>
                    {
                        new ContentValidationRecord<PlayerProxyAuthoring>(authoring, "player_proxy"),
                    });

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV036"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WaveClip_TypedEntryMissingEmission_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(9302);
                def.Prefab = prefab;
                var entry = CreateDefaultTypedEntry(def);
                entry.Emission = null;

                clip.ClipId = 193;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Directives = new[] { entry }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveClipSO>>
                    {
                        new ContentValidationRecord<WaveClipSO>(clip, "clip"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                Assert.That(issues.Any(i => i.Code == "CV040"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        private static WaveSpawnEntryAuthoring CreateDefaultTypedEntry(BulletDefinitionSO def)
        {
            return new WaveSpawnEntryAuthoring
            {
                Payload = new WaveClipSO.SpawnPayloadProfile
                {
                    Bullet = def,
                },
                Emission = new RateFieldEmissionAuthoring
                {
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    MaxActiveDensityPerArea = 0f,
                    RatePerSecPerArea = 1f,
                },
                Sampling = new WaveSamplingAuthoring
                {
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new UniformFieldAreaSamplerAuthoring(),
                },
                PositionPattern = new SinglePointPositionPatternAuthoring(),
                Aim = new RandomAimAuthoring(),
                ShotPattern = new SingleShotPatternAuthoring(),
            };
        }

        private static WaveSpawnEntryAuthoring CreatePointSetTypedEntry(BulletDefinitionSO def, Vector2[] points)
        {
            var entry = CreateDefaultTypedEntry(def);
            entry.Sampling.AreaSampler = new CenterPointAreaSamplerAuthoring();
            entry.PositionPattern = new PointSetPositionPatternAuthoring { Points = points };
            return entry;
        }

        private static PlayerCleanupActionProfileDefinitionSO CreateCleanupProfile(
            string profileKey,
            PlayerCleanupActionId actionKind)
        {
            PlayerCleanupActionProfileDefinitionSO profile = actionKind switch
            {
                PlayerCleanupActionId.BroomSweep => ScriptableObject.CreateInstance<BroomSweepCleanupActionProfileSO>(),
                PlayerCleanupActionId.RadialRing => ScriptableObject.CreateInstance<RadialRingCleanupActionProfileSO>(),
                PlayerCleanupActionId.ForwardFanLine => ScriptableObject.CreateInstance<ForwardFanLineCleanupActionProfileSO>(),
                _ => ScriptableObject.CreateInstance<BroomSweepCleanupActionProfileSO>(),
            };

            profile.ProfileKey = profileKey;
            profile.CaptureActiveTime = 0.2f;
            profile.CaptureCooldown = 0f;
            profile.ActiveTime = 0.22f;
            profile.Cooldown = 1.8f;
            profile.LockFacingWhileActive = true;
            profile.ActiveMoveSpeedScale = 0.5f;

            if (profile is BroomSweepCleanupActionProfileSO broomProfile)
            {
                broomProfile.TrashSweepInnerRadius = 1f;
                broomProfile.TrashSweepOuterRadius = 3.2f;
                broomProfile.TrashSweepHalfAngleDeg = 12f;
                broomProfile.TrashSweepStartAngleDeg = -20f;
                broomProfile.TrashSweepEndAngleDeg = 80f;
                broomProfile.HazardRectLength = 3.2f;
                broomProfile.HazardRectHalfWidth = 0.55f;
                broomProfile.HazardForwardWindowAngleDeg = 7f;
            }
            else if (profile is RadialRingCleanupActionProfileSO radialProfile)
            {
                radialProfile.TrashRange = 3.2f;
                radialProfile.HazardRingRadius = 2.88f;
                radialProfile.HazardRingWidth = 0.8f;
            }
            else if (profile is ForwardFanLineCleanupActionProfileSO forwardProfile)
            {
                forwardProfile.TrashRange = 3.2f;
                forwardProfile.TrashFanHalfAngleDeg = 180f;
                forwardProfile.HazardLineLength = 3.2f;
                forwardProfile.HazardLineHalfWidth = 0.5f;
            }

            return profile;
        }

        private static void DestroyCleanupProfiles(PlayerCleanupActionProfileDefinitionSO[] profiles)
        {
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Length; i++)
            {
                if (profiles[i] != null)
                    Object.DestroyImmediate(profiles[i]);
            }
        }
    }
}



