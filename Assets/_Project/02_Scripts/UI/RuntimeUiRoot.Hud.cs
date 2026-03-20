using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    public sealed partial class RuntimeUiRoot
    {
        private void BuildStageHudPanel()
        {
            var panelGo = EnsurePanel(ref StageHudPanel, HudLayer, "StageHudPanel", Color.clear);
            StageHudPresenter ??= panelGo.GetComponent<StageHudPresenter>() ?? panelGo.AddComponent<StageHudPresenter>();

            if (NeedsStageHudRebuild(panelGo.transform, StageHudPresenter))
            {
                ClearChildrenImmediate(panelGo.transform);
                ResetStageHudReferences();
            }

            var objectiveRoot = CreateHudBannerRoot(
                panelGo.transform,
                "TopCenterObjectiveRoot",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(520f, 190f),
                Color.clear);
            var objectiveBadgeRow = GetOrCreateChildGameObject(objectiveRoot, "ObjectiveBadgeRow").GetComponent<RectTransform>();
            Stretch(objectiveBadgeRow);
            var clearedBadge = CreateHudBadge(
                objectiveBadgeRow,
                "ClearedCountBadge",
                new Vector2(102f, 87f),
                new Color(0.20f, 0.27f, 0.37f, 1f));
            SetTopLeftRect(clearedBadge, 209f, -22f, 102f, 87f);
            StageHudPresenter.ObjectiveSummaryText ??= CreateCenteredOverlayText(clearedBadge, "ObjectiveSummaryText", "0 / 0", 28f);

            var timerBadge = CreateHudBadge(
                objectiveBadgeRow,
                "TimerBadge",
                new Vector2(102f, 76f),
                new Color(0.20f, 0.27f, 0.37f, 1f));
            SetTopLeftRect(timerBadge, 294f, -22f, 102f, 76f);
            StageHudPresenter.TimerValueText ??= CreateCenteredOverlayText(timerBadge, "TimerValueText", "--.-s", 24f);

            var pressureBlock = GetOrCreateChildGameObject(objectiveRoot, "PressureSourceProgressRoot", typeof(Image)).GetComponent<RectTransform>();
            SetTopLeftRect(pressureBlock, 20f, 100f, 480f, 66f);
            var pressureBlockImage = pressureBlock.GetComponent<Image>();
            pressureBlockImage.color = new Color(0.07f, 0.11f, 0.17f, 1f);
            ApplyDefaultImageSprite(pressureBlockImage);
            StageHudPresenter.PressureSourceProgressRoot ??= pressureBlock.gameObject;
            var pressureHeader = GetOrCreateChildGameObject(pressureBlock, "PressureSourceHeaderRow").GetComponent<RectTransform>();
            SetTopLeftRect(pressureHeader, 18f, 10f, 444f, 18f);
            StageHudPresenter.ObjectiveDetailText ??= FindOrCreateText(
                pressureHeader,
                "ObjectiveDetailText",
                "Pressure Source #1002",
                18f,
                FontStyles.Bold,
                TextAlignmentOptions.Left);
            SetTopLeftRect(StageHudPresenter.ObjectiveDetailText.rectTransform, 0f, 0f, 320f, 18f);
            StageHudPresenter.PressureSourceValueText ??= FindOrCreateFixedText(
                pressureHeader,
                "PressureSourceValueText",
                "0 / 0",
                16f,
                96f,
                TextAlignmentOptions.Right);
            SetTopLeftRect(StageHudPresenter.PressureSourceValueText.rectTransform, 348f, 0f, 96f, 18f);
            if (StageHudPresenter.PressureSourceFillImage == null || StageHudPresenter.PressureSourceWeakThresholdMarker == null)
            {
                var refs = CreateProgressBarWithMarker(pressureBlock, "PressureSourceBar", new Vector2(444f, 16f));
                SetTopLeftRect(refs.RootRect, 18f, 34f, 444f, 16f);
                StageHudPresenter.PressureSourceFillImage ??= refs.FillImage;
                StageHudPresenter.PressureSourceWeakThresholdMarker ??= refs.Marker;
            }
            StageHudPresenter.PressureSourceProgressRoot.SetActive(false);

            var carryRoot = CreateHudBannerRoot(
                panelGo.transform,
                "LeftCarryRoot",
                new Vector2(0.33333334f, 0.5f),
                new Vector2(0.33333334f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(68f, 167f),
                new Color(0.39f, 0.45f, 0.55f, 0.45f));
            var carryTotemRoot = CreateCarryTotemRoot(carryRoot, "CarryTotemRoot");
            Stretch(carryTotemRoot);
            StageHudPresenter.CarryLabel = null;
            StageHudPresenter.CarryValueText = null;
            StageHudPresenter.HazardStackLabel = null;
            StageHudPresenter.CarryFillImage ??= CreateVerticalFillBar(carryTotemRoot, "CarryBar", new Vector2(22f, 132f));
            SetBottomLeftRect(StageHudPresenter.CarryFillImage.transform.parent.GetComponent<RectTransform>(), 38f, 28f, 22f, 132f);
            var hazardRoot = CreateHazardStackRoot(carryTotemRoot, "HazardStackRoot");
            Stretch(hazardRoot);
            StageHudPresenter.HazardStackRoot ??= hazardRoot.gameObject;
            StageHudPresenter.HazardStackFrameImage ??= CreateHazardStackFrame(hazardRoot, "Segment Frame");
            var hazardSegmentsRoot = CreateHazardStackSegmentsRoot(hazardRoot, "HazardStackSegmentsRoot");
            SetBottomLeftRect(hazardSegmentsRoot, 0f, 14f, 52f, 130f);
            StageHudPresenter.HazardStackSegmentsRoot ??= hazardSegmentsRoot;
            StageHudPresenter.HazardStackSegmentSlotTemplate ??= CreateHazardSegmentSlotTemplate(hazardSegmentsRoot, "SegmentSlotTemplate");
            StageHudPresenter.SegmentScale = 0.25f;
            StageHudPresenter.SegmentStepY = 16f;
            StageHudPresenter.FrameBaseHeight = 10f;
            StageHudPresenter.FrameHeightPerSegment = 24f;
            StageHudPresenter.HazardStackSegmentImages = System.Array.Empty<Image>();
            StageHudPresenter.RiskMultiplierText ??= FindOrCreateFixedText(
                hazardRoot,
                "RiskMultiplierText",
                "x1.00",
                16f,
                42f,
                TextAlignmentOptions.Center);
            SetBottomLeftRect(StageHudPresenter.RiskMultiplierText.rectTransform, -8f, 6f, 42f, 17f);
        }

        private void BuildNotificationPanel()
        {
            DestroyDirectChildIfExists(HudLayer, "HintToastPanel");

            var panelGo = EnsurePanel(ref NotificationPanel, HudLayer, "NotificationPanel", Color.clear);
            NotificationPresenter ??= panelGo.GetComponent<NotificationPresenter>() ?? panelGo.AddComponent<NotificationPresenter>();
            var root = CreateHudBannerRoot(
                panelGo.transform,
                "NotificationRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 64f),
                new Vector2(520f, 56f),
                new Color(0.09f, 0.19f, 0.29f, 0.12f));
            NotificationPresenter.NotificationRoot = root.gameObject;
            NotificationPresenter.NotificationBackgroundImage = root.GetComponent<Image>();
            NotificationPresenter.NotificationText ??= CreateCenteredOverlayText(root, "NotificationText", "Time critical", 20f);
            NotificationPresenter.NotificationRoot.SetActive(false);
        }

        private void BuildHintPanel()
        {
            var panelGo = EnsurePanel(ref HintPanel, HudLayer, "HintPanel", Color.clear);
            HintPresenter ??= panelGo.GetComponent<HintPresenter>() ?? panelGo.AddComponent<HintPresenter>();
            var root = CreateHudBannerRoot(
                panelGo.transform,
                "HintRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(560f, 48f),
                new Color(0.10f, 0.15f, 0.21f, 0.10f));
            HintPresenter.HintRoot = root.gameObject;
            HintPresenter.HintText ??= CreateCenteredOverlayText(root, "HintText", "Carry is full. Head to Deposit.", 18f);
            HintPresenter.HintRoot.SetActive(false);
        }

        private static RectTransform CreateHudBlock(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = backgroundColor;
            ApplyDefaultImageSprite(image);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rect;
        }

        private static RectTransform CreateHudBannerRoot(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = backgroundColor;
            ApplyDefaultImageSprite(image);
            return rect;
        }

        private static RectTransform CreatePressureSourceProgressBlock(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 80f);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minHeight = 80f;
            layoutElement.preferredHeight = 80f;
            layoutElement.flexibleWidth = 1f;

            var image = go.GetComponent<Image>();
            image.color = new Color(0.11f, 0.14f, 0.19f, 0.96f);
            ApplyDefaultImageSprite(image);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 10);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static RectTransform CreateCarryTotemRoot(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(68f, 167f);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minWidth = 68f;
            layoutElement.preferredWidth = 68f;
            layoutElement.minHeight = 167f;
            layoutElement.preferredHeight = 167f;
            layoutElement.flexibleWidth = 0f;
            return rect;
        }

        private static RectTransform CreateHazardStackRoot(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(68f, 167f);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minWidth = 68f;
            layoutElement.preferredWidth = 68f;
            layoutElement.minHeight = 167f;
            layoutElement.preferredHeight = 167f;
            layoutElement.flexibleWidth = 0f;
            return rect;
        }

        private static RectTransform CreateHazardStackSegmentsRoot(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(52f, 130f);
            return rect;
        }

        private static Image CreateHazardStackFrame(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-1.5f, 65f);
            rect.sizeDelta = new Vector2(3f, 130f);

            var image = go.GetComponent<Image>();
            image.color = Color.white;
            ApplyDefaultImageSprite(image);
            return image;
        }

        private static RectTransform CreateHazardSegmentSlotTemplate(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;

            var display = GetOrCreateChildGameObject(go.transform, "Display", typeof(Image));
            var displayRect = display.GetComponent<RectTransform>();
            displayRect.anchorMin = Vector2.zero;
            displayRect.anchorMax = Vector2.zero;
            displayRect.pivot = new Vector2(0.5f, 0.5f);
            displayRect.anchoredPosition = Vector2.zero;
            displayRect.sizeDelta = new Vector2(20.5f, 24f);

            var image = display.GetComponent<Image>();
            image.color = Color.white;
            image.raycastTarget = false;
            ApplyDefaultImageSprite(image);
            return rect;
        }

        private static void SetVerticalAlignment(RectTransform root, TextAnchor alignment)
        {
            if (root == null)
                return;

            var layout = root.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                return;

            layout.childAlignment = alignment;
        }

        private static RectTransform CreateHorizontalStack(
            Transform parent,
            string name,
            float minHeight,
            float spacing,
            TextAnchor alignment,
            RectOffset padding,
            bool childControlWidth,
            bool childForceExpandWidth)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, minHeight);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minHeight = minHeight;
            layoutElement.preferredHeight = minHeight;
            layoutElement.flexibleWidth = 1f;

            var layout = go.GetComponent<HorizontalLayoutGroup>();
            layout.padding = padding ?? new RectOffset();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = childControlWidth;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = childForceExpandWidth;
            layout.childForceExpandHeight = false;
            return rect;
        }

        private static RectTransform CreateHudBadge(Transform parent, string name, Vector2 size, Color backgroundColor)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var layout = go.GetComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.preferredWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;
            layout.flexibleWidth = 0f;

            var image = go.GetComponent<Image>();
            image.color = backgroundColor;
            ApplyDefaultImageSprite(image);
            return rect;
        }

        private static bool NeedsStageHudRebuild(Transform root, StageHudPresenter presenter)
        {
            return presenter == null
                || presenter.ObjectiveSummaryText == null
                || presenter.ObjectiveDetailText == null
                || presenter.TimerValueText == null
                || presenter.PressureSourceProgressRoot == null
                || presenter.PressureSourceValueText == null
                || presenter.PressureSourceFillImage == null
                || presenter.PressureSourceWeakThresholdMarker == null
                || presenter.CarryFillImage == null
                || presenter.HazardStackRoot == null
                || presenter.RiskMultiplierText == null
                || presenter.HazardStackSegmentsRoot == null
                || presenter.HazardStackFrameImage == null
                || presenter.HazardStackSegmentSlotTemplate == null
                || root.Find("TopCenterObjectiveRoot") == null
                || root.Find("TopCenterObjectiveRoot/ObjectiveBadgeRow") == null
                || root.Find("TopCenterObjectiveRoot/PressureSourceProgressRoot") == null
                || root.Find("LeftCarryRoot") == null
                || root.Find("LeftCarryRoot/CarryTotemRoot") == null
                || root.Find("LeftCarryRoot/CarryTotemRoot/HazardStackRoot") == null
                || root.Find("LeftCarryRoot/CarryTotemRoot/HazardStackRoot/Segment Frame") == null
                || root.Find("LeftCarryRoot/CarryTotemRoot/HazardStackRoot/HazardStackSegmentsRoot/SegmentSlotTemplate") == null
                || root.Find("TopCenterObjectiveRoot").GetComponent<VerticalLayoutGroup>() != null
                || root.Find("TopCenterObjectiveRoot/PressureSourceProgressRoot").GetComponent<VerticalLayoutGroup>() != null
                || root.Find("LeftCarryRoot").GetComponent<VerticalLayoutGroup>() != null
                || root.Find("LeftCarryRoot").GetComponent<RectTransform>().sizeDelta != new Vector2(68f, 167f)
                || root.Find("LeftCarryRoot/CarryTotemRoot/HazardStackRoot/HazardStackSegmentsRoot").GetComponent<VerticalLayoutGroup>() != null;
        }

        private void ResetStageHudReferences()
        {
            if (StageHudPresenter == null)
                return;

            StageHudPresenter.ObjectiveSummaryText = null;
            StageHudPresenter.ObjectiveDetailText = null;
            StageHudPresenter.TimerValueText = null;
            StageHudPresenter.CarryLabel = null;
            StageHudPresenter.CarryValueText = null;
            StageHudPresenter.HazardStackLabel = null;
            StageHudPresenter.RiskMultiplierText = null;
            StageHudPresenter.PressureSourceValueText = null;
            StageHudPresenter.HazardStackRoot = null;
            StageHudPresenter.HazardStackSegmentsRoot = null;
            StageHudPresenter.HazardStackFrameImage = null;
            StageHudPresenter.HazardStackSegmentSlotTemplate = null;
            StageHudPresenter.PressureSourceProgressRoot = null;
            StageHudPresenter.HazardStackSegmentImages = null;
            StageHudPresenter.HazardStackActiveSprite = null;
            StageHudPresenter.HazardStackInactiveSprite = null;
            StageHudPresenter.PressureSourceFillImage = null;
            StageHudPresenter.PressureSourceWeakThresholdMarker = null;
            StageHudPresenter.CarryFillImage = null;
            StageHudPresenter.SegmentScale = 0.25f;
            StageHudPresenter.SegmentStepY = 16f;
            StageHudPresenter.FrameBaseHeight = 10f;
            StageHudPresenter.FrameHeightPerSegment = 24f;
        }

        private static void ClearChildrenImmediate(Transform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child.gameObject);
                    continue;
                }
