using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutRootMarker : MonoBehaviour
    {
        public StageMapCatalogSO TargetCatalog;
        public bool SortByStageId = true;
    }
}
