using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class TitleScreenPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI SubtitleText;
        public TextMeshProUGUI ControlHintText;

        [Header("Buttons")]
        public Button StartButton;
        public Button SettingsButton;
        public Button QuitButton;

        private DemoShellFlowController _shell;
        private Action _openSettings;

        public Selectable DefaultSelectable => StartButton != null ? StartButton : SettingsButton;

        public void Configure(DemoShellFlowController shell, Action openSettings)
        {
            _shell = shell;
            _openSettings = openSettings;

            if (StartButton != null)
            {
                StartButton.onClick.RemoveAllListeners();
                StartButton.onClick.AddListener(OnStartClicked);
            }

            if (SettingsButton != null)
            {
                SettingsButton.onClick.RemoveAllListeners();
                SettingsButton.onClick.AddListener(OnSettingsClicked);
            }

            if (QuitButton != null)
            {
                QuitButton.onClick.RemoveAllListeners();
                QuitButton.onClick.AddListener(OnQuitClicked);
            }
        }

        public void RefreshPresentation()
        {
            if (TitleText != null)
                TitleText.text = "DOTS Minigame";
            if (SubtitleText != null)
                SubtitleText.text = "Bullet sweep / dodge demo";
            if (ControlHintText != null)
                ControlHintText.text = "WASD Move  |  Mouse Aim  |  Left/Right Click Action";
        }

        private void OnStartClicked()
        {
            _shell?.RequestStartFromTitle();
        }

        private void OnSettingsClicked()
        {
            _openSettings?.Invoke();
        }

        private void OnQuitClicked()
        {
            _shell?.RequestQuit();
        }
    }
}
