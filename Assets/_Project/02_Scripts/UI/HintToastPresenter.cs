using TMPro;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class HintToastPresenter : MonoBehaviour
    {
        [Header("Visuals")]
        public GameObject ToastRoot;
        public TextMeshProUGUI ToastText;

        private DemoShellFlowController _shell;
        private PlayerRuntimeHudBridge _runtimeHud;

        public void Configure(DemoShellFlowController shell, PlayerRuntimeHudBridge runtimeHud)
        {
            _shell = shell;
            _runtimeHud = runtimeHud;
        }

        public void RefreshPresentation()
        {
            if (_shell == null
                || _shell.CurrentScreen != DemoShellScreenId.StagePlay
                || _runtimeHud == null
                || !_runtimeHud.TryGetLastFeedbackSnapshot(out var snapshot)
                || snapshot.RemainingSec <= 0f
                || string.IsNullOrEmpty(_runtimeHud.LastFeedbackLine)
                || ShouldSuppress(snapshot))
            {
                SetVisible(false, string.Empty);
                return;
            }

            SetVisible(true, _runtimeHud.LastFeedbackLine);
        }

        private static bool ShouldSuppress(in PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            if (snapshot.Type == PlayerUiFeedbackEventType.PlayerHazardHit)
                return true;

            return snapshot.Type == PlayerUiFeedbackEventType.VacuumStartBlocked
                   && snapshot.Reason == (byte)PlayerUiFeedbackReasonId.CarryBinFull;
        }

        private void SetVisible(bool visible, string text)
        {
            if (ToastRoot != null)
                ToastRoot.SetActive(visible);
            if (ToastText != null)
                ToastText.text = visible ? text : string.Empty;
        }
    }
}
