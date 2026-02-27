using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum RunDirectorSourceStateId : byte
    {
        Baseline = 0,
        Pressure = 1,
        Finish = 2,
    }

    // 디렉터 Pressure 점수 입력 슬롯.
    public enum RunDirectorPressureInputSlotId : byte
    {
        InfluenceOccupancy = 0,
        InfluenceHoldSec = 1,
    }

    public struct SourceDirectorPressureInputBuffer : IBufferElementData
    {
        public RunDirectorPressureInputSlotId Slot;
        public float Value;
    }

    // 런 디렉터 Pressure 입력 가중치 싱글톤 태그.
    public struct RunDirectorPressureWeightSingletonTag : IComponentData
    {
    }

    public struct RunDirectorPressureWeightBuffer : IBufferElementData
    {
        public RunDirectorPressureInputSlotId Slot;
        public float Weight;
    }

    // Source별 런 디렉터 관점 상태.
    // - Clip 선택 주체는 디렉터이며, SourceClipRequestBuildSystem이 이 값을 소비한다.
    public struct SourceRunDirectorStateComponent : IComponentData
    {
        public RunDirectorSourceStateId State;
        public SourceStateId SelectedClipState;
        public float PressureOccupancySec;
        public float DensityScale;
        public uint Version;
    }

    // 런 디렉터 기본 정책(싱글톤).
    public struct RunProgressDirectorConfigComponent : IComponentData
    {
        public float PressureHoldSec;
        public float BaselineTrashDensityScale;
        public float PressureDensityScale;
    }
}
