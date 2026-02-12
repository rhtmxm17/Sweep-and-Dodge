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
        public int MaxActiveTarget;

        public float CellSize;
        public float InvCellSize;

        public float BulletLifetime;
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

    [InternalBufferCapacity(8)]
    public struct SourceSpawnPatternBuffer : IBufferElementData
    {
        public SourceStateId State;
        public int BulletTypeKey;
        public SourceSpawnModeId SpawnMode;
        public float SpawnDensityPerSecPerArea;
        public float MaxActiveDensityPerArea; // FixedDensity 일 경우 무시
        public float SpawnAccumulator;
    }

    [InternalBufferCapacity(8)]
    public struct SourceActiveBulletCountBuffer : IBufferElementData
    {
        public int BulletTypeKey;
        public int ActiveCount;
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
