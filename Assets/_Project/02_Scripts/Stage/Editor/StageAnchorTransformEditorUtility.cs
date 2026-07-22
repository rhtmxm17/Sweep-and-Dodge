using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace SweepNDodge.DotsBullets.Editor
{
    /// <summary>
    /// Owns Edit Mode synchronization between stage anchor data and Scene View transforms.
    /// </summary>
    public static class StageAnchorTransformEditorUtility
    {
        private const float PositionToleranceSqr = 0.000001f;
        private const float RotationToleranceDeg = 0.001f;

        private readonly struct TransformPose
        {
            public TransformPose(Vector3 position, Quaternion rotation)
            {
                Position = position;
                Rotation = rotation;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
        }

        private static readonly Dictionary<int, TransformPose> LastObservedTransformPoses = new();

        /// <summary>
        /// Applies region anchor cell and offset data to its Transform.
        /// </summary>
        public static bool SyncRegionTransformFromData(StageRegionAnchorMarker marker, bool recordUndo)
        {
            if (marker == null
                || !TryResolveAuthoring(marker, out var authoring)
                || !TryGetWorldPosition(authoring, marker.AnchorCell, marker.AnchorOffset, out var worldPosition))
            {
                return false;
            }

            if ((marker.transform.position - worldPosition).sqrMagnitude <= PositionToleranceSqr)
                return false;

            if (recordUndo)
                Undo.RecordObject(marker.transform, "Apply Stage Region Anchor Data");

            marker.transform.position = worldPosition;
            EditorUtility.SetDirty(marker.transform);
            RememberTransformPose(marker.transform);
            return true;
        }

        /// <summary>
        /// Snaps a region anchor Transform to the nearest grid cell center and updates serialized data.
        /// </summary>
        public static bool SyncRegionDataFromTransform(StageRegionAnchorMarker marker, bool recordUndo)
        {
            if (marker == null
                || !TryResolveAuthoring(marker, out var authoring)
                || !TryGetSnappedCell(authoring, marker.transform.position, out var anchorCell, out var worldPosition))
            {
                return false;
            }

            bool dataMatches = marker.AnchorCell == anchorCell && marker.AnchorOffset == Vector2.zero;
            bool transformMatches = (marker.transform.position - worldPosition).sqrMagnitude <= PositionToleranceSqr;
            if (dataMatches && transformMatches)
                return false;

            if (recordUndo)
                Undo.RecordObjects(new Object[] { marker, marker.transform }, "Move Stage Region Anchor");

            marker.AnchorCell = anchorCell;
            marker.AnchorOffset = Vector2.zero;
            marker.transform.position = worldPosition;
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(marker.transform);
            RememberTransformPose(marker.transform);
            return true;
        }

        /// <summary>
        /// Syncs region data only when the Scene View actually changed the Transform since the last observed pose.
        /// </summary>
        public static bool SyncRegionDataFromTransformIfChanged(StageRegionAnchorMarker marker, bool recordUndo)
        {
            if (marker == null || !HasTransformPoseChanged(marker.transform))
                return false;

            bool changed = SyncRegionDataFromTransform(marker, recordUndo);
            RememberTransformPose(marker.transform);
            return changed;
        }

        /// <summary>
        /// Applies player start cell, offset, and yaw data to its Transform.
        /// </summary>
        public static bool SyncPlayerTransformFromData(StagePlayerStartMarker marker, bool recordUndo)
        {
            if (marker == null
                || !TryResolveAuthoring(marker, out var authoring)
                || !TryGetWorldPosition(authoring, marker.AnchorCell, marker.AnchorOffset, out var worldPosition))
            {
                return false;
            }

            Quaternion worldRotation = Quaternion.Euler(0f, marker.YawDeg, 0f);
            bool positionMatches = (marker.transform.position - worldPosition).sqrMagnitude <= PositionToleranceSqr;
            bool rotationMatches = Quaternion.Angle(marker.transform.rotation, worldRotation) <= RotationToleranceDeg;
            if (positionMatches && rotationMatches)
                return false;

            if (recordUndo)
                Undo.RecordObject(marker.transform, "Apply Stage Player Start Data");

            marker.transform.SetPositionAndRotation(worldPosition, worldRotation);
            EditorUtility.SetDirty(marker.transform);
            RememberTransformPose(marker.transform);
            return true;
        }

        /// <summary>
        /// Snaps a player start Transform to the nearest grid cell center and updates cell, offset, and yaw data.
        /// </summary>
        public static bool SyncPlayerDataFromTransform(StagePlayerStartMarker marker, bool recordUndo)
        {
            if (marker == null
                || !TryResolveAuthoring(marker, out var authoring)
                || !TryGetSnappedCell(authoring, marker.transform.position, out var anchorCell, out var worldPosition))
            {
                return false;
            }

            float yawDeg = NormalizeYaw(marker.transform.eulerAngles.y);
            Quaternion worldRotation = Quaternion.Euler(0f, yawDeg, 0f);
            bool dataMatches = marker.AnchorCell == anchorCell
                && marker.AnchorOffset == Vector2.zero
                && Mathf.Abs(Mathf.DeltaAngle(marker.YawDeg, yawDeg)) <= RotationToleranceDeg;
            bool transformMatches = (marker.transform.position - worldPosition).sqrMagnitude <= PositionToleranceSqr
                && Quaternion.Angle(marker.transform.rotation, worldRotation) <= RotationToleranceDeg;
            if (dataMatches && transformMatches)
                return false;

            if (recordUndo)
                Undo.RecordObjects(new Object[] { marker, marker.transform }, "Move Stage Player Start");

            marker.AnchorCell = anchorCell;
            marker.AnchorOffset = Vector2.zero;
            marker.YawDeg = yawDeg;
            marker.transform.SetPositionAndRotation(worldPosition, worldRotation);
            EditorUtility.SetDirty(marker);
            EditorUtility.SetDirty(marker.transform);
            RememberTransformPose(marker.transform);
            return true;
        }

        /// <summary>
        /// Syncs player start data only when the Scene View actually changed the Transform since the last observed pose.
        /// </summary>
        public static bool SyncPlayerDataFromTransformIfChanged(StagePlayerStartMarker marker, bool recordUndo)
        {
            if (marker == null || !HasTransformPoseChanged(marker.transform))
                return false;

            bool changed = SyncPlayerDataFromTransform(marker, recordUndo);
            RememberTransformPose(marker.transform);
            return changed;
        }

        /// <summary>
        /// Records the current Transform as the idle Scene View pose so a repaint does not count as user movement.
        /// </summary>
        public static void RememberTransformPose(Transform transform)
        {
            if (transform == null)
                return;

            LastObservedTransformPoses[transform.GetInstanceID()] = new TransformPose(transform.position, transform.rotation);
        }

        /// <summary>
        /// Resolves the canonical preview position represented by cell and offset data.
        /// </summary>
        public static bool TryGetWorldPosition(
            StageGridAuthoring authoring,
            Vector2Int anchorCell,
            Vector2 anchorOffset,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (authoring == null || authoring.Grid == null)
                return false;

            var grid = authoring.BuildEditorPreviewGridSpec();
            worldPosition = StageRuntimeGridUtility.GetAnchorWorldPosition(
                in grid,
                new Unity.Mathematics.int2(anchorCell.x, anchorCell.y),
                new Unity.Mathematics.float2(anchorOffset.x, anchorOffset.y),
                authoring.GetEditorPreviewPlaneY());
            return true;
        }

        private static bool HasTransformPoseChanged(Transform transform)
        {
            if (transform == null)
                return false;

            int id = transform.GetInstanceID();
            var current = new TransformPose(transform.position, transform.rotation);
            if (!LastObservedTransformPoses.TryGetValue(id, out var previous))
            {
                LastObservedTransformPoses.Add(id, current);
                return false;
            }

            if ((previous.Position - current.Position).sqrMagnitude <= PositionToleranceSqr
                && Quaternion.Angle(previous.Rotation, current.Rotation) <= RotationToleranceDeg)
            {
                return false;
            }

            return true;
        }

        private static bool TryResolveAuthoring(Component marker, out StageGridAuthoring authoring)
        {
            authoring = null;
            if (marker == null)
                return false;

            var stageNode = marker.GetComponentInParent<StageLayoutStageMarker>();
            return stageNode != null
                && stageNode.TryGetComponent(out authoring)
                && authoring != null
                && authoring.Grid != null;
        }

        private static bool TryGetSnappedCell(
            StageGridAuthoring authoring,
            Vector3 worldPosition,
            out Vector2Int anchorCell,
            out Vector3 snappedWorldPosition)
        {
            anchorCell = default;
            snappedWorldPosition = default;
            if (authoring == null || authoring.Grid == null)
                return false;

            var grid = authoring.BuildEditorPreviewGridSpec();
            float cellSize = Mathf.Max(0.0001f, grid.CellSize);
            anchorCell = new Vector2Int(
                Mathf.FloorToInt((worldPosition.x - grid.Origin.x) / cellSize),
                Mathf.FloorToInt((worldPosition.z - grid.Origin.z) / cellSize));
            return TryGetWorldPosition(authoring, anchorCell, Vector2.zero, out snappedWorldPosition);
        }

        private static float NormalizeYaw(float yawDeg)
        {
            float normalized = Mathf.Repeat(yawDeg, 360f);
            return Mathf.Approximately(normalized, 360f) ? 0f : normalized;
        }
    }
}
