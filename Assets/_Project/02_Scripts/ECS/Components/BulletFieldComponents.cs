using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    public enum BulletFieldShapeId : byte
    {
        Circle = 0,
        Rectangle = 1
    }

    // Singleton Config
    public struct BulletFieldConfigComponent : IComponentData
    {
        public int PoolSize;
        public float InvCellSize;
    }

    public struct MetaScrapComponent : IComponentData
    {
        public long Value;
    }

    public enum SourceStateId : byte
    {
        Normal = 0,
        Weakened = 1,
        Depleted = 2
    }

    public enum SourceSpawnModeId : byte
    {
        FixedDensity = 0,
        CapAndMaxDensity = 1
    }

    public enum SourceSpawnEmissionModeId : byte
    {
        RateField = 0,
        Poisson = 1,
        EventBurst = 2,
    }

    public enum SourceSpawnSamplingModeId : byte
    {
        UniformField = 0,
        PollutionTopK = 1,
        LineEven = 2,
        PointSet = 4,
    }

    public enum SourceSpawnCenterModeId : byte
    {
        SourceCenter = 0,
        FixedPoint = 1,
        PlayerRelative = 2,
    }

    public enum SourceSpawnDirectionModeId : byte
    {
        Random = 0,
        NWay = 1,
        Spiral = 2,
        RadialBurst = 3,
        Fixed = 4,
    }

    // v3 lane model.
    // - Trash/Hazard are default lanes.
    // - values >= 2 are reserved for special/custom lanes.
    public enum SourceSpawnLaneId : byte
    {
        Trash = 0,
        Hazard = 1,
        Special = 2,
    }

    public static class SourceSpawnLanePriorityUtility
    {
        public static int ResolvePriority(SourceSpawnLaneId lane)
        {
            if (lane == SourceSpawnLaneId.Trash)
                return 0;
            if (lane == SourceSpawnLaneId.Hazard)
                return 1;

            // Special/custom lanes outrank default lanes.
            return 2 + (int)lane;
        }
    }

    // Source별 고정 설정 + 런타임 누적치(외부 초기값 주입 가능)
    public struct SourceSpawnComponent : IComponentData
    {
        public int ThresholdWeakened;
        public int ThresholdDepleted;
        public int CollectedCount;

        public SourceStateId State;
    }

    // Source별 독립 스폰 루프 상태
    public struct SourceSpawnRuntimeComponent : IComponentData
    {
        public uint SpawnSequence;
    }

    // Source별 청소 흔적(오염도) 설정.
    public struct SourcePollutionConfigComponent : IComponentData
    {
        public float MinValue;
        public float MaxValue;
        public float RegenPerSec;
        public float DropPerCollect;
        public int TopKSampleCount;
    }

    // Source별 청소 흔적 격자 메타데이터.
    // 실제 형상은 셀 valid mask로 제한한다.
    public struct SourcePollutionGridComponent : IComponentData
    {
        public int Cols;
        public int Rows;
        public float CellSize;
        public float InvCellSize;
        public float2 HalfExtents;
    }

    [InternalBufferCapacity(8)]
    public struct SourceActiveBulletCountBuffer : IBufferElementData
    {
        public int BulletTypeKey;
        public int ActiveCount;
    }

    // v3 clip patterns baked from WaveClipSO bindings.
    [InternalBufferCapacity(32)]
    public struct SourceClipPatternBuffer : IBufferElementData
    {
        public int DirectiveId;
        public int ClipId;
        public SourceWavePhaseId Phase;
        public SourceSpawnLaneId Lane;
        public SourceStateId TriggerState;
        public float LocalStartSec;
        public float LocalEndSec;
        public int BulletTypeKey;
        public SourceSpawnEmissionModeId EmissionMode;
        public SourceSpawnModeId SpawnMode;
        public SourceSpawnSamplingModeId SamplingMode;
        public SourceSpawnCenterModeId CenterMode;
        public SourceSpawnDirectionModeId DirectionMode;
        public float2 FixedPoint;
        public float2 SpawnOffset;
        public float2 LineStart;
        public float2 LineEnd;
        public float SampleSpacing;
        public int SpawnSampleBudget;
        public float PlayerNoSpawnRadius;
        public float BaseAngleDeg;
        public int NWayCount;
        public float SpiralStepDeg;
        public float SpawnDensityPerSecPerArea;
        public float MeanEventsPerSec;
        public int BurstRepeatCount;
        public float BurstIntervalSec;
        public int BurstShotsPerEvent;
        public int LanePriority;
        public float MaxActiveDensityPerArea;
        public float SpawnAccumulator;
        public int BurstEventsEmitted;
    }

    [InternalBufferCapacity(16)]
    public struct SourceSustainSlotCandidateBuffer : IBufferElementData
    {
        public SourceStateId State;
        public SourceSpawnLaneId Lane;
        public int ClipId;
        public float Weight;
    }

    [InternalBufferCapacity(8)]
    public struct SourceSustainRuntimeLaneBuffer : IBufferElementData
    {
        public SourceSpawnLaneId Lane;
        public int ActiveClipId;
        public float ElapsedSec;
        public int LastClipId;
        public uint SelectionSequence;
        public uint LastMissingLogFrame;
    }

    public struct SourceSustainRuntimeComponent : IComponentData
    {
        public SourceStateId ActiveState;
    }

    public struct SourceEventRuntimeComponent : IComponentData
    {
        public byte IsPlaying;
        public int ActiveEventClipId;
        public SourceStateId TriggerState;
        public float ElapsedSec;
        public uint SelectionSequence;
    }

    [InternalBufferCapacity(8)]
    public struct SourceEventQueueBuffer : IBufferElementData
    {
        public SourceStateId TriggerState;
        public uint QueuedFrame;
    }

    [InternalBufferCapacity(128)]
    public struct SourcePollutionCellBuffer : IBufferElementData
    {
        public float Value;
        public byte IsValid;
    }

    [InternalBufferCapacity(32)]
    public struct SourcePollutionDropRequestBuffer : IBufferElementData
    {
        public int CellIndex;
        public int Count;
    }

    [InternalBufferCapacity(64)]
    public struct SourcePollutionValidCellIndexBuffer : IBufferElementData
    {
        public int Value;
    }

    // Source 기준 위치(스폰 계산용). LocalTransform RO/RW alias 충돌 회피용.
    public struct SourceAnchorComponent : IComponentData
    {
        public float3 Position;
    }

    // Source가 탄환을 뿌리는 영역 정의(형태 + 면적 캐시)
    public struct BulletFieldAreaComponent : IComponentData
    {
        public BulletFieldShapeId Shape;
        public float Radius;
        public float2 Size;
        public float ComputedArea;
    }
}
