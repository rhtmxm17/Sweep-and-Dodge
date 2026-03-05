using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public class StageLayoutStageMarker : MonoBehaviour
    {
        [Min(1)] public int StageId = 1;
    }
}
