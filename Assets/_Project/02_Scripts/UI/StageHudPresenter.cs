using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class StageHudPresenter : MonoBehaviour
    {
        [Header("Texts")]
        public TextMeshProUGUI ObjectiveSummaryText;
        public TextMeshProUGUI ObjectiveDetailText;
        public TextMeshProUGUI TimerValueText;
        public TextMeshProUGUI CarryLabel;
        public TextMeshProUGUI CarryValueText;
        public TextMeshProUGUI HazardStackLabel;
        public TextMeshProUGUI RiskMultiplierText;
        public TextMeshProUGUI PressureSourceValueText;

        [Header("Visuals")]
        public GameObject HazardStackRoot;
        public GameObject PressureSourceProgressRoot;
        public Image[] HazardStackSegmentImages;
        public Image PressureSourceFillImage;
        public RectTransform PressureSourceWeakThresholdMarker;
        public Image CarryFillImage;

        private static readonly Color TimerNormalColor = new(0.92f, 0.96f, 1f, 1f);
        private static readonly Color WarningColor = new(1f, 0.76f, 0.27f, 1f);
        private static readonly Color DangerColor = new(1f, 0.38f, 0.38f, 1f);
        private static readonly Color CarryNormalColor = new(0.32f, 0.78f, 0.95f, 1f);
        private static readonly Color CarryWarningColor = new(1f, 0.72f, 0.18f, 1f);
        private static readonly Color HazardMutedColor = new(0.54f, 0.59f, 0.66f, 0.42f);
        private static readonly Color HazardActiveColor = new(1f, 0.69f, 0.26f, 1f);
        private static readonly Color HazardCapColor = new(1f, 0.44f, 0.32f, 1f);
        private static readonly Color HazardNeutralTextColor = new(0.82f, 0.88f, 0.94f, 0.74f);
        private static readonly Color HazardActiveTextColor = new(1f, 0.83f, 0.54f, 1f);

        private DemoShellFlowController _shell;
        private PlayerRuntimeHudBridge _runtimeHud;

        public void Configure(DemoShellFlowController shell, PlayerRuntimeHudBridge runtimeHud)
        {
            _shell = shell;
            _runtimeHud = runtimeHud;
        }

        public void RefreshPresentation()
        {
            EnsureRuntimeReferences();

            if (_shell == null)
            {
                ApplyDefaultPresentation();
                return;
            }

            if (CarryLabel != null)
                CarryLabel.text = "Carry";

            if (_runtimeHud == null || !_runtimeHud.TryGetLastSnapshot(out var snapshot))
            {
                ApplyDefaultPresentation();
                return;
            }

            float stageLimitSec = ResolveStageTimeLimitSec();
            float remainingSec = stageLimitSec >= 0f
                ? Mathf.Max(0f, stageLimitSec - snapshot.GameplayElapsedSec)
                : -1f;

            ApplyObjectiveSummary(snapshot);
            ApplyObjectiveDetail(snapshot);
            ApplyCarry(snapshot);
            ApplyHazardStack(snapshot);
            ApplyTimer(remainingSec);
        }

        private void ApplyDefaultPresentation()
        {
            if (ObjectiveSummaryText != null)
                ObjectiveSummaryText.text = "Sources 0/0 cleared";

            if (ObjectiveDetailText != null)
            {
                ObjectiveDetailText.text = string.Empty;
                ObjectiveDetailText.gameObject.SetActive(false);
            }

            if (PressureSourceProgressRoot != null)
                PressureSourceProgressRoot.SetActive(false);

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

            ApplyHazardStack(default);
        }

        private void ApplyObjectiveSummary(in PlayerHudSnapshotComponent snapshot)
        {
            if (ObjectiveSummaryText == null)
                return;

            ObjectiveSummaryText.text =
                $"Sources {Mathf.Max(0, snapshot.DepletedSourceCount)}/{Mathf.Max(0, snapshot.TotalSourceCount)} cleared";
        }

        private void ApplyObjectiveDetail(in PlayerHudSnapshotComponent snapshot)
        {
            bool visible = snapshot.PressureSourceStableId > 0u && snapshot.PressureSourceThresholdDepleted > 0;

            if (ObjectiveDetailText != null)
            {
                ObjectiveDetailText.gameObject.SetActive(visible);
                ObjectiveDetailText.text = visible
                    ? $"Pressure Source #{snapshot.PressureSourceStableId}  {Mathf.Max(0, snapshot.PressureSourceCollected)}/{Mathf.Max(0, snapshot.PressureSourceThresholdDepleted)}"
                    : string.Empty;
            }

            ApplyPressureSourceProgress(snapshot, visible);
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

        private void EnsureRuntimeReferences()
        {
            if (_shell != null && _runtimeHud != null)
                return;

            var root = GetComponentInParent<RuntimeUiRoot>();
            if (root != null)
            {
                _shell ??= root.DemoShell;
                _runtimeHud ??= root.RuntimeHudBridge;
            }

            if (_shell == null)
            {
#if UNITY_2023_1_OR_NEWER
                _shell = FindFirstObjectByType<DemoShellFlowController>();
#else
                _shell = FindObjectOfType<DemoShellFlowController>();
#endif
            }

            if (_runtimeHud == null)
            {
#if UNITY_2023_1_OR_NEWER
                _runtimeHud = FindFirstObjectByType<PlayerRuntimeHudBridge>();
#else
                _runtimeHud = FindObjectOfType<PlayerRuntimeHudBridge>();
#endif
            }
        }

        private void ApplyPressureSourceProgress(in PlayerHudSnapshotComponent snapshot, bool visible)
        {
            if (PressureSourceProgressRoot != null)
                PressureSourceProgressRoot.SetActive(visible);
            if (!visible)
                return;

            if (PressureSourceValueText != null)
            {
                PressureSourceValueText.text =
                    $"{Mathf.Max(0, snapshot.PressureSourceCollected)} / {Mathf.Max(0, snapshot.PressureSourceThresholdDepleted)}";
            }

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

        private void ApplyHazardStack(in PlayerHudSnapshotComponent snapshot)
        {
            if (HazardStackRoot != null)
                HazardStackRoot.SetActive(true);

            if (HazardStackLabel != null)
                HazardStackLabel.text = "Hazard";

            int visibleSegmentCount = HazardStackSegmentImages != null ? HazardStackSegmentImages.Length : 0;
            int activeSegments = Mathf.Clamp(snapshot.HazardStack, 0, visibleSegmentCount);
            bool capped = visibleSegmentCount > 0 && snapshot.HazardStack >= visibleSegmentCount;

            if (HazardStackSegmentImages != null)
            {
                for (int i = 0; i < HazardStackSegmentImages.Length; i++)
                {
                    var segment = HazardStackSegmentImages[i];
                    if (segment == null)
                        continue;

                    segment.enabled = true;
                    segment.color = i < activeSegments
                        ? (capped ? HazardCapColor : HazardActiveColor)
                        : HazardMutedColor;
                }
            }

            if (RiskMultiplierText != null)
            {
                float multiplier = Mathf.Max(1f, snapshot.HazardRiskMultiplier);
                RiskMultiplierText.text = $"x{multiplier:0.00}";
                RiskMultiplierText.color = activeSegments > 0 ? HazardActiveTextColor : HazardNeutralTextColor;
            }
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
