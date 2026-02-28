using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class RunProgressDirectorSettingsAuthoring : MonoBehaviour
    {
        [Header("Run Director Config")]
        [Min(0f)] public float PressureHoldSec = 0.35f;
        [Min(0f)] public float BaselineTrashDensityScale = 0.45f;
        [Min(0f)] public float PressureDensityScale = 1.0f;

        [Header("Stage State Config")]
        // 기본 모드: StageFlow/UI 미연동 환경에서도 기존 플레이 루프를 유지하기 위해 Running 시작을 기본값으로 둔다.
        public RunDirectorStageStateId InitialStageState = RunDirectorStageStateId.Running;
        [Min(0f)] public float MinIdleDurationSec = 0f;
        [Min(0f)] public float ClearAutoAdvanceTimeoutSec = 10f;

        [Header("Pressure Input Weights")]
        public float InfluenceOccupancyWeight = 1.0f;
        public float InfluenceHoldSecWeight = 1.0f;

        private class Baker : Baker<RunProgressDirectorSettingsAuthoring>
        {
            public override void Bake(RunProgressDirectorSettingsAuthoring authoring)
            {
                var e = GetEntity(TransformUsageFlags.None);
                AddComponent(e, new RunProgressDirectorConfigComponent
                {
                    PressureHoldSec = math.max(0f, authoring.PressureHoldSec),
                    BaselineTrashDensityScale = math.max(0f, authoring.BaselineTrashDensityScale),
                    PressureDensityScale = math.max(0f, authoring.PressureDensityScale),
                });
                AddComponent(e, new RunDirectorStageConfigComponent
                {
                    InitialState = authoring.InitialStageState,
                    MinIdleDurationSec = math.max(0f, authoring.MinIdleDurationSec),
                    ClearAutoAdvanceTimeoutSec = math.max(0f, authoring.ClearAutoAdvanceTimeoutSec),
                });
                AddComponent<RunDirectorPressureWeightSingletonTag>(e);

                var weights = AddBuffer<RunDirectorPressureWeightBuffer>(e);
                weights.Clear();
                weights.Add(new RunDirectorPressureWeightBuffer
                {
                    Slot = RunDirectorPressureInputSlotId.InfluenceOccupancy,
                    Weight = authoring.InfluenceOccupancyWeight,
                });
                weights.Add(new RunDirectorPressureWeightBuffer
                {
                    Slot = RunDirectorPressureInputSlotId.InfluenceHoldSec,
                    Weight = authoring.InfluenceHoldSecWeight,
                });
            }
        }
    }
}
