using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets.Tests
{
    public class UiSpritePivotUtilityTests
    {
        [Test]
        public void TryGetNormalizedSpritePivot_ReturnsExpectedNormalizedValue()
        {
            var texture = new Texture2D(200, 100);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 200f, 100f), new Vector2(0.25f, 0.75f));

            try
            {
                Assert.That(UiSpritePivotUtility.TryGetNormalizedSpritePivot(sprite, out var normalizedPivot), Is.True);
                Assert.That(normalizedPivot.x, Is.EqualTo(0.25f).Within(0.0001f));
                Assert.That(normalizedPivot.y, Is.EqualTo(0.75f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TryApplySpritePivot_PreservesWorldCorners_WhenRequested()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var child = new GameObject("Child", typeof(RectTransform), typeof(Image));
            var texture = new Texture2D(200, 100);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 200f, 100f), new Vector2(0f, 0f));

            try
            {
                child.transform.SetParent(root.transform, false);

                var rect = child.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(120f, 80f);
                rect.sizeDelta = new Vector2(200f, 100f);

                var image = child.GetComponent<Image>();
                image.sprite = sprite;

                var before = new Vector3[4];
                var after = new Vector3[4];
                rect.GetWorldCorners(before);

                Assert.That(UiSpritePivotUtility.TryApplySpritePivot(image, preserveWorldRect: true), Is.True);

                rect.GetWorldCorners(after);
                Assert.That(rect.pivot, Is.EqualTo(Vector2.zero));
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(after[i].x, Is.EqualTo(before[i].x).Within(0.0001f));
                    Assert.That(after[i].y, Is.EqualTo(before[i].y).Within(0.0001f));
                    Assert.That(after[i].z, Is.EqualTo(before[i].z).Within(0.0001f));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void TryApplySpritePivot_WithoutPreserveWorldRect_ChangesPivotOnly()
        {
            var root = new GameObject("Root", typeof(RectTransform));
            var child = new GameObject("Child", typeof(RectTransform), typeof(Image));
            var texture = new Texture2D(200, 100);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 200f, 100f), new Vector2(0f, 0f));

            try
            {
                child.transform.SetParent(root.transform, false);

                var rect = child.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(120f, 80f);
                rect.sizeDelta = new Vector2(200f, 100f);

                Vector3 beforePosition = rect.position;
                var image = child.GetComponent<Image>();
                image.sprite = sprite;

                Assert.That(UiSpritePivotUtility.TryApplySpritePivot(image, preserveWorldRect: false), Is.True);
                Assert.That(rect.pivot, Is.EqualTo(Vector2.zero));
                Assert.That(rect.position, Is.EqualTo(beforePosition));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
            }
        }
    }
}
