using UnityEditor;

namespace SweepNDodge.DotsBullets.Editor
{
    public class BulletDefinitionAssetPostprocessor : AssetPostprocessor
    {
        private static bool _isProcessing;
        private static bool _saveScheduled;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (_isProcessing)
                return;

            _isProcessing = true;
            bool dirty = ProcessPaths(importedAssets);
            _isProcessing = false;

            if (dirty)
                ScheduleSaveAssets();
        }

        private static bool ProcessPaths(string[] assetPaths)
        {
            bool dirty = false;
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                var definition = AssetDatabase.LoadAssetAtPath<BulletDefinitionSO>(path);
                if (definition == null)
                    continue;

                if (BulletDefinitionIdUtility.Generate(definition))
                    dirty = true;
            }

            return dirty;
        }

        private static void ScheduleSaveAssets()
        {
            if (_saveScheduled)
                return;

            _saveScheduled = true;
            EditorApplication.delayCall += () =>
            {
                _saveScheduled = false;
                AssetDatabase.SaveAssets();
            };
        }
    }
}