#endif

                // In play mode, Destroy() is deferred until end-of-frame.
                // Detach first so same-frame rebuilds do not rebind soon-to-be-destroyed children by name.
                child.SetParent(null, false);
                Object.Destroy(child.gameObject);
            }
        }

        private static void DestroyDirectChildIfExists(Transform parent, string name)
        {
            if (parent == null)
                return;

            var child = parent.Find(name);
            if (child == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                Object.DestroyImmediate(child.gameObject);
                return;
            }
#endif
            Object.Destroy(child.gameObject);
        }

        private readonly struct ProgressBarMarkerRefs
        {
            public ProgressBarMarkerRefs(RectTransform rootRect, Image fillImage, RectTransform marker)
            {
                RootRect = rootRect;
                FillImage = fillImage;
                Marker = marker;
            }

            public RectTransform RootRect { get; }
            public Image FillImage { get; }
            public RectTransform Marker { get; }
        }

        private static ProgressBarMarkerRefs CreateProgressBarWithMarker(Transform parent, string name, Vector2 size)
        {
            var root = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(LayoutElement));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = size;

            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;
            layout.flexibleWidth = 1f;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.14f, 0.20f, 0.27f, 1f);
            ApplyDefaultImageSprite(background);

            var fill = GetOrCreateChildGameObject(root.transform, "Fill", typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, 3f, 3f);

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.22f, 0.74f, 0.97f, 1f);
            ApplyDefaultImageSprite(fillImage);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;

            var markerGo = GetOrCreateChildGameObject(root.transform, "WeakThresholdMarker", typeof(Image));
            var markerRect = markerGo.GetComponent<RectTransform>();
            markerRect.anchorMin = new Vector2(0.5f, 0f);
            markerRect.anchorMax = new Vector2(0.5f, 1f);
            markerRect.pivot = new Vector2(0.5f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.offsetMin = new Vector2(-2f, 3f);
            markerRect.offsetMax = new Vector2(2f, -3f);

            var markerImage = markerGo.GetComponent<Image>();
            markerImage.color = new Color(0.98f, 0.94f, 0.62f, 1f);
            ApplyDefaultImageSprite(markerImage);

            return new ProgressBarMarkerRefs(rootRect, fillImage, markerRect);
        }

        private static TextMeshProUGUI FindOrCreateText(Transform parent, string name, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                var existing = child.GetComponent<TextMeshProUGUI>();
                if (existing != null)
                    return existing;
            }

            return CreateText(parent, name, text, fontSize, fontStyle, alignment);
        }

        private static TextMeshProUGUI FindOrCreateFixedText(Transform parent, string name, string text, float fontSize, float width, TextAlignmentOptions alignment)
        {
            var child = parent.Find(name);
            if (child != null)
            {
                var existing = child.GetComponent<TextMeshProUGUI>();
                if (existing != null)
                    return existing;
            }

            if (width <= 0f)
                width = 140f;
            return CreateFixedText(parent, name, text, fontSize, width, alignment);
        }

        private static Image CreateFillBar(Transform parent, string name, Vector2 size)
        {
            var root = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(LayoutElement));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = size;

            var layout = root.GetComponent<LayoutElement>();
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;
            layout.flexibleWidth = 1f;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.14f, 0.20f, 0.27f, 1f);
            ApplyDefaultImageSprite(background);

            var fill = GetOrCreateChildGameObject(root.transform, "Fill", typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, 3f, 3f);

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.32f, 0.78f, 0.95f, 1f);
            ApplyDefaultImageSprite(fillImage);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImage.fillAmount = 0f;
            return fillImage;
        }

        private static Image CreateVerticalFillBar(Transform parent, string name, Vector2 size)
        {
            var root = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(LayoutElement));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = size;

            var layout = root.GetComponent<LayoutElement>();
            layout.minWidth = size.x;
            layout.preferredWidth = size.x;
            layout.minHeight = size.y;
            layout.preferredHeight = size.y;
            layout.flexibleWidth = 0f;

            var background = root.GetComponent<Image>();
            background.color = new Color(0.18f, 0.20f, 0.24f, 0.95f);
            ApplyDefaultImageSprite(background);

            var fill = GetOrCreateChildGameObject(root.transform, "Fill", typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, 3f, 3f);

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.32f, 0.78f, 0.95f, 1f);
            ApplyDefaultImageSprite(fillImage);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Vertical;
            fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
            fillImage.fillAmount = 0f;
            return fillImage;
        }

        private static TextMeshProUGUI CreateCenteredOverlayText(Transform parent, string name, string text, float fontSize)
        {
            var go = GetOrCreateChildGameObject(parent, name);
            var rect = go.GetComponent<RectTransform>();
            Stretch(rect, 12f, 8f);

            var textComponent = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = FontStyles.Bold;
            textComponent.alignment = TextAlignmentOptions.Center;
            textComponent.color = Color.white;
            if (textComponent.font == null && TMP_Settings.defaultFontAsset != null)
                textComponent.font = TMP_Settings.defaultFontAsset;
            return textComponent;
        }

        private static void SetTopLeftRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void SetBottomLeftRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
