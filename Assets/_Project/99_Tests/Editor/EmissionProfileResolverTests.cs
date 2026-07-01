using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class EmissionProfileResolverTests
    {
        [Test]
        public void WaveClipResolver_ProfileReferenceOverridesInlineCommonGrammar()
        {
            var inlineBullet = CreateBullet(6101);
            var profileBullet = CreateBullet(6102);
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();

            try
            {
                profile.Bullet = profileBullet;
                profile.SpawnTuning.OverrideSpeed = true;
                profile.SpawnTuning.SpeedOverride = 2.5f;
                profile.PositionPattern = new LineEvenPositionPatternAuthoring
                {
                    LineStart = new Vector2(-1f, 0f),
                    LineEnd = new Vector2(1f, 0f),
                    SampleSpacing = 0.5f,
                };
                profile.Aim = new SpiralAimAuthoring
                {
                    BaseAngleDeg = 30f,
                    SpiralStepDeg = 15f,
                };
                profile.ShotPattern = new NWayShotPatternAuthoring
                {
                    ShotCount = 3,
                    AngleSpacingDeg = 20f,
                };

                var entry = new WaveSpawnEntryAuthoring
                {
                    Profile = profile,
                    Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = inlineBullet },
                    Emission = new RateFieldEmissionAuthoring { RatePerSecPerArea = 4f },
                    Sampling = new WaveSamplingAuthoring(),
                    PositionPattern = new SinglePointPositionPatternAuthoring(),
                    Aim = new FixedAimAuthoring { BaseAngleDeg = 90f },
                    ShotPattern = new SingleShotPatternAuthoring(),
                };

                bool ok = WaveClipAuthoringResolver.TryResolveTypedEntry(entry, out var snapshot, out var error);

                Assert.That(ok, Is.True, error);
                Assert.That(snapshot.Bullet, Is.SameAs(profileBullet));
                Assert.That(snapshot.EmissionCore.ProfileRefId, Is.EqualTo(profile.GetInstanceID()));
                Assert.That(snapshot.EmissionCore.HasSpeedOverride, Is.True);
                Assert.That(snapshot.EmissionCore.SpeedOverride, Is.EqualTo(2.5f));
                Assert.That(snapshot.PositionPatternMode, Is.EqualTo(WavePositionPatternModeId.LineEven));
                Assert.That(snapshot.AimMode, Is.EqualTo(WaveAimModeId.Spiral));
                Assert.That(snapshot.BaseAngleDeg, Is.EqualTo(30f));
                Assert.That(snapshot.ShotPatternMode, Is.EqualTo(WaveShotPatternModeId.NWay));
                Assert.That(snapshot.ShotCount, Is.EqualTo(3));
                Assert.That(snapshot.RatePerSecPerArea, Is.EqualTo(4f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(profileBullet);
                Object.DestroyImmediate(inlineBullet);
            }
        }

        [Test]
        public void WaveClipResolver_InlineCommonGrammarRemainsCompatibilitySource()
        {
            var bullet = CreateBullet(6201);

            try
            {
                var entry = new WaveSpawnEntryAuthoring
                {
                    Payload = new WaveClipSO.SpawnPayloadProfile { Bullet = bullet },
                    Emission = new PoissonEmissionAuthoring
                    {
                        MeanEventsPerSec = 2f,
                        EventRepeatCount = 2,
                    },
                    Sampling = new WaveSamplingAuthoring(),
                    PositionPattern = new PointSetPositionPatternAuthoring
                    {
                        Points = new[] { Vector2.zero, Vector2.one },
                    },
                    Aim = new PlayerPositionAimAuthoring
                    {
                        AngleOffsetDeg = 10f,
                        SnapshotTiming = WaveAimSnapshotTimingId.PerShot,
                    },
                    ShotPattern = new RadialShotPatternAuthoring
                    {
                        ShotCount = 5,
                    },
                };

                bool ok = WaveClipAuthoringResolver.TryResolveTypedEntry(entry, out var snapshot, out var error);

                Assert.That(ok, Is.True, error);
                Assert.That(snapshot.Bullet, Is.SameAs(bullet));
                Assert.That(snapshot.EmissionCore.ProfileRefId, Is.EqualTo(0));
                Assert.That(snapshot.PositionPatternMode, Is.EqualTo(WavePositionPatternModeId.PointSet));
                Assert.That(snapshot.PointSetCount, Is.EqualTo(2));
                Assert.That(snapshot.AimMode, Is.EqualTo(WaveAimModeId.PlayerPosition));
                Assert.That(snapshot.AimSnapshotTiming, Is.EqualTo(WaveAimSnapshotTimingId.PerShot));
                Assert.That(snapshot.ShotPatternMode, Is.EqualTo(WaveShotPatternModeId.Radial));
                Assert.That(snapshot.ShotCount, Is.EqualTo(5));
                Assert.That(snapshot.EmissionMode, Is.EqualTo(SourceSpawnEmissionModeId.Poisson));
                Assert.That(snapshot.MeanEventsPerSec, Is.EqualTo(2f));
            }
            finally
            {
                Object.DestroyImmediate(bullet);
            }
        }

        [Test]
        public void HazardEmitterResolver_ProfileReferenceOverridesInlineCommonGrammar()
        {
            var inlineBullet = CreateBullet(6301);
            var profileBullet = CreateBullet(6302);
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
            var hazardProfile = ScriptableObject.CreateInstance<HazardEmitterEmissionProfileSO>();

            try
            {
                profile.Bullet = profileBullet;
                profile.MovementTuning.OverrideMovement = true;
                profile.MovementTuning.Family = BulletMovementFamilyId.DampedLinear;
                profile.PositionPattern = new LineEvenPositionPatternAuthoring
                {
                    LineStart = Vector2.left,
                    LineEnd = Vector2.right,
                    SampleSpacing = 0.25f,
                };
                profile.Aim = new FixedAimAuthoring { BaseAngleDeg = 45f };
                profile.ShotPattern = new NWayShotPatternAuthoring
                {
                    ShotCount = 4,
                    AngleSpacingDeg = 12f,
                };

                hazardProfile.Profile = profile;
                hazardProfile.Bullet = inlineBullet;
                hazardProfile.PositionPattern = new SinglePointPositionPatternAuthoring();
                hazardProfile.Aim = new FixedAimAuthoring { BaseAngleDeg = 180f };
                hazardProfile.ShotPattern = new SingleShotPatternAuthoring();
                hazardProfile.EventRepeatCount = 2;
                hazardProfile.CooldownSec = 0.75f;

                bool ok = HazardEmitterProfileResolver.TryResolve(hazardProfile, out var snapshot, out var error);

                Assert.That(ok, Is.True, error);
                Assert.That(snapshot.Bullet, Is.SameAs(profileBullet));
                Assert.That(snapshot.EmissionCore.ProfileRefId, Is.EqualTo(profile.GetInstanceID()));
                Assert.That(snapshot.EmissionCore.HasMovementOverride, Is.True);
                Assert.That(snapshot.PositionPatternMode, Is.EqualTo(WavePositionPatternModeId.LineEven));
                Assert.That(snapshot.AimMode, Is.EqualTo(WaveAimModeId.Fixed));
                Assert.That(snapshot.BaseAngleDeg, Is.EqualTo(45f));
                Assert.That(snapshot.ShotPatternMode, Is.EqualTo(WaveShotPatternModeId.NWay));
                Assert.That(snapshot.ShotCount, Is.EqualTo(4));
                Assert.That(snapshot.EventRepeatCount, Is.EqualTo(2));
                Assert.That(snapshot.CooldownSec, Is.EqualTo(0.75f));
            }
            finally
            {
                Object.DestroyImmediate(hazardProfile);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(profileBullet);
                Object.DestroyImmediate(inlineBullet);
            }
        }

        private static BulletDefinitionSO CreateBullet(int definitionId)
        {
            var bullet = ScriptableObject.CreateInstance<BulletDefinitionSO>();
            bullet.Editor_SetDefinitionId(definitionId);
            return bullet;
        }
    }
}
