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

            if (GUILayout.Button("Sync Paint Asset Size From Authoring Bounds"))
                SyncPaintAssets(authoring);
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

        private static void SyncPaintAssets(StageGridAuthoring authoring)
        {
            if (authoring == null)
                return;

            int width = Mathf.Max(1, authoring.BoundsSize.x);
            int height = Mathf.Max(1, authoring.BoundsSize.y);
            if (authoring.SourceRegionPaint != null)
            {
                Undo.RecordObject(authoring.SourceRegionPaint, "Resize Source Region Paint");
                authoring.SourceRegionPaint.Resize(width, height);
                EditorUtility.SetDirty(authoring.SourceRegionPaint);
            }

            if (authoring.DepositRegionPaint != null)
            {
                Undo.RecordObject(authoring.DepositRegionPaint, "Resize Deposit Region Paint");
                authoring.DepositRegionPaint.Resize(width, height);
                EditorUtility.SetDirty(authoring.DepositRegionPaint);
            }
        }

        private static void FitBoundsFromUsedTiles(StageGridAuthoring authoring)
        {
            if (authoring == null || authoring.MovementTilemap == null)
                return;

            if (!TryComputeUsedTileBounds(authoring.MovementTilemap, out var usedBounds))
                return;

            Undo.RecordObject(authoring, "Fit Stage Authoring Bounds From Used Tiles");
            authoring.BoundsMinCell = new Vector2Int(usedBounds.xMin, usedBounds.yMin);
            authoring.BoundsSize = new Vector2Int(Mathf.Max(1, usedBounds.size.x), Mathf.Max(1, usedBounds.size.y));
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

            if (authoring.MovementTilemap != null && TryComputeUsedTileBounds(authoring.MovementTilemap, out var usedBounds))
            {
                IncludeBounds(usedBounds, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            }

            var stageNode = authoring.GetComponent<StageLayoutStageMarker>();
            var anchors = stageNode != null
                ? stageNode.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true)
                : authoring.GetComponentsInChildren<StageRegionAnchorMarker>(includeInactive: true);
            for (int i = 0; i < anchors.Length; i++)
            {
                var anchor = anchors[i];
                if (anchor == null)
                    continue;

                var tileCell = authoring.GetTilemapCell(anchor.AnchorCell.x, anchor.AnchorCell.y);
                IncludeCell(tileCell.x, tileCell.y, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            }

            IncludePaintBounds(authoring, authoring.SourceRegionPaint, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
            IncludePaintBounds(authoring, authoring.DepositRegionPaint, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);

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
                Handles.color = new Color(0.6f, 0.6f, 0.6f, 0.9f);
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
                    Handles.DrawSolidRectangleWithOutline(
                        BuildRectVerts(rect, z - 0.003f),
                        ResolveMovementColor(tile.MovementFlags),
                        Color.clear);
                }
            }

            if (authoring.ShowSourceGizmo && authoring.SourceRegionPaint != null && authoring.SourceRegionPaint.GetCell(localX, localY) != 0u)
            {
                Handles.DrawSolidRectangleWithOutline(
                    BuildRectVerts(rect, z - 0.002f),
                    new Color(0.1f, 0.75f, 1f, 0.2f),
                    Color.clear);
            }

            if (authoring.ShowDepositGizmo && authoring.DepositRegionPaint != null && authoring.DepositRegionPaint.GetCell(localX, localY) != 0u)
            {
                Handles.DrawSolidRectangleWithOutline(
                    BuildRectVerts(rect, z - 0.001f),
                    new Color(1f, 0.7f, 0.1f, 0.2f),
                    Color.clear);
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

                int tileX = authoring.BoundsMinCell.x + anchor.AnchorCell.x;
                int tileY = authoring.BoundsMinCell.y + anchor.AnchorCell.y;
                var pos = new Vector3(
                    (tileX + anchor.AnchorOffset.x + 0.5f) * cellWidth,
                    (tileY + anchor.AnchorOffset.y + 0.5f) * cellHeight,
                    z - 0.004f);
                Handles.color = anchor.RegionKind == StageRegionKind.Source
                    ? new Color(0.1f, 0.85f, 1f, 1f)
                    : new Color(1f, 0.75f, 0.1f, 1f);
                Handles.DrawSolidDisc(pos, Vector3.forward, 0.12f * Mathf.Min(cellWidth, cellHeight));
                Handles.Label(pos, $"{anchor.RegionKind}:{anchor.StableId}");
            }
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
            Handles.DrawLine(
                new Vector3(rect.xMin, rect.yMax, z),
                new Vector3(rect.xMin, rect.yMin, z));
        }

        private static Color ResolveMovementColor(StageCellMovementFlags flags)
        {
            bool blockPlayer = (flags & StageCellMovementFlags.BlockPlayer) != 0;
            bool blockBullet = (flags & StageCellMovementFlags.BlockBullet) != 0;
            if (blockPlayer && blockBullet)
                return new Color(0.55f, 0.05f, 0.05f, 0.26f);
            if (blockPlayer)
                return new Color(0.95f, 0.15f, 0.15f, 0.22f);
            if (blockBullet)
                return new Color(0.9f, 0.1f, 0.85f, 0.22f);
            return Color.clear;
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

        private static void IncludePaintBounds(StageGridAuthoring authoring, StageRegionPaintAsset paint, ref bool hasAny, ref int minX, ref int minY, ref int maxX, ref int maxY)
        {
            if (authoring == null || paint == null)
                return;

            paint.EnsureShape();
            for (int y = 0; y < paint.Height; y++)
            {
                for (int x = 0; x < paint.Width; x++)
                {
                    if (paint.GetCell(x, y) == 0u)
                        continue;

                    var tileCell = authoring.GetTilemapCell(x, y);
                    IncludeCell(tileCell.x, tileCell.y, ref hasAny, ref minX, ref minY, ref maxX, ref maxY);
                }
            }
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
