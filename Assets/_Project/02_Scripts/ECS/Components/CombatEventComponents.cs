using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum CombatEventTypeId : byte
    {
        None = 0,
        Hit = 1,
        Collect = 2,
        Cleanup = 3,
    }

    public struct CombatEventChannelSingletonTag : IComponentData { }

    [InternalBufferCapacity(32)]
    public struct CombatEventBufferElement : IBufferElementData
    {
        public CombatEventTypeId Type;
        public Entity SourceEntity;
        public Entity RelatedEntity;
        public int Count;
        public int Value;
        public uint Frame;
        public uint Sequence;
    }

    // Runtime aggregate metrics from the common combat event channel.
    public struct CombatEventMetricsComponent : IComponentData
    {
        public uint LastConsumedFrame;

        public int LastFrameHitCount;
        public int LastFrameCollectCount;
        public int LastFrameCleanupCount;

        public int LastFrameHitValue;
        public int LastFrameCollectValue;
        public int LastFrameCleanupValue;

        public long TotalHitCount;
        public long TotalCollectCount;
        public long TotalCleanupCount;

        public long TotalHitValue;
        public long TotalCollectValue;
        public long TotalCleanupValue;
    }
}
