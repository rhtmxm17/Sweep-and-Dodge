using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StagePresentationMarker))]
    internal sealed class StagePresentationMarkerEditor : UnityEditor.Editor
    {
        private static readonly GUIContent MissingCatalogContent = new GUIContent("Presentation Catalog is not assigned on the nearest StageLayoutRootMarker.");
        private static readonly GUIContent MissingKeyContent = new GUIContent("PresentationKey is empty.");
        private static readonly GUIContent MissingEntryContent = new GUIContent("PresentationKey is not present in the resolved StagePresentationCatalogSO.");
        private static readonly GUIContent SameGoContent = new GUIContent("StagePresentationMarker must not share a GameObject with Source/Deposit anchor marker.");
        private static readonly GUIContent LinkedParentContent = new GUIContent("LinkedToParent presentation requires a parent Source/Deposit anchor marker.");
        private static readonly GUIContent StandaloneParentContent = new GUIContent("Standalone presentation must not be authored under a topology marker parent.");

        private SerializedProperty _stableId;
        private SerializedProperty _active;
        private SerializedProperty _placementMode;
        private SerializedProperty _presentationKey;
        private SerializedProperty _drawGizmo;

        private void OnEnable()
        {
            _stableId = serializedObject.FindProperty("StableId");
            _active = serializedObject.FindProperty("Active");
            _placementMode = serializedObject.FindProperty("PlacementMode");
            _presentationKey = serializedObject.FindProperty("PresentationKey");
            _drawGizmo = serializedObject.FindProperty("DrawGizmo");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_stableId);
            EditorGUILayout.PropertyField(_active);
            EditorGUILayout.PropertyField(_placementMode);
            DrawPresentationKeyField();
            EditorGUILayout.PropertyField(_drawGizmo);

            DrawResolvedLinkInfo();
            DrawValidationHelpBoxes();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawPresentationKeyField()
        {
            var marker = (StagePresentationMarker)target;
            if (StagePresentationEditorUtility.TryResolveCatalog(marker, out var catalog))
            {
                var keys = StagePresentationEditorUtility.GetPresentationKeys(catalog);
                if (keys.Length > 0)
                {
                    string currentKey = _presentationKey.stringValue?.Trim() ?? string.Empty;
                    int currentIndex = string.IsNullOrEmpty(currentKey) ? 0 : System.Array.IndexOf(keys, currentKey) + 1;
                    if (currentIndex >= 0)
                    {
                        string[] options = new string[keys.Length + 1];
                        options[0] = "<None>";
                        for (int i = 0; i < keys.Length; i++)
                            options[i + 1] = keys[i];

                        int nextIndex = EditorGUILayout.Popup("PresentationKey", currentIndex, options);
                        _presentationKey.stringValue = nextIndex <= 0 ? string.Empty : keys[nextIndex - 1];
                        return;
                    }
                }
            }

            EditorGUILayout.PropertyField(_presentationKey);
        }

        private void DrawResolvedLinkInfo()
        {
            var marker = (StagePresentationMarker)target;
            if (marker.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                return;

            if (StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out var kind, out var linkedStableId, out var linkedParent))
            {
                EditorGUILayout.Space(4f);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.EnumPopup("Resolved LinkKind", kind);
                    EditorGUILayout.LongField("Resolved StableId", linkedStableId);
                    EditorGUILayout.ObjectField("Resolved Parent", linkedParent != null ? linkedParent.gameObject : null, typeof(GameObject), true);
                }
            }
        }

        private void DrawValidationHelpBoxes()
        {
            var marker = (StagePresentationMarker)target;

            if (!StagePresentationEditorUtility.TryResolveCatalog(marker, out _))
                EditorGUILayout.HelpBox(MissingCatalogContent.text, MessageType.Warning);

            if (string.IsNullOrWhiteSpace(marker.PresentationKey))
            {
                EditorGUILayout.HelpBox(MissingKeyContent.text, MessageType.Warning);
            }
            else if (StagePresentationEditorUtility.TryResolveCatalog(marker, out var catalog)
                && !StagePresentationEditorUtility.TryResolveEntry(catalog, marker.PresentationKey, out _))
            {
                EditorGUILayout.HelpBox(MissingEntryContent.text, MessageType.Warning);
            }

            if (StagePresentationEditorUtility.HasTopologyOnSelf(marker))
                EditorGUILayout.HelpBox(SameGoContent.text, MessageType.Error);

            bool hasLinkedParent = StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out _, out _, out _);
            if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && !hasLinkedParent)
                EditorGUILayout.HelpBox(LinkedParentContent.text, MessageType.Error);
            if (marker.PlacementMode == StagePresentationPlacementMode.Standalone && hasLinkedParent)
                EditorGUILayout.HelpBox(StandaloneParentContent.text, MessageType.Error);
        }

        private void OnSceneGUI()
        {
            var marker = (StagePresentationMarker)target;
            if (marker == null || !marker.DrawGizmo)
                return;

            DrawLinkedParentShape(marker);
            DrawLinkedLine(marker);
            DrawWarnings(marker);
        }

        private static void DrawLinkedParentShape(StagePresentationMarker marker)
        {
            if (marker == null || marker.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                return;
            if (!StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out var kind, out _, out var linkedParent) || linkedParent == null)
                return;

            Color color = kind switch
            {
                StagePresentationLinkKind.Source => new Color(0.15f, 0.9f, 0.35f, 1f),
                StagePresentationLinkKind.Deposit => new Color(0.2f, 0.7f, 1f, 1f),
                StagePresentationLinkKind.Obstacle => new Color(1f, 0.55f, 0.2f, 1f),
                _ => Color.white,
            };

            using (new Handles.DrawingScope(color))
            {
                if (linkedParent.TryGetComponent<StageRegionAnchorMarker>(out _))
                {
                    Handles.DrawWireDisc(linkedParent.position, Vector3.up, 0.35f);
                    return;
                }

                if (linkedParent.TryGetComponent<StageSourceMarker>(out var source))
                {
                    DrawShape(linkedParent, source.Shape, source.Radius, source.Size);
                    return;
                }

                if (linkedParent.TryGetComponent<StageDepositMarker>(out var deposit))
                {
                    DrawShape(linkedParent, deposit.Shape, deposit.Radius, deposit.Size);
                    return;
                }

                if (linkedParent.TryGetComponent<StageObstacleMarker>(out var obstacle))
                    DrawShape(linkedParent, obstacle.Shape, obstacle.Radius, obstacle.Size);
            }
        }

        private static void DrawLinkedLine(StagePresentationMarker marker)
        {
            if (marker == null || marker.PlacementMode != StagePresentationPlacementMode.LinkedToParent)
                return;
            if (!StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out var kind, out _, out var linkedParent) || linkedParent == null)
                return;

            Color color = kind switch
            {
                StagePresentationLinkKind.Source => new Color(0.15f, 0.9f, 0.35f, 1f),
                StagePresentationLinkKind.Deposit => new Color(0.95f, 0.85f, 0.2f, 1f),
                StagePresentationLinkKind.Obstacle => new Color(1f, 0.55f, 0.2f, 1f),
                _ => Color.white,
            };

            Handles.color = color;
            Handles.DrawDottedLine(linkedParent.position, marker.transform.position, 4f);
        }

        private static void DrawWarnings(StagePresentationMarker marker)
        {
            if (marker == null)
                return;

            if (StagePresentationEditorUtility.HasTopologyOnSelf(marker))
            {
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "Invalid: same GO as topology marker");
                return;
            }

            bool hasLinkedParent = StagePresentationEditorUtility.TryFindLinkedParent(marker.transform, out _, out _, out _);
            if (marker.PlacementMode == StagePresentationPlacementMode.LinkedToParent && !hasLinkedParent)
            {
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "Missing linked parent");
                return;
            }

            if (marker.PlacementMode == StagePresentationPlacementMode.Standalone && hasLinkedParent)
            {
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "Standalone under topology parent");
                return;
            }

            if (!StagePresentationEditorUtility.TryResolveCatalog(marker, out var catalog))
            {
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "Missing presentation catalog");
                return;
            }

            if (string.IsNullOrWhiteSpace(marker.PresentationKey))
            {
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "Missing PresentationKey");
                return;
            }

            if (!StagePresentationEditorUtility.TryResolveEntry(catalog, marker.PresentationKey, out _))
                Handles.Label(marker.transform.position + Vector3.up * 0.5f, "PresentationKey not found");
        }

        private static void DrawShape(Transform transform, Shape2DKind shape, float radius, Vector2 size)
        {
            if (shape == Shape2DKind.Circle)
            {
                Handles.DrawWireDisc(transform.position, Vector3.up, Mathf.Max(0f, radius));
                return;
            }

            var previousMatrix = Handles.matrix;
            Handles.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Handles.DrawWireCube(Vector3.zero, new Vector3(Mathf.Max(0f, size.x), 0f, Mathf.Max(0f, size.y)));
            Handles.matrix = previousMatrix;
        }
    }
}
