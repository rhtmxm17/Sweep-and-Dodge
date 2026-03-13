using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageHudPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI StageLabel;
        public TextMeshProUGUI ObjectiveText;
        public TextMeshProUGUI SourceProgressText;
        public TextMeshProUGUI PressureSourceLabel;
        public TextMeshProUGUI PressureSourceValueText;
        public TextMeshProUGUI TimerLabel;
        public TextMeshProUGUI TimerValueText;
        public TextMeshProUGUI CarryLabel;
        public TextMeshProUGUI CarryValueText;
        public TextMeshProUGUI DangerText;

        [Header("Visuals")]
        public GameObject PressureSourceProgressRoot;
        public Image PressureSourceFillImage;
        public RectTransform PressureSourceWeakThresholdMarker;
        public Image CarryFillImage;
        public GameObject DangerBannerRoot;
        public Image DangerBannerImage;

        private static readonly Color TimerNormalColor = new(0.92f, 0.96f, 1f, 1f);
        private static readonly Color WarningColor = new(1f, 0.76f, 0.27f, 1f);
        private static readonly Color DangerColor = new(1f, 0.38f, 0.38f, 1f);
        private static readonly Color CarryNormalColor = new(0.32f, 0.78f, 0.95f, 1f);
        private static readonly Color CarryWarningColor = new(1f, 0.72f, 0.18f, 1f);
        private static readonly Color DangerBannerWarningColor = new(0.42f, 0.27f, 0.10f, 0.92f);
        private static readonly Color DangerBannerDangerColor = new(0.42f, 0.10f, 0.10f, 0.94f);

        private DemoShellFlowController _shell;
        private PlayerRuntimeHudBridge _runtimeHud;

        public void Configure(DemoShellFlowController shell, PlayerRuntimeHudBridge runtimeHud)
        {
            _shell = shell;
            _runtimeHud = runtimeHud;
        }

        public void RefreshPresentation()
        {
            if (_shell == null)
            {
                ApplyDefaultPresentation();
                return;
            }

            int stageId = Mathf.Max(0, _shell.CurrentStageId);
            if (StageLabel != null)
                StageLabel.text = stageId > 0 ? $"Stage {stageId}" : "Stage -";

            if (TimerLabel != null)
                TimerLabel.text = "Time";
            if (CarryLabel != null)
                CarryLabel.text = "Carry";

            if (_runtimeHud == null || !_runtimeHud.TryGetLastSnapshot(out var snapshot))
            {
                ApplyDefaultPresentation();
                return;
            }

            float stageLimitSec = ResolveStageTimeLimitSec();
            float remainingSec = stageLimitSec >= 0f
                ? Mathf.Max(0f, stageLimitSec - snapshot.StageStateElapsedSec)
                : -1f;

            ApplySourceProgress(snapshot);
            ApplyCarry(snapshot);
            ApplyTimer(remainingSec);
            ApplyObjective(snapshot);
            ApplyDanger(snapshot, remainingSec);
        }

        private void ApplyDefaultPresentation()
        {
            if (ObjectiveText != null)
                ObjectiveText.text = "Collect trash from sources";

            if (SourceProgressText != null)
            {
                SourceProgressText.text = string.Empty;
                SourceProgressText.gameObject.SetActive(false);
            }
            if (PressureSourceProgressRoot != null)
                PressureSourceProgressRoot.SetActive(false);
            if (PressureSourceLabel != null)
                PressureSourceLabel.text = "Pressure Source";
            if (PressureSourceValueText != null)
                PressureSourceValueText.text = "0 / 0";
            if (PressureSourceFillImage != null)
            {
                PressureSourceFillImage.type = Image.Type.Filled;
                PressureSourceFillImage.fillMethod = Image.FillMethod.Horizontal;
                PressureSourceFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                PressureSourceFillImage.fillAmount = 0f;
                PressureSourceFillImage.color = CarryNormalColor;
            }
            if (PressureSourceWeakThresholdMarker != null)
                PressureSourceWeakThresholdMarker.gameObject.SetActive(false);

            if (TimerValueText != null)
            {
                TimerValueText.text = "--.-s";
                TimerValueText.color = TimerNormalColor;
            }

            if (CarryValueText != null)
                CarryValueText.text = "0 / 0";
            if (CarryFillImage != null)
            {
                CarryFillImage.type = Image.Type.Filled;
                CarryFillImage.fillMethod = Image.FillMethod.Horizontal;
                CarryFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                CarryFillImage.fillAmount = 0f;
                CarryFillImage.color = CarryNormalColor;
            }

            if (DangerBannerRoot != null)
                DangerBannerRoot.SetActive(false);
            if (DangerText != null)
                DangerText.text = string.Empty;
        }

        private void ApplySourceProgress(in PlayerHudSnapshotComponent snapshot)
        {
            if (SourceProgressText == null)
                return;

            bool visible = snapshot.TotalSourceCount > 0;
            SourceProgressText.gameObject.SetActive(visible);
            if (!visible)
                return;

            SourceProgressText.text =
                $"Sources {Mathf.Max(0, snapshot.DepletedSourceCount)}/{Mathf.Max(0, snapshot.TotalSourceCount)} cleared";

            ApplyPressureSourceProgress(snapshot);
        }

        private void ApplyCarry(in PlayerHudSnapshotComponent snapshot)
        {
            int capacity = Mathf.Max(0, snapshot.CarryCapacity);
            int load = Mathf.Clamp(snapshot.CarryLoad, 0, capacity <= 0 ? int.MaxValue : capacity);
            float ratio = capacity <= 0 ? 0f : Mathf.Clamp01((float)load / capacity);
            bool carryFull = capacity > 0 && load >= capacity;

            if (CarryValueText != null)
                CarryValueText.text = $"{load} / {capacity}";

            if (CarryFillImage != null)
            {
                CarryFillImage.type = Image.Type.Filled;
                CarryFillImage.fillMethod = Image.FillMethod.Horizontal;
                CarryFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                CarryFillImage.fillAmount = ratio;
                CarryFillImage.color = carryFull ? CarryWarningColor : CarryNormalColor;
            }
        }

        private void ApplyTimer(float remainingSec)
        {
            if (TimerValueText == null)
                return;

            if (remainingSec < 0f)
            {
                TimerValueText.text = "--.-s";
                TimerValueText.color = TimerNormalColor;
                return;
            }

            TimerValueText.text = $"{remainingSec:0.0}s";
            TimerValueText.color = remainingSec <= 10f
                ? DangerColor
                : remainingSec <= 30f
                    ? WarningColor
                    : TimerNormalColor;
        }

        private void ApplyObjective(in PlayerHudSnapshotComponent snapshot)
        {
            if (ObjectiveText == null)
                return;

            string objective;
            int capacity = Mathf.Max(0, snapshot.CarryCapacity);
            bool carryFull = capacity > 0 && snapshot.CarryLoad >= capacity;
            bool hasSources = snapshot.TotalSourceCount > 0;
            bool hasRemainingSources = hasSources && snapshot.DepletedSourceCount < snapshot.TotalSourceCount;

            if (carryFull)
                objective = "Deposit collected trash";
            else if (hasRemainingSources)
                objective = "Collect trash from sources";
            else if (snapshot.CarryLoad > 0)
                objective = "Deposit remaining trash";
            else
                objective = "Finish the cleanup";

            ObjectiveText.text = objective;
        }

        private void ApplyDanger(in PlayerHudSnapshotComponent snapshot, float remainingSec)
        {
            if (DangerBannerRoot == null || DangerText == null)
                return;

            bool showDanger = false;
            string message = string.Empty;
            Color color = DangerBannerDangerColor;

            int capacity = Mathf.Max(0, snapshot.CarryCapacity);
            bool carryFull = capacity > 0 && snapshot.CarryLoad >= capacity;
            bool allSourcesCleared = snapshot.TotalSourceCount > 0 && snapshot.DepletedSourceCount >= snapshot.TotalSourceCount;

            if (snapshot.HitFlashRemainingSec > 0f && snapshot.LastHitLossValue > 0)
            {
                showDanger = true;
                message = "Hit! Carry lost";
                color = DangerBannerDangerColor;
            }
            else if (remainingSec >= 0f && remainingSec <= 10f)
            {
                showDanger = true;
                message = "Time critical";
                color = DangerBannerDangerColor;
            }
            else if (carryFull)
            {
                showDanger = true;
                message = "Carry full - deposit now";
                color = DangerBannerDangerColor;
            }
            else if (remainingSec >= 0f && remainingSec <= 30f)
            {
                showDanger = true;
                message = "Time is running out";
                color = DangerBannerWarningColor;
            }
            else if (allSourcesCleared && snapshot.CarryLoad > 0)
            {
                showDanger = true;
                message = "Deposit remaining trash";
                color = DangerBannerWarningColor;
            }

            DangerBannerRoot.SetActive(showDanger);
            DangerText.text = showDanger ? message : string.Empty;
            if (DangerBannerImage != null)
                DangerBannerImage.color = color;
        }

        private void ApplyPressureSourceProgress(in PlayerHudSnapshotComponent snapshot)
        {
            bool visible = snapshot.PressureSourceStableId > 0u && snapshot.PressureSourceThresholdDepleted >= 0;
            if (PressureSourceProgressRoot != null)
                PressureSourceProgressRoot.SetActive(visible);
            if (!visible)
                return;

            if (PressureSourceLabel != null)
                PressureSourceLabel.text = $"Pressure Source #{snapshot.PressureSourceStableId}";
            if (PressureSourceValueText != null)
                PressureSourceValueText.text =
                    $"{Mathf.Max(0, snapshot.PressureSourceCollected)} / {Mathf.Max(0, snapshot.PressureSourceThresholdDepleted)}";
            if (PressureSourceFillImage != null)
            {
                PressureSourceFillImage.type = Image.Type.Filled;
                PressureSourceFillImage.fillMethod = Image.FillMethod.Horizontal;
                PressureSourceFillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
                PressureSourceFillImage.fillAmount = Mathf.Clamp01(snapshot.PressureSourceProgress01);
            }

            if (PressureSourceWeakThresholdMarker == null)
                return;

            bool showMarker = snapshot.PressureSourceThresholdWeakened > 0 && snapshot.PressureSourceThresholdDepleted > 0;
            PressureSourceWeakThresholdMarker.gameObject.SetActive(showMarker);
            if (!showMarker)
                return;

            float markerRatio = Mathf.Clamp01((float)snapshot.PressureSourceThresholdWeakened / Mathf.Max(1, snapshot.PressureSourceThresholdDepleted));
            PressureSourceWeakThresholdMarker.anchorMin = new Vector2(markerRatio, 0f);
            PressureSourceWeakThresholdMarker.anchorMax = new Vector2(markerRatio, 1f);
            PressureSourceWeakThresholdMarker.pivot = new Vector2(0.5f, 0.5f);
            PressureSourceWeakThresholdMarker.anchoredPosition = Vector2.zero;
            PressureSourceWeakThresholdMarker.sizeDelta = new Vector2(4f, 0f);
        }

        private float ResolveStageTimeLimitSec()
        {
            if (_shell == null || _shell.StageProfiles == null || _shell.StageProfiles.Length <= 0)
                return -1f;

            int stageIndex = _shell.CurrentStageIndex;
            if (stageIndex >= 0 && stageIndex < _shell.StageProfiles.Length)
                return Mathf.Max(0f, _shell.StageProfiles[stageIndex].StageTimeLimitSec);

            int stageId = _shell.CurrentStageId;
            for (int i = 0; i < _shell.StageProfiles.Length; i++)
            {
                if (_shell.StageProfiles[i].StageId == stageId)
                    return Mathf.Max(0f, _shell.StageProfiles[i].StageTimeLimitSec);
            }

            return -1f;
        }
    }
}
