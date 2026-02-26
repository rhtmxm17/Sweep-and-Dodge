using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

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
        public void AutoCorrectionInputs_AreReportedAsWarningsOnly()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<BulletSourceAuthoring>();

            try
            {
                def.Editor_SetDefinitionId(2001);
                def.Prefab = new GameObject("bullet_prefab");
                def.PoolSize = -1;
                def.Speed = -2f;
                def.Lifetime = -3f;
                def.Radius = -0.5f;
                def.ScoreValue = -7;

                sourceAuthoring.SustainClipSlots = new[]
                {
                    new BulletSourceAuthoring.SustainClipSlotAuthoring
                    {
                        State = SourceStateId.Normal,
                        Lane = SourceSpawnLaneId.Hazard,
                        Clips = new[] { clip },
                        Weights = new[] { 1f },
                    }
                };
                sourceAuthoring.ThresholdWeakened = -1;
                sourceAuthoring.ThresholdDepleted = -2;
                sourceAuthoring.InitialCollectedCount = -3;
                sourceAuthoring.FieldRadius = -4f;
                sourceAuthoring.FieldSize = new Vector2(-5f, -6f);
                sourceAuthoring.PollutionCellSize = 0.01f;
                sourceAuthoring.PollutionMin = -1f;
                sourceAuthoring.PollutionMax = -2f;
                sourceAuthoring.PollutionRegenPerSec = -3f;
                sourceAuthoring.PollutionDropPerCollect = -4f;
                sourceAuthoring.PollutionTopKSampleCount = 0;

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    null,
                    null,
                    new List<ContentValidationRecord<BulletSourceAuthoring>>
                    {
                        new ContentValidationRecord<BulletSourceAuthoring>(sourceAuthoring, "source"),
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
        public void SourceAuthoring_WithoutAnyWaveClipBinding_IsError()
        {
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<BulletSourceAuthoring>();

            try
            {
                sourceAuthoring.SustainClipSlots = System.Array.Empty<BulletSourceAuthoring.SustainClipSlotAuthoring>();
                sourceAuthoring.EventClipSlots = System.Array.Empty<BulletSourceAuthoring.EventClipSlotAuthoring>();
                var input = new ContentValidationInput(
                    null,
                    null,
                    null,
                    new List<ContentValidationRecord<BulletSourceAuthoring>>
                    {
                        new ContentValidationRecord<BulletSourceAuthoring>(sourceAuthoring, "source"),
                    },
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV006").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
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

                var entry = CreateDefaultEntry(def);
                entry.Emission.EmissionMode = SourceSpawnEmissionModeId.RateField;
                entry.Emission.RatePerSecPerArea = -1f;

                clip.ClipId = 11;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[] { entry }
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
        public void WaveClip_OverlappingSegments_IsAllowed()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(4001);
                def.Prefab = prefab;
                var entry = CreateDefaultEntry(def);

                clip.ClipId = 21;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 5f,
                        Entries = new[] { entry }
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 4f,
                        EndSec = 8f,
                        Entries = new[] { entry }
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
                var entry = CreateDefaultEntry(def);

                clip.ClipId = 22;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 5f,
                        Entries = new[] { entry }
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 5f,
                        EndSec = 9f,
                        Entries = new[] { entry }
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
                clipA.Segments = new[] { new WaveClipSO.ClipSegment { StartSec = 0f, EndSec = 1f, Entries = new WaveClipSO.SpawnEntry[0] } };
                clipB.Segments = new[] { new WaveClipSO.ClipSegment { StartSec = 0f, EndSec = 1f, Entries = new WaveClipSO.SpawnEntry[0] } };

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
                var entry = CreateDefaultEntry(def);

                clip.ClipId = 91;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[] { entry }
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
                var entry = CreateDefaultEntry(def);
                entry.Sampling.SamplingMode = SourceSpawnSamplingModeId.PointSet;
                entry.Sampling.PointCount = 0;

                clip.ClipId = 191;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[] { entry }
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
                var entry = CreateDefaultEntry(def);
                entry.Sampling.SamplingMode = SourceSpawnSamplingModeId.PointSet;
                entry.Sampling.PointCount = WaveClipSO.SpawnSamplingProfile.PointSetMaxCount + 1;

                clip.ClipId = 192;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[] { entry }
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

        private static WaveClipSO.SpawnEntry CreateDefaultEntry(BulletDefinitionSO def)
        {
            return new WaveClipSO.SpawnEntry
            {
                Payload = new WaveClipSO.SpawnPayloadProfile
                {
                    Bullet = def,
                },
                Emission = new WaveClipSO.SpawnEmissionProfile
                {
                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    RatePerSecPerArea = 1f,
                    MeanEventsPerSec = 0f,
                    BurstRepeatCount = 1,
                    BurstIntervalSec = 1f,
                    BurstShotsPerEvent = 1,
                    MaxActiveDensityPerArea = 0f,
                },
                Sampling = new WaveClipSO.SpawnSamplingProfile
                {
                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                    FixedPoint = Vector2.zero,
                    SpawnOffset = Vector2.zero,
                    LineStart = Vector2.zero,
                    LineEnd = Vector2.zero,
                    SampleSpacing = 1f,
                    PointCount = 0,
                    Point0 = Vector2.zero,
                    Point1 = Vector2.zero,
                    Point2 = Vector2.zero,
                    Point3 = Vector2.zero,
                    SpawnSampleBudget = 16,
                    PlayerNoSpawnRadius = 0f,
                },
                Direction = new WaveClipSO.SpawnDirectionProfile
                {
                    DirectionMode = SourceSpawnDirectionModeId.Random,
                    BaseAngleDeg = 0f,
                    NWayCount = 1,
                    SpiralStepDeg = 0f,
                }
            };
        }
    }
}
