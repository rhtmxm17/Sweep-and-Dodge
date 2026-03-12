using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class ResultPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI OutcomeText;
        public TextMeshProUGUI StageText;
        public TextMeshProUGUI TimeText;
        public TextMeshProUGUI CollectText;
        public TextMeshProUGUI CleanupText;
        public TextMeshProUGUI HitText;

        [Header("Buttons")]
        public Button NextStageButton;
        public Button RetryButton;
        public Button ReturnToLobbyButton;
        public Button SettingsButton;

        private DemoShellFlowController _shell;
        private Action _openSettings;

        public Selectable DefaultSelectable
        {
            get
            {
                if (_shell != null
                    && _shell.CurrentStageOutcome == DemoShellStageOutcomeId.Clear
                    && NextStageButton != null
                    && NextStageButton.gameObject.activeInHierarchy)
                {
                    return NextStageButton;
                }

                return RetryButton != null ? RetryButton : ReturnToLobbyButton;
            }
        }

        public void Configure(DemoShellFlowController shell, Action openSettings)
        {
            _shell = shell;
            _openSettings = openSettings;

            if (NextStageButton != null)
            {
                NextStageButton.onClick.RemoveAllListeners();
                NextStageButton.onClick.AddListener(() => _shell?.RequestResultAction(DemoShellResultActionId.NextStage));
            }

            if (RetryButton != null)
            {
                RetryButton.onClick.RemoveAllListeners();
                RetryButton.onClick.AddListener(() => _shell?.RequestResultAction(DemoShellResultActionId.Retry));
            }

            if (ReturnToLobbyButton != null)
            {
                ReturnToLobbyButton.onClick.RemoveAllListeners();
                ReturnToLobbyButton.onClick.AddListener(() => _shell?.RequestResultAction(DemoShellResultActionId.ReturnToLobby));
            }

            if (SettingsButton != null)
            {
                SettingsButton.onClick.RemoveAllListeners();
                SettingsButton.onClick.AddListener(() => _openSettings?.Invoke());
            }
        }

        public void RefreshPresentation()
        {
            if (_shell == null)
                return;

            DemoShellStageOutcomeId outcome = _shell.CurrentStageOutcome;
            DemoShellStageResultMetrics result = _shell.HasCurrentStageResult ? _shell.CurrentStageResult : default;

            if (OutcomeText != null)
            {
                OutcomeText.text = outcome == DemoShellStageOutcomeId.Clear ? "Clear" : "Fail";
                OutcomeText.color = outcome == DemoShellStageOutcomeId.Clear
                    ? new Color(0.45f, 0.95f, 0.65f, 1f)
                    : new Color(1f, 0.45f, 0.45f, 1f);
            }

            if (StageText != null)
                StageText.text = $"Stage {Mathf.Max(1, result.StageId)}";
            if (TimeText != null)
                TimeText.text = $"Time  {Mathf.Max(0f, result.ElapsedSec):0.0}s";
            if (CollectText != null)
                CollectText.text = $"Collect  {Mathf.Max(0, result.CollectValue)}";
            if (CleanupText != null)
                CleanupText.text = $"Cleanup  {Mathf.Max(0, result.CleanupValue)}";
            if (HitText != null)
                HitText.text = $"Hit  {Mathf.Max(0, result.HitValue)}";

            if (NextStageButton != null)
                NextStageButton.gameObject.SetActive(outcome == DemoShellStageOutcomeId.Clear);
        }
    }
}
