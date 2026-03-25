using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    public sealed class StageRegionPaintEditorWindow : EditorWindow
    {
        private enum PaintMode
        {
            Paint,
            Erase,
        }

        private StageRegionPaintAsset _targetAsset;
        private StageGridAuthoring _gridAuthoring;
        private PaintMode _mode = PaintMode.Paint;
        private uint _selectedStableId = 1u;
        private Vector2 _scroll;

        public static void Open(StageRegionPaintAsset asset)
        {
            var window = GetWindow<StageRegionPaintEditorWindow>("Stage Region Paint");
            window._targetAsset = asset;
            window.Focus();
        }

        private void OnGUI()
        {
            _targetAsset = (StageRegionPaintAsset)EditorGUILayout.ObjectField("Target Asset", _targetAsset, typeof(StageRegionPaintAsset), false);
            _gridAuthoring = (StageGridAuthoring)EditorGUILayout.ObjectField("Grid Authoring", _gridAuthoring, typeof(StageGridAuthoring), true);
            _selectedStableId = (uint)Mathf.Max(1, EditorGUILayout.IntField("Selected StableId", (int)_selectedStableId));
            _mode = (PaintMode)EditorGUILayout.EnumPopup("Mode", _mode);

            if (_targetAsset == null)
            {
                EditorGUILayout.HelpBox("Assign a StageRegionPaintAsset to edit cells.", MessageType.Info);
                return;
            }

            _targetAsset.EnsureShape();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Resize To GridAuthoring"))
                    ResizeToGridAuthoring();
                if (GUILayout.Button("Clear All"))
                    ClearAll();
                if (GUILayout.Button("Fill Selection"))
                    FillAll();
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawGrid();
            EditorGUILayout.EndScrollView();
        }

        private void ResizeToGridAuthoring()
        {
            if (_gridAuthoring == null || _gridAuthoring.MovementTilemap == null)
                return;

            var bounds = _gridAuthoring.MovementTilemap.cellBounds;
            Undo.RecordObject(_targetAsset, "Resize Stage Region Paint To Grid");
            _targetAsset.Resize(bounds.size.x, bounds.size.y);
            EditorUtility.SetDirty(_targetAsset);
        }

        private void ClearAll()
        {
            Undo.RecordObject(_targetAsset, "Clear Stage Region Paint");
            _targetAsset.ClearAll();
            EditorUtility.SetDirty(_targetAsset);
        }

        private void FillAll()
        {
            Undo.RecordObject(_targetAsset, "Fill Stage Region Paint");
            for (int y = 0; y < _targetAsset.Height; y++)
            {
                for (int x = 0; x < _targetAsset.Width; x++)
                    _targetAsset.SetCell(x, y, _mode == PaintMode.Paint ? _selectedStableId : 0u);
            }

            EditorUtility.SetDirty(_targetAsset);
        }

        private void DrawGrid()
        {
            for (int y = _targetAsset.Height - 1; y >= 0; y--)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int x = 0; x < _targetAsset.Width; x++)
                    {
                        uint current = _targetAsset.GetCell(x, y);
                        string label = current == 0 ? "." : current.ToString();
                        if (GUILayout.Button(label, GUILayout.Width(38f), GUILayout.Height(24f)))
                            PaintCell(x, y);
                    }
                }
            }
        }

        private void PaintCell(int x, int y)
        {
            Undo.RecordObject(_targetAsset, "Paint Stage Region Cell");
            _targetAsset.SetCell(x, y, _mode == PaintMode.Paint ? _selectedStableId : 0u);
            EditorUtility.SetDirty(_targetAsset);
        }
    }
}
