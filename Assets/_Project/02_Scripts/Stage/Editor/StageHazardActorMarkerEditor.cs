using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageHazardActorMarker))]
    public sealed class StageHazardActorMarkerEditor : UnityEditor.Editor
    {
        private SerializedProperty _placementInstanceIdProperty;
        private SerializedProperty _actorArchetypePrefabProperty;
        private SerializedProperty _localYawDegProperty;

        private void OnEnable()
        {
            _placementInstanceIdProperty = serializedObject.FindProperty(nameof(StageHazardActorMarker.PlacementInstanceId));
            _actorArchetypePrefabProperty = serializedObject.FindProperty(nameof(StageHazardActorMarker.ActorArchetypePrefab));
            _localYawDegProperty = serializedObject.FindProperty(nameof(StageHazardActorMarker.LocalYawDeg));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(_placementInstanceIdProperty);
            EditorGUILayout.PropertyField(_actorArchetypePrefabProperty);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_localYawDegProperty, new GUIContent("Local Yaw Deg"));
            bool yawChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();

            var marker = (StageHazardActorMarker)target;
            if (yawChanged)
                StageHazardActorPlacementEditorUtility.ApplyCachedYawToTransform(marker, recordUndo: true);

            DrawPosePreview(marker);
            DrawValidation(marker);
        }

        private void OnSceneGUI()
        {
            var marker = (StageHazardActorMarker)target;
            if (StageHazardActorPlacementEditorUtility.SyncCachedYawFromTransform(marker, recordUndo: true))
                Repaint();
        }

        private static void DrawPosePreview(StageHazardActorMarker marker)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Generated Source-Local Pose", EditorStyles.boldLabel);
            if (!StageHazardActorPlacementEditorUtility.TryGetLocalPose(
                    marker,
                    out _,
                    out Vector3 localOffset,
                    out float localYawDeg))
            {
                EditorGUILayout.HelpBox("Owning SourceRuntimeTemplateAuthoringBase was not found.", MessageType.Error);
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Vector3Field("Local Offset", localOffset);
                EditorGUILayout.FloatField("Transform Local Yaw", localYawDeg);
            }
        }

        private static void DrawValidation(StageHazardActorMarker marker)
        {
            var errors = StageHazardActorPlacementEditorUtility.CollectValidationErrors(marker);
            for (int i = 0; i < errors.Count; i++)
                EditorGUILayout.HelpBox(errors[i], MessageType.Error);
        }
    }
}
