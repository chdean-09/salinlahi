using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class MainMenuLogoTests
    {
        private const string MainMenuScenePath = "Assets/_Scenes/MainMenu.unity";
        private const string LogoAssetPath = "Assets/Art/UI/ui_mainmenu_logo.png";

        [Test]
        public void MainMenuTitleSlotUsesLogoImage()
        {
            Sprite logoSprite = LoadLogoSprite();

            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            Assert.IsNull(GameObject.Find("TitleText"), "Legacy title text should be replaced by the logo image.");

            GameObject logoObject = GameObject.Find("MainMenuLogo");
            Assert.IsNotNull(logoObject, "Main menu should have a logo object in the title slot.");

            Image logoImage = logoObject.GetComponent<Image>();
            Assert.IsNotNull(logoImage, "Main menu logo object should render through a UI Image.");
            Assert.AreSame(logoSprite, logoImage.sprite);
            Assert.IsTrue(logoImage.preserveAspect);
            Assert.IsFalse(logoImage.raycastTarget);
        }

        private static Sprite LoadLogoSprite()
        {
            Sprite sprite = AssetDatabase
                .LoadAllAssetsAtPath(LogoAssetPath)
                .OfType<Sprite>()
                .FirstOrDefault(asset => asset.name == "ui_mainmenu_logo_0")
                ?? AssetDatabase.LoadAssetAtPath<Sprite>(LogoAssetPath);

            Assert.IsNotNull(sprite, $"Expected logo sprite at {LogoAssetPath}.");
            return sprite;
        }
    }
}
