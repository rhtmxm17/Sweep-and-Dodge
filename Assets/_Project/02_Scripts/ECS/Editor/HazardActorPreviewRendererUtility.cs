using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum HazardActorPreviewDisplayMode
    {
        Exact = 0,
        Density = 1,
    }

    /// <summary>
    /// Draws the embedded top-down preview as one UI Toolkit mesh while retaining one
    /// projected position per visible ghost. Density is an explicit diagnostic mode,
    /// never an automatic replacement for the exact view.
    /// </summary>
    public sealed class HazardActorWorkbenchPreviewElement : VisualElement
    {
        private const int DensityGridSize = 16;
        private const float ExactGhostSizePx = 5f;
        private const float MinViewHalfHeight = 0.25f;
        private const float MaxViewHalfHeight = 32f;
        private readonly HazardActorPreviewSession _session;
        private readonly int[] _density = new int[DensityGridSize * DensityGridSize];
        private HazardActorPreviewDisplayMode _displayMode;
        private Vector2 _viewCenter;
        private float _viewHalfHeight = 8f;
        private bool _middleDragging;
        private Vector2 _lastPointerPosition;

        public HazardActorWorkbenchPreviewElement(HazardActorPreviewSession session)
        {
            _session = session;
            focusable = true;
            pickingMode = PickingMode.Position;
            style.minHeight = 120f;
            style.backgroundColor = new Color(0.055f, 0.065f, 0.075f, 1f);
            style.overflow = Overflow.Hidden;
            tooltip = "Click target, middle-drag pan, wheel zoom, F fit, R reset, Shift+R restart with target";
            generateVisualContent += GenerateVisualContent;
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<MouseMoveEvent>(OnMouseMove);
            RegisterCallback<MouseUpEvent>(OnMouseUp);
            RegisterCallback<WheelEvent>(OnWheel);
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public event System.Action<Vector3> TargetWorldPositionChanged;
        public event System.Action<Vector2, float> ViewChanged;
        public event System.Action FitRequested;
        public event System.Action ResetViewRequested;
        public event System.Action RestartWithTargetRequested;

        public HazardActorPreviewDisplayMode DisplayMode
        {
            get => _displayMode;
            set
            {
                if (_displayMode == value)
                    return;
                _displayMode = value;
                MarkDirtyRepaint();
            }
        }

        public Vector2 ViewCenter => _viewCenter;
        public float ViewHalfHeight => _viewHalfHeight;
        public int LastVisibleGhostCount { get; private set; }
        public int LastDrawSubmissions { get; private set; }

        public void SetView(Vector2 center, float halfHeight)
        {
            _viewCenter = center;
            _viewHalfHeight = Mathf.Clamp(halfHeight, MinViewHalfHeight, MaxViewHalfHeight);
            MarkDirtyRepaint();
        }

        public static bool TryProjectWorldToPreview(
            Vector3 worldPosition,
            Vector2 viewCenter,
            float viewHalfHeight,
            Rect rect,
            out Vector2 previewPosition)
        {
            previewPosition = default;
            if (rect.width <= 0f || rect.height <= 0f || viewHalfHeight <= 0f)
                return false;

            float halfWidth = viewHalfHeight * (rect.width / rect.height);
            float normalizedX = (worldPosition.x - viewCenter.x) / halfWidth;
            float normalizedY = (worldPosition.z - viewCenter.y) / viewHalfHeight;
            if (normalizedX < -1f || normalizedX > 1f || normalizedY < -1f || normalizedY > 1f)
                return false;

            previewPosition = new Vector2(
                rect.center.x + (normalizedX * rect.width * 0.5f),
                rect.center.y - (normalizedY * rect.height * 0.5f));
            return true;
        }

        public static bool TryProjectPreviewToWorld(
            Vector2 previewPosition,
            Vector2 viewCenter,
            float viewHalfHeight,
            Rect rect,
            out Vector3 worldPosition)
        {
            worldPosition = default;
            if (rect.width <= 0f || rect.height <= 0f || viewHalfHeight <= 0f)
                return false;
            if (!rect.Contains(previewPosition))
                return false;

            float halfWidth = viewHalfHeight * (rect.width / rect.height);
            float normalizedX = (previewPosition.x - rect.center.x) / (rect.width * 0.5f);
            float normalizedY = -(previewPosition.y - rect.center.y) / (rect.height * 0.5f);
            worldPosition = new Vector3(
                viewCenter.x + (normalizedX * halfWidth),
                0f,
                viewCenter.y + (normalizedY * viewHalfHeight));
            return true;
        }

        public static Vector2 CalculatePannedCenter(
            Vector2 viewCenter,
            float viewHalfHeight,
            Rect rect,
            Vector2 pixelDelta)
        {
            if (rect.width <= 0f || rect.height <= 0f || viewHalfHeight <= 0f)
                return viewCenter;

            float halfWidth = viewHalfHeight * (rect.width / rect.height);
            return new Vector2(
                viewCenter.x - ((pixelDelta.x / rect.width) * halfWidth * 2f),
                viewCenter.y + ((pixelDelta.y / rect.height) * viewHalfHeight * 2f));
        }

        public static void CalculateZoomAroundPoint(
            Vector2 pointerPosition,
            Vector2 viewCenter,
            float viewHalfHeight,
            Rect rect,
            float zoomFactor,
            out Vector2 nextCenter,
            out float nextHalfHeight)
        {
            nextCenter = viewCenter;
            nextHalfHeight = Mathf.Clamp(viewHalfHeight * Mathf.Max(0.01f, zoomFactor), MinViewHalfHeight, MaxViewHalfHeight);
            if (!TryProjectPreviewToWorld(pointerPosition, viewCenter, viewHalfHeight, rect, out Vector3 world))
                return;

            float normalizedX = (pointerPosition.x - rect.center.x) / (rect.width * 0.5f);
            float normalizedY = -(pointerPosition.y - rect.center.y) / (rect.height * 0.5f);
            float nextHalfWidth = nextHalfHeight * (rect.width / rect.height);
            nextCenter = new Vector2(
                world.x - (normalizedX * nextHalfWidth),
                world.z - (normalizedY * nextHalfHeight));
        }

        public static int CountVisibleGhosts(
            IReadOnlyList<HazardActorPreviewGhost> ghosts,
            Vector2 viewCenter,
            float viewHalfHeight,
            Rect rect)
        {
            if (ghosts == null)
                return 0;

            int count = 0;
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (TryProjectWorldToPreview(ghosts[i].Position, viewCenter, viewHalfHeight, rect, out _))
                    count++;
            }
            return count;
        }

        private void GenerateVisualContent(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            var ghosts = _session?.Ghosts;
            LastVisibleGhostCount = 0;
            LastDrawSubmissions = 0;
            if (ghosts == null || rect.width <= 0f || rect.height <= 0f)
                return;

            int ghostQuadCount = _displayMode == HazardActorPreviewDisplayMode.Exact
                ? CountVisibleGhosts(ghosts, _viewCenter, _viewHalfHeight, rect)
                : PrepareDensity(ghosts, rect);
            bool drawActor = TryProjectWorldToPreview(
                _session.Input.ActorWorldPosition,
                _viewCenter,
                _viewHalfHeight,
                rect,
                out Vector2 actorPosition);
            bool drawTarget = TryProjectWorldToPreview(
                _session.Input.TargetWorldPosition,
                _viewCenter,
                _viewHalfHeight,
                rect,
                out Vector2 targetPosition);
            int markerQuadCount = (drawActor ? 1 : 0) + (drawTarget ? 1 : 0);
            int quadCount = ghostQuadCount + markerQuadCount;
            LastVisibleGhostCount = _displayMode == HazardActorPreviewDisplayMode.Exact
                ? ghostQuadCount
                : CountVisibleGhosts(ghosts, _viewCenter, _viewHalfHeight, rect);
            if (quadCount == 0)
                return;

            var mesh = context.Allocate(quadCount * 4, quadCount * 6, null);
            int quadIndex = 0;
            if (_displayMode == HazardActorPreviewDisplayMode.Exact)
                DrawExactGhosts(mesh, ghosts, rect, ref quadIndex);
            else
                DrawDensity(mesh, rect, ref quadIndex);

            if (drawActor)
                AddQuad(mesh, actorPosition, 7f, new Color32(255, 70, 210, 255), quadIndex++);
            if (drawTarget)
                AddQuad(mesh, targetPosition, 6f, new Color32(255, 220, 60, 255), quadIndex++);
            LastDrawSubmissions = 1;
        }

        private void DrawExactGhosts(
            MeshWriteData mesh,
            IReadOnlyList<HazardActorPreviewGhost> ghosts,
            Rect rect,
            ref int quadIndex)
        {
            var color = new Color32(25, 220, 255, 230);
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (!TryProjectWorldToPreview(
                        ghosts[i].Position,
                        _viewCenter,
                        _viewHalfHeight,
                        rect,
                        out Vector2 position))
                    continue;
                AddQuad(mesh, position, ExactGhostSizePx, color, quadIndex++);
            }
        }

        private int PrepareDensity(IReadOnlyList<HazardActorPreviewGhost> ghosts, Rect rect)
        {
            System.Array.Clear(_density, 0, _density.Length);
            for (int i = 0; i < ghosts.Count; i++)
            {
                if (!TryProjectWorldToPreview(
                        ghosts[i].Position,
                        _viewCenter,
                        _viewHalfHeight,
                        rect,
                        out Vector2 position))
                    continue;

                int x = Mathf.Clamp(
                    Mathf.FloorToInt(((position.x - rect.xMin) / rect.width) * DensityGridSize),
                    0,
                    DensityGridSize - 1);
                int y = Mathf.Clamp(
                    Mathf.FloorToInt(((position.y - rect.yMin) / rect.height) * DensityGridSize),
                    0,
                    DensityGridSize - 1);
                _density[(y * DensityGridSize) + x]++;
            }

            int occupied = 0;
            for (int i = 0; i < _density.Length; i++)
            {
                if (_density[i] > 0)
                    occupied++;
            }
            return occupied;
        }

        private void DrawDensity(MeshWriteData mesh, Rect rect, ref int quadIndex)
        {
            float cellWidth = rect.width / DensityGridSize;
            float cellHeight = rect.height / DensityGridSize;
            for (int i = 0; i < _density.Length; i++)
            {
                int count = _density[i];
                if (count <= 0)
                    continue;

                int x = i % DensityGridSize;
                int y = i / DensityGridSize;
                float intensity = Mathf.Clamp01(0.2f + Mathf.Log(count + 1, DensityGridSize));
                var position = new Vector2(
                    rect.xMin + ((x + 0.5f) * cellWidth),
                    rect.yMin + ((y + 0.5f) * cellHeight));
                float size = Mathf.Max(1f, Mathf.Min(cellWidth, cellHeight) * 0.9f);
                AddQuad(mesh, position, size, new Color(0.1f, 0.85f, 1f, intensity), quadIndex++);
            }
        }

        private static void AddQuad(MeshWriteData mesh, Vector2 center, float size, Color32 color, int quadIndex)
        {
            float halfSize = size * 0.5f;
            float z = Vertex.nearZ;
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x - halfSize, center.y - halfSize, z), tint = color, uv = Vector2.zero });
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + halfSize, center.y - halfSize, z), tint = color, uv = Vector2.right });
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + halfSize, center.y + halfSize, z), tint = color, uv = Vector2.one });
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x - halfSize, center.y + halfSize, z), tint = color, uv = Vector2.up });

            ushort first = (ushort)(quadIndex * 4);
            mesh.SetNextIndex(first);
            mesh.SetNextIndex((ushort)(first + 1));
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex(first);
            mesh.SetNextIndex((ushort)(first + 2));
            mesh.SetNextIndex((ushort)(first + 3));
        }

        private void OnMouseDown(MouseDownEvent evt)
        {
            Focus();
            if (evt.button == 0)
            {
                if (TryProjectPreviewToWorld(evt.localMousePosition, _viewCenter, _viewHalfHeight, contentRect, out Vector3 world))
                {
                    if (_session != null)
                        world.y = _session.Input.TargetWorldPosition.y;
                    TargetWorldPositionChanged?.Invoke(world);
                }
                evt.StopPropagation();
            }
            else if (evt.button == 2)
            {
                _middleDragging = true;
                _lastPointerPosition = evt.localMousePosition;
                MouseCaptureController.CaptureMouse(this);
                evt.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent evt)
        {
            if (!_middleDragging)
                return;

            Vector2 delta = evt.localMousePosition - _lastPointerPosition;
            _lastPointerPosition = evt.localMousePosition;
            _viewCenter = CalculatePannedCenter(_viewCenter, _viewHalfHeight, contentRect, delta);
            ViewChanged?.Invoke(_viewCenter, _viewHalfHeight);
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnMouseUp(MouseUpEvent evt)
        {
            if (evt.button != 2 || !_middleDragging)
                return;

            _middleDragging = false;
            MouseCaptureController.ReleaseMouse(this);
            evt.StopPropagation();
        }

        private void OnWheel(WheelEvent evt)
        {
            float factor = evt.delta.y > 0f ? 1.1f : 0.9f;
            CalculateZoomAroundPoint(evt.localMousePosition, _viewCenter, _viewHalfHeight, contentRect, factor, out _viewCenter, out _viewHalfHeight);
            ViewChanged?.Invoke(_viewCenter, _viewHalfHeight);
            MarkDirtyRepaint();
            evt.StopPropagation();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F)
            {
                FitRequested?.Invoke();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.R)
            {
                if (evt.shiftKey)
                    RestartWithTargetRequested?.Invoke();
                else
                    ResetViewRequested?.Invoke();
                evt.StopPropagation();
            }
        }
    }

    public static class HazardActorPreviewRendererUtility
    {
        public const int MaxInstancesPerSubmission = 1023;

        private static readonly Matrix4x4[] Matrices = new Matrix4x4[MaxInstancesPerSubmission];
        private static Mesh _quadMesh;
        private static Material _ghostMaterial;
        private static Material _aggregateMaterial;

        public static int LastDrawSubmissions { get; private set; }
        public static int LastRenderedGhostCount { get; private set; }
        public static int LastAggregateCount { get; private set; }
        public static bool HasAllocatedResources => _quadMesh != null || _ghostMaterial != null || _aggregateMaterial != null;

        public static int EstimateDrawSubmissions(int ghostCount, bool drawAggregate)
        {
            int submissions = Mathf.CeilToInt(Mathf.Max(0, ghostCount) / (float)MaxInstancesPerSubmission);
            return submissions + (drawAggregate ? 1 : 0);
        }

        public static void DrawGhostInstances(IReadOnlyList<HazardActorPreviewGhost> ghosts, float size, bool drawAggregate)
        {
            LastDrawSubmissions = 0;
            LastRenderedGhostCount = ghosts?.Count ?? 0;
            LastAggregateCount = 0;
            if (ghosts == null || ghosts.Count == 0)
                return;

            EnsureResources();
            int index = 0;
            while (index < ghosts.Count)
            {
                int count = Mathf.Min(MaxInstancesPerSubmission, ghosts.Count - index);
                for (int i = 0; i < count; i++)
                {
                    var ghost = ghosts[index + i];
                    Matrices[i] = Matrix4x4.TRS(
                        ghost.Position + Vector3.up * 0.02f,
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(size, size, size));
                }

                Graphics.DrawMeshInstanced(_quadMesh, 0, _ghostMaterial, Matrices, count);
                LastDrawSubmissions++;
                index += count;
            }

            if (drawAggregate)
            {
                LastAggregateCount = 1;
                Matrices[0] = Matrix4x4.TRS(Vector3.up * 0.04f, Quaternion.Euler(90f, 0f, 0f), Vector3.one * (size * 6f));
                Graphics.DrawMeshInstanced(_quadMesh, 0, _aggregateMaterial, Matrices, 1);
                LastDrawSubmissions++;
            }
        }

        public static int PrepareGhostBatchesForMeasurement(IReadOnlyList<HazardActorPreviewGhost> ghosts, float size, bool drawAggregate)
        {
            LastDrawSubmissions = 0;
            LastRenderedGhostCount = ghosts?.Count ?? 0;
            LastAggregateCount = 0;
            if (ghosts == null || ghosts.Count == 0)
                return 0;

            int index = 0;
            while (index < ghosts.Count)
            {
                int count = Mathf.Min(MaxInstancesPerSubmission, ghosts.Count - index);
                for (int i = 0; i < count; i++)
                {
                    var ghost = ghosts[index + i];
                    Matrices[i] = Matrix4x4.TRS(
                        ghost.Position + Vector3.up * 0.02f,
                        Quaternion.Euler(90f, 0f, 0f),
                        new Vector3(size, size, size));
                }

                LastDrawSubmissions++;
                index += count;
            }

            if (drawAggregate)
            {
                LastAggregateCount = 1;
                Matrices[0] = Matrix4x4.TRS(Vector3.up * 0.04f, Quaternion.Euler(90f, 0f, 0f), Vector3.one * (size * 6f));
                LastDrawSubmissions++;
            }

            return ghosts.Count;
        }

        public static void Dispose()
        {
            if (_quadMesh != null)
                Object.DestroyImmediate(_quadMesh);
            if (_ghostMaterial != null)
                Object.DestroyImmediate(_ghostMaterial);
            if (_aggregateMaterial != null)
                Object.DestroyImmediate(_aggregateMaterial);
            _quadMesh = null;
            _ghostMaterial = null;
            _aggregateMaterial = null;
            LastDrawSubmissions = 0;
            LastRenderedGhostCount = 0;
            LastAggregateCount = 0;
        }

        private static void EnsureResources()
        {
            if (_quadMesh == null)
            {
                _quadMesh = new Mesh
                {
                    name = "HazardActorPreviewGhostQuad",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f),
                        new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f),
                        new Vector3(-0.5f, 0.5f, 0f),
                    },
                    triangles = new[] { 0, 1, 2, 0, 2, 3 },
                    uv = new[]
                    {
                        new Vector2(0f, 0f),
                        new Vector2(1f, 0f),
                        new Vector2(1f, 1f),
                        new Vector2(0f, 1f),
                    },
                };
                _quadMesh.RecalculateBounds();
            }

            _ghostMaterial ??= CreateMaterial(new Color(0.1f, 0.85f, 1f, 0.85f));
            _aggregateMaterial ??= CreateMaterial(new Color(1f, 0.75f, 0.1f, 0.35f));
        }

        private static Material CreateMaterial(Color color)
        {
            var shader = Shader.Find("Hidden/Internal-Colored") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave,
                enableInstancing = true,
            };
            material.SetColor("_Color", color);
            return material;
        }
    }
}
