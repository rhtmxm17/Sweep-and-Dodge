using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Editor
{
    internal static class BulletDefinitionIdUtility
    {
        public static bool Generate(BulletDefinitionSO definition)
        {
            if (definition == null)
                return false;

            string path = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrEmpty(path))
                return false;

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return false;

            int nextId = GenerateUniqueIdFromGuid(guid, definition);
            if (definition.DefinitionId == nextId)
                return false;

            definition.Editor_SetDefinitionId(nextId);
            return true;
        }

        private static int GenerateUniqueIdFromGuid(string guid, BulletDefinitionSO self)
        {
            int id = ComputeGuidHash(guid);
            if (id == 0)
                id = 1;

            var used = CollectUsedIds(self);
            while (used.Contains(id) || id == 0)
            {
                unchecked
                {
                    id++;
                    if (id == 0)
                        id = 1;
                }
            }

            return id;
        }

        private static HashSet<int> CollectUsedIds(BulletDefinitionSO except)
        {
            var used = new HashSet<int>();
            string[] guids = AssetDatabase.FindAssets("t:BulletDefinitionSO");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<BulletDefinitionSO>(path);
                if (asset == null || asset == except)
                    continue;

                if (asset.DefinitionId != 0)
                    used.Add(asset.DefinitionId);
            }

            return used;
        }

        private static int ComputeGuidHash(string guid)
        {
            unchecked
            {
                const int fnvPrime = 16777619;
                int hash = (int)2166136261;
                for (int i = 0; i < guid.Length; i++)
                {
                    hash ^= guid[i];
                    hash *= fnvPrime;
                }

                if (hash == int.MinValue)
                    hash = int.MaxValue;

                return Mathf.Abs(hash);
            }
        }
    }
}
