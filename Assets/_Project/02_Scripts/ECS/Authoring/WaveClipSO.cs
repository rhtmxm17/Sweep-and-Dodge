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

    public enum WaveSamplingAnchorModeId : byte
    {
        SourceCenter = 0,
        FixedPoint = 1,
        PlayerRelative = 2,
    }

    public enum WaveAreaSamplerModeId : byte
    {
        CenterPoint = 0,
        UniformField = 1,
        PollutionTopK = 2,
    }

    public enum WavePositionPatternModeId : byte
    {
        SinglePoint = 0,
        LineEven = 1,
        PointSet = 2,
    }

    public enum WaveAimModeId : byte
    {
        Random = 0,
        Fixed = 1,
        Spiral = 2,
        PlayerPosition = 3,
    }

    public enum WaveAimSnapshotTimingId : byte
    {
        EventStart = 0,
        PerShot = 1,
    }

    public enum WaveShotPatternModeId : byte
    {
        Single = 0,
        NWay = 1,
        Radial = 2,
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
        [FormerlySerializedAs("BurstShotsPerEvent")] public int EventRepeatCount = 1;
        public SourceSpawnEventShotScheduleId EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
        public float EventShotIntervalSec = 0.1f;

        public override SourceSpawnEmissionModeId EmissionMode => SourceSpawnEmissionModeId.Poisson;
    }

    [Serializable]
    public sealed class EventBurstEmissionAuthoring : WaveEmissionAuthoringBase
    {
        public int BurstRepeatCount = 1;
        public float BurstIntervalSec = 1f;
        [FormerlySerializedAs("BurstShotsPerEvent")] public int EventRepeatCount = 1;
        public SourceSpawnEventShotScheduleId EventShotSchedule = SourceSpawnEventShotScheduleId.Instant;
        public float EventShotIntervalSec = 0.1f;

        public override SourceSpawnEmissionModeId EmissionMode => SourceSpawnEmissionModeId.EventBurst;
    }

    [Serializable]
    public sealed class WaveSamplingAuthoring
    {
        public int SpawnSampleBudget = 16;
        public float PlayerNoSpawnRadius = 0f;
        [SerializeReference] public WaveSamplingAnchorAuthoringBase Anchor = new SourceCenterSamplingAnchorAuthoring();
        [SerializeReference] public WaveAreaSamplerAuthoringBase AreaSampler = new UniformFieldAreaSamplerAuthoring();
    }

    [Serializable]
    public abstract class WaveSamplingAnchorAuthoringBase
    {
        public abstract WaveSamplingAnchorModeId AnchorMode { get; }
    }

    [Serializable]
    public sealed class SourceCenterSamplingAnchorAuthoring : WaveSamplingAnchorAuthoringBase
    {
        public override WaveSamplingAnchorModeId AnchorMode => WaveSamplingAnchorModeId.SourceCenter;
    }

    [Serializable]
    public sealed class FixedPointSamplingAnchorAuthoring : WaveSamplingAnchorAuthoringBase
    {
        public Vector2 FixedPoint = Vector2.zero;

        public override WaveSamplingAnchorModeId AnchorMode => WaveSamplingAnchorModeId.FixedPoint;
    }

    [Serializable]
    public sealed class PlayerRelativeSamplingAnchorAuthoring : WaveSamplingAnchorAuthoringBase
    {
        public Vector2 SpawnOffset = Vector2.zero;

        public override WaveSamplingAnchorModeId AnchorMode => WaveSamplingAnchorModeId.PlayerRelative;
    }

    [Serializable]
    public abstract class WaveAreaSamplerAuthoringBase
    {
        public abstract WaveAreaSamplerModeId AreaSamplerMode { get; }
    }

    [Serializable]
    public sealed class CenterPointAreaSamplerAuthoring : WaveAreaSamplerAuthoringBase
    {
        public override WaveAreaSamplerModeId AreaSamplerMode => WaveAreaSamplerModeId.CenterPoint;
    }

    [Serializable]
    public sealed class UniformFieldAreaSamplerAuthoring : WaveAreaSamplerAuthoringBase
    {
        public override WaveAreaSamplerModeId AreaSamplerMode => WaveAreaSamplerModeId.UniformField;
    }

    [Serializable]
    public sealed class PollutionTopKAreaSamplerAuthoring : WaveAreaSamplerAuthoringBase
    {
        public override WaveAreaSamplerModeId AreaSamplerMode => WaveAreaSamplerModeId.PollutionTopK;
    }

    [Serializable]
    public abstract class WavePositionPatternAuthoringBase
    {
        public abstract WavePositionPatternModeId PositionPatternMode { get; }
    }

    [Serializable]
    public sealed class SinglePointPositionPatternAuthoring : WavePositionPatternAuthoringBase
    {
        public override WavePositionPatternModeId PositionPatternMode => WavePositionPatternModeId.SinglePoint;
    }

    [Serializable]
    public sealed class LineEvenPositionPatternAuthoring : WavePositionPatternAuthoringBase
    {
        public Vector2 LineStart = Vector2.zero;
        public Vector2 LineEnd = Vector2.zero;
        public float SampleSpacing = 1f;

        public override WavePositionPatternModeId PositionPatternMode => WavePositionPatternModeId.LineEven;
    }

    [Serializable]
    public sealed class PointSetPositionPatternAuthoring : WavePositionPatternAuthoringBase
    {
        public const int MaxPointCount = 4;

        public Vector2[] Points = Array.Empty<Vector2>();

        public override WavePositionPatternModeId PositionPatternMode => WavePositionPatternModeId.PointSet;
    }

    [Serializable]
    public abstract class WaveAimAuthoringBase
    {
        public abstract WaveAimModeId AimMode { get; }
    }

    [Serializable]
    public sealed class RandomAimAuthoring : WaveAimAuthoringBase
    {
        public override WaveAimModeId AimMode => WaveAimModeId.Random;
    }

    [Serializable]
    public sealed class FixedAimAuthoring : WaveAimAuthoringBase
    {
        public float BaseAngleDeg = 0f;

        public override WaveAimModeId AimMode => WaveAimModeId.Fixed;
    }

    [Serializable]
    public sealed class SpiralAimAuthoring : WaveAimAuthoringBase
    {
        public float BaseAngleDeg = 0f;
        public float SpiralStepDeg = 0f;

        public override WaveAimModeId AimMode => WaveAimModeId.Spiral;
    }

    [Serializable]
    public sealed class PlayerPositionAimAuthoring : WaveAimAuthoringBase
    {
        public float AngleOffsetDeg = 0f;
        public WaveAimSnapshotTimingId SnapshotTiming = WaveAimSnapshotTimingId.EventStart;

        public override WaveAimModeId AimMode => WaveAimModeId.PlayerPosition;
    }

    [Serializable]
    public abstract class WaveShotPatternAuthoringBase
    {
        public abstract WaveShotPatternModeId ShotPatternMode { get; }
    }

    [Serializable]
    public sealed class SingleShotPatternAuthoring : WaveShotPatternAuthoringBase
    {
        public override WaveShotPatternModeId ShotPatternMode => WaveShotPatternModeId.Single;
    }

    [Serializable]
    public sealed class NWayShotPatternAuthoring : WaveShotPatternAuthoringBase
    {
        public int ShotCount = 2;

        public override WaveShotPatternModeId ShotPatternMode => WaveShotPatternModeId.NWay;
    }

    [Serializable]
    public sealed class RadialShotPatternAuthoring : WaveShotPatternAuthoringBase
    {
        public int ShotCount = 2;

        public override WaveShotPatternModeId ShotPatternMode => WaveShotPatternModeId.Radial;
    }

    [Serializable]
    public sealed class WaveSpawnEntryAuthoring
    {
        [Header("Payload")]
        public WaveClipSO.SpawnPayloadProfile Payload;

        [Header("Directive")]
        [SerializeReference] public WaveEmissionAuthoringBase Emission = new RateFieldEmissionAuthoring();
        public WaveSamplingAuthoring Sampling = new WaveSamplingAuthoring();
        [SerializeReference] public WavePositionPatternAuthoringBase PositionPattern = new SinglePointPositionPatternAuthoring();
        [FormerlySerializedAs("Direction")]
        [SerializeReference] public WaveAimAuthoringBase Aim = new RandomAimAuthoring();
        [SerializeReference] public WaveShotPatternAuthoringBase ShotPattern = new SingleShotPatternAuthoring();
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
