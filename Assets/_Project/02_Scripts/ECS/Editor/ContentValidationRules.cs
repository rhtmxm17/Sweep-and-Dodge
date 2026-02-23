using System;
using System.Collections.Generic;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum ContentValidationSeverity : byte
    {
        Warning = 0,
        Error = 1,
    }

    public readonly struct ContentValidationIssue
    {
        public readonly ContentValidationSeverity Severity;
        public readonly string Code;
        public readonly string Location;
        public readonly string Message;

        public ContentValidationIssue(ContentValidationSeverity severity, string code, string location, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Location = location ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public readonly struct ContentValidationRecord<T> where T : UnityEngine.Object
    {
        public readonly T Value;
        public readonly string Location;

        public ContentValidationRecord(T value, string location)
        {
            Value = value;
            Location = location ?? string.Empty;
        }
    }

    public readonly struct ContentValidationInput
    {
        public readonly IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> Definitions;
        public readonly IReadOnlyList<ContentValidationRecord<WaveTimelineSO>> WaveTimelines;
        public readonly IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> VisualAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<BulletSourceAuthoring>> SourceAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<BulletAuthoring>> BulletAuthorings;

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveTimelineSO>> waveTimelines,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletSourceAuthoring>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings)
        {
            Definitions = definitions ?? Array.Empty<ContentValidationRecord<BulletDefinitionSO>>();
            WaveTimelines = waveTimelines ?? Array.Empty<ContentValidationRecord<WaveTimelineSO>>();
            VisualAuthorings = visualAuthorings ?? Array.Empty<ContentValidationRecord<BulletVisualPrefabAuthoring>>();
            SourceAuthorings = sourceAuthorings ?? Array.Empty<ContentValidationRecord<BulletSourceAuthoring>>();
            BulletAuthorings = bulletAuthorings ?? Array.Empty<ContentValidationRecord<BulletAuthoring>>();
        }
    }

    public static class ContentValidationRules
    {
        public static List<ContentValidationIssue> Validate(in ContentValidationInput input)
        {
            var issues = new List<ContentValidationIssue>(64);

            ValidateDefinitionUniqueness(input.Definitions, issues);
            ValidateDefinitionPrefabReferences(input.Definitions, issues);
            ValidateVisualAuthoringContracts(input.VisualAuthorings, issues);
            ValidateWaveTimelineContracts(input.Definitions, input.WaveTimelines, issues);
            ValidateSourceAuthoringContracts(input.SourceAuthorings, issues);
            ValidateBulletAuthoringRenderContracts(input.BulletAuthorings, issues);
            ValidateAutoCorrectionWarnings(input.Definitions, input.VisualAuthorings, input.SourceAuthorings, issues);

            return issues;
        }

        private static void ValidateDefinitionUniqueness(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            var ownersById = new Dictionary<int, List<string>>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null || def.DefinitionId == 0)
                    continue;

                if (!ownersById.TryGetValue(def.DefinitionId, out var locations))
                {
                    locations = new List<string>(2);
                    ownersById.Add(def.DefinitionId, locations);
                }

                locations.Add(definitions[i].Location);
            }

            foreach (var pair in ownersById)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV001",
                        pair.Value[i],
                        $"Duplicate DefinitionId detected: {pair.Key}. Owners: {joined}"));
                }
            }
        }

        private static void ValidateDefinitionPrefabReferences(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                if (def.Prefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV002",
                        definitions[i].Location,
                        "BulletDefinitionSO.Prefab is null."));
                }
            }
        }

        private static void ValidateVisualAuthoringContracts(
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> authorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < authorings.Count; i++)
            {
                var authoring = authorings[i].Value;
                if (authoring == null || authoring.Definitions == null)
                    continue;

                var duplicateCheck = new HashSet<int>();
                for (int j = 0; j < authoring.Definitions.Length; j++)
                {
                    var def = authoring.Definitions[j];
                    if (def == null)
                        continue;

                    if (!duplicateCheck.Add(def.DefinitionId))
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV003",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring contains duplicate DefinitionId {def.DefinitionId}."));
                    }
                }
            }
        }

        private static void ValidateWaveTimelineContracts(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveTimelineSO>> timelines,
            List<ContentValidationIssue> issues)
        {
            var knownKeys = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def != null && def.DefinitionId != 0)
                    knownKeys.Add(def.DefinitionId);
            }

            for (int i = 0; i < timelines.Count; i++)
            {
                var timeline = timelines[i].Value;
                if (timeline == null || timeline.Segments == null || timeline.Segments.Length <= 0)
                    continue;

                var validSegments = new List<(int Index, float Start, float End)>(timeline.Segments.Length);
                for (int s = 0; s < timeline.Segments.Length; s++)
                {
                    var seg = timeline.Segments[s];
                    if (seg.EndSec <= seg.StartSec)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV010",
                            timelines[i].Location,
                            $"Wave segment has invalid range at segmentIndex={s}. StartSec={seg.StartSec}, EndSec={seg.EndSec}."));
                        continue;
                    }

                    var entries = seg.Entries;
                    if (entries == null || entries.Length <= 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV012",
                            timelines[i].Location,
                            $"Wave segment has no entries at segmentIndex={s}."));
                    }
                    else
                    {
                        for (int e = 0; e < entries.Length; e++)
                        {
                            var entry = entries[e];
                            var bullet = entry.ResolveBullet();
                            if (bullet == null)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV013",
                                    timelines[i].Location,
                                    $"Wave segment has null bullet entry at segmentIndex={s}, entryIndex={e}."));
                                continue;
                            }

                            if (!knownKeys.Contains(bullet.DefinitionId))
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV014",
                                    timelines[i].Location,
                                    $"Wave segment references unknown DefinitionId {bullet.DefinitionId} at segmentIndex={s}, entryIndex={e}."));
                            }

                            var emissionMode = entry.ResolveEmissionMode();
                            if (emissionMode == SourceSpawnEmissionModeId.RateField && entry.ResolveRatePerSecPerArea() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV015",
                                    timelines[i].Location,
                                    $"Wave segment has negative RatePerSecPerArea at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (emissionMode == SourceSpawnEmissionModeId.Poisson && entry.ResolveMeanEventsPerSec() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV017",
                                    timelines[i].Location,
                                    $"Wave segment has negative MeanEventsPerSec at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (entry.ResolveSpawnMode() == SourceSpawnModeId.CapAndMaxDensity && entry.ResolveMaxActiveDensityPerArea() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV016",
                                    timelines[i].Location,
                                    $"Wave segment has negative MaxActiveDensityPerArea for CapAndMaxDensity at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (entry.Sampling.SpawnSampleBudget < 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV018",
                                    timelines[i].Location,
                                    $"Wave segment has negative SpawnSampleBudget at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (entry.ResolvePlayerNoSpawnRadius() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV019",
                                    timelines[i].Location,
                                    $"Wave segment has negative PlayerNoSpawnRadius at segmentIndex={s}, entryIndex={e}."));
                            }
                        }
                    }

                    validSegments.Add((s, seg.StartSec, seg.EndSec));
                }

                validSegments.Sort((a, b) => a.Start.CompareTo(b.Start));
                for (int s = 1; s < validSegments.Count; s++)
                {
                    var prev = validSegments[s - 1];
                    var curr = validSegments[s];
                    if (curr.Start >= prev.End)
                        continue;

                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV011",
                        timelines[i].Location,
                        $"Wave segments overlap: prev(segmentIndex={prev.Index}, [{prev.Start}, {prev.End})) and curr(segmentIndex={curr.Index}, [{curr.Start}, {curr.End}))."));
                }
            }
        }

        private static void ValidateAutoCorrectionWarnings(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletSourceAuthoring>> sourceAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                WarnIf(def.PoolSize < 0, "CVW001", definitions[i].Location, "PoolSize < 0 will be clamped to 0.", issues);
                WarnIf(def.Speed < 0f, "CVW002", definitions[i].Location, "Speed < 0 will be clamped to 0.", issues);
                WarnIf(def.Lifetime < 0f, "CVW003", definitions[i].Location, "Lifetime < 0 will be clamped to 0.", issues);
                WarnIf(def.Radius < 0f, "CVW004", definitions[i].Location, "Radius < 0 will be clamped to 0.", issues);
                WarnIf(def.ScoreValue < 0, "CVW005", definitions[i].Location, "ScoreValue < 0 will be clamped to 0.", issues);
            }

            for (int i = 0; i < visualAuthorings.Count; i++)
            {
                var authoring = visualAuthorings[i].Value;
                if (authoring == null || authoring.Definitions == null)
                    continue;

                for (int d = 0; d < authoring.Definitions.Length; d++)
                {
                    var def = authoring.Definitions[d];
                    if (def == null)
                        continue;

                    WarnIf(def.PoolSize < 0, "CVW011", visualAuthorings[i].Location, "Definition PoolSize < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Speed < 0f, "CVW012", visualAuthorings[i].Location, "Definition Speed < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Lifetime < 0f, "CVW013", visualAuthorings[i].Location, "Definition Lifetime < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.Radius < 0f, "CVW014", visualAuthorings[i].Location, "Definition Radius < 0 will be clamped to 0 at bake.", issues);
                    WarnIf(def.ScoreValue < 0, "CVW015", visualAuthorings[i].Location, "Definition ScoreValue < 0 will be clamped to 0 at bake.", issues);
                }
            }

            for (int i = 0; i < sourceAuthorings.Count; i++)
            {
                var authoring = sourceAuthorings[i].Value;
                if (authoring == null)
                    continue;

                string location = sourceAuthorings[i].Location;

                WarnIf(authoring.ThresholdWeakened < 0, "CVW021", location, "ThresholdWeakened < 0 will be clamped to 0.", issues);
                WarnIf(authoring.ThresholdDepleted < authoring.ThresholdWeakened, "CVW022", location, "ThresholdDepleted < ThresholdWeakened will be clamped up to ThresholdWeakened.", issues);
                WarnIf(authoring.InitialCollectedCount < 0, "CVW023", location, "InitialCollectedCount < 0 will be clamped to 0.", issues);
                WarnIf(authoring.FieldRadius < 0f, "CVW024", location, "FieldRadius < 0 will be clamped to 0.", issues);
                WarnIf(authoring.FieldSize.x < 0f || authoring.FieldSize.y < 0f, "CVW025", location, "FieldSize contains negative values and will be clamped to >= 0.", issues);
                WarnIf(authoring.PollutionCellSize < 0.1f, "CVW026", location, "PollutionCellSize < 0.1 will be clamped to 0.1.", issues);
                WarnIf(authoring.PollutionMin < 0f, "CVW027", location, "PollutionMin < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionMax < authoring.PollutionMin, "CVW028", location, "PollutionMax < PollutionMin will be clamped up to PollutionMin.", issues);
                WarnIf(authoring.PollutionRegenPerSec < 0f, "CVW029", location, "PollutionRegenPerSec < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionDropPerCollect < 0f, "CVW030", location, "PollutionDropPerCollect < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionTopKSampleCount < 1, "CVW031", location, "PollutionTopKSampleCount < 1 will be clamped to 1.", issues);
            }
        }

        private static void WarnIf(bool condition, string code, string location, string message, List<ContentValidationIssue> issues)
        {
            if (!condition)
                return;

            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, code, location, message));
        }

        private static void ValidateSourceAuthoringContracts(
            IReadOnlyList<ContentValidationRecord<BulletSourceAuthoring>> sourceAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < sourceAuthorings.Count; i++)
            {
                var source = sourceAuthorings[i].Value;
                if (source == null)
                    continue;

                if (source.WaveTimeline == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV006",
                        sourceAuthorings[i].Location,
                        "BulletSourceAuthoring.WaveTimeline is null."));
                }
            }
        }

        private static void ValidateBulletAuthoringRenderContracts(
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < bulletAuthorings.Count; i++)
            {
                var bullet = bulletAuthorings[i].Value;
                if (bullet == null)
                    continue;

                var meshRenderers = bullet.GetComponentsInChildren<MeshRenderer>(true);
                var skinnedRenderers = bullet.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                bool hasRenderablePart = (meshRenderers != null && meshRenderers.Length > 0)
                    || (skinnedRenderers != null && skinnedRenderers.Length > 0);

                if (!hasRenderablePart)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV007",
                        bulletAuthorings[i].Location,
                        "BulletAuthoring has no MeshRenderer/SkinnedMeshRenderer render parts."));
                }
            }
        }
    }
}
