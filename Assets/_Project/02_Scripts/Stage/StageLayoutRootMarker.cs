using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutRootMarker : MonoBehaviour
    {
        [Header("Catalog")]
        public StageCatalogSO TargetStageCatalog;

        public bool SortByStageId = true;
    }
}
