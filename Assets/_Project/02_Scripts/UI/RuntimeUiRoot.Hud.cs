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

            var topLeft = CreateHudBlock(
                panelGo.transform,
                "TopLeftAnchor",
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(24f, -24f),
                new Vector2(420f, 214f),
                new Color(0.08f, 0.10f, 0.14f, 0.72f));
            StageHudPresenter.StageLabel ??= FindOrCreateText(topLeft, "StageLabel", "Stage 1", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
            StageHudPresenter.ObjectiveText ??= FindOrCreateText(topLeft, "ObjectiveText", "Collect trash from sources", 22f, FontStyles.Normal, TextAlignmentOptions.Left);
            StageHudPresenter.SourceProgressText ??= FindOrCreateText(topLeft, "SourceProgressText", "Sources 0/0 cleared", 18f, FontStyles.Normal, TextAlignmentOptions.Left);

            var pressureBlock = CreatePressureSourceProgressBlock(topLeft, "PressureSourceProgressRoot");
            StageHudPresenter.PressureSourceProgressRoot ??= pressureBlock.gameObject;
            StageHudPresenter.PressureSourceLabel ??= FindOrCreateFixedText(pressureBlock, "PressureSourceLabel", "Pressure Source", 18f, 0f, TextAlignmentOptions.Left);
            StageHudPresenter.PressureSourceValueText ??= FindOrCreateFixedText(pressureBlock, "PressureSourceValueText", "0 / 0", 16f, 0f, TextAlignmentOptions.Right);
            if (StageHudPresenter.PressureSourceFillImage == null || StageHudPresenter.PressureSourceWeakThresholdMarker == null)
            {
                var refs = CreateProgressBarWithMarker(pressureBlock, "PressureSourceBar", new Vector2(0f, 22f));
                StageHudPresenter.PressureSourceFillImage ??= refs.FillImage;
                StageHudPresenter.PressureSourceWeakThresholdMarker ??= refs.Marker;
            }
            StageHudPresenter.PressureSourceProgressRoot.SetActive(false);

            var topRight = CreateHudBlock(
                panelGo.transform,
                "TopRightAnchor",
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-24f, -24f),
                new Vector2(360f, 188f),
                new Color(0.08f, 0.10f, 0.14f, 0.72f));
            StageHudPresenter.TimerLabel ??= FindOrCreateText(topRight, "TimerLabel", "Time", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            StageHudPresenter.TimerValueText ??= FindOrCreateText(topRight, "TimerValueText", "--.-s", 34f, FontStyles.Bold, TextAlignmentOptions.Left);
            StageHudPresenter.CarryLabel ??= FindOrCreateText(topRight, "CarryLabel", "Carry", 20f, FontStyles.Bold, TextAlignmentOptions.Left);
            StageHudPresenter.CarryFillImage ??= CreateFillBar(topRight, "CarryBar", new Vector2(0f, 26f));
            StageHudPresenter.CarryValueText ??= FindOrCreateText(topRight, "CarryValueText", "0 / 0", 18f, FontStyles.Normal, TextAlignmentOptions.Left);

            var topCenter = CreateHudBannerRoot(
                panelGo.transform,
                "TopCenterAnchor",
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -24f),
                new Vector2(480f, 64f),
                new Color(0.42f, 0.10f, 0.10f, 0.94f));
            StageHudPresenter.DangerBannerRoot ??= topCenter.gameObject;
            StageHudPresenter.DangerBannerImage ??= topCenter.GetComponent<Image>();
            StageHudPresenter.DangerText ??= CreateCenteredOverlayText(topCenter, "DangerText", "Carry full - deposit now", 22f);
            StageHudPresenter.DangerBannerRoot.SetActive(false);
        }

        private void BuildHintToastPanel()
        {
            var panelGo = EnsurePanel(ref HintToastPanel, HudLayer, "HintToastPanel", Color.clear);
            HintToastPresenter ??= panelGo.GetComponent<HintToastPresenter>() ?? panelGo.AddComponent<HintToastPresenter>();
            if (HintToastPresenter.ToastRoot != null)
                return;

            var toastRoot = CreateHudBannerRoot(
                panelGo.transform,
                "ToastRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 28f),
                new Vector2(420f, 56f),
                new Color(0.10f, 0.16f, 0.24f, 0.90f));
            HintToastPresenter.ToastRoot = toastRoot.gameObject;
            HintToastPresenter.ToastText = CreateCenteredOverlayText(toastRoot, "ToastText", "Hazard Captured", 20f);
            HintToastPresenter.ToastRoot.SetActive(false);
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
