using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum PlayerCleanupActionId : byte
    {
        None = 0,
        RadialRing = 1,
        ForwardFanLine = 2,
    }

    public enum PlayerCleanupActionSlotId : byte
    {
        None = 0,
        Primary = 1,
        Secondary = 2,
    }

    public struct PlayerCleanupActionStateComponent : IComponentData
    {
        public PlayerCleanupActionId SelectedActionId;
        public PlayerCleanupActionId PendingActionId;
        public uint Version;
    }

    public struct PlayerCleanupActionSlotMapComponent : IComponentData
    {
        public PlayerCleanupActionId PrimaryActionId;
        public PlayerCleanupActionId SecondaryActionId;
    }

    [InternalBufferCapacity(4)]
    public struct PlayerCleanupActionProfileBufferElement : IBufferElementData
    {
        public PlayerCleanupActionId ActionId;

        // Trash 판정
        public float TrashRange;
        public float TrashFanHalfAngleDeg;

        // Hazard 판정
        public float HazardRingRadius;
        public float HazardRingWidth;
        public float HazardLineLength;
        public float HazardLineHalfWidth;
    }
}
