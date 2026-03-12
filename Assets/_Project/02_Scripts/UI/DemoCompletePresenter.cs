using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class DemoCompletePresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI HeaderText;
        public TextMeshProUGUI ClearedStagesText;
        public TextMeshProUGUI TotalTimeText;
        public TextMeshProUGUI TotalCollectText;
        public TextMeshProUGUI TotalCleanupText;
        public TextMeshProUGUI TotalHitText;

        [Header("Buttons")]
        public Button RestartDemoButton;
        public Button ReturnToLobbyButton;
        public Button QuitButton;
        public Button SettingsButton;

        private DemoShellFlowController _shell;
        private Action _openSettings;

        public Selectable DefaultSelectable => RestartDemoButton != null ? RestartDemoButton : ReturnToLobbyButton;

        public void Configure(DemoShellFlowController shell, Action openSettings)
        {
            _shell = shell;
            _openSettings = openSettings;

            if (RestartDemoButton != null)
            {
                RestartDemoButton.onClick.RemoveAllListeners();
                RestartDemoButton.onClick.AddListener(() => _shell?.RequestRestartDemo());
            }

            if (ReturnToLobbyButton != null)
            {
                ReturnToLobbyButton.onClick.RemoveAllListeners();
                ReturnToLobbyButton.onClick.AddListener(() => _shell?.RequestReturnToLobbyFromComplete());
            }

            if (QuitButton != null)
            {
                QuitButton.onClick.RemoveAllListeners();
                QuitButton.onClick.AddListener(() => _shell?.RequestQuit());
            }

            if (SettingsButton != null)
            {
                SettingsButton.onClick.RemoveAllListeners();
                SettingsButton.onClick.AddListener(() => _openSettings?.Invoke());
            }
        }

        public void RefreshPresentation()
        {
            if (HeaderText != null)
                HeaderText.text = "Demo Complete";

            DemoShellSessionMetrics metrics = _shell != null && _shell.HasSessionMetrics
                ? _shell.SessionMetrics
                : default;

            if (ClearedStagesText != null)
                ClearedStagesText.text = $"Cleared Stages  {Mathf.Max(0, metrics.ClearedStageCount)}";
            if (TotalTimeText != null)
                TotalTimeText.text = $"Total Time  {Mathf.Max(0f, metrics.TotalElapsedSec):0.0}s";
            if (TotalCollectText != null)
                TotalCollectText.text = $"Total Collect  {Mathf.Max(0, metrics.TotalCollectValue)}";
            if (TotalCleanupText != null)
                TotalCleanupText.text = $"Total Cleanup  {Mathf.Max(0, metrics.TotalCleanupValue)}";
            if (TotalHitText != null)
                TotalHitText.text = $"Total Hit  {Mathf.Max(0, metrics.TotalHitValue)}";
        }
    }
}
