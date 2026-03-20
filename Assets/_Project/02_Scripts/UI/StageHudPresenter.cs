using System;
using System.Collections.Generic;
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
        public RectTransform HazardStackSegmentsRoot;
        public Image HazardStackFrameImage;
        public RectTransform HazardStackSegmentSlotTemplate;
        public Sprite HazardStackActiveSprite;
        public Sprite HazardStackInactiveSprite;
        public Image PressureSourceFillImage;
        public RectTransform PressureSourceWeakThresholdMarker;
        public Image CarryFillImage;

        [Header("Hazard Layout")]
        public float SegmentScale = 0.25f;
        public float SegmentStepY = 16f;
        public float FrameBaseHeight = 10f;
        public float FrameHeightPerSegment = 24f;

        private static readonly Color TimerNormalColor = new(0.92f, 0.96f, 1f, 1f);
        private static readonly Color WarningColor = new(1f, 0.76f, 0.27f, 1f);
        private static readonly Color DangerColor = new(1f, 0.38f, 0.38f, 1f);
        private static readonly Color CarryNormalColor = new(0.22f, 0.74f, 0.97f, 1f);
        private static readonly Color CarryWarningColor = new(1f, 0.72f, 0.18f, 1f);
        private static readonly Color HazardNeutralTextColor = new(0.82f, 0.88f, 0.94f, 0.74f);
        private static readonly Color HazardActiveTextColor = new(1f, 0.83f, 0.54f, 1f);

        private readonly List<HazardSegmentView> _hazardSegmentViews = new();

        private DemoShellFlowController _shell;
        private PlayerRuntimeHudBridge _runtimeHud;
        private int _configuredHazardStackMax = -1;

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
                ObjectiveSummaryText.text = "0 / 0";

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
                CarryFillImage.fillMethod = Image.FillMethod.Vertical;
                CarryFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
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
                $"{Mathf.Max(0, snapshot.DepletedSourceCount)} / {Mathf.Max(0, snapshot.TotalSourceCount)}";
        }

        private void ApplyObjectiveDetail(in PlayerHudSnapshotComponent snapshot)
        {
            bool visible = snapshot.PressureSourceStableId > 0u && snapshot.PressureSourceThresholdDepleted > 0;

            if (ObjectiveDetailText != null)
            {
                ObjectiveDetailText.gameObject.SetActive(visible);
                ObjectiveDetailText.text = visible
                    ? $"Pressure Source #{snapshot.PressureSourceStableId}"
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
                CarryFillImage.fillMethod = Image.FillMethod.Vertical;
                CarryFillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
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

            int maxSegments = Mathf.Max(0, snapshot.HazardStackMax);
            EnsureHazardSegmentViews(maxSegments);
            ApplyHazardFrameHeight(maxSegments);

            int activeSegments = Mathf.Clamp(snapshot.HazardStack, 0, maxSegments);
            for (int i = 0; i < _hazardSegmentViews.Count; i++)
            {
                ApplyHazardSegmentState(_hazardSegmentViews[i], i < activeSegments);
            }

            ReorderHazardSegments(activeSegments);

            if (RiskMultiplierText != null)
            {
                float multiplier = Mathf.Max(1f, snapshot.HazardRiskMultiplier);
                RiskMultiplierText.text = $"x{multiplier:0.00}";
                RiskMultiplierText.color = activeSegments > 0 ? HazardActiveTextColor : HazardNeutralTextColor;
            }
        }

        private void EnsureHazardSegmentViews(int maxSegments)
        {
            ResolveHazardStackReferences();

            if (HazardStackSegmentsRoot == null || HazardStackSegmentSlotTemplate == null)
            {
                HazardStackSegmentImages = Array.Empty<Image>();
                _hazardSegmentViews.Clear();
                _configuredHazardStackMax = maxSegments;
                return;
            }

            if (_configuredHazardStackMax == maxSegments && ValidateHazardSegmentViews(maxSegments))
                return;

            var templateDisplay = ResolveHazardSegmentDisplay(HazardStackSegmentSlotTemplate, createIfMissing: true);
            if (templateDisplay == null)
            {
                HazardStackSegmentImages = Array.Empty<Image>();
                _hazardSegmentViews.Clear();
                _configuredHazardStackMax = maxSegments;
                return;
            }

            _hazardSegmentViews.Clear();
            HazardStackSegmentSlotTemplate.gameObject.SetActive(maxSegments > 0);

            for (int i = 0; i < maxSegments; i++)
            {
                var slot = GetOrCreateHazardSegmentSlot(i);
                if (slot == null)
                    continue;

                ConfigureHazardSlotTransform(slot, i);
                var display = ResolveHazardSegmentDisplay(slot, createIfMissing: true);
                if (display == null)
                    continue;

                ConfigureHazardDisplayTransform(display.rectTransform);
                slot.gameObject.SetActive(true);
                _hazardSegmentViews.Add(new HazardSegmentView(i, slot, display));
            }

            SetUnusedHazardSlotsInactive(maxSegments);

            HazardStackSegmentImages = new Image[_hazardSegmentViews.Count];
            for (int i = 0; i < _hazardSegmentViews.Count; i++)
                HazardStackSegmentImages[i] = _hazardSegmentViews[i].DisplayImage;

            _configuredHazardStackMax = maxSegments;
        }

        private void ResolveHazardStackReferences()
        {
            if (HazardStackRoot != null)
            {
                if (HazardStackSegmentsRoot == null)
                {
                    var root = HazardStackRoot.transform.Find("HazardStackSegmentsRoot");
                    HazardStackSegmentsRoot = root as RectTransform;
                }

                if (HazardStackFrameImage == null)
                {
                    var frame = HazardStackRoot.transform.Find("Segment Frame");
                    if (frame != null)
                        HazardStackFrameImage = frame.GetComponent<Image>();
                }
            }

            if (HazardStackSegmentsRoot != null && HazardStackSegmentSlotTemplate == null)
            {
                var template = HazardStackSegmentsRoot.Find("SegmentSlotTemplate");
                if (template == null && HazardStackSegmentsRoot.childCount > 0)
                    template = HazardStackSegmentsRoot.GetChild(0);

                HazardStackSegmentSlotTemplate = template as RectTransform;
            }
        }

        private bool ValidateHazardSegmentViews(int maxSegments)
        {
            if (HazardStackSegmentImages == null || HazardStackSegmentImages.Length != maxSegments)
                return false;

            if (_hazardSegmentViews.Count != maxSegments)
                return false;

            for (int i = 0; i < _hazardSegmentViews.Count; i++)
            {
                if (_hazardSegmentViews[i].SlotRoot == null || _hazardSegmentViews[i].DisplayImage == null)
                    return false;
            }

            return true;
        }

        private RectTransform GetOrCreateHazardSegmentSlot(int index)
        {
            if (index == 0)
                return HazardStackSegmentSlotTemplate;

            string name = $"SegmentSlot_{index}";
            var existing = HazardStackSegmentsRoot.Find(name) as RectTransform;
            if (existing != null)
                return existing;

            var clone = Instantiate(HazardStackSegmentSlotTemplate.gameObject, HazardStackSegmentsRoot);
            clone.name = name;
            clone.SetActive(true);
            return clone.GetComponent<RectTransform>();
        }

        private void SetUnusedHazardSlotsInactive(int maxSegments)
        {
            if (HazardStackSegmentsRoot == null)
                return;

            for (int i = 0; i < HazardStackSegmentsRoot.childCount; i++)
            {
                var child = HazardStackSegmentsRoot.GetChild(i);
                if (child == null)
                    continue;

                if (child == HazardStackSegmentSlotTemplate)
                {
                    child.gameObject.SetActive(maxSegments > 0);
                    continue;
                }

                if (TryGetHazardSegmentIndex(child.name, out int slotIndex))
                {
                    child.gameObject.SetActive(slotIndex < maxSegments);
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        private void ConfigureHazardSlotTransform(RectTransform slot, int index)
        {
            if (slot == null)
                return;

            slot.anchorMin = new Vector2(0.5f, 0f);
            slot.anchorMax = new Vector2(0.5f, 0f);
            slot.pivot = new Vector2(0.5f, 0f);
            slot.anchoredPosition = new Vector2(0f, index * SegmentStepY);
            slot.sizeDelta = Vector2.zero;
            slot.localScale = Vector3.one;
            slot.localRotation = Quaternion.identity;
        }

        private static Image ResolveHazardSegmentDisplay(RectTransform slot, bool createIfMissing)
        {
            if (slot == null)
                return null;

            var display = slot.Find("Display");
            if (display != null)
            {
                var existing = display.GetComponent<Image>();
                if (existing != null)
                    return existing;
            }

            var imageOnSlot = slot.GetComponent<Image>();
            if (imageOnSlot != null)
                return imageOnSlot;

            if (!createIfMissing)
                return null;

            var displayGo = new GameObject("Display", typeof(RectTransform), typeof(Image));
            var displayRect = displayGo.GetComponent<RectTransform>();
            displayRect.SetParent(slot, false);
            return displayGo.GetComponent<Image>();
        }

        private static void ConfigureHazardDisplayTransform(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void ApplyHazardSegmentState(HazardSegmentView segment, bool active)
        {
            if (segment.DisplayImage == null)
                return;

            Sprite sprite = active ? HazardStackActiveSprite : HazardStackInactiveSprite;
            ApplyHazardSegmentSprite(segment.DisplayImage, sprite);
        }

        private void ApplyHazardSegmentSprite(Image displayImage, Sprite sprite)
        {
            if (displayImage == null)
                return;

            displayImage.enabled = sprite != null;
            displayImage.sprite = sprite;
            displayImage.type = Image.Type.Simple;
            displayImage.preserveAspect = true;
            displayImage.color = Color.white;
            displayImage.raycastTarget = false;

            if (sprite == null)
                return;

            displayImage.SetNativeSize();

            float scale = SegmentScale > 0f ? SegmentScale : 1f;
            var rect = displayImage.rectTransform;
            rect.sizeDelta *= scale;
            UiSpritePivotUtility.TryApplySpritePivot(displayImage, preserveWorldRect: false);
            rect.anchoredPosition = Vector2.zero;
            rect.localPosition = Vector3.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        private void ApplyHazardFrameHeight(int maxSegments)
        {
            float height = Mathf.Max(0f, FrameBaseHeight + maxSegments * FrameHeightPerSegment);

            if (HazardStackFrameImage != null)
            {
                var rect = HazardStackFrameImage.rectTransform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }

            if (HazardStackSegmentsRoot != null)
                HazardStackSegmentsRoot.sizeDelta = new Vector2(HazardStackSegmentsRoot.sizeDelta.x, height);
        }

        private void ReorderHazardSegments(int activeSegments)
        {
            int drawIndex = 0;
            for (int i = _hazardSegmentViews.Count - 1; i >= activeSegments; i--)
            {
                var slot = _hazardSegmentViews[i].SlotRoot;
                if (slot != null)
                    slot.SetSiblingIndex(drawIndex++);
            }

            for (int i = 0; i < activeSegments; i++)
            {
                var slot = _hazardSegmentViews[i].SlotRoot;
                if (slot != null)
                    slot.SetSiblingIndex(drawIndex++);
            }
        }

        private static bool TryGetHazardSegmentIndex(string name, out int index)
        {
            index = -1;
            const string prefix = "SegmentSlot_";
            if (string.IsNullOrEmpty(name) || !name.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            return int.TryParse(name.Substring(prefix.Length), out index);
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

        private readonly struct HazardSegmentView
        {
            public HazardSegmentView(int logicalIndex, RectTransform slotRoot, Image displayImage)
            {
                LogicalIndex = logicalIndex;
                SlotRoot = slotRoot;
                DisplayImage = displayImage;
            }

            public int LogicalIndex { get; }
            public RectTransform SlotRoot { get; }
            public Image DisplayImage { get; }
        }
    }
}
