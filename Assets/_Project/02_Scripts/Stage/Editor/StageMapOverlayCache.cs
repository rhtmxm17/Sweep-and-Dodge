using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SweepNDodge.DotsBullets.Editor
{
    public enum StageMapOverlayGeometryLayer : byte
    {
        Movement = 0,
        Source = 1,
        Deposit = 2,
        OverlapError = 3,
    }

    public readonly struct StageMapOverlayGeometryStats
    {
        public StageMapOverlayGeometryStats(
            int buildCount,
            int scannedCellCount,
            int movementCellCount,
            int sourceCellCount,
            int depositCellCount,
            int overlapCellCount,
            int movementVertexCount,
            int sourceVertexCount,
            int depositVertexCount,
            int overlapVertexCount,
            int movementIndexCount,
            int sourceIndexCount,
            int depositIndexCount,
            int overlapIndexCount)
        {
            BuildCount = buildCount;
            ScannedCellCount = scannedCellCount;
            MovementCellCount = movementCellCount;
            SourceCellCount = sourceCellCount;
            DepositCellCount = depositCellCount;
            OverlapCellCount = overlapCellCount;
            MovementVertexCount = movementVertexCount;
            SourceVertexCount = sourceVertexCount;
            DepositVertexCount = depositVertexCount;
            OverlapVertexCount = overlapVertexCount;
            MovementIndexCount = movementIndexCount;
            SourceIndexCount = sourceIndexCount;
            DepositIndexCount = depositIndexCount;
            OverlapIndexCount = overlapIndexCount;
        }

        public int BuildCount { get; }
        public int ScannedCellCount { get; }
        public int MovementCellCount { get; }
        public int SourceCellCount { get; }
        public int DepositCellCount { get; }
        public int OverlapCellCount { get; }
        public int MovementVertexCount { get; }
        public int SourceVertexCount { get; }
        public int DepositVertexCount { get; }
        public int OverlapVertexCount { get; }
        public int MovementIndexCount { get; }
        public int SourceIndexCount { get; }
        public int DepositIndexCount { get; }
        public int OverlapIndexCount { get; }
    }

    /// <summary>
    /// Owns transient layer meshes used by the Stage Map Scene View overlay.
    /// </summary>
    public sealed class StageMapOverlayCache : IDisposable
    {
        private readonly Mesh[] _meshes = new Mesh[4];
        private Material _material;
        private StageMapDocument _document;
        private bool _dirty = true;
        private int _buildCount;
        private int _scannedCellCount;
        private int _movementCellCount;
        private int _sourceCellCount;
        private int _depositCellCount;
        private int _overlapCellCount;

        public StageMapOverlayGeometryStats Stats => new StageMapOverlayGeometryStats(
            _buildCount,
            _scannedCellCount,
            _movementCellCount,
            _sourceCellCount,
            _depositCellCount,
            _overlapCellCount,
            GetVertexCount(StageMapOverlayGeometryLayer.Movement),
            GetVertexCount(StageMapOverlayGeometryLayer.Source),
            GetVertexCount(StageMapOverlayGeometryLayer.Deposit),
            GetVertexCount(StageMapOverlayGeometryLayer.OverlapError),
            GetIndexCount(StageMapOverlayGeometryLayer.Movement),
            GetIndexCount(StageMapOverlayGeometryLayer.Source),
            GetIndexCount(StageMapOverlayGeometryLayer.Deposit),
            GetIndexCount(StageMapOverlayGeometryLayer.OverlapError));

        public void Invalidate()
        {
            _dirty = true;
        }

        public void EnsureBuilt(StageMapDocument document)
        {
            if (!_dirty && _document == document)
                return;

            Rebuild(document);
        }

        public int GetDrawSubmissionCount(bool showMovement, bool showSource, bool showDeposit)
        {
            int count = 0;
            if (showMovement && GetVertexCount(StageMapOverlayGeometryLayer.Movement) > 0)
                count++;
            if (showSource && GetVertexCount(StageMapOverlayGeometryLayer.Source) > 0)
                count++;
            if (showDeposit && GetVertexCount(StageMapOverlayGeometryLayer.Deposit) > 0)
                count++;
            if (showSource && showDeposit && GetVertexCount(StageMapOverlayGeometryLayer.OverlapError) > 0)
                count++;
            return count;
        }

        public int Draw(bool showMovement, bool showSource, bool showDeposit)
        {
            int submissions = GetDrawSubmissionCount(showMovement, showSource, showDeposit);
            if (submissions == 0)
                return 0;

            var material = GetMaterial();
            if (material == null)
                return 0;

            int submitted = 0;
            if (showMovement)
                submitted += DrawLayer(StageMapOverlayGeometryLayer.Movement, material);
            if (showSource)
                submitted += DrawLayer(StageMapOverlayGeometryLayer.Source, material);
            if (showDeposit)
                submitted += DrawLayer(StageMapOverlayGeometryLayer.Deposit, material);
            if (showSource && showDeposit)
                submitted += DrawLayer(StageMapOverlayGeometryLayer.OverlapError, material);
            return submitted;
        }

        public Mesh GetMesh(StageMapOverlayGeometryLayer layer)
        {
            return _meshes[(int)layer];
        }

        public void Dispose()
        {
            for (int i = 0; i < _meshes.Length; i++)
            {
                if (_meshes[i] != null)
                    UnityEngine.Object.DestroyImmediate(_meshes[i]);
                _meshes[i] = null;
            }

            if (_material != null)
                UnityEngine.Object.DestroyImmediate(_material);
            _material = null;
            _document = null;
            _dirty = true;
        }

        private void Rebuild(StageMapDocument document)
        {
            EnsureMeshes();
            ClearMeshes();
            _document = document;
            _dirty = false;
            _buildCount++;
            _scannedCellCount = 0;
            _movementCellCount = 0;
            _sourceCellCount = 0;
            _depositCellCount = 0;
            _overlapCellCount = 0;

            if (document == null
                || document.Cells == null
                || document.Grid.Width <= 0
                || document.Grid.Height <= 0
                || document.Grid.CellSize <= 0f)
            {
                return;
            }

            int expected = document.Grid.Width * document.Grid.Height;
            int count = Mathf.Min(expected, document.Cells.Length);
            _scannedCellCount = count;
            var movement = new GeometryBuffer(Mathf.Min(count, 256));
            var source = new GeometryBuffer(Mathf.Min(count, 256));
            var deposit = new GeometryBuffer(Mathf.Min(count, 256));
            var overlap = new GeometryBuffer(Mathf.Min(count, 64));

            for (int i = 0; i < count; i++)
            {
                var cell = document.Cells[i];
                int x = i % document.Grid.Width;
                int y = i / document.Grid.Width;
                if (cell.MovementFlags != StageCellMovementFlags.None)
                {
                    AppendCellQuad(document.Grid, x, y, 0.010f, ResolveMovementColor(cell.MovementFlags), movement);
                    _movementCellCount++;
                }

                if (cell.SourceRegionId != 0u)
                {
                    AppendCellQuad(document.Grid, x, y, 0.012f, new Color(0.1f, 0.85f, 1f, 0.18f), source);
                    _sourceCellCount++;
                }

                if (cell.DepositRegionId != 0u)
                {
                    AppendCellQuad(document.Grid, x, y, 0.014f, new Color(1f, 0.75f, 0.1f, 0.18f), deposit);
                    _depositCellCount++;
                }

                if (cell.SourceRegionId != 0u && cell.DepositRegionId != 0u)
                {
                    AppendCellQuad(document.Grid, x, y, 0.020f, new Color(1f, 0.05f, 0.05f, 0.55f), overlap);
                    _overlapCellCount++;
                }
            }

            ApplyGeometry(_meshes[(int)StageMapOverlayGeometryLayer.Movement], movement);
            ApplyGeometry(_meshes[(int)StageMapOverlayGeometryLayer.Source], source);
            ApplyGeometry(_meshes[(int)StageMapOverlayGeometryLayer.Deposit], deposit);
            ApplyGeometry(_meshes[(int)StageMapOverlayGeometryLayer.OverlapError], overlap);
        }

        private void EnsureMeshes()
        {
            for (int i = 0; i < _meshes.Length; i++)
            {
                if (_meshes[i] != null)
                    continue;

                _meshes[i] = new Mesh
                {
                    name = $"StageMapOverlay_{(StageMapOverlayGeometryLayer)i}",
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
        }

        private void ClearMeshes()
        {
            for (int i = 0; i < _meshes.Length; i++)
                _meshes[i].Clear();
        }

        private int GetVertexCount(StageMapOverlayGeometryLayer layer)
        {
            var mesh = _meshes[(int)layer];
            return mesh != null ? mesh.vertexCount : 0;
        }

        private int GetIndexCount(StageMapOverlayGeometryLayer layer)
        {
            Mesh mesh = _meshes[(int)layer];
            return mesh != null && mesh.subMeshCount > 0 ? (int)mesh.GetIndexCount(0) : 0;
        }

        private int DrawLayer(StageMapOverlayGeometryLayer layer, Material material)
        {
            var mesh = _meshes[(int)layer];
            if (mesh == null || mesh.vertexCount == 0 || !material.SetPass(0))
                return 0;

            Graphics.DrawMeshNow(mesh, Matrix4x4.identity);
            return 1;
        }

        private Material GetMaterial()
        {
            if (_material != null)
                return _material;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            _material = new Material(shader)
            {
                name = "StageMapOverlayMaterial",
                hideFlags = HideFlags.HideAndDontSave,
            };
            if (shader.name == "Hidden/Internal-Colored")
            {
                _material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                _material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                _material.SetInt("_Cull", (int)CullMode.Off);
                _material.SetInt("_ZWrite", 0);
            }

            return _material;
        }

        private static void AppendCellQuad(
            StageGridSpec grid,
            int x,
            int y,
            float yOffset,
            Color color,
            GeometryBuffer buffer)
        {
            float x0 = grid.Origin.x + (x * grid.CellSize);
            float z0 = grid.Origin.z + (y * grid.CellSize);
            float x1 = x0 + grid.CellSize;
            float z1 = z0 + grid.CellSize;
            float worldY = grid.Origin.y + yOffset;
            int start = buffer.Vertices.Count;
            buffer.Vertices.Add(new Vector3(x0, worldY, z0));
            buffer.Vertices.Add(new Vector3(x1, worldY, z0));
            buffer.Vertices.Add(new Vector3(x1, worldY, z1));
            buffer.Vertices.Add(new Vector3(x0, worldY, z1));
            buffer.Colors.Add(color);
            buffer.Colors.Add(color);
            buffer.Colors.Add(color);
            buffer.Colors.Add(color);
            buffer.Indices.Add(start);
            buffer.Indices.Add(start + 1);
            buffer.Indices.Add(start + 2);
            buffer.Indices.Add(start);
            buffer.Indices.Add(start + 2);
            buffer.Indices.Add(start + 3);
        }

        private static void ApplyGeometry(Mesh mesh, GeometryBuffer buffer)
        {
            mesh.indexFormat = buffer.Vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(buffer.Vertices);
            mesh.SetColors(buffer.Colors);
            mesh.SetIndices(buffer.Indices, MeshTopology.Triangles, 0, true);
        }

        private static Color ResolveMovementColor(StageCellMovementFlags flags)
        {
            bool blockPlayer = (flags & StageCellMovementFlags.BlockPlayer) != 0;
            bool blockBullet = (flags & StageCellMovementFlags.BlockBullet) != 0;
            if (blockPlayer && blockBullet)
                return new Color(0.75f, 0.05f, 0.05f, 0.28f);
            if (blockPlayer)
                return new Color(0.95f, 0.35f, 0.1f, 0.24f);
            return new Color(0.9f, 0.1f, 0.85f, 0.24f);
        }

        private sealed class GeometryBuffer
        {
            public GeometryBuffer(int cellCapacity)
            {
                Vertices = new List<Vector3>(cellCapacity * 4);
                Colors = new List<Color>(cellCapacity * 4);
                Indices = new List<int>(cellCapacity * 6);
            }

            public List<Vector3> Vertices { get; }
            public List<Color> Colors { get; }
            public List<int> Indices { get; }
        }
    }

    public static class StageMapOverlayCacheBuilder
    {
        public static StageMapOverlayCache Build(StageMapDocument document)
        {
            var cache = new StageMapOverlayCache();
            cache.EnsureBuilt(document);
            return cache;
        }
    }
}
