using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutStageMarker : MonoBehaviour
    {
        [Min(1)] public int StageId = 1;

        [Header("Dual Catalog Entry")]
        public bool IncludeInCatalog = true;
        public bool EnabledInCatalog = true;
        public string EntryKey;
        public StageDefinitionSO TargetDefinition;
        public StageLayoutSO TargetLayout;
    }
}
