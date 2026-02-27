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
