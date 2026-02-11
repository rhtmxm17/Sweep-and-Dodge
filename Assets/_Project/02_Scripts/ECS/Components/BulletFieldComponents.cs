using Unity.Entities;
using Unity.Mathematics;

namespace SweepNDodge.DotsBullets
{
    // Singleton Config
    public struct BulletFieldConfigComponent : IComponentData
    {
        public int PoolSize;
        public int MaxActiveTarget;

        public float CellSize;
        public float InvCellSize;

        public float BulletLifetime;
    }

    public struct ScoreComponent : IComponentData
    {
        public long Value;
    }

    public enum SourceStateId : byte
    {
        Normal = 0,
        Weakened = 1,
        Depleted = 2
    }

    // Source별 고정 설정 + 런타임 누적치(외부 초기값 주입 가능)
    public struct SourceSpawnComponent : IComponentData
    {
        public float Radius;
        public float SpawnRateNormal;
        public float WeakenedMultiplier;

        public int ThresholdWeakened;
        public int ThresholdDepleted;
        public int CollectedCount;

        public SourceStateId State;
    }

    // Source별 독립 스폰 루프 상태
    public struct SourceSpawnRuntimeComponent : IComponentData
    {
        public float SpawnAccumulator;
        public uint SpawnSequence;
    }

    // Source 기준 위치(스폰 계산용). LocalTransform RO/RW alias 충돌 회피용.
    public struct SourceAnchorComponent : IComponentData
    {
        public float3 Position;
    }
}
