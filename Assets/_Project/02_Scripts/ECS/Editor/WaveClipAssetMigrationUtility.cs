using System;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class WaveClipAssetMigrationUtility
    {
        private static readonly string[] SearchRoots =
        {
            "Assets/_Project/03_Datas/WaveClips",
            "Assets/_Project/99_Tests/TestData/WaveClips",
        };

        [MenuItem("Tools/Project/Migrate WaveClip Assets To Typed Authoring")]
        public static void MigrateProjectWaveClipAssets()
        {
            int migratedAssets = 0;
            int migratedSegments = 0;

            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WaveClipSO)}", SearchRoots);
            Array.Sort(guids, StringComparer.Ordinal);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<WaveClipSO>(path);
                if (clip == null)
                    continue;

                if (!MigrateClipInPlace(clip, out int changedSegments))
                    continue;

                migratedAssets++;
                migratedSegments += changedSegments;
                EditorUtility.SetDirty(clip);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[WaveClipMigration] Completed. migratedAssets={migratedAssets}, migratedSegments={migratedSegments}");
        }

        public static void MigrateProjectWaveClipAssetsBatch()
        {
            MigrateProjectWaveClipAssets();
        }

        private static bool MigrateClipInPlace(WaveClipSO clip, out int changedSegments)
        {
            changedSegments = 0;
            var segments = clip.Segments;
            if (segments == null || segments.Length <= 0)
                return false;

            bool changed = false;
            for (int i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                bool hasTyped = segment.Directives != null && segment.Directives.Length > 0;
                bool hasLegacy = segment.LegacyEntries != null && segment.LegacyEntries.Length > 0;
                if (!hasTyped && !hasLegacy)
                    continue;

                if (!hasTyped && hasLegacy)
                {
                    var directives = new WaveSpawnEntryAuthoring[segment.LegacyEntries.Length];
                    for (int e = 0; e < segment.LegacyEntries.Length; e++)
                        directives[e] = WaveClipAuthoringResolver.ConvertLegacyEntry(in segment.LegacyEntries[e]);

                    segment.Directives = directives;
                    segment.LegacyEntries = Array.Empty<WaveClipSO.SpawnEntry>();
                    segments[i] = segment;
                    changed = true;
                    changedSegments++;
                    continue;
                }

                if (hasTyped && hasLegacy)
                {
                    segment.LegacyEntries = Array.Empty<WaveClipSO.SpawnEntry>();
                    segments[i] = segment;
                    changed = true;
                    changedSegments++;
                }
            }

            if (changed)
                clip.Segments = segments;

            return changed;
        }
    }
}
