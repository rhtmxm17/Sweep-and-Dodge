using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace SweepNDodge.DotsBullets.Editor
{
    /// <summary>
    /// Builds the project's NanumSquare Neo TMP assets without pre-baking all 11,172 Hangul syllables.
    /// Current project Hangul is prewarmed, while Dynamic Multi Atlas handles future content on demand.
    /// </summary>
    public static class NanumSquareNeoFontAssetBuilder
    {
        public const string RegularFontAssetPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-Regular SDF.asset";

        public const string BoldFontAssetPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-Bold SDF.asset";

        private const string RegularSourceFontPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-bRg.ttf";

        private const string BoldSourceFontPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-cBd.ttf";

        private const string RuntimeUiRootPrefabPath =
            "Assets/_Project/04_Prefabs/UI/RuntimeUiRoot.prefab";

        private const int SamplingPointSize = 72;
        private const int AtlasPadding = 8;
        private const int AtlasSize = 2048;
        private const int BoldWeightIndex = 7;

        private static readonly HashSet<string> TextAssetExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".asset",
            ".cs",
            ".csv",
            ".json",
            ".prefab",
            ".txt",
            ".unity",
            ".uss",
            ".uxml",
        };

        [MenuItem("Tools/Project/UI/Rebuild NanumSquare Neo TMP Font Assets")]
        public static void Rebuild()
        {
            ConfigureTmpSettingsBeforeBuild();

            var prewarmCharacters = CollectPrewarmCharacters();
            var regular = BuildFontAsset(RegularSourceFontPath, RegularFontAssetPath, prewarmCharacters);
            var bold = BuildFontAsset(BoldSourceFontPath, BoldFontAssetPath, prewarmCharacters);

            ConfigureBoldTypeface(regular, bold);
            ConfigureTmpSettings(regular);
            ApplyToRuntimeUiPrefab(regular);

            EditorUtility.SetDirty(regular);
            EditorUtility.SetDirty(bold);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"NanumSquare Neo TMP font assets rebuilt. " +
                $"Prewarmed characters: {prewarmCharacters.Length}, " +
                $"Regular atlases: {regular.atlasTextureCount}, Bold atlases: {bold.atlasTextureCount}.");
        }

        private static TMP_FontAsset BuildFontAsset(
            string sourceFontPath,
            string fontAssetPath,
            uint[] prewarmCharacters)
        {
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourceFontPath);
            if (sourceFont == null)
                throw new InvalidOperationException($"Source font was not found: {sourceFontPath}");

            var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
            if (fontAsset != null
                && (fontAsset.atlasTextures == null
                    || fontAsset.atlasTextures.Length == 0
                    || fontAsset.atlasTextures[0] == null
                    || fontAsset.material == null))
            {
                AssetDatabase.DeleteAsset(fontAssetPath);
                fontAsset = null;
            }

            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    SamplingPointSize,
                    AtlasPadding,
                    GlyphRenderMode.SDFAA,
                    AtlasSize,
                    AtlasSize,
                    AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (fontAsset == null)
                    throw new InvalidOperationException($"TMP font asset creation failed: {sourceFontPath}");

                fontAsset.name = Path.GetFileNameWithoutExtension(fontAssetPath);
                var atlasTexture = fontAsset.atlasTextures[0];
                var material = fontAsset.material;

                AssetDatabase.CreateAsset(fontAsset, fontAssetPath);
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                AssetDatabase.AddObjectToAsset(material, fontAsset);
                EditorUtility.SetDirty(fontAsset);
                AssetDatabase.SaveAssets();
            }
            else
            {
                var serializedFontAsset = new SerializedObject(fontAsset);
                serializedFontAsset.FindProperty("m_SourceFontFileGUID").stringValue =
                    AssetDatabase.AssetPathToGUID(sourceFontPath);
                serializedFontAsset.FindProperty("m_SourceFontFile").objectReferenceValue = sourceFont;
                serializedFontAsset.FindProperty("m_AtlasWidth").intValue = AtlasSize;
                serializedFontAsset.FindProperty("m_AtlasHeight").intValue = AtlasSize;
                serializedFontAsset.FindProperty("m_AtlasPadding").intValue = AtlasPadding;
                serializedFontAsset.FindProperty("m_AtlasRenderMode").intValue = (int)GlyphRenderMode.SDFAA;
                serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();

                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;
                fontAsset.ClearFontAssetData(setAtlasSizeToZero: true);
            }

            SetClearDynamicDataOnBuild(fontAsset, false);

            if (!fontAsset.TryAddCharacters(prewarmCharacters, out uint[] missingCharacters, includeFontFeatures: true))
            {
                string missing = string.Join(", ", missingCharacters.Select(value => $"U+{value:X4}"));
                throw new InvalidOperationException(
                    $"The source font is missing required prewarm characters ({fontAsset.name}): {missing}");
            }

            EditorUtility.SetDirty(fontAsset);
            return fontAsset;
        }

        private static uint[] CollectPrewarmCharacters()
        {
            var characters = new SortedSet<uint>();

            for (uint value = 0x20; value <= 0x7E; value++)
                characters.Add(value);

            uint[] commonKoreanPunctuation =
            {
                0x00A0, 0x00B7,
                0x2013, 0x2014, 0x2018, 0x2019, 0x201C, 0x201D, 0x2026,
                0x2190, 0x2191, 0x2192, 0x2193,
                0x3001, 0x3002, 0x300A, 0x300B,
                0x300C, 0x300D, 0x300E, 0x300F,
            };

            for (int i = 0; i < commonKoreanPunctuation.Length; i++)
                characters.Add(commonKoreanPunctuation[i]);

            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (!assetPath.StartsWith("Assets/_Project/", StringComparison.Ordinal)
                    || !TextAssetExtensions.Contains(Path.GetExtension(assetPath))
                    || !File.Exists(assetPath))
                {
                    continue;
                }

                string content = File.ReadAllText(assetPath, Encoding.UTF8);
                for (int charIndex = 0; charIndex < content.Length; charIndex++)
                {
                    uint value = content[charIndex];
                    if (IsHangul(value))
                        characters.Add(value);
                }
            }

            return characters.ToArray();
        }

        private static bool IsHangul(uint value)
        {
            return value is >= 0xAC00 and <= 0xD7A3
                or >= 0x3131 and <= 0x318E
                or >= 0x1100 and <= 0x11FF;
        }

        private static void ConfigureBoldTypeface(TMP_FontAsset regular, TMP_FontAsset bold)
        {
            var weightTable = regular.fontWeightTable;
            if (weightTable == null || weightTable.Length <= BoldWeightIndex)
                throw new InvalidOperationException("TMP font weight table does not contain the 700 weight slot.");

            var boldPair = weightTable[BoldWeightIndex];
            boldPair.regularTypeface = bold;
            weightTable[BoldWeightIndex] = boldPair;
        }

        private static void ConfigureTmpSettingsBeforeBuild()
        {
            var settings = TMP_Settings.instance;
            if (settings == null)
                throw new InvalidOperationException("TMP Settings could not be loaded.");

            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("m_ClearDynamicDataOnBuild").boolValue = false;
            serializedSettings.FindProperty("m_UseModernHangulLineBreakingRules").boolValue = true;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureTmpSettings(TMP_FontAsset regular)
        {
            TMP_Settings.defaultFontAsset = regular;
            TMP_Settings.useModernHangulLineBreakingRules = true;

            var settings = TMP_Settings.instance;
            EditorUtility.SetDirty(settings);
        }

        private static void SetClearDynamicDataOnBuild(TMP_FontAsset fontAsset, bool value)
        {
            var serializedFontAsset = new SerializedObject(fontAsset);
            serializedFontAsset.FindProperty("m_ClearDynamicDataOnBuild").boolValue = value;
            serializedFontAsset.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ApplyToRuntimeUiPrefab(TMP_FontAsset regular)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(RuntimeUiRootPrefabPath);
            try
            {
                var textComponents = prefabRoot.GetComponentsInChildren<TMP_Text>(includeInactive: true);
                for (int i = 0; i < textComponents.Length; i++)
                {
                    textComponents[i].font = regular;
                    EditorUtility.SetDirty(textComponents[i]);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, RuntimeUiRootPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }
    }
}
