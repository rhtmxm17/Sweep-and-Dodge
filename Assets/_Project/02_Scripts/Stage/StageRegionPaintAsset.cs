using System;
using UnityEngine;

namespace SweepNDodge.DotsBullets
{
    [CreateAssetMenu(menuName = "SweepNDodge/Stage/Stage Region Paint", fileName = "srp_")]
    public sealed class StageRegionPaintAsset : ScriptableObject
    {
        public StageRegionKind RegionKind;
        [Min(1)] public int Width = 1;
        [Min(1)] public int Height = 1;
        [SerializeField] private uint[] cells = { 0u };

        public uint[] Cells
        {
            get => cells;
            set => cells = value;
        }

        public int CellCount => Math.Max(0, Width) * Math.Max(0, Height);

        public void Resize(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            var next = new uint[width * height];
            int copyWidth = Math.Min(width, Width);
            int copyHeight = Math.Min(height, Height);
            for (int y = 0; y < copyHeight; y++)
            {
                int srcOffset = y * Math.Max(1, Width);
                int dstOffset = y * width;
                for (int x = 0; x < copyWidth; x++)
                    next[dstOffset + x] = GetCellUnchecked(srcOffset + x);
            }

            Width = width;
            Height = height;
            cells = next;
        }

        public void ClearAll()
        {
            EnsureShape();
            Array.Clear(cells, 0, cells.Length);
        }

        public void EnsureShape()
        {
            Width = Math.Max(1, Width);
            Height = Math.Max(1, Height);
            int required = Width * Height;
            if (cells == null || cells.Length != required)
                Array.Resize(ref cells, required);
        }

        public uint GetCell(int x, int y)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return 0u;

            EnsureShape();
            return cells[(y * Width) + x];
        }

        public void SetCell(int x, int y, uint stableId)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height)
                return;

            EnsureShape();
            cells[(y * Width) + x] = stableId;
        }

        private uint GetCellUnchecked(int index)
        {
            if (cells == null || index < 0 || index >= cells.Length)
                return 0u;

            return cells[index];
        }

        private void OnValidate()
        {
            EnsureShape();
        }
    }
}
