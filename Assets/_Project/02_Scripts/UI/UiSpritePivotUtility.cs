using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets
{
    /// <summary>
    /// Copies a sprite pivot into a RectTransform pivot for UI Images.
    /// Optionally compensates world position so the visible rect stays in place.
    /// </summary>
    public static class UiSpritePivotUtility
    {
        public static bool TryGetNormalizedSpritePivot(Sprite sprite, out Vector2 normalizedPivot)
        {
            normalizedPivot = new Vector2(0.5f, 0.5f);
            if (sprite == null)
                return false;

            Rect rect = sprite.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return false;

            normalizedPivot = new Vector2(
                Mathf.Clamp01(sprite.pivot.x / rect.width),
                Mathf.Clamp01(sprite.pivot.y / rect.height));
            return true;
        }

        public static bool TryApplySpritePivot(Image image, bool preserveWorldRect = true)
        {
            if (image == null || image.sprite == null)
                return false;

            return TryApplySpritePivot(image.rectTransform, image.sprite, preserveWorldRect);
        }

        public static bool TryApplySpritePivot(RectTransform rectTransform, Sprite sprite, bool preserveWorldRect = true)
        {
            if (rectTransform == null || !TryGetNormalizedSpritePivot(sprite, out var normalizedPivot))
                return false;

            ApplyPivot(rectTransform, normalizedPivot, preserveWorldRect);
            return true;
        }

        public static void ApplyPivot(RectTransform rectTransform, Vector2 normalizedPivot, bool preserveWorldRect = true)
        {
            if (rectTransform == null)
                return;

            var oldPivot = rectTransform.pivot;
            if ((oldPivot - normalizedPivot).sqrMagnitude <= 1e-8f)
                return;

            Vector3 worldCompensation = Vector3.zero;
            if (preserveWorldRect)
            {
                Vector2 pivotDelta = normalizedPivot - oldPivot;
                Vector3 localCompensation = new Vector3(
                    pivotDelta.x * rectTransform.rect.width,
                    pivotDelta.y * rectTransform.rect.height,
                    0f);
                worldCompensation = rectTransform.TransformVector(localCompensation);
            }

            rectTransform.pivot = normalizedPivot;

            if (preserveWorldRect)
                rectTransform.position += worldCompensation;
        }
    }
}
