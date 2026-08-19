using NUnit.Framework;
using SweepNDodge.DotsBullets.Editor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class WaveClipManagedReferenceGraphUtilityTests
    {
        [Test]
        public void CloneDirective_DeepClonesEmissionAndSampling()
        {
            var source = CreateRichDirective();
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
            source.Profile = profile;

            try
            {
                var clone = WaveClipManagedReferenceGraphUtility.CloneDirective(source);

                Assert.That(clone, Is.Not.Null);
                Assert.That(clone, Is.Not.SameAs(source));
                Assert.That(clone.Profile, Is.SameAs(profile));
                Assert.That(clone.Emission, Is.Not.SameAs(source.Emission));
                Assert.That(clone.Sampling, Is.Not.SameAs(source.Sampling));
                Assert.That(clone.Sampling.Anchor, Is.Not.SameAs(source.Sampling.Anchor));
                Assert.That(clone.Sampling.AreaSampler, Is.Not.SameAs(source.Sampling.AreaSampler));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void DetectSharedManagedReferences_IgnoresSharedProfileAsset()
        {
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
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
                                Profile = profile,
                                Emission = new RateFieldEmissionAuthoring(),
                                Sampling = CreateSampling(),
                            },
                            new WaveSpawnEntryAuthoring
                            {
                                Profile = profile,
                                Emission = new RateFieldEmissionAuthoring(),
                                Sampling = CreateSampling(),
                            },
                        },
                    },
                };

                var issues = WaveClipManagedReferenceGraphUtility.DetectSharedManagedReferences(clip);

                Assert.That(issues, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void RepairSharedManagedReferences_ClonesSharedEmissionAndSamplingNodes()
        {
            var sharedEmission = new EventBurstEmissionAuthoring();
            var sharedAnchor = new SourceCenterSamplingAnchorAuthoring();
            var sharedSampler = new CenterPointAreaSamplerAuthoring();
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
                                Sampling = new WaveSamplingAuthoring { Anchor = sharedAnchor, AreaSampler = sharedSampler },
                            },
                            new WaveSpawnEntryAuthoring
                            {
                                Emission = sharedEmission,
                                Sampling = new WaveSamplingAuthoring { Anchor = sharedAnchor, AreaSampler = sharedSampler },
                            },
                        },
                    },
                };

                bool changed = WaveClipManagedReferenceGraphUtility.RepairSharedManagedReferences(clip);

                Assert.That(changed, Is.True);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].Emission, clip.Segments[0].Directives[1].Emission), Is.False);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].Sampling.Anchor, clip.Segments[0].Directives[1].Sampling.Anchor), Is.False);
                Assert.That(ReferenceEquals(clip.Segments[0].Directives[0].Sampling.AreaSampler, clip.Segments[0].Directives[1].Sampling.AreaSampler), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void BuildDirectiveSummary_UsesProfileCommonGrammar()
        {
            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();

            try
            {
                bullet.Editor_SetDefinitionId(3101);
                profile.name = "ep_summary";
                profile.Bullet = bullet;
                profile.PositionPattern = new LineEvenPositionPatternAuthoring();
                profile.Aim = new LineNormalAimAuthoring { NormalSide = WaveLineNormalSideId.Right };
                profile.ShotPattern = new NWayShotPatternAuthoring { ShotCount = 3, AngleSpacingDeg = 15f };

                var directive = new WaveSpawnEntryAuthoring
                {
                    Profile = profile,
                    Emission = new EventBurstEmissionAuthoring { EventRepeatCount = 2 },
                    Sampling = CreateSampling(),
                };

                string summary = WaveClipEditorPresentationUtility.BuildDirectiveSummary(directive);

                Assert.That(summary, Does.Contain("Profile=ep_summary"));
                Assert.That(summary, Does.Contain("LineEven"));
                Assert.That(summary, Does.Contain("LineNormal"));
                Assert.That(summary, Does.Contain("NWay"));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(bullet);
            }
        }

        private static WaveSpawnEntryAuthoring CreateRichDirective()
        {
            return new WaveSpawnEntryAuthoring
            {
                Emission = new EventBurstEmissionAuthoring
                {
                    SpawnMode = SourceSpawnModeId.CapAndMaxDensity,
                    MaxActiveDensityPerArea = 3f,
                    BurstRepeatCount = 2,
                    BurstIntervalSec = 0.5f,
                    EventRepeatCount = 3,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Timed,
                    EventShotIntervalSec = 0.1f,
                },
                Sampling = new WaveSamplingAuthoring
                {
                    SpawnSampleBudget = 8,
                    PlayerNoSpawnRadius = 1.5f,
                    Anchor = new PlayerRelativeSamplingAnchorAuthoring { SpawnOffset = new Vector2(1f, -2f) },
                    AreaSampler = new PollutionTopKAreaSamplerAuthoring(),
                },
            };
        }

        private static WaveSamplingAuthoring CreateSampling()
        {
            return new WaveSamplingAuthoring
            {
                SpawnSampleBudget = 1,
                Anchor = new SourceCenterSamplingAnchorAuthoring(),
                AreaSampler = new CenterPointAreaSamplerAuthoring(),
            };
        }
    }
}
