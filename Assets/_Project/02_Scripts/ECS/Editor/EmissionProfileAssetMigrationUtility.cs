using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public readonly struct EmissionProfileAssetMigrationResult
    {
        public readonly int CreatedProfileCount;
        public readonly int UpdatedProfileCount;
        public readonly int MigratedBulletReactionProfileCount;
        public readonly int MigratedWaveDirectiveCount;
        public readonly int MigratedHazardProfileCount;

        public EmissionProfileAssetMigrationResult(
            int createdProfileCount,
            int updatedProfileCount,
            int migratedBulletReactionProfileCount,
            int migratedWaveDirectiveCount,
            int migratedHazardProfileCount)
        {
            CreatedProfileCount = createdProfileCount;
            UpdatedProfileCount = updatedProfileCount;
            MigratedBulletReactionProfileCount = migratedBulletReactionProfileCount;
            MigratedWaveDirectiveCount = migratedWaveDirectiveCount;
            MigratedHazardProfileCount = migratedHazardProfileCount;
        }
    }

    public static class EmissionProfileAssetMigrationUtility
    {
        private const string RootFolder = "Assets/_Project/03_Datas/EmissionProfiles";
        private const string WaveClipProfileFolder = RootFolder + "/WaveClips";
        private const string HazardProfileFolder = RootFolder + "/Hazards";
        private const string BulletReactionProfileFolder = RootFolder + "/BulletReactions";
        private static readonly string[] OperationalWaveClipRoots = { "Assets/_Project/03_Datas/WaveClips" };
        private static readonly string[] OperationalHazardRoots = { "Assets/_Project/03_Datas" };

        private static int _createdProfileCount;
        private static int _updatedProfileCount;

        [MenuItem("Tools/Project/Migrate Emission Profiles")]
        private static void MigrateProjectAssetsMenu()
        {
            var result = MigrateProjectAssets();
            Debug.Log(BuildSummary(result));
        }

        public static void MigrateProjectAssetsBatch()
        {
            var result = MigrateProjectAssets();
            Debug.Log(BuildSummary(result));
        }

        public static EmissionProfileAssetMigrationResult MigrateProjectAssets()
        {
            _createdProfileCount = 0;
            _updatedProfileCount = 0;
            EnsureFolder(RootFolder);
            EnsureFolder(WaveClipProfileFolder);
            EnsureFolder(HazardProfileFolder);
            EnsureFolder(BulletReactionProfileFolder);

            int migratedBulletReactionProfiles = MigrateBulletReactionProfiles();
            int migratedWaveDirectives = MigrateWaveClipAssets();
            int migratedHazardProfiles = MigrateHazardEmitterEmissionProfiles();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return new EmissionProfileAssetMigrationResult(
                _createdProfileCount,
                _updatedProfileCount,
                migratedBulletReactionProfiles,
                migratedWaveDirectives,
                migratedHazardProfiles);
        }

        public static string BuildSummary(in EmissionProfileAssetMigrationResult result)
        {
            return "[EmissionProfileAssetMigrationUtility] "
                + $"createdProfiles={result.CreatedProfileCount}, "
                + $"updatedProfiles={result.UpdatedProfileCount}, "
                + $"migratedBulletReactionProfiles={result.MigratedBulletReactionProfileCount}, "
                + $"migratedWaveDirectives={result.MigratedWaveDirectiveCount}, "
                + $"migratedHazardProfiles={result.MigratedHazardProfileCount}";
        }

        private static int MigrateBulletReactionProfiles()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(BulletDefinitionSO)}", new[] { "Assets/_Project/03_Datas/BulletDefinitions" });
            Array.Sort(guids, StringComparer.Ordinal);
            int migratedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var bullet = AssetDatabase.LoadAssetAtPath<BulletDefinitionSO>(path);
                if (bullet == null || !bullet.OnMotionCompletedExplode.Enabled || bullet.OnMotionCompletedExplode.SecondaryBullet == null)
                    continue;

                string bulletStem = SanitizeAssetStem(bullet.name);
                string parentName = bulletStem == "bd_sample_bubble"
                    ? "ep_sample_bubble_parent"
                    : $"ep_{RemovePrefix(bulletStem, "bd_")}_parent";
                string parentPath = $"{BulletReactionProfileFolder}/{parentName}.asset";
                var parentProfile = LoadOrCreateProfile(parentPath);
                parentProfile.Bullet = bullet;
                ApplyBulletGameplayFallbacks(parentProfile, bullet);
                parentProfile.PositionPattern = new SinglePointPositionPatternAuthoring();
                parentProfile.Aim = new FixedAimAuthoring();
                parentProfile.ShotPattern = new SingleShotPatternAuthoring();
                ApplyMotionCompletedReactionIfPresent(parentProfile, bullet);
                EditorUtility.SetDirty(parentProfile);
                migratedCount++;
            }

            return migratedCount;
        }

        private static int MigrateWaveClipAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WaveClipSO)}", OperationalWaveClipRoots);
            Array.Sort(guids, StringComparer.Ordinal);
            int migratedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<WaveClipSO>(path);
                if (clip == null || clip.Segments == null)
                    continue;

                bool changed = false;
                var segments = clip.Segments;
                string clipStem = SanitizeAssetStem(Path.GetFileNameWithoutExtension(path));
                for (int s = 0; s < segments.Length; s++)
                {
                    var segment = segments[s];
                    if (segment.Directives == null)
                        continue;

                    for (int d = 0; d < segment.Directives.Length; d++)
                    {
                        var directive = segment.Directives[d];
                        if (directive == null)
                            continue;

                        string profileName = $"ep_{clipStem}_s{s}_d{d}";
                        string profilePath = $"{WaveClipProfileFolder}/{profileName}.asset";
                        var profile = LoadOrCreateProfile(profilePath);
                        CopyDirectiveCommonGrammarToProfile(profile, directive);
                        directive.Profile = profile;
                        segment.Directives[d] = directive;
                        changed = true;
                        migratedCount++;
                    }

                    segments[s] = segment;
                }

                if (!changed)
                    continue;

                clip.Segments = segments;
                EditorUtility.SetDirty(clip);
            }

            return migratedCount;
        }

        private static int MigrateHazardEmitterEmissionProfiles()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(HazardEmitterEmissionProfileSO)}", OperationalHazardRoots);
            Array.Sort(guids, StringComparer.Ordinal);
            int migratedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Replace('\\', '/').StartsWith("Assets/_Project/99_Tests/", StringComparison.OrdinalIgnoreCase))
                    continue;

                var hazardProfile = AssetDatabase.LoadAssetAtPath<HazardEmitterEmissionProfileSO>(path);
                if (hazardProfile == null)
                    continue;

                string profileName = "ep_" + RemovePrefix(SanitizeAssetStem(Path.GetFileNameWithoutExtension(path)), "heep_");
                string profilePath = $"{HazardProfileFolder}/{profileName}.asset";
                var profile = LoadOrCreateProfile(profilePath);
                CopyHazardCommonGrammarToProfile(profile, hazardProfile);
                hazardProfile.Profile = profile;
                EditorUtility.SetDirty(hazardProfile);
                migratedCount++;
            }

            return migratedCount;
        }

        private static EmissionProfileSO LoadOrCreateProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EmissionProfileSO>(path);
            if (profile != null)
            {
                _updatedProfileCount++;
                return profile;
            }

            profile = ScriptableObject.CreateInstance<EmissionProfileSO>();
            AssetDatabase.CreateAsset(profile, path);
            _createdProfileCount++;
            return profile;
        }

        private static void CopyDirectiveCommonGrammarToProfile(
            EmissionProfileSO profile,
            WaveSpawnEntryAuthoring directive)
        {
            if (profile == null || directive == null)
                return;

            profile.Bullet = directive.Payload.Bullet;
            ApplyBulletGameplayFallbacks(profile, profile.Bullet);
            profile.PositionPattern = ClonePositionPattern(directive.PositionPattern);
            profile.Aim = CloneAim(directive.Aim);
            profile.ShotPattern = CloneShotPattern(directive.ShotPattern);
            ApplyMotionCompletedReactionIfPresent(profile, profile.Bullet);
            EditorUtility.SetDirty(profile);
        }

        private static void CopyHazardCommonGrammarToProfile(
            EmissionProfileSO profile,
            HazardEmitterEmissionProfileSO hazardProfile)
        {
            if (profile == null || hazardProfile == null)
                return;

            profile.Bullet = hazardProfile.Bullet;
            ApplyBulletGameplayFallbacks(profile, profile.Bullet);
            profile.PositionPattern = ClonePositionPattern(hazardProfile.PositionPattern);
            profile.Aim = CloneAim(hazardProfile.Aim);
            profile.ShotPattern = CloneShotPattern(hazardProfile.ShotPattern);
            ApplyMotionCompletedReactionIfPresent(profile, profile.Bullet);
            EditorUtility.SetDirty(profile);
        }

        private static void ApplyBulletGameplayFallbacks(EmissionProfileSO profile, BulletDefinitionSO bullet)
        {
            if (profile == null)
                return;

            if (profile.SpawnTuning == null)
                profile.SpawnTuning = new EmissionSpawnTuningAuthoring();
            if (profile.MovementTuning == null)
                profile.MovementTuning = new EmissionMovementTuningAuthoring();
            if (profile.LifecycleTriggers == null)
                profile.LifecycleTriggers = new EmissionLifecycleTriggersAuthoring();
            if (profile.LifecycleTriggers.MotionCompleted == null)
                profile.LifecycleTriggers.MotionCompleted = new EmissionMotionCompletedTriggerAuthoring();

            if (bullet == null)
            {
                profile.SpawnTuning.OverrideSpeed = false;
                profile.SpawnTuning.OverrideLifetime = false;
                profile.MovementTuning.OverrideMovement = false;
                profile.LifecycleTriggers.MotionCompleted.Enabled = false;
                profile.LifecycleTriggers.MotionCompleted.TargetProfile = null;
                return;
            }

            profile.SpawnTuning.OverrideSpeed = true;
            profile.SpawnTuning.SpeedOverride = Mathf.Max(0.001f, bullet.Speed);
            profile.SpawnTuning.OverrideLifetime = true;
            profile.SpawnTuning.LifetimeOverride = Mathf.Max(0.001f, bullet.Lifetime);
            profile.MovementTuning.OverrideMovement = true;
            profile.MovementTuning.Family = bullet.MovementFamily;
            profile.MovementTuning.DampedLinear = bullet.DampedLinear;
            profile.MovementTuning.HomingLite = bullet.HomingLite;
            profile.LifecycleTriggers.MotionCompleted.Enabled = false;
            profile.LifecycleTriggers.MotionCompleted.TargetProfile = null;
            profile.LifecycleTriggers.MotionCompleted.DelaySec = 0f;
        }

        private static void ApplyMotionCompletedReactionIfPresent(EmissionProfileSO profile, BulletDefinitionSO bullet)
        {
            if (profile == null || bullet == null || !bullet.OnMotionCompletedExplode.Enabled)
                return;

            var reaction = bullet.OnMotionCompletedExplode;
            if (reaction.SecondaryBullet == null)
                return;

            string parentStem = SanitizeAssetStem(bullet.name);
            string targetName = parentStem == "bd_sample_bubble"
                ? "ep_sample_bubble_fragments"
                : $"ep_{RemovePrefix(parentStem, "bd_")}_motion_completed";
            string targetPath = $"{BulletReactionProfileFolder}/{targetName}.asset";
            var target = LoadOrCreateProfile(targetPath);

            target.Bullet = reaction.SecondaryBullet;
            ApplyBulletGameplayFallbacks(target, reaction.SecondaryBullet);
            target.PositionPattern = new SinglePointPositionPatternAuthoring();
            target.Aim = new FixedAimAuthoring();
            target.ShotPattern = reaction.Shape == BulletSecondarySpawnShapeId.PointBurst
                ? new RadialShotPatternAuthoring { ShotCount = Mathf.Max(2, reaction.SpawnCount) }
                : new NWayShotPatternAuthoring
                {
                    ShotCount = Mathf.Max(2, reaction.SpawnCount),
                    AngleSpacingDeg = reaction.SpawnCount > 1
                        ? Mathf.Max(0.001f, reaction.SpreadAngleDeg / Mathf.Max(1, reaction.SpawnCount - 1))
                        : Mathf.Max(0.001f, reaction.SpreadAngleDeg),
                };
            EditorUtility.SetDirty(target);

            profile.LifecycleTriggers.MotionCompleted.Enabled = true;
            profile.LifecycleTriggers.MotionCompleted.TargetProfile = target;
            profile.LifecycleTriggers.MotionCompleted.DelaySec = Mathf.Max(0f, reaction.SpawnDelaySec);
        }

        private static WavePositionPatternAuthoringBase ClonePositionPattern(WavePositionPatternAuthoringBase source)
        {
            return source switch
            {
                null => new SinglePointPositionPatternAuthoring(),
                SinglePointPositionPatternAuthoring => new SinglePointPositionPatternAuthoring(),
                LineEvenPositionPatternAuthoring lineEven => new LineEvenPositionPatternAuthoring
                {
                    LineStart = lineEven.LineStart,
                    LineEnd = lineEven.LineEnd,
                    SampleSpacing = lineEven.SampleSpacing,
                },
                PointSetPositionPatternAuthoring pointSet => new PointSetPositionPatternAuthoring
                {
                    Points = pointSet.Points != null ? (Vector2[])pointSet.Points.Clone() : Array.Empty<Vector2>(),
                },
                _ => new SinglePointPositionPatternAuthoring(),
            };
        }

        private static WaveAimAuthoringBase CloneAim(WaveAimAuthoringBase source)
        {
            return source switch
            {
                null => new FixedAimAuthoring(),
                RandomAimAuthoring => new RandomAimAuthoring(),
                FixedAimAuthoring fixedAim => new FixedAimAuthoring { BaseAngleDeg = fixedAim.BaseAngleDeg },
                SpiralAimAuthoring spiralAim => new SpiralAimAuthoring
                {
                    BaseAngleDeg = spiralAim.BaseAngleDeg,
                    SpiralStepDeg = spiralAim.SpiralStepDeg,
                },
                PlayerPositionAimAuthoring playerAim => new PlayerPositionAimAuthoring
                {
                    AngleOffsetDeg = playerAim.AngleOffsetDeg,
                    SnapshotTiming = playerAim.SnapshotTiming,
                },
                LineNormalAimAuthoring lineNormal => new LineNormalAimAuthoring
                {
                    NormalSide = lineNormal.NormalSide,
                    AngleOffsetDeg = lineNormal.AngleOffsetDeg,
                },
                _ => new FixedAimAuthoring(),
            };
        }

        private static WaveShotPatternAuthoringBase CloneShotPattern(WaveShotPatternAuthoringBase source)
        {
            return source switch
            {
                null => new SingleShotPatternAuthoring(),
                SingleShotPatternAuthoring => new SingleShotPatternAuthoring(),
                NWayShotPatternAuthoring nWay => new NWayShotPatternAuthoring
                {
                    ShotCount = nWay.ShotCount,
                    AngleSpacingDeg = nWay.AngleSpacingDeg,
                },
                RadialShotPatternAuthoring radial => new RadialShotPatternAuthoring { ShotCount = radial.ShotCount },
                _ => new SingleShotPatternAuthoring(),
            };
        }

        private static string RemovePrefix(string value, string prefix)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(prefix))
                return value ?? string.Empty;

            return value.StartsWith(prefix, StringComparison.Ordinal)
                ? value.Substring(prefix.Length)
                : value;
        }

        private static string SanitizeAssetStem(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "profile";

            var chars = value.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '-'))
                    chars[i] = '_';
            }

            return new string(chars);
        }

        private static void EnsureFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(normalized))
                return;

            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
