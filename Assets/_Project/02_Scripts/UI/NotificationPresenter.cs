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
                || !_runtimeHud.TryGetLastSnapshot(out var snapshot))
            {
                SetVisible(false, string.Empty, EventColor);
                return;
            }

            float remainingSec = ResolveRemainingSec(_shell, snapshot.StageStateElapsedSec);
            if (TryResolveDanger(snapshot, remainingSec, out string dangerMessage, out Color dangerColor))
            {
                SetVisible(true, dangerMessage, dangerColor);
                return;
            }

            if (!_runtimeHud.TryGetLastFeedbackSnapshot(out var feedbackSnapshot)
                || feedbackSnapshot.RemainingSec <= 0f
                || string.IsNullOrEmpty(_runtimeHud.LastFeedbackLine)
                || ShouldSuppressFeedback(feedbackSnapshot))
            {
                SetVisible(false, string.Empty, EventColor);
                return;
            }

            SetVisible(true, _runtimeHud.LastFeedbackLine, EventColor);
        }

        private static bool TryResolveDanger(
            in PlayerHudSnapshotComponent snapshot,
            float remainingSec,
            out string message,
            out Color color)
        {
            int capacity = Mathf.Max(0, snapshot.CarryCapacity);
            bool carryFull = capacity > 0 && snapshot.CarryLoad >= capacity;

            if (snapshot.HitFlashRemainingSec > 0f && snapshot.LastHitLossValue > 0)
            {
                message = "Hit! Carry lost";
                color = DangerColor;
                return true;
            }

            if (remainingSec >= 0f && remainingSec <= 10f)
            {
                message = "Time critical";
                color = DangerColor;
                return true;
            }

            if (carryFull)
            {
                message = "Carry full - deposit now";
                color = DangerColor;
                return true;
            }

            if (remainingSec >= 0f && remainingSec <= 30f)
            {
                message = "Time is running out";
                color = WarningColor;
                return true;
            }

            message = string.Empty;
            color = EventColor;
            return false;
        }

        private static bool ShouldSuppressFeedback(in PlayerUiFeedbackPresentationSnapshotComponent snapshot)
        {
            if (snapshot.Type == PlayerUiFeedbackEventType.PlayerHazardHit)
                return true;

            return snapshot.Type == PlayerUiFeedbackEventType.VacuumStartBlocked
                   && snapshot.Reason == (byte)PlayerUiFeedbackReasonId.CarryBinFull;
        }

        private static float ResolveRemainingSec(DemoShellFlowController shell, float elapsedSec)
        {
            if (shell == null || shell.StageProfiles == null || shell.StageProfiles.Length <= 0)
                return -1f;

            int stageIndex = shell.CurrentStageIndex;
            if (stageIndex >= 0 && stageIndex < shell.StageProfiles.Length)
                return Mathf.Max(0f, shell.StageProfiles[stageIndex].StageTimeLimitSec - elapsedSec);

            int stageId = shell.CurrentStageId;
            for (int i = 0; i < shell.StageProfiles.Length; i++)
            {
                if (shell.StageProfiles[i].StageId != stageId)
                    continue;

                return Mathf.Max(0f, shell.StageProfiles[i].StageTimeLimitSec - elapsedSec);
            }

            return -1f;
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
