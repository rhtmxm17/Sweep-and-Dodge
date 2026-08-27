using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace SweepNDodge.DotsBullets.Tests
{
    public class NanumSquareNeoFontAssetTests
    {
        private const string RegularFontAssetPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-Regular SDF.asset";

        private const string BoldFontAssetPath =
            "Assets/_Project/05_Content/Fonts/NanumSquareNeo/NanumSquareNeo-Bold SDF.asset";

        private const string RuntimeUiRootPrefabPath =
            "Assets/_Project/04_Prefabs/UI/RuntimeUiRoot.prefab";

        [Test]
        public void NanumSquareNeoFontAssets_SupportKoreanWithoutFullStaticAtlas()
        {
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontAssetPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontAssetPath);

            Assert.That(regular, Is.Not.Null);
            Assert.That(bold, Is.Not.Null);
            Assert.That(regular.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(bold.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Dynamic));
            Assert.That(regular.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(bold.isMultiAtlasTexturesEnabled, Is.True);
            Assert.That(regular.sourceFontFile, Is.Not.Null);
            Assert.That(bold.sourceFontFile, Is.Not.Null);
            Assert.That(regular.HasCharacters("한글 폰트 적용 확인"), Is.True);
            Assert.That(bold.HasCharacters("한글 폰트 적용 확인"), Is.True);
        }

        [Test]
        public void RuntimeUiRoot_UsesNanumSquareNeoRegularAndRealBoldTypeface()
        {
            var regular = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(RegularFontAssetPath);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontAssetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RuntimeUiRootPrefabPath);

            Assert.That(regular, Is.Not.Null);
            Assert.That(bold, Is.Not.Null);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(regular.fontWeightTable[7].regularTypeface, Is.SameAs(bold));

            var textComponents = prefab.GetComponentsInChildren<TMP_Text>(includeInactive: true);
            Assert.That(textComponents, Is.Not.Empty);
            for (int i = 0; i < textComponents.Length; i++)
                Assert.That(textComponents[i].font, Is.SameAs(regular), textComponents[i].name);

            Assert.That(TMP_Settings.defaultFontAsset, Is.SameAs(regular));
            Assert.That(TMP_Settings.clearDynamicDataOnBuild, Is.False);
            Assert.That(TMP_Settings.useModernHangulLineBreakingRules, Is.True);
        }
    }
}
