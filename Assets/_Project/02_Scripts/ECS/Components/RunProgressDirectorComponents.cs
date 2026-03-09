using Unity.Entities;

namespace SweepNDodge.DotsBullets
{
    public enum RunDirectorStageStateId : byte
    {
        Idle = 0,
        Running = 1,
        ClearReady = 2,
        Completed = 3,
    }

    public enum RunDirectorStageTransitionReasonId : byte
    {
        None = 0,
        StartRequested = 1,
        AllSourcesDepleted = 2,
        ConfirmPressed = 3,
        AutoAdvanceTimeout = 4,
        DebugForceClearReady = 5,
    }

    public struct RunDirectorStageConfigComponent : IComponentData
    {
        public RunDirectorStageStateId InitialState;
        public float MinIdleDurationSec;
        public float ClearAutoAdvanceTimeoutSec;
    }

    public struct RunDirectorStageStateComponent : IComponentData
    {
        public RunDirectorStageStateId State;
        public float StateElapsedSec;
        public uint EnteredFrame;
        public RunDirectorStageTransitionReasonId LastTransitionReason;
    }

    public struct RunDirectorStageGateComponent : IComponentData
    {
        public byte IntroPresentationDone;
        public byte ClearPresentationDone;
        public byte MinIdleDurationElapsed;
        public byte AutoAdvanceTimeoutElapsed;
    }

    // 외부(StageFlow/UI)에서 보내는 one-shot 요청.
    public struct RunDirectorStageRequestComponent : IComponentData
    {
        public byte StageStartRequested;
        public byte ConfirmPressed;
        public byte ForceClearReadyRequested;
    }

    public struct StageTopologyRequestComponent : IComponentData
    {
        public byte ApplyRequested;
        public int RequestedStageId;
    }

    public struct StageTopologyStateComponent : IComponentData
    {
        public int SelectedStageId;
        public int AppliedStageId;
        public byte Ready;
    }

    // 상위 StageFlow가 소비할 완료 신호(one-shot).
    public struct RunDirectorStageSignalComponent : IComponentData
    {
        public byte StageRunCompleted;
    }

    // 런타임 StageCatalog 참조(Managed Singleton).
    public class StageCatalogRuntimeComponent : IComponentData
    {
        public StageCatalogSO Catalog;
    }

    public struct StageTopologyPrefabCatalogComponent : IComponentData
    {
        public Entity SourceTemplate;
        public Entity DepositTemplate;
        public Entity ObstacleTemplate;
    }

    public enum StageTopologyKind : byte
    {
        Source = 0,
        Deposit = 1,
        Obstacle = 2,
        Visual = 3,
    }

    public struct StageTopologyLifecycleStateComponent : IComponentData
    {
        public uint CurrentAppliedVersion;
    }

    public struct StageTopologyOwnedTag : IComponentData
    {
    }

    public struct StageTopologyOwnedComponent : IComponentData
    {
        public StageTopologyKind Kind;
        public uint LastAppliedVersion;
    }

    public struct StageTopologySourceTag : IComponentData
    {
    }

    public struct StageTopologyDepositTag : IComponentData
    {
    }

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

