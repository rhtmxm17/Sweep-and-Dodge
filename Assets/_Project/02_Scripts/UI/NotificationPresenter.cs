using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class NotificationPresenter : MonoBehaviour
    {
        [Header("Visuals")]
        public GameObject NotificationRoot;
        public Image NotificationBackgroundImage;
        public TextMeshProUGUI NotificationText;

        private static readonly Color WarningColor = new(0.42f, 0.27f, 0.10f, 0.92f);
        private static readonly Color DangerColor = new(0.42f, 0.10f, 0.10f, 0.94f);
        private static readonly Color EventColor = new(0.10f, 0.16f, 0.24f, 0.90f);

        private DemoShellNotificationBridge _bridge;

        public void Configure(DemoShellNotificationBridge bridge)
        {
            _bridge = bridge;
        }

        public void RefreshPresentation()
        {
            if (_bridge == null)
            {
                SetVisible(false, string.Empty, EventColor);
                return;
            }

            var state = _bridge.CurrentNotification;
            if (!state.Visible || string.IsNullOrEmpty(state.Message))
            {
                SetVisible(false, string.Empty, EventColor);
                return;
            }

            SetVisible(true, state.Message, ResolveColor(state.Severity));
        }

        private static Color ResolveColor(NotificationSeverity severity)
        {
            return severity switch
            {
                NotificationSeverity.Danger => DangerColor,
                NotificationSeverity.Warning => WarningColor,
                _ => EventColor,
            };
        }

        private void SetVisible(bool visible, string text, Color color)
        {
            if (NotificationRoot != null)
                NotificationRoot.SetActive(visible);
            if (NotificationText != null)
                NotificationText.text = visible ? text : string.Empty;
            if (NotificationBackgroundImage != null)
                NotificationBackgroundImage.color = color;
        }
    }
}
