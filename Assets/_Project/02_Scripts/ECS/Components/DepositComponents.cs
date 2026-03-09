using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public struct DepositPointComponent : IComponentData
    {
    }

    public struct DepositStableIdComponent : IComponentData
    {
        public uint Value;
    }
}
