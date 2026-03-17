using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    public sealed partial class RuntimeUiRoot
    {
        private void BuildDialoguePanel()
        {
            var panelGo = EnsurePanel(ref DialoguePanel, PresentationLayer, "DialoguePanel", Color.clear);
            DialoguePresenter ??= panelGo.GetComponent<InWorldDialoguePresenter>() ?? panelGo.AddComponent<InWorldDialoguePresenter>();

            if (NeedsDialogueRebuild(panelGo.transform, DialoguePresenter))
            {
                ClearChildrenImmediate(panelGo.transform);
                ResetDialogueReferences();
            }

            var dialogueRoot = GetOrCreateChildGameObject(panelGo.transform, "DialogueRoot");
            var dialogueRootRect = dialogueRoot.GetComponent<RectTransform>();
            Stretch(dialogueRootRect);
            DialoguePresenter.DialogueRoot ??= dialogueRoot;

            var dimGo = GetOrCreateChildGameObject(dialogueRoot.transform, "DimRoot", typeof(Image));
            var dimRect = dimGo.GetComponent<RectTransform>();
            Stretch(dimRect);
            var dimImage = dimGo.GetComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, 0.42f);
            ApplyDefaultImageSprite(dimImage);
            dimImage.raycastTarget = false;
            DialoguePresenter.DimRoot ??= dimGo;
            DialoguePresenter.DimImage ??= dimImage;

            var plateRoot = CreateDialoguePlateRoot(dialogueRoot.transform, "PlateRoot");
            DialoguePresenter.DialoguePlateRoot ??= plateRoot;
            DialoguePresenter.NameText ??= FindOrCreateText(
                plateRoot,
                "NameText",
                "Speaker",
                28f,
                FontStyles.Bold,
                TextAlignmentOptions.Center);
            DialoguePresenter.BodyText ??= FindOrCreateText(
                plateRoot,
                "BodyText",
                "Dialogue line",
                24f,
                FontStyles.Normal,
                TextAlignmentOptions.Center);

            var portraitRoot = GetOrCreateChildGameObject(dialogueRoot.transform, "PortraitRoot", typeof(Image));
            var portraitRect = portraitRoot.GetComponent<RectTransform>();
            portraitRect.anchorMin = new Vector2(0f, 0f);
            portraitRect.anchorMax = new Vector2(0f, 0f);
            portraitRect.pivot = new Vector2(0f, 0f);
            portraitRect.anchoredPosition = new Vector2(72f, 72f);
            portraitRect.sizeDelta = new Vector2(420f, 620f);
            var portraitImage = portraitRoot.GetComponent<Image>();
            portraitImage.color = Color.white;
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;
            DialoguePresenter.PortraitRoot ??= portraitRoot;
            DialoguePresenter.PortraitImage ??= portraitImage;

            var advanceRoot = CreateDialoguePromptRoot(
                dialogueRoot.transform,
                "AdvancePromptRoot",
                new Vector2(1f, 0f),
                new Vector2(-180f, 28f),
                new Vector2(128f, 42f),
                new Color(0.12f, 0.16f, 0.22f, 0.88f));
            DialoguePresenter.AdvancePromptRoot ??= advanceRoot.gameObject;
            DialoguePresenter.AdvancePromptText ??= CreateCenteredOverlayText(advanceRoot, "AdvancePromptText", "Next", 18f);

            var skipRoot = CreateDialoguePromptRoot(
                dialogueRoot.transform,
                "SkipPromptRoot",
                new Vector2(1f, 0f),
                new Vector2(-40f, 28f),
                new Vector2(112f, 42f),
                new Color(0.18f, 0.14f, 0.12f, 0.88f));
            DialoguePresenter.SkipPromptRoot ??= skipRoot.gameObject;
            DialoguePresenter.SkipPromptText ??= CreateCenteredOverlayText(skipRoot, "SkipPromptText", "Skip", 18f);

            var bubbleRoot = CreateDialoguePromptRoot(
                dialogueRoot.transform,
                "WorldBubbleRoot",
                new Vector2(0.5f, 0f),
                new Vector2(0f, 272f),
                new Vector2(420f, 60f),
                new Color(0.94f, 0.94f, 0.96f, 0.96f));
            DialoguePresenter.WorldBubbleRoot ??= bubbleRoot.gameObject;
            DialoguePresenter.WorldBubbleText ??= CreateCenteredOverlayText(bubbleRoot, "WorldBubbleText", "...", 18f);
            if (DialoguePresenter.WorldBubbleText != null)
                DialoguePresenter.WorldBubbleText.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            panelGo.SetActive(false);
            DialoguePresenter.DialogueRoot.SetActive(false);
            if (DialoguePresenter.DimRoot != null)
                DialoguePresenter.DimRoot.SetActive(false);
            if (DialoguePresenter.PortraitRoot != null)
                DialoguePresenter.PortraitRoot.SetActive(false);
            if (DialoguePresenter.AdvancePromptRoot != null)
                DialoguePresenter.AdvancePromptRoot.SetActive(false);
            if (DialoguePresenter.SkipPromptRoot != null)
                DialoguePresenter.SkipPromptRoot.SetActive(false);
            if (DialoguePresenter.WorldBubbleRoot != null)
                DialoguePresenter.WorldBubbleRoot.SetActive(false);
        }

        private static bool NeedsDialogueRebuild(Transform root, InWorldDialoguePresenter presenter)
        {
            return presenter == null
                || presenter.DialogueRoot == null
                || presenter.DimRoot == null
                || presenter.DimImage == null
                || presenter.DialoguePlateRoot == null
                || presenter.NameText == null
                || presenter.BodyText == null
                || presenter.PortraitRoot == null
                || presenter.PortraitImage == null
                || presenter.AdvancePromptRoot == null
                || presenter.AdvancePromptText == null
                || presenter.SkipPromptRoot == null
                || presenter.SkipPromptText == null
                || presenter.WorldBubbleRoot == null
                || presenter.WorldBubbleText == null
                || root.Find("DialogueRoot") == null
                || root.Find("DialogueRoot/PlateRoot") == null
                || root.Find("DialogueRoot/PortraitRoot") == null;
        }

        private void ResetDialogueReferences()
        {
            if (DialoguePresenter == null)
                return;

            DialoguePresenter.DialogueRoot = null;
            DialoguePresenter.DimRoot = null;
            DialoguePresenter.DimImage = null;
            DialoguePresenter.DialoguePlateRoot = null;
            DialoguePresenter.NameText = null;
            DialoguePresenter.BodyText = null;
            DialoguePresenter.PortraitRoot = null;
            DialoguePresenter.PortraitImage = null;
            DialoguePresenter.AdvancePromptRoot = null;
            DialoguePresenter.AdvancePromptText = null;
            DialoguePresenter.SkipPromptRoot = null;
            DialoguePresenter.SkipPromptText = null;
            DialoguePresenter.WorldBubbleRoot = null;
            DialoguePresenter.WorldBubbleText = null;
        }

        private static RectTransform CreateDialoguePlateRoot(Transform parent, string name)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(920f, 180f);

            var image = go.GetComponent<Image>();
            image.color = new Color(0.08f, 0.10f, 0.14f, 0.92f);
            image.raycastTarget = false;
            ApplyDefaultImageSprite(image);

            var layout = go.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 18, 18);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = go.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            return rect;
        }

        private static RectTransform CreateDialoguePromptRoot(
            Transform parent,
            string name,
            Vector2 pivotAnchor,
            Vector2 anchoredPosition,
            Vector2 size,
            Color backgroundColor)
        {
            var go = GetOrCreateChildGameObject(parent, name, typeof(Image));
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = pivotAnchor;
            rect.anchorMax = pivotAnchor;
            rect.pivot = pivotAnchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var image = go.GetComponent<Image>();
            image.color = backgroundColor;
            image.raycastTarget = false;
            ApplyDefaultImageSprite(image);
            return rect;
        }
    }
}
