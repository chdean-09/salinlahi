using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class CampaignArtworkSceneTests
    {
        private const string ArtPath = "Assets/Art/UI/Campaign/";
        [TestCase(1, "ui_era_one_banner")]
        [TestCase(2, "ui_era_two_banner")]
        [TestCase(3, "ui_era_three_banner")]
        public void Era_UsesSuppliedBanner(int eraNumber, string spriteName)
        {
            var era = AssetDatabase.LoadAssetAtPath<EraConfigSO>(
                "Assets/ScriptableObjects/Themes/Era_0" + eraNumber + ".asset");
            Assert.IsNotNull(era);
            Assert.AreSame(ImportedSprite(spriteName), era.bannerSprite);
        }

        [TestCase("ui_era_one_banner")]
        [TestCase("ui_era_two_banner")]
        [TestCase("ui_era_three_banner")]
        public void EraBanner_ImportsTheCompleteArtworkWithoutDownscaling(string spriteName)
        {
            string path = ArtPath + spriteName + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Sprite sprite = ImportedSprite(spriteName);
            Assert.AreEqual(new Rect(0f, 0f, width, height), sprite.rect,
                "The supplied glow and border must not be cropped by the previous sprite rectangle.");
            Assert.AreEqual(width, sprite.texture.width);
            Assert.AreEqual(height, sprite.texture.height);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
        }

        [TestCase("_prevEraButton", "ui_era_arrow_left", false)]
        [TestCase("_nextEraButton", "ui_era_arrow_right", true)]
        public void LevelSelect_UsesSuppliedArrowAndInitialBoundaryState(
            string field, string spriteName, bool interactable)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/LevelSelect.unity", OpenSceneMode.Single);
            LevelSelectUI ui = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelSelectUI>(true)).Single();
            var button = new SerializedObject(ui).FindProperty(field).objectReferenceValue as Button;
            Assert.IsNotNull(button);
            Image image = button.GetComponent<Image>();
            Assert.AreSame(ImportedSprite(spriteName), image.sprite);
            Assert.IsTrue(image.preserveAspect);
            Assert.AreEqual(interactable, button.interactable);
            Assert.Less(button.colors.disabledColor.a, button.colors.normalColor.a);
        }

        [TestCase("Gameplay", "VictoryPanel", "VictoryImage", "ui_victory_banner")]
        [TestCase("Gameplay", "DefeatPanel", "DefeatImage", "ui_defeat_banner")]
        [TestCase("Level_01_Tutorial", "VictoryPanel", "VictoryImage", "ui_victory_banner")]
        [TestCase("Level_01_Tutorial", "DefeatPanel", "DefeatImage", "ui_defeat_banner")]
        public void Results_UseSuppliedBannerWithoutTintOrBlockedClicks(
            string sceneName, string panelName, string imageName, string spriteName)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/_Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            Transform panel = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(transform => transform.name == panelName);
            Transform banner = panel.Find(imageName);
            Assert.IsNotNull(banner);
            Image image = banner.GetComponent<Image>();
            Assert.IsNotNull(image);
            Assert.AreSame(ImportedSprite(spriteName), image.sprite);
            Assert.AreEqual(Color.white, image.color);
            Assert.IsTrue(image.preserveAspect);
            Assert.IsFalse(image.raycastTarget);
        }

        private static Sprite ImportedSprite(string name)
        {
            Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(ArtPath + name + ".png")
                .OfType<Sprite>().SingleOrDefault(asset => asset.name == name);
            Assert.IsNotNull(sprite, "Supplied artwork did not resolve to an imported sprite: " + name);
            return sprite;
        }
    }
}
