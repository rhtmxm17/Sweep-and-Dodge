using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    public enum StagePresentationAnchorKind : byte
    {
        DialogueBubble = 0,
    }

    [DisallowMultipleComponent]
    public sealed class StagePresentationAnchorMarker : MonoBehaviour
    {
        public StagePresentationAnchorKind AnchorKind = StagePresentationAnchorKind.DialogueBubble;
    }
}
