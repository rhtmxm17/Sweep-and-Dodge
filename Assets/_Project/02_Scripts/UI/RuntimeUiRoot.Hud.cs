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

            var objectiveRoot = CreateHudBlock(
                panelGo.transform,
                "TopCenterObjectiveRoot",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(520f, 190f),
                new Color(0.08f, 0.10f, 0.14f, 0.72f));
            SetVerticalAlignment(objectiveRoot, TextAnchor.UpperCenter);
            StageHudPresenter.ObjectiveSummaryText ??= FindOrCreateText(
                objectiveRoot,
                "ObjectiveSummaryText",
                "Sources 0/0 cleared",
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            StageHudPresenter.ObjectiveDetailText ??= FindOrCreateText(
                objectiveRoot,
                "ObjectiveDetailText",
                "Pressure Source #1002  0/0",
                18f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);
            StageHudPresenter.TimerValueText ??= FindOrCreateText(
                objectiveRoot,
                "TimerValueText",
                "--.-s",
                24f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);

            var pressureBlock = CreatePressureSourceProgressBlock(objectiveRoot, "PressureSourceProgressRoot");
            SetVerticalAlignment(pressureBlock, TextAnchor.UpperCenter);
            StageHudPresenter.PressureSourceProgressRoot ??= pressureBlock.gameObject;
            StageHudPresenter.PressureSourceValueText ??= FindOrCreateFixedText(
                pressureBlock,
                "PressureSourceValueText",
                "0 / 0",
                16f,
                220f,
                TextAlignmentOptions.Center);
            if (StageHudPresenter.PressureSourceFillImage == null || StageHudPresenter.PressureSourceWeakThresholdMarker == null)
            {
                var refs = CreateProgressBarWithMarker(pressureBlock, "PressureSourceBar", new Vector2(0f, 22f));
                StageHudPresenter.PressureSourceFillImage ??= refs.FillImage;
                StageHudPresenter.PressureSourceWeakThresholdMarker ??= refs.Marker;
            }
            StageHudPresenter.PressureSourceProgressRoot.SetActive(false);

            var carryRoot = CreateHudBlock(
                panelGo.transform,
                "LeftCarryRoot",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(24f, 0f),
                new Vector2(300f, 120f),
                new Color(0.08f, 0.10f, 0.14f, 0.72f));
            StageHudPresenter.CarryLabel ??= FindOrCreateText(carryRoot, "CarryLabel", "Carry", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            StageHudPresenter.CarryFillImage ??= CreateFillBar(carryRoot, "CarryBar", new Vector2(0f, 24f));
            StageHudPresenter.CarryValueText ??= FindOrCreateText(carryRoot, "CarryValueText", "0 / 0", 18f, FontStyles.Normal, TextAlignmentOptions.Left);
        }

        private void BuildNotificationPanel()
        {
            DestroyDirectChildIfExists(HudLayer, "HintToastPanel");

            var panelGo = EnsurePanel(ref NotificationPanel, HudLayer, "NotificationPanel", Color.clear);
            NotificationPresenter ??= panelGo.GetComponent<NotificationPresenter>() ?? panelGo.AddComponent<NotificationPresenter>();
            if (NotificationPresenter.NotificationRoot != null)
                return;

            var root = CreateHudBannerRoot(
                panelGo.transform,
                "NotificationRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 64f),
                new Vector2(520f, 56f),
                new Color(0.10f, 0.16f, 0.24f, 0.90f));
            NotificationPresenter.NotificationRoot = root.gameObject;
            NotificationPresenter.NotificationBackgroundImage = root.GetComponent<Image>();
            NotificationPresenter.NotificationText = CreateCenteredOverlayText(root, "NotificationText", "Time critical", 20f);
            NotificationPresenter.NotificationRoot.SetActive(false);
        }

        private void BuildHintPanel()
        {
            var panelGo = EnsurePanel(ref HintPanel, HudLayer, "HintPanel", Color.clear);
            HintPresenter ??= panelGo.GetComponent<HintPresenter>() ?? panelGo.AddComponent<HintPresenter>();
            if (HintPresenter.HintRoot != null)
                return;

            var root = CreateHudBannerRoot(
                panelGo.transform,
                "HintRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 16f),
                new Vector2(560f, 48f),
                new Color(0.12f, 0.16f, 0.18f, 0.88f));
            HintPresenter.HintRoot = root.gameObject;
            HintPresenter.HintText = CreateCenteredOverlayText(root, "HintText", "Carry is full. Head to Deposit.", 18f);
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
            var go = GetOrCreateChildGameObject(parent, name, typeof(VerticalLayoutGroup), typeof(LayoutElement));
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 72f);

            var layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minHeight = 72f;
            layoutElement.preferredHeight = 72f;
            layoutElement.flexibleWidth = 1f;

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 4, 0);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
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

        private static bool NeedsStageHudRebuild(Transform root, StageHudPresenter presenter)
        {
            return presenter == null
                || presenter.ObjectiveSummaryText == null
                || presenter.ObjectiveDetailText == null
                || presenter.CarryFillImage == null
                || root.Find("TopCenterObjectiveRoot") == null
                || root.Find("LeftCarryRoot") == null;
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
            StageHudPresenter.PressureSourceValueText = null;
            StageHudPresenter.PressureSourceProgressRoot = null;
            StageHudPresenter.PressureSourceFillImage = null;
            StageHudPresenter.PressureSourceWeakThresholdMarker = null;
            StageHudPresenter.CarryFillImage = null;
        }

        private static void ClearChildrenImmediate(Transform root)
        {
            if (root == null)
                return;

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    Object.DestroyImmediate(child);
                    continue;
                }
#endif
                Object.Destroy(child);
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
            public ProgressBarMarkerRefs(Image fillImage, RectTransform marker)
            {
                FillImage = fillImage;
                Marker = marker;
            }

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
            background.color = new Color(0.18f, 0.20f, 0.24f, 0.95f);
            ApplyDefaultImageSprite(background);

            var fill = GetOrCreateChildGameObject(root.transform, "Fill", typeof(Image));
            var fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, 3f, 3f);

            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.38f, 0.82f, 0.58f, 1f);
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

            return new ProgressBarMarkerRefs(fillImage, markerRect);
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
            background.color = new Color(0.18f, 0.20f, 0.24f, 0.95f);
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
    }
}
