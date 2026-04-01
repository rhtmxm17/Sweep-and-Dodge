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
        public readonly IReadOnlyList<ContentValidationRecord<WaveClipSO>> WaveClips;
        public readonly IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> TopologyPrefabCatalogs;
        public readonly IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> VisualAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> SourceAuthorings;
        public readonly IReadOnlyList<ContentValidationRecord<BulletAuthoring>> BulletAuthorings;

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> waveClips,
            IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> topologyPrefabCatalogs,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings)
        {
            Definitions = definitions ?? Array.Empty<ContentValidationRecord<BulletDefinitionSO>>();
            WaveClips = waveClips ?? Array.Empty<ContentValidationRecord<WaveClipSO>>();
            TopologyPrefabCatalogs = topologyPrefabCatalogs ?? Array.Empty<ContentValidationRecord<StageTopologyPrefabCatalogSO>>();
            VisualAuthorings = visualAuthorings ?? Array.Empty<ContentValidationRecord<BulletVisualPrefabAuthoring>>();
            SourceAuthorings = sourceAuthorings ?? Array.Empty<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>>();
            BulletAuthorings = bulletAuthorings ?? Array.Empty<ContentValidationRecord<BulletAuthoring>>();
        }

        public ContentValidationInput(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> waveClips,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
            IReadOnlyList<ContentValidationRecord<BulletAuthoring>> bulletAuthorings)
            : this(definitions, waveClips, null, visualAuthorings, sourceAuthorings, bulletAuthorings)
        {
        }
    }

    public static class ContentValidationRules
    {
        public static List<ContentValidationIssue> Validate(in ContentValidationInput input)
        {
            var issues = new List<ContentValidationIssue>(64);

            ValidateDefinitionUniqueness(input.Definitions, issues);
            ValidateDefinitionPrefabReferences(input.Definitions, issues);
            ValidateDefinitionBehaviorContracts(input.Definitions, issues);
            ValidateStageTopologyPrefabCatalogContracts(input.TopologyPrefabCatalogs, issues);
            ValidateVisualAuthoringContracts(input.VisualAuthorings, issues);
            ValidateWaveClipContracts(input.Definitions, input.WaveClips, issues);
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

        private static void ValidateDefinitionBehaviorContracts(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            List<ContentValidationIssue> issues)
        {
            var knownKeys = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def != null && def.DefinitionId > 0)
                    knownKeys.Add(def.DefinitionId);
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def == null)
                    continue;

                string location = definitions[i].Location;
                ValidateMovementDefinition(def, location, issues);
                ValidateReactionDefinition(def.OnMotionCompletedExplode, knownKeys, location, "OnMotionCompletedExplode", issues);
                ValidateReactionDefinition(def.OnCollectedSpawnSecondary, knownKeys, location, "OnCollectedSpawnSecondary", issues);
                ValidateForbiddenOptionalAuthorings(def, location, issues);
            }
        }

        private static void ValidateVisualAuthoringContracts(
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> authorings,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < authorings.Count; i++)
            {
                var authoring = authorings[i].Value;
                if (authoring == null)
                    continue;

                if (authoring.Definitions == null || authoring.Definitions.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV004",
                        authorings[i].Location,
                        "BulletVisualPrefabAuthoring.Definitions buffer is null or empty."));
                    continue;
                }

                var duplicateCheck = new HashSet<int>();
                for (int j = 0; j < authoring.Definitions.Length; j++)
                {
                    var def = authoring.Definitions[j];
                    if (def == null)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV005",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring.Definitions[{j}] is null."));
                        continue;
                    }

                    if (def.DefinitionId <= 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV027",
                            authorings[i].Location,
                            $"BulletVisualPrefabAuthoring.Definitions[{j}] has invalid DefinitionId {def.DefinitionId}."));
                        continue;
                    }

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

        private static void ValidateWaveClipContracts(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<WaveClipSO>> clips,
            List<ContentValidationIssue> issues)
        {
            var knownKeys = new HashSet<int>();
            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i].Value;
                if (def != null && def.DefinitionId != 0)
                    knownKeys.Add(def.DefinitionId);
            }

            var clipOwnersByClipId = new Dictionary<int, List<string>>();
            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i].Value;
                if (clip == null || clip.ClipId <= 0)
                    continue;

                if (!clipOwnersByClipId.TryGetValue(clip.ClipId, out var owners))
                {
                    owners = new List<string>(2);
                    clipOwnersByClipId.Add(clip.ClipId, owners);
                }

                owners.Add(clips[i].Location);
            }

            foreach (var pair in clipOwnersByClipId)
            {
                if (pair.Value.Count <= 1)
                    continue;

                string joined = string.Join(", ", pair.Value);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV009",
                        pair.Value[i],
                        $"Duplicate ClipId detected: {pair.Key}. Owners: {joined}"));
                }
            }

            for (int i = 0; i < clips.Count; i++)
            {
                var clip = clips[i].Value;
                if (clip == null)
                    continue;

                if (clip.Segments == null || clip.Segments.Length <= 0)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV008",
                        clips[i].Location,
                        "WaveClipSO.Segments buffer is null or empty."));
                    continue;
                }

                for (int s = 0; s < clip.Segments.Length; s++)
                {
                    var seg = clip.Segments[s];
                    if (seg.EndSec <= seg.StartSec)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV010",
                            clips[i].Location,
                            $"Clip segment has invalid range at segmentIndex={s}. StartSec={seg.StartSec}, EndSec={seg.EndSec}."));
                        continue;
                    }

                    var entries = seg.Entries;
                    if (entries == null || entries.Length <= 0)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV012",
                            clips[i].Location,
                            $"Clip segment has no entries at segmentIndex={s}."));
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
                                    clips[i].Location,
                                    $"Clip segment has null bullet entry at segmentIndex={s}, entryIndex={e}."));
                                continue;
                            }

                            if (bullet.DefinitionId <= 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV027",
                                    clips[i].Location,
                                    $"Clip segment references invalid DefinitionId {bullet.DefinitionId} at segmentIndex={s}, entryIndex={e}."));
                                continue;
                            }

                            if (!knownKeys.Contains(bullet.DefinitionId))
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV014",
                                    clips[i].Location,
                                    $"Clip segment references unknown DefinitionId {bullet.DefinitionId} at segmentIndex={s}, entryIndex={e}."));
                            }

                            var emissionMode = entry.ResolveEmissionMode();
                            if (emissionMode == SourceSpawnEmissionModeId.RateField && entry.ResolveRatePerSecPerArea() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV015",
                                    clips[i].Location,
                                    $"Clip segment has negative RatePerSecPerArea at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (emissionMode == SourceSpawnEmissionModeId.Poisson && entry.ResolveMeanEventsPerSec() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV017",
                                    clips[i].Location,
                                    $"Clip segment has negative MeanEventsPerSec at segmentIndex={s}, entryIndex={e}."));
                            }

                            if ((emissionMode == SourceSpawnEmissionModeId.Poisson
                                 || emissionMode == SourceSpawnEmissionModeId.EventBurst)
                                && entry.Emission.BurstShotsPerEvent < 1)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV022",
                                    clips[i].Location,
                                    $"Clip segment has invalid BurstShotsPerEvent at segmentIndex={s}, entryIndex={e}."));
                            }

                            if ((emissionMode == SourceSpawnEmissionModeId.Poisson
                                 || emissionMode == SourceSpawnEmissionModeId.EventBurst)
                                && entry.ResolveEventShotSchedule() == SourceSpawnEventShotScheduleId.Timed
                                && entry.Emission.EventShotIntervalSec <= 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV029",
                                    clips[i].Location,
                                    $"Clip segment uses Timed EventShotSchedule with non-positive EventShotIntervalSec at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (emissionMode == SourceSpawnEmissionModeId.EventBurst)
                            {
                                if (entry.Emission.BurstIntervalSec <= 0f)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV020",
                                        clips[i].Location,
                                        $"Clip segment has non-positive BurstIntervalSec at segmentIndex={s}, entryIndex={e}."));
                                }

                                int repeatCount = entry.Emission.BurstRepeatCount;
                                if (repeatCount == 0 || repeatCount < -1)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV021",
                                        clips[i].Location,
                                        $"Clip segment has invalid BurstRepeatCount at segmentIndex={s}, entryIndex={e}. Use -1 or >= 1."));
                                }
                            }

                            if (entry.ResolveSpawnMode() == SourceSpawnModeId.CapAndMaxDensity && entry.ResolveMaxActiveDensityPerArea() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV016",
                                    clips[i].Location,
                                    $"Clip segment has negative MaxActiveDensityPerArea for CapAndMaxDensity at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (entry.Sampling.SpawnSampleBudget < 0)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV018",
                                    clips[i].Location,
                                    $"Clip segment has negative SpawnSampleBudget at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (entry.ResolvePlayerNoSpawnRadius() < 0f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV019",
                                    clips[i].Location,
                                    $"Clip segment has negative PlayerNoSpawnRadius at segmentIndex={s}, entryIndex={e}."));
                            }

                            var directionMode = entry.ResolveDirectionMode();
                            if (directionMode == SourceSpawnDirectionModeId.NWay && entry.ResolveNWayCount() < 2)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV023",
                                    clips[i].Location,
                                    $"Clip segment uses NWay with NWayCount < 2 at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (directionMode == SourceSpawnDirectionModeId.RadialBurst && entry.Emission.BurstShotsPerEvent < 2)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Error,
                                    "CV024",
                                    clips[i].Location,
                                    $"Clip segment uses RadialBurst with BurstShotsPerEvent < 2 at segmentIndex={s}, entryIndex={e}."));
                            }

                            if (directionMode == SourceSpawnDirectionModeId.Spiral && Mathf.Abs(entry.ResolveSpiralStepDeg()) < 0.0001f)
                            {
                                issues.Add(new ContentValidationIssue(
                                    ContentValidationSeverity.Warning,
                                    "CVW032",
                                    clips[i].Location,
                                    $"Clip segment uses Spiral with near-zero SpiralStepDeg at segmentIndex={s}, entryIndex={e}."));
                            }

                            var samplingMode = entry.ResolveSamplingMode();
                            if (samplingMode == SourceSpawnSamplingModeId.LineEven)
                            {
                                float len = (entry.ResolveLineEnd() - entry.ResolveLineStart()).magnitude;
                                if (len <= 0f || entry.Sampling.SampleSpacing <= 0f)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV026",
                                        clips[i].Location,
                                        $"Clip segment has invalid LineEven parameters at segmentIndex={s}, entryIndex={e}."));
                                }
                            }

                            if (samplingMode == SourceSpawnSamplingModeId.PointSet)
                            {
                                if (entry.Sampling.PointCount <= 0)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Error,
                                        "CV028",
                                        clips[i].Location,
                                        $"Clip segment uses PointSet with PointCount <= 0 at segmentIndex={s}, entryIndex={e}."));
                                }

                                if (entry.Sampling.PointCount > WaveClipSO.SpawnSamplingProfile.PointSetMaxCount)
                                {
                                    issues.Add(new ContentValidationIssue(
                                        ContentValidationSeverity.Warning,
                                        "CVW033",
                                        clips[i].Location,
                                        $"Clip segment PointSet PointCount exceeds max({WaveClipSO.SpawnSamplingProfile.PointSetMaxCount}) and will be clamped at segmentIndex={s}, entryIndex={e}."));
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateAutoCorrectionWarnings(
            IReadOnlyList<ContentValidationRecord<BulletDefinitionSO>> definitions,
            IReadOnlyList<ContentValidationRecord<BulletVisualPrefabAuthoring>> visualAuthorings,
            IReadOnlyList<ContentValidationRecord<SourceRuntimeTemplateAuthoringBase>> sourceAuthorings,
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

                WarnIf(authoring.Radius < 0f, "CVW024", location, "Radius < 0 will be clamped to 0.", issues);
                WarnIf(authoring.Size.x < 0f || authoring.Size.y < 0f, "CVW025", location, "Size contains negative values and will be clamped to >= 0.", issues);
                WarnIf(authoring.PollutionCellSize < 0.1f, "CVW026", location, "PollutionCellSize < 0.1 will be clamped to 0.1.", issues);
                WarnIf(authoring.PollutionMin < 0f, "CVW027", location, "PollutionMin < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionMax < authoring.PollutionMin, "CVW028", location, "PollutionMax < PollutionMin will be clamped up to PollutionMin.", issues);
                WarnIf(authoring.PollutionRegenPerSec < 0f, "CVW029", location, "PollutionRegenPerSec < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionDropPerCollect < 0f, "CVW030", location, "PollutionDropPerCollect < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionTopKSampleCount < 1, "CVW031", location, "PollutionTopKSampleCount < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionActiveRatioThreshold < 0f || authoring.PollutionActiveRatioThreshold > 1f, "CVW034", location, "PollutionActiveRatioThreshold will be clamped to [0, 1].", issues);
                WarnIf(authoring.PollutionRecoveryCooldownFrames < 0, "CVW035", location, "PollutionRecoveryCooldownFrames < 0 will be clamped to 0.", issues);
                WarnIf(authoring.PollutionRecoveryWaveSeedCount < 1, "CVW036", location, "PollutionRecoveryWaveSeedCount < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionRecoveryWaveClusterSize < 1, "CVW037", location, "PollutionRecoveryWaveClusterSize < 1 will be clamped to 1.", issues);
                WarnIf(authoring.PollutionRecoveryRestoreValue < authoring.PollutionMin || authoring.PollutionRecoveryRestoreValue > authoring.PollutionMax, "CVW038", location, "PollutionRecoveryRestoreValue will be clamped to [PollutionMin, PollutionMax].", issues);
                WarnIf(authoring.PollutionRecoveryRecentCleanBiasFrames < 0, "CVW039", location, "PollutionRecoveryRecentCleanBiasFrames < 0 will be clamped to 0.", issues);
            }
        }

        private static void ValidateStageTopologyPrefabCatalogContracts(
            IReadOnlyList<ContentValidationRecord<StageTopologyPrefabCatalogSO>> catalogs,
            List<ContentValidationIssue> issues)
        {
            for (int i = 0; i < catalogs.Count; i++)
            {
                var catalog = catalogs[i].Value;
                if (catalog == null)
                    continue;

                if (catalog.SourceTemplatePrefab == null)
                {
                    issues.Add(new ContentValidationIssue(
                        ContentValidationSeverity.Error,
                        "CV030",
                        catalogs[i].Location,
                        "StageTopologyPrefabCatalogSO.SourceTemplatePrefab is null."));
                }
            }
        }

        private static void ValidateMovementDefinition(
            BulletDefinitionSO definition,
            string location,
            List<ContentValidationIssue> issues)
        {
            switch (definition.MovementFamily)
            {
                case BulletMovementFamilyId.DampedLinear:
                    if (definition.DampedLinear.DampingPerSec < 0f || definition.DampedLinear.StopSpeedThreshold < 0f)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV031",
                            location,
                            "DampedLinear movement requires non-negative DampingPerSec and StopSpeedThreshold."));
                    }
                    break;

                case BulletMovementFamilyId.HomingLite:
                    if (definition.HomingLite.TurnRateDegPerSec < 0f
                        || definition.HomingLite.MaxAcquireDistance < 0f
                        || definition.HomingLite.MinRetargetDistance < 0f
                        || definition.HomingLite.MinRetargetDistance > definition.HomingLite.MaxAcquireDistance)
                    {
                        issues.Add(new ContentValidationIssue(
                            ContentValidationSeverity.Error,
                            "CV032",
                            location,
                            "HomingLite movement requires non-negative parameters and MinRetargetDistance <= MaxAcquireDistance."));
                    }
                    break;
            }
        }

        private static void ValidateReactionDefinition(
            in BulletSecondarySpawnReactionDefinition reaction,
            HashSet<int> knownKeys,
            string location,
            string reactionName,
            List<ContentValidationIssue> issues)
        {
            if (!reaction.Enabled)
                return;

            if (reaction.SpawnCount <= 0
                || reaction.SpreadAngleDeg < 0f
                || reaction.SpawnRadius < 0f
                || reaction.SecondaryBullet == null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV033",
                    location,
                    $"{reactionName} has invalid enabled reaction parameters."));
                return;
            }

            if (reaction.SecondaryBullet.DefinitionId <= 0)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV035",
                    location,
                    $"{reactionName} references invalid SecondaryBullet definition id {reaction.SecondaryBullet.DefinitionId}."));
                return;
            }

            if (!knownKeys.Contains(reaction.SecondaryBullet.DefinitionId))
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV035",
                    location,
                    $"{reactionName} references unknown SecondaryBullet definition id {reaction.SecondaryBullet.DefinitionId}."));
            }
        }

        private static void ValidateForbiddenOptionalAuthorings(
            BulletDefinitionSO definition,
            string location,
            List<ContentValidationIssue> issues)
        {
            if (definition.Prefab == null)
                return;

            var prefab = definition.Prefab;
            if (prefab.GetComponent<BulletDampedMotionAuthoring>() != null
                || prefab.GetComponent<BulletHomingLiteMotionAuthoring>() != null
                || prefab.GetComponent<BulletOnMotionCompletedExplodeReactionAuthoring>() != null
                || prefab.GetComponent<BulletOnCollectedSpawnSecondaryReactionAuthoring>() != null)
            {
                issues.Add(new ContentValidationIssue(
                    ContentValidationSeverity.Error,
                    "CV034",
                    location,
                    "BulletDefinitionSO.Prefab contains forbidden optional bullet behavior authoring. Use BulletDefinitionSO schema instead."));
            }
        }

        private static void WarnIf(bool condition, string code, string location, string message, List<ContentValidationIssue> issues)
        {
            if (!condition)
                return;

            issues.Add(new ContentValidationIssue(ContentValidationSeverity.Warning, code, location, message));
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

