using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class SettingsPresenter : MonoBehaviour
    {
        [Serializable]
        public sealed class AudioSliderRefs
        {
            public TextMeshProUGUI Label;
            public Slider Slider;
            public TextMeshProUGUI ValueText;
        }

        [Header("Texts")]
        public TextMeshProUGUI HeaderText;

        [Header("Buttons")]
        public Button CloseButton;

        [Header("Audio")]
        public AudioSliderRefs Master = new AudioSliderRefs();
        public AudioSliderRefs Bgm = new AudioSliderRefs();
        public AudioSliderRefs Sfx = new AudioSliderRefs();
        public AudioSliderRefs Ui = new AudioSliderRefs();

        private DemoAudioBridge _audio;
        private Action _closeSettings;

        public Selectable DefaultSelectable => Master?.Slider != null ? Master.Slider : CloseButton;

        public void Configure(DemoAudioBridge audio, Action closeSettings)
        {
            _audio = audio;
            _closeSettings = closeSettings;

            BindSlider(Master, "Master", DemoAudioBusId.Master);
            BindSlider(Bgm, "BGM", DemoAudioBusId.Bgm);
            BindSlider(Sfx, "SFX", DemoAudioBusId.Sfx);
            BindSlider(Ui, "UI", DemoAudioBusId.Ui);

            if (CloseButton != null)
            {
                CloseButton.onClick.RemoveAllListeners();
                CloseButton.onClick.AddListener(() => _closeSettings?.Invoke());
            }
        }

        public void RefreshPresentation()
        {
            if (HeaderText != null)
                HeaderText.text = "Settings";

            RefreshSlider(Master, "Master", DemoAudioBusId.Master);
            RefreshSlider(Bgm, "BGM", DemoAudioBusId.Bgm);
            RefreshSlider(Sfx, "SFX", DemoAudioBusId.Sfx);
            RefreshSlider(Ui, "UI", DemoAudioBusId.Ui);
        }

        private void BindSlider(AudioSliderRefs refs, string label, DemoAudioBusId bus)
        {
            if (refs == null || refs.Slider == null)
                return;

            if (refs.Label != null)
                refs.Label.text = label;

            refs.Slider.minValue = 0f;
            refs.Slider.maxValue = 1f;
            refs.Slider.wholeNumbers = false;
            refs.Slider.onValueChanged.RemoveAllListeners();
            refs.Slider.onValueChanged.AddListener(value =>
            {
                _audio?.SetBusVolume(bus, value);
                if (refs.ValueText != null)
                    refs.ValueText.text = value.ToString("0.00");
            });
        }

        private void RefreshSlider(AudioSliderRefs refs, string label, DemoAudioBusId bus)
        {
            if (refs == null)
                return;

            if (refs.Label != null)
                refs.Label.text = label;

            float value = _audio != null ? Mathf.Clamp01(_audio.GetBusVolume(bus)) : 1f;
            if (refs.Slider != null)
                refs.Slider.SetValueWithoutNotify(value);
            if (refs.ValueText != null)
                refs.ValueText.text = value.ToString("0.00");
        }
    }
}
