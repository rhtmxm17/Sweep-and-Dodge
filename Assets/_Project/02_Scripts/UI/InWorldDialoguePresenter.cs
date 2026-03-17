using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    [DisallowMultipleComponent]
    public sealed class InWorldDialoguePresenter : MonoBehaviour
    {
        [Header("Runtime References")]
        public StagePresentationRuntimeController PresentationRuntime;
        public Canvas RootCanvas;
        public Camera ProjectionCamera;

        [Header("Visual Roots")]
        public GameObject DialogueRoot;
        public GameObject DimRoot;
        public Image DimImage;
        public RectTransform DialoguePlateRoot;
        public GameObject PortraitRoot;
        public Image PortraitImage;
        public GameObject AdvancePromptRoot;
        public TextMeshProUGUI AdvancePromptText;
        public GameObject SkipPromptRoot;
        public TextMeshProUGUI SkipPromptText;
        public GameObject WorldBubbleRoot;
        public TextMeshProUGUI WorldBubbleText;

        [Header("Texts")]
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI BodyText;

        private DemoShellDialogueBridge _bridge;

        public void Configure(
            DemoShellDialogueBridge bridge,
            StagePresentationRuntimeController presentationRuntime,
            Canvas rootCanvas)
        {
            _bridge = bridge;
            PresentationRuntime = presentationRuntime;
            RootCanvas = rootCanvas;
        }

        public void RefreshPresentation()
        {
            NormalizeRaycastTargets();

            var state = _bridge != null ? _bridge.CurrentPresentation : DialoguePresentationState.Hidden;
            if (!state.Visible)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);

            if (NameText != null)
                NameText.text = state.SpeakerDisplayName;
            if (BodyText != null)
                BodyText.text = state.BodyText;

            ApplyDim(state);
            ApplyPortrait(state);
            ApplyPrompts(state);
            ApplyWorldBubble(state);
        }

        private void SetVisible(bool visible)
        {
            if (DialogueRoot != null)
                DialogueRoot.SetActive(visible);
            if (!visible)
            {
                if (DimRoot != null)
                    DimRoot.SetActive(false);
                if (PortraitRoot != null)
                    PortraitRoot.SetActive(false);
                if (AdvancePromptRoot != null)
                    AdvancePromptRoot.SetActive(false);
                if (SkipPromptRoot != null)
                    SkipPromptRoot.SetActive(false);
                if (WorldBubbleRoot != null)
                    WorldBubbleRoot.SetActive(false);
            }
        }

        private void ApplyDim(in DialoguePresentationState state)
        {
            bool showDim = state.BlockingMode == InWorldDialogueBlockingMode.GateClear;
            if (DimRoot != null)
                DimRoot.SetActive(showDim);
            if (DimImage != null)
                DimImage.color = new Color(0f, 0f, 0f, showDim ? 0.42f : 0f);
        }

        private void ApplyPortrait(in DialoguePresentationState state)
        {
            bool showPortrait = PortraitRoot != null && PortraitImage != null && state.SpeakerPortrait != null;
            if (PortraitRoot != null)
                PortraitRoot.SetActive(showPortrait);
            if (!showPortrait)
                return;

            PortraitImage.sprite = state.SpeakerPortrait;
            var side = ResolvePortraitSide(state);
            ApplyPortraitPlacement(side);
        }

        private DialoguePortraitSide ResolvePortraitSide(in DialoguePresentationState state)
        {
            if (state.Anchor.Kind == InWorldDialogueAnchorKind.ScreenAnchor)
            {
                if (state.Anchor.ScreenAnchor == InWorldDialogueScreenAnchorId.LeftActor)
                    return DialoguePortraitSide.Left;
                if (state.Anchor.ScreenAnchor == InWorldDialogueScreenAnchorId.RightActor)
                    return DialoguePortraitSide.Right;
            }

            return state.PortraitSide == DialoguePortraitSide.Right
                ? DialoguePortraitSide.Right
                : DialoguePortraitSide.Left;
        }

        private void ApplyPortraitPlacement(DialoguePortraitSide side)
        {
            if (PortraitRoot == null)
                return;

            var rect = PortraitRoot.GetComponent<RectTransform>();
            if (rect == null)
                return;

            bool right = side == DialoguePortraitSide.Right;
            rect.anchorMin = new Vector2(right ? 1f : 0f, 0f);
            rect.anchorMax = new Vector2(right ? 1f : 0f, 0f);
            rect.pivot = new Vector2(right ? 1f : 0f, 0f);
            rect.anchoredPosition = new Vector2(right ? -72f : 72f, 72f);
        }

        private void ApplyPrompts(in DialoguePresentationState state)
        {
            if (AdvancePromptRoot != null)
                AdvancePromptRoot.SetActive(state.CanAdvance);
            if (SkipPromptRoot != null)
                SkipPromptRoot.SetActive(state.CanSkip);
            if (AdvancePromptText != null)
                AdvancePromptText.text = "Next";
            if (SkipPromptText != null)
                SkipPromptText.text = "Skip";
        }

        private void ApplyWorldBubble(in DialoguePresentationState state)
        {
            bool showBubble;
            if (state.Anchor.Kind == InWorldDialogueAnchorKind.StagePresentationStableId)
            {
                showBubble = TryProjectPresentationAnchor(state.Anchor.StagePresentationStableId, out var anchoredPosition);
                if (showBubble)
                    ApplyWorldBubblePlacement(anchoredPosition);
            }
            else
            {
                showBubble = state.Anchor.Kind == InWorldDialogueAnchorKind.ScreenAnchor
                    && (state.Anchor.ScreenAnchor == InWorldDialogueScreenAnchorId.Center
                        || state.Anchor.ScreenAnchor == InWorldDialogueScreenAnchorId.LowerCenter);
                if (showBubble)
                    ApplyDecorativeBubblePlacement(state.Anchor.ScreenAnchor);
            }

            if (WorldBubbleRoot != null)
                WorldBubbleRoot.SetActive(showBubble);
            if (WorldBubbleText != null)
                WorldBubbleText.text = showBubble ? state.BodyText : string.Empty;
        }

        private bool TryProjectPresentationAnchor(uint stableId, out Vector2 anchoredPosition)
        {
            anchoredPosition = default;
            if (stableId == 0
                || PresentationRuntime == null
                || RootCanvas == null
                || !PresentationRuntime.TryGetPresentationAnchor(stableId, out var anchor)
                || anchor == null)
            {
                return false;
            }

            var projectionCamera = ResolveProjectionCamera();
            if (projectionCamera == null)
                return false;

            Vector3 viewport = projectionCamera.WorldToViewportPoint(anchor.position);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return false;

            var canvasRect = RootCanvas.transform as RectTransform;
            if (canvasRect == null)
                return false;

            Vector3 screenPoint = projectionCamera.WorldToScreenPoint(anchor.position);
            Camera canvasCamera = RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : RootCanvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, canvasCamera, out anchoredPosition))
                return false;

            return true;
        }

        private Camera ResolveProjectionCamera()
        {
            if (ProjectionCamera != null)
                return ProjectionCamera;

            if (Camera.main != null)
                return Camera.main;

#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<Camera>();
#else
            return Object.FindObjectOfType<Camera>();
#endif
        }

        private void ApplyWorldBubblePlacement(Vector2 anchoredPosition)
        {
            if (WorldBubbleRoot == null)
                return;

            var rect = WorldBubbleRoot.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = anchoredPosition;
        }

        private void ApplyDecorativeBubblePlacement(InWorldDialogueScreenAnchorId screenAnchor)
        {
            if (WorldBubbleRoot == null)
                return;

            var rect = WorldBubbleRoot.GetComponent<RectTransform>();
            if (rect == null)
                return;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = screenAnchor == InWorldDialogueScreenAnchorId.Center
                ? new Vector2(0f, 300f)
                : new Vector2(0f, 272f);
        }

        private void NormalizeRaycastTargets()
        {
            SetRaycastTarget(DimImage, false);
            SetRaycastTarget(PortraitImage, false);
            SetRaycastTarget(NameText, false);
            SetRaycastTarget(BodyText, false);
            SetRaycastTarget(AdvancePromptText, false);
            SetRaycastTarget(SkipPromptText, false);
            SetRaycastTarget(WorldBubbleText, false);
        }

        private static void SetRaycastTarget(Graphic graphic, bool enabled)
        {
            if (graphic != null)
                graphic.raycastTarget = enabled;
        }
    }
}
