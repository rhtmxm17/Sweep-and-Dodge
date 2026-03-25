using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    [CustomEditor(typeof(StageGridAuthoring))]
    public sealed class StageGridAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (StageGridAuthoring)target;
            if (authoring == null)
                return;

            EditorGUILayout.Space(6f);
            if (authoring.Grid == null || authoring.MovementTilemap == null || authoring.SourceRegionPaint == null || authoring.DepositRegionPaint == null)
            {
                EditorGUILayout.HelpBox("Grid, MovementTilemap, SourceRegionPaint, DepositRegionPaint must all be assigned.", MessageType.Warning);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Open Source Paint") && authoring.SourceRegionPaint != null)
                    StageRegionPaintEditorWindow.Open(authoring.SourceRegionPaint);
                if (GUILayout.Button("Open Deposit Paint") && authoring.DepositRegionPaint != null)
                    StageRegionPaintEditorWindow.Open(authoring.DepositRegionPaint);
            }

            if (GUILayout.Button("Sync Paint Asset Size From Grid"))
                SyncPaintAssets(authoring);

            if (GUILayout.Button("Validate Authoring Inputs"))
                ValidateAuthoring(authoring);
        }

        private static void SyncPaintAssets(StageGridAuthoring authoring)
        {
            if (authoring == null || authoring.MovementTilemap == null)
                return;

            var bounds = authoring.MovementTilemap.cellBounds;
            if (authoring.SourceRegionPaint != null)
            {
                Undo.RecordObject(authoring.SourceRegionPaint, "Resize Source Region Paint");
                authoring.SourceRegionPaint.Resize(bounds.size.x, bounds.size.y);
                EditorUtility.SetDirty(authoring.SourceRegionPaint);
            }

            if (authoring.DepositRegionPaint != null)
            {
                Undo.RecordObject(authoring.DepositRegionPaint, "Resize Deposit Region Paint");
                authoring.DepositRegionPaint.Resize(bounds.size.x, bounds.size.y);
                EditorUtility.SetDirty(authoring.DepositRegionPaint);
            }
        }

        private static void ValidateAuthoring(StageGridAuthoring authoring)
        {
            var issues = new List<ContentValidationIssue>();
            StageGridAuthoringValidationRules.Validate(authoring != null ? authoring.GetComponent<StageLayoutStageMarker>() : null, issues);
            for (int i = 0; i < issues.Count; i++)
            {
                string line = $"[StageGridAuthoring] {issues[i].Code} {issues[i].Location} - {issues[i].Message}";
                if (issues[i].Severity == ContentValidationSeverity.Error)
                    Debug.LogError(line);
                else
                    Debug.LogWarning(line);
            }

            if (issues.Count == 0)
                Debug.Log("[StageGridAuthoring] Validation passed.");
        }
    }
}
