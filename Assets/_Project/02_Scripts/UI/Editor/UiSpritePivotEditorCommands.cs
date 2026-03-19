using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SweepNDodge.DotsBullets.Editor
{
    public static class UiSpritePivotEditorCommands
    {
        [MenuItem("Tools/Project/UI/Apply Sprite Pivot To Selected Images")]
        private static void ApplySpritePivotToSelectedImages()
        {
            var images = CollectSelectedImages();
            if (images.Count <= 0)
                return;

            int appliedCount = 0;
            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                if (image == null || image.sprite == null)
                    continue;

                Undo.RecordObject(image.rectTransform, "Apply Sprite Pivot To RectTransform");
                if (!UiSpritePivotUtility.TryApplySpritePivot(image))
                    continue;

                EditorUtility.SetDirty(image.rectTransform);
                appliedCount++;
            }

            if (appliedCount > 0)
                MarkSelectionDirty(images);

            Debug.Log($"Applied sprite pivot to {appliedCount} UI image(s).");
        }

        [MenuItem("Tools/Project/UI/Apply Sprite Pivot To Selected Images", true)]
        private static bool CanApplySpritePivotToSelectedImages()
        {
            var images = Selection.GetFiltered<Image>(SelectionMode.TopLevel | SelectionMode.Editable);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].sprite != null)
                    return true;
            }

            return false;
        }

        [MenuItem("CONTEXT/Image/Apply Sprite Pivot To RectTransform")]
        private static void ApplySpritePivotFromContext(MenuCommand command)
        {
            if (command.context is not Image image || image.sprite == null)
                return;

            Undo.RecordObject(image.rectTransform, "Apply Sprite Pivot To RectTransform");
            if (!UiSpritePivotUtility.TryApplySpritePivot(image))
                return;

            EditorUtility.SetDirty(image.rectTransform);
            MarkSelectionDirty(new List<Image> { image });
        }

        private static List<Image> CollectSelectedImages()
        {
            var selected = Selection.GetFiltered<Image>(SelectionMode.TopLevel | SelectionMode.Editable);
            var result = new List<Image>(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                if (selected[i] == null)
                    continue;

                result.Add(selected[i]);
            }

            return result;
        }

        private static void MarkSelectionDirty(List<Image> images)
        {
            for (int i = 0; i < images.Count; i++)
            {
                var image = images[i];
                if (image == null)
                    continue;

                var gameObject = image.gameObject;
                if (gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
        }
    }
}
