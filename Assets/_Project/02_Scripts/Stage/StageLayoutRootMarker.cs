using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutRootMarker : MonoBehaviour
    {
        [Header("Catalog")]
        public StageCatalogSO TargetStageCatalog;
        public StagePresentationCatalogSO TargetPresentationCatalog;

        public bool SortByStageId = true;
    }
}
