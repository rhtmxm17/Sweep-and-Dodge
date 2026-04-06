using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
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

                    RegisterNode(ownerByNode, issues, directive.PositionPattern, BuildSlotLocation(s, d, nameof(WaveSpawnEntryAuthoring.PositionPattern)));
                    RegisterNode(ownerByNode, issues, directive.Aim, BuildSlotLocation(s, d, nameof(WaveSpawnEntryAuthoring.Aim)));
                    RegisterNode(ownerByNode, issues, directive.ShotPattern, BuildSlotLocation(s, d, nameof(WaveSpawnEntryAuthoring.ShotPattern)));
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

                    directive.PositionPattern = EnsureUniqueClone(directive.PositionPattern, seenNodes, ClonePositionPattern, ref changed);
                    directive.Aim = EnsureUniqueClone(directive.Aim, seenNodes, CloneAim, ref changed);
                    directive.ShotPattern = EnsureUniqueClone(directive.ShotPattern, seenNodes, CloneShotPattern, ref changed);
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
                EndSec = 1f,
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
                Payload = new WaveClipSO.SpawnPayloadProfile(),
                Emission = new RateFieldEmissionAuthoring(),
                Sampling = new WaveSamplingAuthoring
                {
                    Anchor = new SourceCenterSamplingAnchorAuthoring(),
                    AreaSampler = new UniformFieldAreaSamplerAuthoring(),
                },
                PositionPattern = new SinglePointPositionPatternAuthoring(),
                Aim = new RandomAimAuthoring(),
                ShotPattern = new SingleShotPatternAuthoring(),
            };
        }

        public static WaveClipSO.ClipSegment CloneSegment(in WaveClipSO.ClipSegment source)
        {
            return new WaveClipSO.ClipSegment
            {
                StartSec = source.StartSec,
                EndSec = source.EndSec,
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
                Payload = source.Payload,
                Emission = CloneEmission(source.Emission),
                Sampling = CloneSampling(source.Sampling),
                PositionPattern = ClonePositionPattern(source.PositionPattern),
                Aim = CloneAim(source.Aim),
                ShotPattern = CloneShotPattern(source.ShotPattern),
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

        private static WavePositionPatternAuthoringBase ClonePositionPattern(WavePositionPatternAuthoringBase source)
        {
            return source switch
            {
                null => null,
                SinglePointPositionPatternAuthoring => new SinglePointPositionPatternAuthoring(),
                LineEvenPositionPatternAuthoring lineEven => new LineEvenPositionPatternAuthoring
                {
                    LineStart = lineEven.LineStart,
                    LineEnd = lineEven.LineEnd,
                    SampleSpacing = lineEven.SampleSpacing,
                },
                PointSetPositionPatternAuthoring pointSet => new PointSetPositionPatternAuthoring
                {
                    Points = pointSet.Points != null ? (Vector2[])pointSet.Points.Clone() : null,
                },
                _ => throw new InvalidOperationException($"Unsupported position pattern type '{source.GetType().Name}'."),
            };
        }

        private static WaveAimAuthoringBase CloneAim(WaveAimAuthoringBase source)
        {
            return source switch
            {
                null => null,
                RandomAimAuthoring => new RandomAimAuthoring(),
                FixedAimAuthoring fixedAim => new FixedAimAuthoring
                {
                    BaseAngleDeg = fixedAim.BaseAngleDeg,
                },
                SpiralAimAuthoring spiralAim => new SpiralAimAuthoring
                {
                    BaseAngleDeg = spiralAim.BaseAngleDeg,
                    SpiralStepDeg = spiralAim.SpiralStepDeg,
                },
                PlayerPositionAimAuthoring playerPositionAim => new PlayerPositionAimAuthoring
                {
                    AngleOffsetDeg = playerPositionAim.AngleOffsetDeg,
                    SnapshotTiming = playerPositionAim.SnapshotTiming,
                },
                _ => throw new InvalidOperationException($"Unsupported aim type '{source.GetType().Name}'."),
            };
        }

        private static WaveShotPatternAuthoringBase CloneShotPattern(WaveShotPatternAuthoringBase source)
        {
            return source switch
            {
                null => null,
                SingleShotPatternAuthoring => new SingleShotPatternAuthoring(),
                NWayShotPatternAuthoring nWay => new NWayShotPatternAuthoring
                {
                    ShotCount = nWay.ShotCount,
                },
                RadialShotPatternAuthoring radial => new RadialShotPatternAuthoring
                {
                    ShotCount = radial.ShotCount,
                },
                _ => throw new InvalidOperationException($"Unsupported shot pattern type '{source.GetType().Name}'."),
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
