using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace SweepNDodge.DotsBullets.Editor
{
    /// <summary>
    /// Caches StageGrid Scene View lines so repaint does not rescan every authored cell.
    /// </summary>
    [InitializeOnLoad]
    public static class StageGridSceneVisualizationRenderer
    {
        /// <summary>
        /// Reports the latest cache build cost for diagnostics and editor tests.
        /// </summary>
        public readonly struct CacheStats
        {
            public CacheStats(int cellCount, int tileLookupCount, int vertexCount, int rebuildCount)
            {
                CellCount = cellCount;
                TileLookupCount = tileLookupCount;
                VertexCount = vertexCount;
                RebuildCount = rebuildCount;
            }

            public int CellCount { get; }
            public int TileLookupCount { get; }
            public int VertexCount { get; }
            public int RebuildCount { get; }
        }

        private sealed class CacheEntry
        {
            public StageGridAuthoring Authoring;
            public Mesh Mesh;
            public int Signature;
            public bool Dirty = true;
            public int CellCount;
            public int TileLookupCount;
            public int RebuildCount;
        }

        private static readonly Dictionary<int, CacheEntry> Entries = new();
        private static Material _lineMaterial;
        private static bool _warnedMissingLineShader;
        private static bool _warnedFallbackLineShader;

        static StageGridSceneVisualizationRenderer()
        {
            Tilemap.tilemapTileChanged += OnTilemapTileChanged;
            EditorApplication.projectChanged += MarkAllDirty;
            EditorApplication.hierarchyChanged += RemoveStaleEntries;
            Undo.undoRedoPerformed += MarkAllDirty;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeAll;
            EditorApplication.quitting += DisposeAll;
        }

        /// <summary>
        /// Draws the current cached visualization for one stage authoring object.
        /// </summary>
        public static void Draw(StageGridAuthoring authoring)
        {
            var entry = EnsureCache(authoring);
            if (entry == null || entry.Mesh == null || entry.Mesh.vertexCount == 0)
                return;

            var material = GetLineMaterial();
            if (material == null || !material.SetPass(0))
                return;

            Matrix4x4 matrix = Matrix4x4.TRS(
                authoring.Grid.transform.position,
                authoring.Grid.transform.rotation,
                Vector3.one);
            Graphics.DrawMeshNow(entry.Mesh, matrix);
        }

        /// <summary>
        /// Ensures the authoring cache is current and returns its latest build statistics.
        /// </summary>
        public static CacheStats GetOrBuildCacheStats(StageGridAuthoring authoring)
        {
            var entry = EnsureCache(authoring);
            return entry == null
                ? default
                : new CacheStats(entry.CellCount, entry.TileLookupCount, entry.Mesh != null ? entry.Mesh.vertexCount : 0, entry.RebuildCount);
        }

        /// <summary>
        /// Marks one authoring cache dirty without rebuilding it immediately.
        /// </summary>
        public static void Invalidate(StageGridAuthoring authoring)
        {
            if (authoring != null && Entries.TryGetValue(authoring.GetInstanceID(), out var entry))
                entry.Dirty = true;
        }

        /// <summary>
        /// Destroys all transient meshes and materials owned by this renderer.
        /// </summary>
        public static void ClearCaches()
        {
            DisposeAll();
        }

        private static CacheEntry EnsureCache(StageGridAuthoring authoring)
        {
            if (authoring == null || authoring.Grid == null)
                return null;

            int id = authoring.GetInstanceID();
            if (!Entries.TryGetValue(id, out var entry))
            {
                entry = new CacheEntry
                {
                    Authoring = authoring,
                    Mesh = CreateMesh(authoring),
                };
                Entries.Add(id, entry);
            }

            int signature = ComputeSignature(authoring);
            if (!entry.Dirty && entry.Signature == signature)
                return entry;

            Rebuild(entry, signature);
            return entry;
        }

        private static void Rebuild(CacheEntry entry, int signature)
        {
            var authoring = entry.Authoring;
            if (authoring.BoundsSize.x <= 0 || authoring.BoundsSize.y <= 0)
            {
                ApplyEmptyMesh(entry.Mesh);
                entry.Signature = signature;
                entry.Dirty = false;
                entry.CellCount = 0;
                entry.TileLookupCount = 0;
                entry.RebuildCount++;
                return;
            }

            var bounds = authoring.GetAuthoringBounds();
            var vertices = new List<Vector3>(EstimateVertexCapacity(authoring, bounds));
            var colors = new List<Color>(vertices.Capacity);
            int tileLookups = 0;

            float cellWidth = authoring.Grid.cellSize.x;
            float cellHeight = authoring.Grid.cellSize.y;
            if (authoring.ShowGridGizmo)
                AppendGrid(bounds, cellWidth, cellHeight, vertices, colors);

            for (int localY = 0; localY < bounds.size.y; localY++)
            {
                for (int localX = 0; localX < bounds.size.x; localX++)
                {
                    int tileX = bounds.xMin + localX;
                    int tileY = bounds.yMin + localY;
                    var rect = new Rect(tileX * cellWidth, tileY * cellHeight, cellWidth, cellHeight);

                    if (authoring.ShowMovementGizmo && authoring.MovementTilemap != null)
                    {
                        tileLookups++;
                        var movementTile = authoring.MovementTilemap.GetTile(new Vector3Int(tileX, tileY, 0)) as StageMovementTile;
                        if (movementTile != null && movementTile.MovementFlags != StageCellMovementFlags.None)
                        {
                            AppendCellHatch(
                                rect,
                                -0.003f,
                                ResolveMovementColor(movementTile.MovementFlags),
                                forwardSlash: true,
                                vertices,
                                colors);
                        }
                    }

                    if ((authoring.ShowSourceGizmo || authoring.ShowDepositGizmo) && authoring.RegionTilemap != null)
                    {
                        tileLookups++;
                        var regionTile = authoring.RegionTilemap.GetTile(authoring.GetTilemapCell(localX, localY)) as StageRegionTile;
                        if (regionTile == null
                            || regionTile.RegionSlotIndex <= 0
                            || !authoring.TryResolveStableId(regionTile.RegionKind, regionTile.RegionSlotIndex, out _))
                        {
                            continue;
                        }

                        if (regionTile.RegionKind == StageRegionKind.Source && authoring.ShowSourceGizmo)
                        {
                            AppendCellHatch(
                                rect,
                                -0.002f,
                                new Color(0.1f, 0.75f, 1f, 0.55f),
                                forwardSlash: false,
                                vertices,
                                colors);
                        }
                        else if (regionTile.RegionKind == StageRegionKind.Deposit && authoring.ShowDepositGizmo)
                        {
                            AppendCellHatch(
                                rect,
                                -0.001f,
                                new Color(1f, 0.7f, 0.1f, 0.55f),
                                forwardSlash: false,
                                vertices,
                                colors);
                        }
                    }
                }
            }

            ApplyMesh(entry.Mesh, vertices, colors);
            entry.Signature = signature;
            entry.Dirty = false;
            entry.CellCount = bounds.size.x * bounds.size.y;
            entry.TileLookupCount = tileLookups;
            entry.RebuildCount++;
        }

        private static void AppendGrid(
            BoundsInt bounds,
            float cellWidth,
            float cellHeight,
            List<Vector3> vertices,
            List<Color> colors)
        {
            var color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            float minX = bounds.xMin * cellWidth;
            float maxX = bounds.xMax * cellWidth;
            float minY = bounds.yMin * cellHeight;
            float maxY = bounds.yMax * cellHeight;

            for (int x = 0; x <= bounds.size.x; x++)
            {
                float lineX = (bounds.xMin + x) * cellWidth;
                AppendLine(new Vector3(lineX, minY, 0f), new Vector3(lineX, maxY, 0f), color, vertices, colors);
            }

            for (int y = 0; y <= bounds.size.y; y++)
            {
                float lineY = (bounds.yMin + y) * cellHeight;
                AppendLine(new Vector3(minX, lineY, 0f), new Vector3(maxX, lineY, 0f), color, vertices, colors);
            }
        }

        private static void AppendCellHatch(
            Rect rect,
            float z,
            Color color,
            bool forwardSlash,
            List<Vector3> vertices,
            List<Color> colors)
        {
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
                if (forwardSlash)
                {
                    AppendLine(
                        new Vector3(maxX, Mathf.Lerp(minY, maxY, t), z),
                        new Vector3(Mathf.Lerp(minX, maxX, t), maxY, z),
                        color,
                        vertices,
                        colors);
                }
                else
                {
                    AppendLine(
                        new Vector3(minX, Mathf.Lerp(maxY, minY, t), z),
                        new Vector3(Mathf.Lerp(minX, maxX, t), maxY, z),
                        color,
                        vertices,
                        colors);
                }
            }
        }

        private static void AppendLine(
            Vector3 start,
            Vector3 end,
            Color color,
            List<Vector3> vertices,
            List<Color> colors)
        {
            vertices.Add(start);
            vertices.Add(end);
            colors.Add(color);
            colors.Add(color);
        }

        private static void ApplyMesh(Mesh mesh, List<Vector3> vertices, List<Color> colors)
        {
            mesh.Clear();
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++)
                indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0, calculateBounds: true);
        }

        private static void ApplyEmptyMesh(Mesh mesh)
        {
            mesh.Clear();
        }

        private static Mesh CreateMesh(StageGridAuthoring authoring)
        {
            return new Mesh
            {
                name = $"StageGridSceneVisualization_{authoring.GetInstanceID()}",
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private static Material GetLineMaterial()
        {
            if (_lineMaterial != null)
                return _lineMaterial;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    if (!_warnedMissingLineShader)
                    {
                        Debug.LogWarning("StageGridSceneVisualizationRenderer could not find a line rendering shader. Stage grid Scene View visualization will be skipped.");
                        _warnedMissingLineShader = true;
                    }

                    return null;
                }

                if (!_warnedFallbackLineShader)
                {
                    Debug.LogWarning("StageGridSceneVisualizationRenderer could not find Hidden/Internal-Colored and will use Sprites/Default as a fallback line shader.");
                    _warnedFallbackLineShader = true;
                }
            }

            bool usesInternalColored = shader.name == "Hidden/Internal-Colored";
            if (usesInternalColored)
                _warnedMissingLineShader = false;

            _lineMaterial = new Material(shader)
            {
                name = "StageGridSceneVisualizationMaterial",
                hideFlags = HideFlags.HideAndDontSave,
            };

            if (!usesInternalColored)
                return _lineMaterial;

            _lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            _lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            _lineMaterial.SetInt("_Cull", (int)CullMode.Off);
            _lineMaterial.SetInt("_ZWrite", 0);
            return _lineMaterial;
        }

        private static int ComputeSignature(StageGridAuthoring authoring)
        {
            unchecked
            {
                int hash = 17;
                AddHash(ref hash, authoring.Grid);
                AddHash(ref hash, authoring.MovementTilemap);
                AddHash(ref hash, authoring.RegionTilemap);
                AddHash(ref hash, authoring.BoundsMinCell);
                AddHash(ref hash, authoring.BoundsSize);
                AddHash(ref hash, authoring.Grid.cellSize);
                AddHash(ref hash, authoring.ShowGridGizmo);
                AddHash(ref hash, authoring.ShowMovementGizmo);
                AddHash(ref hash, authoring.ShowSourceGizmo);
                AddHash(ref hash, authoring.ShowDepositGizmo);
                AddMappingsHash(ref hash, authoring.SourceRegionMappings);
                AddMappingsHash(ref hash, authoring.DepositRegionMappings);
                return hash;
            }
        }

        private static void AddMappingsHash(ref int hash, StageRegionSlotMapping[] mappings)
        {
            int count = mappings?.Length ?? 0;
            AddHash(ref hash, count);
            for (int i = 0; i < count; i++)
            {
                AddHash(ref hash, mappings[i].RegionSlotIndex);
                AddHash(ref hash, mappings[i].StableId);
            }
        }

        private static void AddHash(ref int hash, UnityEngine.Object value)
        {
            hash = (hash * 31) + (value != null ? value.GetInstanceID() : 0);
        }

        private static void AddHash(ref int hash, bool value)
        {
            hash = (hash * 31) + (value ? 1 : 0);
        }

        private static void AddHash(ref int hash, int value)
        {
            hash = (hash * 31) + value;
        }

        private static void AddHash(ref int hash, uint value)
        {
            hash = (hash * 31) + (int)value;
        }

        private static void AddHash(ref int hash, Vector2Int value)
        {
            AddHash(ref hash, value.x);
            AddHash(ref hash, value.y);
        }

        private static void AddHash(ref int hash, Vector3 value)
        {
            AddHash(ref hash, value.x.GetHashCode());
            AddHash(ref hash, value.y.GetHashCode());
            AddHash(ref hash, value.z.GetHashCode());
        }

        private static int EstimateVertexCapacity(StageGridAuthoring authoring, BoundsInt bounds)
        {
            int gridVertices = authoring.ShowGridGizmo
                ? ((bounds.size.x + 1) + (bounds.size.y + 1)) * 2
                : 0;
            return gridVertices + (bounds.size.x * bounds.size.y * 8);
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

        private static void OnTilemapTileChanged(Tilemap tilemap, Tilemap.SyncTile[] syncTiles)
        {
            foreach (var pair in Entries)
            {
                var authoring = pair.Value.Authoring;
                if (authoring != null
                    && (authoring.MovementTilemap == tilemap || authoring.RegionTilemap == tilemap))
                {
                    pair.Value.Dirty = true;
                }
            }
        }

        private static void MarkAllDirty()
        {
            foreach (var pair in Entries)
                pair.Value.Dirty = true;
        }

        private static void RemoveStaleEntries()
        {
            var staleIds = new List<int>();
            foreach (var pair in Entries)
            {
                if (pair.Value.Authoring == null)
                    staleIds.Add(pair.Key);
            }

            for (int i = 0; i < staleIds.Count; i++)
            {
                int id = staleIds[i];
                DestroyEntry(Entries[id]);
                Entries.Remove(id);
            }
        }

        private static void DisposeAll()
        {
            foreach (var pair in Entries)
                DestroyEntry(pair.Value);
            Entries.Clear();

            if (_lineMaterial != null)
                UnityEngine.Object.DestroyImmediate(_lineMaterial);
            _lineMaterial = null;
        }

        private static void DestroyEntry(CacheEntry entry)
        {
            if (entry?.Mesh != null)
                UnityEngine.Object.DestroyImmediate(entry.Mesh);
        }
    }
}
