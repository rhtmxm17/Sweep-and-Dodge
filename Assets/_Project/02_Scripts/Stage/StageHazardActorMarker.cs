using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageHazardActorMarker : MonoBehaviour
    {
        [Min(1)] public int PlacementInstanceId = 1;
        public GameObject ActorArchetypePrefab;
        public float LocalYawDeg;
    }
}
