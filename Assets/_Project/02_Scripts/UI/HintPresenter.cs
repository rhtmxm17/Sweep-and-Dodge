using TMPro;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class HintPresenter : MonoBehaviour
    {
        [Header("Visuals")]
        public GameObject HintRoot;
        public TextMeshProUGUI HintText;

        private DemoShellHintBridge _bridge;

        public void Configure(DemoShellHintBridge bridge)
        {
            _bridge = bridge;
        }

        public void RefreshPresentation()
        {
            if (_bridge == null)
            {
                SetVisible(false, string.Empty);
                return;
            }

            var state = _bridge.CurrentHint;
            if (!state.Visible || string.IsNullOrEmpty(state.Message))
            {
                SetVisible(false, string.Empty);
                return;
            }

            SetVisible(true, state.Message);
        }

        private void SetVisible(bool visible, string text)
        {
            if (HintRoot != null)
                HintRoot.SetActive(visible);
            if (HintText != null)
                HintText.text = visible ? text : string.Empty;
        }
    }
}
