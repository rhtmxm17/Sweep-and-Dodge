using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum WaveClipDirectivePresetId : byte
    {
        SingleHazard = 0,
        FanBurst = 1,
        RadialBurst = 2,
        LineNormalFan = 3,
    }

    public readonly struct WaveClipSharedManagedReferenceIssue
    {
        public readonly string SlotName;
        public readonly string FirstLocation;
        public readonly string DuplicateLocation;

        public WaveClipSharedManagedReferenceIssue(string slotName, string firstLocation, string duplicateLocation)
        {
            SlotName = slotName ?? string.Empty;
            FirstLocation = firstLocation ?? string.Empty;
            DuplicateLocation = duplicateLocation ?? string.Empty;
        }
    }

    public static class WaveClipManagedReferenceGraphUtility
    {
        private static readonly string[] ProjectWaveClipSearchRoots =
        {
            "Assets/_Project/03_Datas/WaveClips",
            "Assets/_Project/99_Tests/TestData/WaveClips",
        };

        public static List<WaveClipSharedManagedReferenceIssue> DetectSharedManagedReferences(WaveClipSO clip)
        {
            var issues = new List<WaveClipSharedManagedReferenceIssue>();
            if (clip == null || clip.Segments == null)
                return issues;

            var ownerByNode = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
            for (int s = 0; s < clip.Segments.Length; s++)
            {
                var segment = clip.Segments[s];
                if (segment.Directives == null)
                    continue;

                for (int d = 0; d < segment.Directives.Length; d++)
                {
                    var directive = segment.Directives[d];
                    if (directive == null)
                        continue;

                    RegisterNode(ownerByNode, issues, directive.Emission, BuildSlotLocation(s, d, nameof(WaveSpawnEntryAuthoring.Emission)));

                    var sampling = directive.Sampling;
                    if (sampling != null)
                    {
                        RegisterNode(ownerByNode, issues, sampling.Anchor, BuildSlotLocation(s, d, "Sampling.Anchor"));
                        RegisterNode(ownerByNode, issues, sampling.AreaSampler, BuildSlotLocation(s, d, "Sampling.AreaSampler"));
                    }

                }
            }

            return issues;
        }

        public static bool RepairSharedManagedReferences(WaveClipSO clip)
        {
            if (clip == null || clip.Segments == null || clip.Segments.Length == 0)
                return false;

            bool changed = false;
            var seenNodes = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var segments = clip.Segments;
            for (int s = 0; s < segments.Length; s++)
            {
                var segment = segments[s];
                if (segment.Directives == null || segment.Directives.Length == 0)
                    continue;

                for (int d = 0; d < segment.Directives.Length; d++)
                {
                    var directive = segment.Directives[d];
                    if (directive == null)
                        continue;

                    directive.Emission = EnsureUniqueClone(directive.Emission, seenNodes, CloneEmission, ref changed);
                    if (directive.Sampling != null)
                    {
                        directive.Sampling.Anchor = EnsureUniqueClone(directive.Sampling.Anchor, seenNodes, CloneSamplingAnchor, ref changed);
                        directive.Sampling.AreaSampler = EnsureUniqueClone(directive.Sampling.AreaSampler, seenNodes, CloneAreaSampler, ref changed);
                    }

                }

                segments[s] = segment;
            }

            if (changed)
                clip.Segments = segments;

            return changed;
        }

        public static WaveClipSO.ClipSegment CreateDefaultSegment()
        {
            return new WaveClipSO.ClipSegment
            {
                StartSec = 0f,
                DurationSec = 1f,
                Directives = new[] { CreateDefaultDirective() },
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                EditorOnlyDescription = string.Empty,
                #endif
            };
        }

        public static WaveSpawnEntryAuthoring CreateDefaultDirective()
        {
            return new WaveSpawnEntryAuthoring
            {
                Emission = new RateFieldEmissionAuthoring(),
                Sampling = new WaveSamplingAuthoring
                {
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new UniformFieldAreaSamplerAuthoring(),
                },
            };
        }

        public static WaveSpawnEntryAuthoring CreatePresetDirective(WaveClipDirectivePresetId preset)
        {
            return preset switch
            {
                WaveClipDirectivePresetId.SingleHazard => CreateDefaultDirective(),
                WaveClipDirectivePresetId.FanBurst => new WaveSpawnEntryAuthoring
                {
                    Emission = new EventBurstEmissionAuthoring(),
                    Sampling = new WaveSamplingAuthoring
                    {
                        Anchor = new SourceCenterSamplingAnchorAuthoring(),
                        AreaSampler = new CenterPointAreaSamplerAuthoring(),
                    },
                },
                WaveClipDirectivePresetId.RadialBurst => new WaveSpawnEntryAuthoring
                {
                    Emission = new EventBurstEmissionAuthoring(),
                    Sampling = new WaveSamplingAuthoring
                    {
                        Anchor = new SourceCenterSamplingAnchorAuthoring(),
                        AreaSampler = new CenterPointAreaSamplerAuthoring(),
                    },
                },
                WaveClipDirectivePresetId.LineNormalFan => new WaveSpawnEntryAuthoring
                {
                    Emission = new EventBurstEmissionAuthoring(),
                    Sampling = new WaveSamplingAuthoring
                    {
                        Anchor = new FixedPointSamplingAnchorAuthoring(),
                        AreaSampler = new CenterPointAreaSamplerAuthoring(),
                    },
                },
                _ => CreateDefaultDirective(),
            };
        }

        public static bool MoveSegment(WaveClipSO clip, int fromIndex, int toIndex)
        {
            if (clip == null || clip.Segments == null)
                return false;

            if (fromIndex < 0 || fromIndex >= clip.Segments.Length || toIndex < 0 || toIndex >= clip.Segments.Length || fromIndex == toIndex)
                return false;

            var segments = new List<WaveClipSO.ClipSegment>(clip.Segments);
            var segment = segments[fromIndex];
            segments.RemoveAt(fromIndex);
            segments.Insert(toIndex, segment);
            clip.Segments = segments.ToArray();
            return true;
        }

        public static bool MoveDirective(WaveClipSO clip, int segmentIndex, int fromIndex, int toIndex)
        {
            if (clip?.Segments == null || segmentIndex < 0 || segmentIndex >= clip.Segments.Length)
                return false;

            var segments = clip.Segments;
            var segment = segments[segmentIndex];
            if (segment.Directives == null)
                return false;

            if (fromIndex < 0 || fromIndex >= segment.Directives.Length || toIndex < 0 || toIndex >= segment.Directives.Length || fromIndex == toIndex)
                return false;

            var directives = new List<WaveSpawnEntryAuthoring>(segment.Directives);
            var directive = directives[fromIndex];
            directives.RemoveAt(fromIndex);
            directives.Insert(toIndex, directive);
            segment.Directives = directives.ToArray();
            segments[segmentIndex] = segment;
            clip.Segments = segments;
            return true;
        }

        public static WaveClipSO.ClipSegment CloneSegment(in WaveClipSO.ClipSegment source)
        {
            return new WaveClipSO.ClipSegment
            {
                StartSec = source.StartSec,
                DurationSec = source.DurationSec,
                Directives = CloneDirectiveArray(source.Directives),
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                EditorOnlyDescription = source.EditorOnlyDescription,
                #endif
            };
        }

        public static WaveSpawnEntryAuthoring CloneDirective(WaveSpawnEntryAuthoring source)
        {
            if (source == null)
                return null;

            return new WaveSpawnEntryAuthoring
            {
                Profile = source.Profile,
                Emission = CloneEmission(source.Emission),
                Sampling = CloneSampling(source.Sampling),
            };
        }

        public static int RepairProjectWaveClipAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(WaveClipSO)}", ProjectWaveClipSearchRoots);
            int repairedCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var clip = AssetDatabase.LoadAssetAtPath<WaveClipSO>(path);
                if (clip == null)
                    continue;

                if (!RepairSharedManagedReferences(clip))
                    continue;

                EditorUtility.SetDirty(clip);
                repairedCount++;
            }

            if (repairedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            return repairedCount;
        }

        public static void RepairProjectWaveClipAssetsBatch()
        {
            int repairedCount = RepairProjectWaveClipAssets();
            Debug.Log($"[WaveClipManagedReferenceGraphUtility] Repaired shared managed references in {repairedCount} WaveClip asset(s).");
        }

        private static void RegisterNode(
            Dictionary<object, string> ownerByNode,
            List<WaveClipSharedManagedReferenceIssue> issues,
            object node,
            string location)
        {
            if (node == null)
                return;

            if (ownerByNode.TryGetValue(node, out string firstLocation))
            {
                issues.Add(new WaveClipSharedManagedReferenceIssue(
                    ExtractSlotName(location),
                    firstLocation,
                    location));
                return;
            }

            ownerByNode.Add(node, location);
        }

        private static string BuildSlotLocation(int segmentIndex, int directiveIndex, string slotName)
        {
            return $"Segments[{segmentIndex}]/Directives[{directiveIndex}]/{slotName}";
        }

        private static string ExtractSlotName(string location)
        {
            int slashIndex = location.LastIndexOf('/');
            return slashIndex >= 0 && slashIndex < location.Length - 1
                ? location.Substring(slashIndex + 1)
                : location;
        }

        private static T EnsureUniqueClone<T>(T value, HashSet<object> seenNodes, Func<T, T> cloneFunc, ref bool changed)
            where T : class
        {
            if (value == null)
                return null;

            if (seenNodes.Add(value))
                return value;

            changed = true;
            return cloneFunc(value);
        }

        private static WaveSpawnEntryAuthoring[] CloneDirectiveArray(WaveSpawnEntryAuthoring[] source)
        {
            if (source == null)
                return null;

            if (source.Length == 0)
                return Array.Empty<WaveSpawnEntryAuthoring>();

            var clone = new WaveSpawnEntryAuthoring[source.Length];
            for (int i = 0; i < source.Length; i++)
                clone[i] = CloneDirective(source[i]);

            return clone;
        }

        private static WaveSamplingAuthoring CloneSampling(WaveSamplingAuthoring source)
        {
            if (source == null)
                return null;

            return new WaveSamplingAuthoring
            {
                SpawnSampleBudget = source.SpawnSampleBudget,
                PlayerNoSpawnRadius = source.PlayerNoSpawnRadius,
                Anchor = CloneSamplingAnchor(source.Anchor),
                AreaSampler = CloneAreaSampler(source.AreaSampler),
            };
        }

        private static WaveEmissionAuthoringBase CloneEmission(WaveEmissionAuthoringBase source)
        {
            return source switch
            {
                null => null,
                RateFieldEmissionAuthoring rateField => new RateFieldEmissionAuthoring
                {
                    SpawnMode = rateField.SpawnMode,
                    MaxActiveDensityPerArea = rateField.MaxActiveDensityPerArea,
                    RatePerSecPerArea = rateField.RatePerSecPerArea,
                },
                PoissonEmissionAuthoring poisson => new PoissonEmissionAuthoring
                {
                    SpawnMode = poisson.SpawnMode,
                    MaxActiveDensityPerArea = poisson.MaxActiveDensityPerArea,
                    MeanEventsPerSec = poisson.MeanEventsPerSec,
                    EventRepeatCount = poisson.EventRepeatCount,
                    EventShotSchedule = poisson.EventShotSchedule,
                    EventShotIntervalSec = poisson.EventShotIntervalSec,
                },
                EventBurstEmissionAuthoring burst => new EventBurstEmissionAuthoring
                {
                    SpawnMode = burst.SpawnMode,
                    MaxActiveDensityPerArea = burst.MaxActiveDensityPerArea,
                    BurstRepeatCount = burst.BurstRepeatCount,
                    BurstIntervalSec = burst.BurstIntervalSec,
                    EventRepeatCount = burst.EventRepeatCount,
                    EventShotSchedule = burst.EventShotSchedule,
                    EventShotIntervalSec = burst.EventShotIntervalSec,
                },
                _ => throw new InvalidOperationException($"Unsupported emission type '{source.GetType().Name}'."),
            };
        }

        private static WaveSamplingAnchorAuthoringBase CloneSamplingAnchor(WaveSamplingAnchorAuthoringBase source)
        {
            return source switch
            {
                null => null,
                SourceCenterSamplingAnchorAuthoring => new SourceCenterSamplingAnchorAuthoring(),
                FixedPointSamplingAnchorAuthoring fixedPoint => new FixedPointSamplingAnchorAuthoring
                {
                    FixedPoint = fixedPoint.FixedPoint,
                },
                PlayerRelativeSamplingAnchorAuthoring playerRelative => new PlayerRelativeSamplingAnchorAuthoring
                {
                    SpawnOffset = playerRelative.SpawnOffset,
                },
                _ => throw new InvalidOperationException($"Unsupported sampling anchor type '{source.GetType().Name}'."),
            };
        }

        private static WaveAreaSamplerAuthoringBase CloneAreaSampler(WaveAreaSamplerAuthoringBase source)
        {
            return source switch
            {
                null => null,
                CenterPointAreaSamplerAuthoring => new CenterPointAreaSamplerAuthoring(),
                UniformFieldAreaSamplerAuthoring => new UniformFieldAreaSamplerAuthoring(),
                PollutionTopKAreaSamplerAuthoring => new PollutionTopKAreaSamplerAuthoring(),
                _ => throw new InvalidOperationException($"Unsupported area sampler type '{source.GetType().Name}'."),
            };
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => obj != null ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj) : 0;
        }
    }
}
