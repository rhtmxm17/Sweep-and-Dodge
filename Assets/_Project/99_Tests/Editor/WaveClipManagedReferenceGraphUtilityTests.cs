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
                EndSec = 4f,
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
            Assert.That(clone.EndSec, Is.EqualTo(source.EndSec));
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
                        EndSec = 1f,
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
                        EndSec = 2f,
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
                        EndSec = 1f,
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
