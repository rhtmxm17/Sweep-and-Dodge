using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class PausePresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI HeaderText;

        [Header("Buttons")]
        public Button ResumeButton;
        public Button SettingsButton;
        public Button RestartStageButton;
        public Button ReturnToLobbyButton;
        public Button QuitButton;

        private DemoShellPauseBridge _pauseBridge;
        private Action _openSettingsFromPause;
        private Action<DemoShellPauseActionId> _openConfirm;

        public Selectable DefaultSelectable => ResumeButton != null ? ResumeButton : SettingsButton;

        public void Configure(
            DemoShellPauseBridge pauseBridge,
            Action openSettingsFromPause,
            Action<DemoShellPauseActionId> openConfirm)
        {
            _pauseBridge = pauseBridge;
            _openSettingsFromPause = openSettingsFromPause;
            _openConfirm = openConfirm;

            if (ResumeButton != null)
            {
                ResumeButton.onClick.RemoveAllListeners();
                ResumeButton.onClick.AddListener(OnResumeClicked);
            }

            if (SettingsButton != null)
            {
                SettingsButton.onClick.RemoveAllListeners();
                SettingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (RestartStageButton != null)
            {
                RestartStageButton.onClick.RemoveAllListeners();
                RestartStageButton.onClick.AddListener(() => OnConfirmActionRequested(DemoShellPauseActionId.RestartStage));
            }

            if (ReturnToLobbyButton != null)
            {
                ReturnToLobbyButton.onClick.RemoveAllListeners();
                ReturnToLobbyButton.onClick.AddListener(() => OnConfirmActionRequested(DemoShellPauseActionId.ReturnToLobby));
            }

            if (QuitButton != null)
            {
                QuitButton.onClick.RemoveAllListeners();
                QuitButton.onClick.AddListener(() => OnConfirmActionRequested(DemoShellPauseActionId.QuitApplication));
            }
        }

        public void RefreshPresentation()
        {
            if (HeaderText != null)
                HeaderText.text = "Paused";

            bool interactable = _pauseBridge != null && _pauseBridge.IsPaused;
            SetInteractable(ResumeButton, interactable);
            SetInteractable(SettingsButton, interactable);
            SetInteractable(RestartStageButton, interactable);
            SetInteractable(ReturnToLobbyButton, interactable);
            SetInteractable(QuitButton, interactable);
        }

        public Selectable ResolveSelectableForAction(DemoShellPauseActionId action)
        {
            return action switch
            {
                DemoShellPauseActionId.RestartStage => RestartStageButton,
                DemoShellPauseActionId.ReturnToLobby => ReturnToLobbyButton,
                DemoShellPauseActionId.QuitApplication => QuitButton,
                DemoShellPauseActionId.OpenSettings => SettingsButton,
                _ => ResumeButton,
            };
        }

        private void OnResumeClicked()
        {
            _pauseBridge?.RequestResume();
        }

        private void OnSettingsClicked()
        {
            _openSettingsFromPause?.Invoke();
        }

        private void OnConfirmActionRequested(DemoShellPauseActionId action)
        {
            _openConfirm?.Invoke(action);
        }

        private static void SetInteractable(Selectable selectable, bool interactable)
        {
            if (selectable != null)
                selectable.interactable = interactable;
        }
    }
}
