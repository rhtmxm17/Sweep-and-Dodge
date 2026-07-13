using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

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
            if (authoring.Grid == null || authoring.MovementTilemap == null || authoring.RegionTilemap == null)
                EditorGUILayout.HelpBox("Grid, MovementTilemap, and RegionTilemap must be assigned.", MessageType.Warning);

            EditorGUILayout.HelpBox("RegionTilemap is the only region metadata workflow. Source/Deposit are distinguished by StageRegionTile.RegionKind and resolved through slot mappings.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fit Bounds From Used Tiles"))
                    FitBoundsFromUsedTiles(authoring);
                if (GUILayout.Button("Frame Bounds To Anchors And Used Tiles"))
                    FrameBounds(authoring);
            }

            if (GUILayout.Button("Validate Authoring Inputs"))
                ValidateAuthoring(authoring);
        }

        private static void FitBoundsFromUsedTiles(StageGridAuthoring authoring)
        {
            if (authoring == null)
                return;

            bool hasAny = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            IncludeTilemapBounds(authoring.MovementTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.RegionTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.GroundVisualTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.WallVisualTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            if (!hasAny)
                return;

            Undo.RecordObject(authoring, "Fit Stage Authoring Bounds From Used Tiles");
            authoring.BoundsMinCell = new Vector2Int(minX, minY);
            authoring.BoundsSize = new Vector2Int((maxX - minX) + 1, (maxY - minY) + 1);
            EditorUtility.SetDirty(authoring);
        }

        private static void FrameBounds(StageGridAuthoring authoring)
        {
            if (authoring == null)
                return;

            bool hasAny = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            IncludeTilemapBounds(authoring.MovementTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.RegionTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.GroundVisualTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeTilemapBounds(authoring.WallVisualTilemap, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);

            var stageNode = authoring.GetComponent<StageLayoutStageMarker>();
            var anchors = stageNode != null
                ? stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                IncludeCell(anchor.AnchorCell.x, anchor.AnchorCell.y, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            }

            var playerStarts = stageNode != null
                ? stageNode.GetComponentsInChildren<StagePlayerStartMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StagePlayerStartMarker>(includeInactive: true);
            for (int i = 0; i < playerStarts.Length; i++)
            {
                var marker = playerStarts[i];
                if (marker == null)
                    continue;

                IncludeCell(marker.AnchorCell.x, marker.AnchorCell.y, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            }

            if (!hasAny)
                return;

            Undo.RecordObject(authoring, "Frame Stage Authoring Bounds");
            authoring.BoundsMinCell = new Vector2Int(minX, minY);
            authoring.BoundsSize = new Vector2Int((maxX - minX) + 1, (maxY - minY) + 1);
            EditorUtility.SetDirty(authoring);
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

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        private static void DrawGizmos(StageGridAuthoring authoring, GizmoType gizmoType)
        {
            if (Application.isPlaying)
                return;

            if (authoring == null || authoring.Grid == null)
                return;

            StageGridSceneVisualizationRenderer.Draw(authoring);

            if (authoring.ShowAnchorGizmo)
                DrawAnchors(authoring, authoring.Grid.cellSize.x, authoring.Grid.cellSize.y, 0f);
        }

        private static void DrawAnchors(StageGridAuthoring authoring, float cellWidth, float cellHeight, float z)
        {
            var stageNode = authoring.GetComponent<StageLayoutStageMarker>();
            var anchors = stageNode != null
                ? stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                int tileX = anchor.AnchorCell.x;
                int tileY = anchor.AnchorCell.y;
                var pos = new Vector3(
                    (tileX + anchor.AnchorOffset.x + 0.5f) * cellWidth,
                    (tileY + anchor.AnchorOffset.y + 0.5f) * cellHeight,
                    z - 0.004f);
                Handles.color = anchor.RegionKind == StageRegionKind.Source
                    ? new Color(0.1f, 0.85f, 1f, 1f)
                    : new Color(1f, 0.75f, 0.1f, 1f);
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f * Mathf.Min(cellWidth, cellHeight));
                string label = authoring.TryResolveStableId(anchor.RegionKind, anchor.RegionSlotIndex, out uint stableId)
                    ? $"{anchor.RegionKind}:slot{anchor.RegionSlotIndex}->{stableId}"
                    : $"{anchor.RegionKind}:slot{anchor.RegionSlotIndex}";
                Handles.Label(pos, label);
            }
        }

        private static void IncludeTilemapBounds(Tilemap tilemap, ref bool hasAny, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (!TryComputeUsedTileBounds(tilemap, out var bounds))
                return;

            IncludeBounds(bounds, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
        }

        private static bool TryComputeUsedTileBounds(Tilemap tilemap, out BoundsInt bounds)
        {
            bounds = default;
            if (tilemap == null)
                return false;

            bool hasAny = false;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            var cellBounds = tilemap.cellBounds;
            foreach (var cell in cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) == null)
                    continue;

                IncludeCell(cell.x, cell.y, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            }

            if (!hasAny)
                return false;

            bounds = new BoundsInt(minX, minY, 0, (maxX - minX) + 1, (maxY - minY) + 1, 1);
            return true;
        }

        private static void IncludeBounds(BoundsInt bounds, ref bool hasAny, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            IncludeCell(bounds.xMin, bounds.yMin, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludeCell(bounds.xMax - 1, bounds.yMax - 1, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
        }

        private static void IncludeCell(int x, int y, ref bool hasAny, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (!hasAny)
            {
                hasAny = true;
                minX = maxX = x;
                minY = maxY = y;
                return;
            }

            minX = Mathf.Min(minX, x);
            minY = Mathf.Min(minY, y);
            maxX = Mathf.Max(maxX, x);
            maxY = Mathf.Max(maxY, y);
        }

    }
}
