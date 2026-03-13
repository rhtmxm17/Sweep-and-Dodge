using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class ConfirmDialogPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI BodyText;

        [Header("Buttons")]
        public Button ConfirmButton;
        public Button CancelButton;

        private DemoShellPauseBridge _pauseBridge;
        private Action _closeConfirm;

        public Selectable DefaultSelectable => CancelButton != null ? CancelButton : ConfirmButton;

        public void Configure(DemoShellPauseBridge pauseBridge, Action closeConfirm)
        {
            _pauseBridge = pauseBridge;
            _closeConfirm = closeConfirm;

            if (ConfirmButton != null)
            {
                ConfirmButton.onClick.RemoveAllListeners();
                ConfirmButton.onClick.AddListener(OnConfirmClicked);
            }

            if (CancelButton != null)
            {
                CancelButton.onClick.RemoveAllListeners();
                CancelButton.onClick.AddListener(() => _closeConfirm?.Invoke());
            }
        }

        public void RefreshPresentation()
        {
            DemoShellPauseActionId action = _pauseBridge != null
                ? _pauseBridge.PendingAction
                : DemoShellPauseActionId.Resume;

            if (TitleText != null)
                TitleText.text = ResolveTitle(action);
            if (BodyText != null)
                BodyText.text = ResolveBody(action);
        }

        private void OnConfirmClicked()
        {
            if (_pauseBridge == null)
                return;

            if (_pauseBridge.RequestConfirmedAction(_pauseBridge.PendingAction))
                _closeConfirm?.Invoke();
        }

        private static string ResolveTitle(DemoShellPauseActionId action)
        {
            return action switch
            {
                DemoShellPauseActionId.RestartStage => "Restart Stage?",
                DemoShellPauseActionId.ReturnToLobby => "Return to Lobby?",
                DemoShellPauseActionId.QuitApplication => "Quit Demo?",
                _ => "Confirm Action?",
            };
        }

        private static string ResolveBody(DemoShellPauseActionId action)
        {
            return action switch
            {
                DemoShellPauseActionId.RestartStage => "Current stage progress will be lost.",
                DemoShellPauseActionId.ReturnToLobby => "Current run progress will be lost and the lobby will open.",
                DemoShellPauseActionId.QuitApplication => "The demo will close immediately.",
                _ => "This action cannot be undone.",
            };
        }
    }
}
