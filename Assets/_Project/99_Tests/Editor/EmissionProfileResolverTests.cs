using NUnit.Framework;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class EmissionProfileResolverTests
    {
        [Test]
        public void WaveClipResolver_ProfileReferenceResolvesCommonGrammar()
        {
            var bullet = CreateBullet(6102);
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();

            try
            {
                profile.Bullet = bullet;
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
                    Emission = new RateFieldEmissionAuthoring { RatePerSecPerArea = 4f },
                    Sampling = new WaveSamplingAuthoring(),
                };

                bool ok = WaveClipAuthoringResolver.TryResolveTypedEntry(entry, out var snapshot, out var error);

                Assert.That(ok, Is.True, error);
                Assert.That(snapshot.Bullet, Is.SameAs(bullet));
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
                Object.DestroyImmediate(bullet);
            }
        }

        [Test]
        public void WaveClipResolver_MissingProfileFails()
        {
            var entry = new WaveSpawnEntryAuthoring
            {
                Emission = new PoissonEmissionAuthoring
                {
                    MeanEventsPerSec = 2f,
                    EventRepeatCount = 2,
                },
                Sampling = new WaveSamplingAuthoring(),
            };

            bool ok = WaveClipAuthoringResolver.TryResolveTypedEntry(entry, out _, out var error);

            Assert.That(ok, Is.False);
            Assert.That(error, Does.Contain("Emission profile is null"));
        }

        [Test]
        public void HazardEmitterResolver_ProfileReferenceResolvesCommonGrammarAndSchedule()
        {
            var bullet = CreateBullet(6302);
            var profile = ScriptableObject.CreateInstance<EmissionProfileSO>();

            try
            {
                profile.Bullet = bullet;
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

                var emission = new HazardActorEmissionAuthoring
                {
                    Profile = profile,
                    EventRepeatCount = 2,
                    EventShotSchedule = SourceSpawnEventShotScheduleId.Instant,
                    CooldownSec = 0.75f,
                };

                bool ok = HazardEmitterProfileResolver.TryResolve(emission, out var snapshot, out var error);

                Assert.That(ok, Is.True, error);
                Assert.That(snapshot.Bullet, Is.SameAs(bullet));
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
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(bullet);
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
