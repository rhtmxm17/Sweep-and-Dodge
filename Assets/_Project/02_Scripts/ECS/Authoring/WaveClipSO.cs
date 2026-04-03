using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace SweepNDodge.DotsBullets
{
    public enum SourceWavePhaseId : byte
    {
        Sustain = 0,
        OnStateEnterOnce = 1
    }

    [Serializable]
    public abstract class WaveEmissionAuthoringBase
    {
        public SourceSpawnModeId SpawnMode = SourceSpawnModeId.FixedDensity;
        public float MaxActiveDensityPerArea = 0f;

        public abstract SourceSpawnEmissionModeId EmissionMode { get; }
    }

    [Serializable]
    public sealed class RateFieldEmissionAuthoring : WaveEmissionAuthoringBase
    {
        public float RatePerSecPerArea = 1f;

        public override SourceSpawnEmissionModeId EmissionMode => SourceSpawnEmissionModeId.RateField;
    }

    [Serializable]
    public sealed class PoissonEmissionAuthoring : WaveEmissionAuthoringBase
    {
        public float MeanEventsPerSec = 0f;
        public int BurstShotsPerEvent = 1;
        public SourceSpawnEventShotScheduleId EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
        public float EventShotIntervalSec = 0.1f;

        public override SourceSpawnEmissionModeId EmissionMode => SourceSpawnEmissionModeId.Poisson;
    }

    [Serializable]
    public sealed class EventBurstEmissionAuthoring : WaveEmissionAuthoringBase
    {
        public int BurstRepeatCount = 1;
        public float BurstIntervalSec = 1f;
        public int BurstShotsPerEvent = 1;
        public SourceSpawnEventShotScheduleId EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
        public float EventShotIntervalSec = 0.1f;

        public override SourceSpawnEmissionModeId EmissionMode => SourceSpawnEmissionModeId.EventBurst;
    }

    [Serializable]
    public abstract class WaveSamplingAuthoringBase
    {
        public SourceSpawnCenterModeId CenterMode = SourceSpawnCenterModeId.SourceCenter;
        public Vector2 FixedPoint = Vector2.zero;
        public Vector2 SpawnOffset = Vector2.zero;
        public int SpawnSampleBudget = 16;
        public float PlayerNoSpawnRadius = 0f;

        public abstract SourceSpawnSamplingModeId SamplingMode { get; }
    }

    [Serializable]
    public sealed class UniformFieldSamplingAuthoring : WaveSamplingAuthoringBase
    {
        public override SourceSpawnSamplingModeId SamplingMode => SourceSpawnSamplingModeId.UniformField;
    }

    [Serializable]
    public sealed class PollutionTopKSamplingAuthoring : WaveSamplingAuthoringBase
    {
        public override SourceSpawnSamplingModeId SamplingMode => SourceSpawnSamplingModeId.PollutionTopK;
    }

    [Serializable]
    public sealed class LineEvenSamplingAuthoring : WaveSamplingAuthoringBase
    {
        public Vector2 LineStart = Vector2.zero;
        public Vector2 LineEnd = Vector2.zero;
        public float SampleSpacing = 1f;

        public override SourceSpawnSamplingModeId SamplingMode => SourceSpawnSamplingModeId.LineEven;
    }

    [Serializable]
    public sealed class PointSetSamplingAuthoring : WaveSamplingAuthoringBase
    {
        public Vector2[] Points = Array.Empty<Vector2>();

        public override SourceSpawnSamplingModeId SamplingMode => SourceSpawnSamplingModeId.PointSet;
    }

    [Serializable]
    public abstract class WaveDirectionAuthoringBase
    {
        public abstract SourceSpawnDirectionModeId DirectionMode { get; }
    }

    [Serializable]
    public sealed class RandomDirectionAuthoring : WaveDirectionAuthoringBase
    {
        public override SourceSpawnDirectionModeId DirectionMode => SourceSpawnDirectionModeId.Random;
    }

    [Serializable]
    public sealed class FixedDirectionAuthoring : WaveDirectionAuthoringBase
    {
        public float BaseAngleDeg = 0f;

        public override SourceSpawnDirectionModeId DirectionMode => SourceSpawnDirectionModeId.Fixed;
    }

    [Serializable]
    public sealed class NWayDirectionAuthoring : WaveDirectionAuthoringBase
    {
        public float BaseAngleDeg = 0f;
        public int NWayCount = 2;

        public override SourceSpawnDirectionModeId DirectionMode => SourceSpawnDirectionModeId.NWay;
    }

    [Serializable]
    public sealed class SpiralDirectionAuthoring : WaveDirectionAuthoringBase
    {
        public float BaseAngleDeg = 0f;
        public float SpiralStepDeg = 0f;

        public override SourceSpawnDirectionModeId DirectionMode => SourceSpawnDirectionModeId.Spiral;
    }

    [Serializable]
    public sealed class RadialBurstDirectionAuthoring : WaveDirectionAuthoringBase
    {
        public float BaseAngleDeg = 0f;

        public override SourceSpawnDirectionModeId DirectionMode => SourceSpawnDirectionModeId.RadialBurst;
    }

    [Serializable]
    public sealed class WaveSpawnEntryAuthoring
    {
        [Header("Payload")]
        public WaveClipSO.SpawnPayloadProfile Payload;

        [Header("Directive")]
        [SerializeReference] public WaveEmissionAuthoringBase Emission = new RateFieldEmissionAuthoring();
        [SerializeReference] public WaveSamplingAuthoringBase Sampling = new UniformFieldSamplingAuthoring();
        [SerializeReference] public WaveDirectionAuthoringBase Direction = new RandomDirectionAuthoring();
    }

    [CreateAssetMenu(menuName = "SweepNDodge/Bullet/Wave Clip", fileName = "bwc_")]
    public class WaveClipSO : ScriptableObject
    {
        [Serializable]
        public struct SpawnPayloadProfile
        {
            public BulletDefinitionSO Bullet;
        }

        [Serializable]
        public struct SpawnEmissionProfile
        {
            public SourceSpawnEmissionModeId EmissionMode;
            public SourceSpawnModeId SpawnMode;
            public float RatePerSecPerArea;
            public float MeanEventsPerSec;
            public int BurstRepeatCount;
            public float BurstIntervalSec;
            public int BurstShotsPerEvent;
            public SourceSpawnEventShotScheduleId EventShotSchedule;
            public float EventShotIntervalSec;
            public float MaxActiveDensityPerArea;
        }

        [Serializable]
        public struct SpawnSamplingProfile
        {
            public const int PointSetMaxCount = 4;

            public SourceSpawnSamplingModeId SamplingMode;
            public SourceSpawnCenterModeId CenterMode;
            public Vector2 FixedPoint;
            public Vector2 SpawnOffset;
            public Vector2 LineStart;
            public Vector2 LineEnd;
            public float SampleSpacing;
            public int PointCount;
            public Vector2 Point0;
            public Vector2 Point1;
            public Vector2 Point2;
            public Vector2 Point3;
            public int SpawnSampleBudget;
            public float PlayerNoSpawnRadius;
        }

        [Serializable]
        public struct SpawnDirectionProfile
        {
            public SourceSpawnDirectionModeId DirectionMode;
            public float BaseAngleDeg;
            public int NWayCount;
            public float SpiralStepDeg;
        }

        [Serializable]
        public struct SpawnEntry
        {
            [Header("Payload")]
            public SpawnPayloadProfile Payload;

            [Header("Directive Profiles")]
            public SpawnEmissionProfile Emission;
            public SpawnSamplingProfile Sampling;
            public SpawnDirectionProfile Direction;

            public BulletDefinitionSO ResolveBullet()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).Bullet;
            }

            public SourceSpawnEmissionModeId ResolveEmissionMode()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).EmissionMode;
            }

            public SourceSpawnModeId ResolveSpawnMode()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SpawnMode;
            }

            public float ResolveRatePerSecPerArea()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).RatePerSecPerArea;
            }

            public float ResolveMaxActiveDensityPerArea()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).MaxActiveDensityPerArea;
            }

            public float ResolveMeanEventsPerSec()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).MeanEventsPerSec;
            }

            public int ResolveBurstRepeatCount()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).BurstRepeatCount;
            }

            public float ResolveBurstIntervalSec()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).BurstIntervalSec;
            }

            public int ResolveBurstShotsPerEvent()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).BurstShotsPerEvent;
            }

            public SourceSpawnEventShotScheduleId ResolveEventShotSchedule()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).EventShotSchedule;
            }

            public float ResolveEventShotIntervalSec()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).EventShotIntervalSec;
            }

            public SourceSpawnSamplingModeId ResolveSamplingMode()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SamplingMode;
            }

            public SourceSpawnCenterModeId ResolveCenterMode()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).CenterMode;
            }

            public Vector2 ResolveFixedPoint()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).FixedPoint;
            }

            public Vector2 ResolveSpawnOffset()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SpawnOffset;
            }

            public Vector2 ResolveLineStart()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).LineStart;
            }

            public Vector2 ResolveLineEnd()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).LineEnd;
            }

            public float ResolveSampleSpacing()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SampleSpacing;
            }

            public int ResolvePointSetCount()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).PointSetCount;
            }

            public Vector2 ResolvePointSetPoint(int index)
            {
                var snapshot = WaveClipAuthoringResolver.ResolveLegacyEntry(in this);
                return index switch
                {
                    0 => snapshot.Point0,
                    1 => snapshot.Point1,
                    2 => snapshot.Point2,
                    3 => snapshot.Point3,
                    _ => Vector2.zero,
                };
            }

            public SourceSpawnDirectionModeId ResolveDirectionMode()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).DirectionMode;
            }

            public float ResolveBaseAngleDeg()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).BaseAngleDeg;
            }

            public int ResolveNWayCount()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).NWayCount;
            }

            public float ResolveSpiralStepDeg()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SpiralStepDeg;
            }

            public int ResolveSpawnSampleBudget()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).SpawnSampleBudget;
            }

            public float ResolvePlayerNoSpawnRadius()
            {
                return WaveClipAuthoringResolver.ResolveLegacyEntry(in this).PlayerNoSpawnRadius;
            }
        }

        [Serializable]
        public struct ClipSegment
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            [SerializeField, TextArea(2, 5)]
            private string editorOnlyDescription;
            #endif
            public float StartSec;
            public float EndSec;
            public WaveSpawnEntryAuthoring[] Directives;
            [FormerlySerializedAs("Entries")] public SpawnEntry[] LegacyEntries;

            [Obsolete("Use Directives for typed authoring or LegacyEntries for migration fallback.")]
            public SpawnEntry[] Entries
            {
                get => LegacyEntries;
                set => LegacyEntries = value;
            }
        }

        [Header("Clip Metadata")]
        public int ClipId = 1;
        public SourceWavePhaseId Phase = SourceWavePhaseId.Sustain;
        public SourceSpawnLaneId Lane = SourceSpawnLaneId.Hazard;
        public float DurationSec = 1f;

        [Header("Local Segments (Overlap Allowed)")]
        public ClipSegment[] Segments;
    }
}
