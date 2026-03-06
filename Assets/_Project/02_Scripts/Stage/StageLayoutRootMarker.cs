using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutRootMarker : MonoBehaviour
    {
        [Header("Dual Catalog")]
        public StageCatalogSO TargetStageCatalog;

        [Header("Legacy (Deprecated)")]
        public StageMapCatalogSO TargetCatalog;

        public bool SortByStageId = true;
    }
}
