using Unity.Entities;

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
        public float SpawnRate; // bullets/sec (평균)
    }

    public struct ScoreComponent : IComponentData
    {
        public long Value;
    }
}