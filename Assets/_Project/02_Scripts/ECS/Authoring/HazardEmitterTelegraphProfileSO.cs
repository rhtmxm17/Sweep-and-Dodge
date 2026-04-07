using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Hazard/Hazard Emitter Telegraph Profile", fileName = "hetp_")]
    public class HazardEmitterTelegraphProfileSO : ScriptableObject
    {
        [Min(0f)] public float TelegraphDurationSec = 0f;
    }
}
