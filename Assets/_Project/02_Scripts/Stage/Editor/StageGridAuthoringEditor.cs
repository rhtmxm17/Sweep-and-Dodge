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

            var bounds = authoring.GetAuthoringBounds();
            if (bounds.size.x <= 0 || bounds.size.y <= 0)
                return;

            using var drawingScope = new Handles.DrawingScope(Matrix4x4.TRS(authoring.Grid.transform.position, authoring.Grid.transform.rotation, Vector3.one));
            float cellWidth = authoring.Grid.cellSize.x;
            float cellHeight = authoring.Grid.cellSize.y;
            float z = 0f;

            if (authoring.ShowGridGizmo)
            {
                Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                DrawBoundsOutline(bounds, cellWidth, cellHeight, z);
                for (int x = 0; x <= bounds.size.x; x++)
                {
                    float lineX = (bounds.xMin + x) * cellWidth;
                    Handles.DrawLine(new Vector3(lineX, bounds.yMin * cellHeight, z), new Vector3(lineX, (bounds.yMin + bounds.size.y) * cellHeight, z));
                }

                for (int y = 0; y <= bounds.size.y; y++)
                {
                    float lineY = (bounds.yMin + y) * cellHeight;
                    Handles.DrawLine(new Vector3(bounds.xMin * cellWidth, lineY, z), new Vector3((bounds.xMin + bounds.size.x) * cellWidth, lineY, z));
                }
            }

            for (int localY = 0; localY < bounds.size.y; localY++)
            {
                for (int localX = 0; localX < bounds.size.x; localX++)
                {
                    int tileX = bounds.xMin + localX;
                    int tileY = bounds.yMin + localY;
                    DrawCellOverlays(authoring, tileX, tileY, localX, localY, cellWidth, cellHeight, z);
                }
            }

            if (authoring.ShowAnchorGizmo)
                DrawAnchors(authoring, cellWidth, cellHeight, z);
        }

        private static void DrawCellOverlays(StageGridAuthoring authoring, int tileX, int tileY, int localX, int localY, float cellWidth, float cellHeight, float z)
        {
            Rect rect = new Rect(tileX * cellWidth, tileY * cellHeight, cellWidth, cellHeight);

            if (authoring.ShowMovementGizmo && authoring.MovementTilemap != null)
            {
                var tile = authoring.MovementTilemap.GetTile(new Vector3Int(tileX, tileY, 0)) as StageMovementTile;
                if (tile != null && tile.MovementFlags != StageCellMovementFlags.None)
                {
                    DrawCellHatch(rect, z - 0.003f, ResolveMovementColor(tile.MovementFlags), StageGridHatchDirection.ForwardSlash);
                }
            }

            uint sourceCell = ResolveRegionCell(authoring, StageRegionKind.Source, localX, localY);
            if (authoring.ShowSourceGizmo && sourceCell != 0u)
            {
                DrawCellHatch(rect, z - 0.002f, new Color(0.1f, 0.75f, 1f, 0.55f), StageGridHatchDirection.BackSlash);
            }

            uint depositCell = ResolveRegionCell(authoring, StageRegionKind.Deposit, localX, localY);
            if (authoring.ShowDepositGizmo && depositCell != 0u)
            {
                DrawCellHatch(rect, z - 0.001f, new Color(1f, 0.7f, 0.1f, 0.55f), StageGridHatchDirection.BackSlash);
            }
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

        private static uint ResolveRegionCell(StageGridAuthoring authoring, StageRegionKind kind, int localX, int localY)
        {
            if (authoring == null)
                return 0u;

            var tile = authoring.RegionTilemap != null
                ? authoring.RegionTilemap.GetTile(authoring.GetTilemapCell(localX, localY)) as StageRegionTile
                : null;
            if (tile == null || tile.RegionKind != kind || tile.RegionSlotIndex <= 0)
                return 0u;

            return authoring.TryResolveStableId(kind, tile.RegionSlotIndex, out uint stableId) ? stableId : 0u;
        }

        private static Vector3[] BuildRectVerts(Rect rect, float z)
        {
            return new[]
            {
                new Vector3(rect.xMin, rect.yMin, z),
                new Vector3(rect.xMax, rect.yMin, z),
                new Vector3(rect.xMax, rect.yMax, z),
                new Vector3(rect.xMin, rect.yMax, z),
            };
        }

        private static void DrawBoundsOutline(BoundsInt bounds, float cellWidth, float cellHeight, float z)
        {
            var rect = new Rect(bounds.xMin * cellWidth, bounds.yMin * cellHeight, bounds.size.x * cellWidth, bounds.size.y * cellHeight);
            Handles.DrawPolyLine(BuildRectVerts(rect, z));
            Handles.DrawLine(new Vector3(rect.xMin, rect.yMax, z), new Vector3(rect.xMin, rect.yMin, z));
        }

        private static void DrawCellHatch(Rect rect, float z, Color color, StageGridHatchDirection direction)
        {
            Handles.color = color;
            float insetX = rect.width * 0.08f;
            float insetY = rect.height * 0.08f;
            float minX = rect.xMin + insetX;
            float maxX = rect.xMax - insetX;
            float minY = rect.yMin + insetY;
            float maxY = rect.yMax - insetY;
            const int lineCount = 2;

            for (int i = 0; i < lineCount; i++)
            {
                float t = (i + 0.5f) / lineCount;
                if (direction == StageGridHatchDirection.ForwardSlash)
                {
                    Handles.DrawLine(
                        new Vector3(maxX, Mathf.Lerp(minY, maxY, t), z),
                        new Vector3(Mathf.Lerp(minX, maxX, t), maxY, z));
                }
                else
                {
                    Handles.DrawLine(
                        new Vector3(minX, Mathf.Lerp(maxY, minY, t), z),
                        new Vector3(Mathf.Lerp(minX, maxX, t), maxY, z));
                }
            }
        }

        private static Color ResolveMovementColor(StageCellMovementFlags flags)
        {
            bool blockPlayer = (flags & StageCellMovementFlags.BlockPlayer) != 0;
            bool blockBullet = (flags & StageCellMovementFlags.BlockBullet) != 0;
            if (blockPlayer && blockBullet)
                return new Color(0.55f, 0.05f, 0.05f, 0.26f);
            if (blockPlayer)
                return new Color(0.95f, 0.55f, 0.15f, 0.22f);
            if (blockBullet)
                return new Color(0.9f, 0.1f, 0.85f, 0.22f);
            return Color.clear;
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

        private enum StageGridHatchDirection : byte
        {
            ForwardSlash = 0,
            BackSlash = 1,
        }
    }
}
