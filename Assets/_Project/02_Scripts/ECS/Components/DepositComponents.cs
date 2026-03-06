using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public struct DepositPointComponent : IComponentData
    {
        public float Radius;
    }

    public struct DepositStableIdComponent : IComponentData
    {
        public uint Value;
    }
}
