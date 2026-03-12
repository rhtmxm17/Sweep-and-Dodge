using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class LobbyScreenPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI HeaderText;
        public TextMeshProUGUI SubtitleText;

        [Header("Stage Buttons")]
        public RectTransform StageButtonContainer;
        public Button StageButtonTemplate;

        [Header("Buttons")]
        public Button SettingsButton;
        public Button QuitButton;

        private readonly List<Button> _stageButtons = new List<Button>(8);
        private DemoShellFlowController _shell;
        private Action _openSettings;
        private string _lastSignature;

        public Selectable DefaultSelectable
        {
            get
            {
                for (int i = 0; i < _stageButtons.Count; i++)
                {
                    if (_stageButtons[i] != null && _stageButtons[i].gameObject.activeSelf)
                        return _stageButtons[i];
                }

                return SettingsButton;
            }
        }

        public void Configure(DemoShellFlowController shell, Action openSettings)
        {
            _shell = shell;
            _openSettings = openSettings;

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
            if (HeaderText != null)
                HeaderText.text = "Select Stage";
            if (SubtitleText != null)
                SubtitleText.text = "Pick a stage to start the run.";

            if (_shell == null || StageButtonContainer == null || StageButtonTemplate == null)
                return;

            string nextSignature = BuildSignature(_shell.StageProfiles);
            if (!string.Equals(_lastSignature, nextSignature, StringComparison.Ordinal))
            {
                RebuildStageButtons(_shell.StageProfiles);
                _lastSignature = nextSignature;
            }
        }

        private void RebuildStageButtons(DemoShellStageProfile[] profiles)
        {
            for (int i = 0; i < _stageButtons.Count; i++)
            {
                if (_stageButtons[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(_stageButtons[i].gameObject);
                    else
                        DestroyImmediate(_stageButtons[i].gameObject);
                }
            }

            _stageButtons.Clear();
            StageButtonTemplate.gameObject.SetActive(false);

            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                if (profile.StageId <= 0)
                    continue;

                var button = Instantiate(StageButtonTemplate, StageButtonContainer);
                button.name = $"StageButton_{profile.StageId}";
                button.gameObject.SetActive(true);
                button.onClick.RemoveAllListeners();

                int stageId = profile.StageId;
                button.onClick.AddListener(() => _shell?.RequestSelectStageById(stageId));

                var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                    label.text = $"{profile.StageId}. {profile.DisplayName}";

                _stageButtons.Add(button);
            }
        }

        private static string BuildSignature(DemoShellStageProfile[] profiles)
        {
            if (profiles == null || profiles.Length == 0)
                return string.Empty;

            var builder = new System.Text.StringBuilder(profiles.Length * 16);
            for (int i = 0; i < profiles.Length; i++)
            {
                var profile = profiles[i];
                builder.Append(profile.StageId);
                builder.Append(':');
                builder.Append(profile.DisplayName);
                builder.Append('|');
            }

            return builder.ToString();
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
