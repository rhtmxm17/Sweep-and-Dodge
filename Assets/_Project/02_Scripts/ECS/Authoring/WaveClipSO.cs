using System;
using UnityEngine;

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
        public const int MaxPointCount = 4;

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
        public struct ClipSegment
        {
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            [SerializeField, TextArea(2, 5)]
            private string editorOnlyDescription;
            #endif
            public float StartSec;
            public float EndSec;
            public WaveSpawnEntryAuthoring[] Directives;
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
