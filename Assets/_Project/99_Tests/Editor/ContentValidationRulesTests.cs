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
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<BulletSourceAuthoring>();
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();

            try
            {
                def.Editor_SetDefinitionId(2001);
                def.Prefab = new GameObject("bullet_prefab");
                def.PoolSize = -1;
                def.Speed = -2f;
                def.Lifetime = -3f;
                def.Radius = -0.5f;
                def.ScoreValue = -7;

                sourceAuthoring.WaveTimeline = timeline;
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
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(sourceRoot);
            }
        }

        [Test]
        public void SourceAuthoring_WithNullWaveTimeline_IsError()
        {
            var sourceRoot = new GameObject("source_root");
            var sourceAuthoring = sourceRoot.AddComponent<BulletSourceAuthoring>();

            try
            {
                sourceAuthoring.WaveTimeline = null;
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
        public void WaveTimeline_WithNegativeSpawnDensity_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3001);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 10,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = -1f,
                                    MaxActiveDensityPerArea = 0f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "timeline"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV015").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveTimeline_CapModeWithNegativeMaxActiveDensity_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(3002);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 20,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.CapAndMaxDensity,
                                    RatePerSecPerArea = 1f,
                                    MaxActiveDensityPerArea = -2f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "timeline"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV016").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveTimeline_OverlappingSegments_IsError()
        {
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(4001);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 1,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 5f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = 1f,
                                    MaxActiveDensityPerArea = 0f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    },
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 2,
                        TargetState = SourceStateId.Weakened,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 4f,
                        EndSec = 8f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = 1f,
                                    MaxActiveDensityPerArea = 0f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    },
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "wave_timeline"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV011").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void WaveTimeline_BoundaryTouch_IsNotOverlapError()
        {
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(4002);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 1,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 5f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = 1f,
                                    MaxActiveDensityPerArea = 0f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    },
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 2,
                        TargetState = SourceStateId.Weakened,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 5f,
                        EndSec = 9f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = 1f,
                                    MaxActiveDensityPerArea = 0f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    },
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "wave_timeline"),
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
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(timeline);
            }
        }

        [Test]
        public void WaveTimeline_PoissonWithNegativeMean_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(5001);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 30,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.Poisson,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    MeanEventsPerSec = -1f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = 0f,
                                }
                            }
                        }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "timeline"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV017").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void WaveTimeline_NegativePlayerNoSpawnRadius_IsError()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var timeline = ScriptableObject.CreateInstance<WaveTimelineSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(5002);
                def.Prefab = prefab;

                timeline.Segments = new[]
                {
                    new WaveTimelineSO.WaveSegment
                    {
                        WaveId = 31,
                        TargetState = SourceStateId.Normal,
                        Phase = SourceWavePhaseId.Sustain,
                        StartSec = 0f,
                        EndSec = 1f,
                        Entries = new[]
                        {
                            new WaveTimelineSO.SpawnEntry
                            {
                                Payload = new WaveTimelineSO.SpawnPayloadProfile
                                {
                                    Bullet = def,
                                },
                                Emission = new WaveTimelineSO.SpawnEmissionProfile
                                {
                                    EmissionMode = SourceSpawnEmissionModeId.RateField,
                                    SpawnMode = SourceSpawnModeId.FixedDensity,
                                    RatePerSecPerArea = 1f,
                                },
                                Sampling = new WaveTimelineSO.SpawnSamplingProfile
                                {
                                    SamplingMode = SourceSpawnSamplingModeId.UniformField,
                                    CenterMode = SourceSpawnCenterModeId.SourceCenter,
                                    SpawnSampleBudget = 16,
                                    PlayerNoSpawnRadius = -0.5f,
                                }
                            }
                        }
                    }
                };

                var input = new ContentValidationInput(
                    new List<ContentValidationRecord<BulletDefinitionSO>>
                    {
                        new ContentValidationRecord<BulletDefinitionSO>(def, "def"),
                    },
                    new List<ContentValidationRecord<WaveTimelineSO>>
                    {
                        new ContentValidationRecord<WaveTimelineSO>(timeline, "timeline"),
                    },
                    null,
                    null,
                    null);

                var issues = ContentValidationRules.Validate(input);
                var errors = issues.Where(i => i.Code == "CV019").ToArray();
                Assert.That(errors.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(errors.All(i => i.Severity == ContentValidationSeverity.Error), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(timeline);
                Object.DestroyImmediate(prefab);
            }
        }
    }
}
