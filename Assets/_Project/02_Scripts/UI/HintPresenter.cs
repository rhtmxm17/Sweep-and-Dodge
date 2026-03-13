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

        private const float CarryFullHintDurationSec = 2.5f;
        private const string CarryFullHintMessage = "Carry is full. Head to Deposit.";

        private DemoShellFlowController _shell;
        private DemoShellPauseBridge _pauseBridge;
        private PlayerRuntimeHudBridge _runtimeHud;
        private bool _carryFullHintShown;
        private bool _previousCarryFull;
        private float _remainingVisibleSec;

        public void Configure(
            DemoShellFlowController shell,
            DemoShellPauseBridge pauseBridge,
            PlayerRuntimeHudBridge runtimeHud)
        {
            _shell = shell;
            _pauseBridge = pauseBridge;
            _runtimeHud = runtimeHud;
        }

        public void RefreshPresentation()
        {
            if (_shell == null
                || _shell.CurrentScreen != DemoShellScreenId.StagePlay
                || _runtimeHud == null
                || !_runtimeHud.TryGetLastSnapshot(out var snapshot))
            {
                _previousCarryFull = false;
                SetVisible(false, string.Empty);
                return;
            }

            bool paused = _pauseBridge != null && _pauseBridge.IsPaused;
            int capacity = Mathf.Max(0, snapshot.CarryCapacity);
            bool carryFull = capacity > 0 && snapshot.CarryLoad >= capacity;

            if (!paused && !_carryFullHintShown && !_previousCarryFull && carryFull)
            {
                _carryFullHintShown = true;
                _remainingVisibleSec = CarryFullHintDurationSec;
                SetVisible(true, CarryFullHintMessage);
            }
            else if (!paused && _remainingVisibleSec > 0f)
            {
                _remainingVisibleSec = Mathf.Max(0f, _remainingVisibleSec - Time.unscaledDeltaTime);
                if (_remainingVisibleSec <= 0f)
                    SetVisible(false, string.Empty);
                else
                    SetVisible(true, CarryFullHintMessage);
            }
            else if (_remainingVisibleSec > 0f)
            {
                SetVisible(true, CarryFullHintMessage);
            }
            else
            {
                SetVisible(false, string.Empty);
            }

            if (!paused)
                _previousCarryFull = carryFull;
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
