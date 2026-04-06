using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SweepNDodge.DotsBullets.Tests
{
    public class WaveClipManagedReferenceGraphUtilityTests
    {
        [Test]
        public void CloneDirective_DeepClonesAllManagedNodes()
        {
            var source = CreateRichDirective();

            var clone = WaveClipManagedReferenceGraphUtility.CloneDirective(source);

            Assert.That(clone, Is.Not.Null);
            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.Emission, Is.Not.SameAs(source.Emission));
            Assert.That(clone.Sampling, Is.Not.SameAs(source.Sampling));
            Assert.That(clone.Sampling.Anchor, Is.Not.SameAs(source.Sampling.Anchor));
            Assert.That(clone.Sampling.AreaSampler, Is.Not.SameAs(source.Sampling.AreaSampler));
            Assert.That(clone.PositionPattern, Is.Not.SameAs(source.PositionPattern));
            Assert.That(clone.Aim, Is.Not.SameAs(source.Aim));
            Assert.That(clone.ShotPattern, Is.Not.SameAs(source.ShotPattern));

            var sourcePointSet = (PointSetPositionPatternAuthoring)source.PositionPattern;
            var clonePointSet = (PointSetPositionPatternAuthoring)clone.PositionPattern;
            Assert.That(clonePointSet.Points, Is.Not.SameAs(sourcePointSet.Points));
            Assert.That(clonePointSet.Points, Is.EqualTo(sourcePointSet.Points));

            var cloneAim = (PlayerPositionAimAuthoring)clone.Aim;
            Assert.That(cloneAim.SnapshotTiming, Is.EqualTo(WaveAimSnapshotTimingId.PerShot));
        }

        [Test]
        public void CloneSegment_DeepClonesNestedDirectiveGraph()
        {
            var source = new WaveClipSO.ClipSegment
            {
                StartSec = 2f,
                DurationSec = 2f,
                Directives = new[]
                {
                    CreateRichDirective(),
                },
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                EditorOnlyDescription = "segment description",
                #endif
            };

            var clone = WaveClipManagedReferenceGraphUtility.CloneSegment(source);

            Assert.That(clone.StartSec, Is.EqualTo(source.StartSec));
            Assert.That(clone.DurationSec, Is.EqualTo(source.DurationSec));
            Assert.That(clone.Directives, Is.Not.SameAs(source.Directives));
            Assert.That(clone.Directives.Length, Is.EqualTo(1));
            Assert.That(clone.Directives[0], Is.Not.SameAs(source.Directives[0]));
            Assert.That(clone.Directives[0].Emission, Is.Not.SameAs(source.Directives[0].Emission));
            Assert.That(clone.Directives[0].Aim, Is.Not.SameAs(source.Directives[0].Aim));
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Assert.That(clone.EditorOnlyDescription, Is.EqualTo(source.EditorOnlyDescription));
            #endif
        }

        [Test]
        public void CloneDirective_LineNormalAim_PreservesSideAndOffsetWithoutSharing()
        {
            var source = CreateRichDirective();
            source.PositionPattern = new LineEvenPositionPatternAuthoring
            {
                LineStart = new Vector2(-1f, 0f),
                LineEnd = new Vector2(1f, 0f),
                SampleSpacing = 1f,
            };
            source.Aim = new LineNormalAimAuthoring
            {
                NormalSide = WaveLineNormalSideId.Right,
                AngleOffsetDeg = 15f,
            };

            var clone = WaveClipManagedReferenceGraphUtility.CloneDirective(source);

            var cloneAim = clone.Aim as LineNormalAimAuthoring;
            Assert.That(cloneAim, Is.Not.Null);
            Assert.That(cloneAim, Is.Not.SameAs(source.Aim));
            Assert.That(cloneAim.NormalSide, Is.EqualTo(WaveLineNormalSideId.Right));
            Assert.That(cloneAim.AngleOffsetDeg, Is.EqualTo(15f));
        }

        [Test]
        public void BuildDirectiveSummary_LineNormalAndNWay_IsReadable()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();

            try
            {
                def.name = "haz_01";
                var directive = new WaveSpawnEntryAuthoring
                {
                    Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = def },
                    Emission = new EventBurstEmissionAuthoring
                    {
                        EventRepeatCount = 3,
                        EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    },
                    Sampling = new WaveSamplingAuthoring
                    {
                        Anchor = new FixedPointSamplingAnchorAuthoring(),
                        AreaSampler = new CenterPointAreaSamplerAuthoring(),
                    },
                    PositionPattern = new LineEvenPositionPatternAuthoring(),
                    Aim = new LineNormalAimAuthoring
                    {
                        NormalSide = WaveLineNormalSideId.Left,
                        AngleOffsetDeg = 15f,
                    },
                    ShotPattern = new NWayShotPatternAuthoring
                    {
                        ShotCount = 4,
                        AngleSpacingDeg = 30f,
                    },
                };

                string summary = WaveClipEditorPresentationUtility.BuildDirectiveSummary(directive);
                Assert.That(summary, Does.Contain("Bullet=haz_01"));
                Assert.That(summary, Does.Contain("EventBurst x3 Timed"));
                Assert.That(summary, Does.Contain("FixedPoint+CenterPoint"));
                Assert.That(summary, Does.Contain("LineEven"));
                Assert.That(summary, Does.Contain("LineNormal(L,+15"));
                Assert.That(summary, Does.Contain("NWay(4@30"));
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        [Test]
        public void BuildDirectiveSummary_PlayerPositionPerShotAndRadial_IsReadable()
        {
            var directive = new WaveSpawnEntryAuthoring
            {
                Emission = new PoissonEmissionAuthoring
                {
                    EventRepeatCount = 2,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                },
                Sampling = new WaveSamplingAuthoring
                {
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new UniformFieldAreaSamplerAuthoring(),
                },
                PositionPattern = new PointSetPositionPatternAuthoring
                {
                    Points = new[] { Vector2.zero, Vector2.right }
                },
                Aim = new PlayerPositionAimAuthoring
                {
                    SnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                    AngleOffsetDeg = 10f,
                },
                ShotPattern = new RadialShotPatternAuthoring
                {
                    ShotCount = 8,
                },
            };

            string summary = WaveClipEditorPresentationUtility.BuildDirectiveSummary(directive);
            Assert.That(summary, Does.Contain("Poisson x2 Instant"));
            Assert.That(summary, Does.Contain("SourceCenter+UniformField"));
            Assert.That(summary, Does.Contain("PointSet(2)"));
            Assert.That(summary, Does.Contain("PlayerPosition(PerShot,+10"));
            Assert.That(summary, Does.Contain("Radial(8)"));
        }

        [Test]
        public void CollectInlineWarnings_LineNormalWithoutLineEven_ReturnsCV042()
        {
            var directive = new WaveSpawnEntryAuthoring
            {
                Emission = new EventBurstEmissionAuthoring(),
                Sampling = new WaveSamplingAuthoring
                {
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new CenterPointAreaSamplerAuthoring(),
                },
                PositionPattern = new SinglePointPositionPatternAuthoring(),
                Aim = new LineNormalAimAuthoring(),
                ShotPattern = new SingleShotPatternAuthoring(),
            };

            var warnings = WaveClipEditorPresentationUtility.CollectInlineWarnings(directive);
            Assert.That(warnings.Any(w => w.Contains("CV042")), Is.True);
        }

        [Test]
        public void ValidateCurrentClip_LineNormalWithoutLineEven_ReturnsCV042()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Prefab = prefab;
                def.Editor_SetDefinitionId(8801);

                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[]
                        {
                            new WaveSpawnEntryAuthoring
                            {
                                Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = def },
                                Emission = new EventBurstEmissionAuthoring(),
                                Sampling = new WaveSamplingAuthoring
                                {
                                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                                    AreaSampler = new CenterPointAreaSamplerAuthoring(),
                                },
                                PositionPattern = new SinglePointPositionPatternAuthoring(),
                                Aim = new LineNormalAimAuthoring(),
                                ShotPattern = new SingleShotPatternAuthoring(),
                            }
                        }
                    }
                };

                var issues = WaveClipEditorPresentationUtility.ValidateCurrentClip(clip);
                Assert.That(issues.Any(i => i.Code == "CV042"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(clip);
                Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void CreatePresetDirective_LineNormalFan_HasExpectedSubtypeGraph()
        {
            var directive = WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.LineNormalFan);

            Assert.That(directive.Emission, Is.TypeOf<EventBurstEmissionAuthoring>());
            Assert.That(directive.Sampling.Anchor, Is.TypeOf<FixedPointSamplingAnchorAuthoring>());
            Assert.That(directive.Sampling.AreaSampler, Is.TypeOf<CenterPointAreaSamplerAuthoring>());
            Assert.That(directive.PositionPattern, Is.TypeOf<LineEvenPositionPatternAuthoring>());
            Assert.That(directive.Aim, Is.TypeOf<LineNormalAimAuthoring>());
            Assert.That(directive.ShotPattern, Is.TypeOf<NWayShotPatternAuthoring>());

            var aim = (LineNormalAimAuthoring)directive.Aim;
            var shotPattern = (NWayShotPatternAuthoring)directive.ShotPattern;
            Assert.That(aim.NormalSide, Is.EqualTo(WaveLineNormalSideId.Left));
            Assert.That(aim.AngleOffsetDeg, Is.EqualTo(0f));
            Assert.That(shotPattern.ShotCount, Is.EqualTo(3));
            Assert.That(shotPattern.AngleSpacingDeg, Is.EqualTo(30f));
        }

        [Test]
        public void CreatePresetDirective_TwoCalls_DoNotShareManagedNodes()
        {
            var first = WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.LineNormalFan);
            var second = WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.LineNormalFan);

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Emission, Is.Not.SameAs(second.Emission));
            Assert.That(first.Sampling, Is.Not.SameAs(second.Sampling));
            Assert.That(first.Sampling.Anchor, Is.Not.SameAs(second.Sampling.Anchor));
            Assert.That(first.Sampling.AreaSampler, Is.Not.SameAs(second.Sampling.AreaSampler));
            Assert.That(first.PositionPattern, Is.Not.SameAs(second.PositionPattern));
            Assert.That(first.Aim, Is.Not.SameAs(second.Aim));
            Assert.That(first.ShotPattern, Is.Not.SameAs(second.ShotPattern));
        }

        [Test]
        public void MoveSegment_ReordersWithoutIntroducingSharedManagedNodes()
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();

            try
            {
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[] { WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.SingleHazard) },
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 1f,
                        DurationSec = 1f,
                        Directives = new[] { WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.LineNormalFan) },
                    }
                };

                Assert.That(WaveClipManagedReferenceGraphUtility.MoveSegment(clip, 1, 0), Is.True);
                Assert.That(clip.Segments[0].StartSec, Is.EqualTo(1f));
                Assert.That(WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void MoveDirective_ReordersWithoutIntroducingSharedManagedNodes()
        {
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();

            try
            {
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[]
                        {
                            WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.SingleHazard),
                            WaveClipManagedReferenceGraphUtility.CreatePresetDirective(WaveClipDirectivePresetId.LineNormalFan),
                        },
                    }
                };

                Assert.That(WaveClipManagedReferenceGraphUtility.MoveDirective(clip, 0, 1, 0), Is.True);
                Assert.That(clip.Segments[0].Directives[0].Aim, Is.TypeOf<LineNormalAimAuthoring>());
                Assert.That(WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void TryParseJumpTarget_ExtractsSegmentAndDirective()
        {
            Assert.That(
                WaveClipEditorPresentationUtility.TryParseJumpTarget("clip/Segments[3]/Directives[2]", out int segmentIndex, out int directiveIndex),
                Is.True);
            Assert.That(segmentIndex, Is.EqualTo(3));
            Assert.That(directiveIndex, Is.EqualTo(2));
        }

        [Test]
        public void RepairSharedManagedReferences_UniquifiesClipGraph()
        {
            var sharedEmission = new EventBurstEmissionAuthoring
            {
                BurstRepeatCount = 2,
                EventRepeatCount = 3,
            };
            var sharedAim = new PlayerPositionAimAuthoring
            {
                SnapshotTiming = WaveAimSnapshotTimingId.PerShot,
            };
            var sharedShotPattern = new NWayShotPatternAuthoring
            {
                ShotCount = 3,
                AngleSpacingDeg = 45f,
            };
            var sharedAnchor = new SourceCenterSamplingAnchorAuthoring();
            var sharedAreaSampler = new UniformFieldAreaSamplerAuthoring();
            var sharedPattern = new PointSetPositionPatternAuthoring
            {
                Points = new[] { Vector2.zero, Vector2.right },
            };

            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            try
            {
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[]
                        {
                            new WaveSpawnEntryAuthoring
                            {
                                Emission = sharedEmission,
                                Sampling = new WaveSamplingAuthoring
                                {
                                    Anchor = sharedAnchor,
                                    AreaSampler = sharedAreaSampler,
                                },
                                PositionPattern = sharedPattern,
                                Aim = sharedAim,
                                ShotPattern = sharedShotPattern,
                            }
                        }
                    },
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 1f,
                        DurationSec = 1f,
                        Directives = new[]
                        {
                            new WaveSpawnEntryAuthoring
                            {
                                Emission = sharedEmission,
                                Sampling = new WaveSamplingAuthoring
                                {
                                    Anchor = sharedAnchor,
                                    AreaSampler = sharedAreaSampler,
                                },
                                PositionPattern = sharedPattern,
                                Aim = sharedAim,
                                ShotPattern = sharedShotPattern,
                            }
                        }
                    },
                };

                var issuesBefore = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);
                Assert.That(issuesBefore.Count, Is.GreaterThan(0));

                bool changed = WaveClipManagedReferenceGraphUtility.RepairSharedManagedReferences(clip);

                Assert.That(changed, Is.True);
                Assert.That(WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip), Is.Empty);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].Emission, clip.Segments[1].Directives[0].Emission), Is.False);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].Aim, clip.Segments[1].Directives[0].Aim), Is.False);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].ShotPattern, clip.Segments[1].Directives[0].ShotPattern), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void Validation_SharedManagedReferenceGraph_IsCV041()
        {
            var def = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var clip = ScriptableObject.CreateInstance<WaveClipSO>();
            var prefab = new GameObject("bullet_prefab");

            try
            {
                def.Editor_SetDefinitionId(9901);
                def.Prefab = prefab;

                var sharedAim = new PlayerPositionAimAuthoring
                {
                    SnapshotTiming = WaveAimSnapshotTimingId.EventStart,
                };

                var entryA = CreateRichDirective();
                entryA.Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = def };
                entryA.Aim = sharedAim;

                var entryB = CreateRichDirective();
                entryB.Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = def };
                entryB.Aim = sharedAim;

                clip.ClipId = 901;
                clip.Segments = new[]
                {
                    new WaveClipSO.ClipSegment
                    {
                        StartSec = 0f,
                        DurationSec = 1f,
                        Directives = new[] { entryA, entryB },
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

        private static WaveSpawnEntryAuthoring CreateRichDirective()
        {
            return new WaveSpawnEntryAuthoring
            {
                Emission = new EventBurstEmissionAuthoring
                {
                    SpawnMode = SourceSpawnModeId.FixedDensity,
                    MaxActiveDensityPerArea = 0f,
                    BurstRepeatCount = 2,
                    BurstIntervalSec = 0.5f,
                    EventRepeatCount = 4,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.1f,
                },
                Sampling = new WaveSamplingAuthoring
                {
                    SpawnSampleBudget = 7,
                    PlayerNoSpawnRadius = 0.25f,
                    Anchor = new PlayerRelativeSamplingAnchorAuthoring
                    {
                        SpawnOffset = new Vector2(1f, 2f),
                    },
                    AreaSampler = new PollutionTopKAreaSamplerAuthoring(),
                },
                PositionPattern = new PointSetPositionPatternAuthoring
                {
                    Points = new[]
                    {
                        Vector2.zero,
                        Vector2.right,
                    },
                },
                Aim = new PlayerPositionAimAuthoring
                {
                    AngleOffsetDeg = 20f,
                    SnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                },
                ShotPattern = new RadialShotPatternAuthoring
                {
                    ShotCount = 5,
                },
            };
        }
    }
}

