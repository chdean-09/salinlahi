using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// Ugat QA 2026-09-16. The end screens got their own canvas above the HUD, and the first
    /// attempt backed them with ONE opaque scrim owned by that canvas. A canvas-level scrim is
    /// active from scene load and nothing ever toggled it, so it covered the gameplay HUD and
    /// swallowed every tap for the whole level — the game was unplayable and the EditMode suite
    /// was entirely green. Only a render of the HUD with both panels closed showed it.
    ///
    /// These pin the shape that cannot regress that way: the backdrop belongs to a PANEL, so it is
    /// visible exactly when its panel is, and EndScreenCanvas owns nothing that is always on.
    /// <see cref="SettingsPanelTests.OnEnable_ReusesExistingBackdropBehindSliderCard"/> guards the
    /// same class of defect for the settings card.
    /// </summary>
    [TestFixture]
    public sealed class EndScreenBackdropSceneTests
    {
        private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";

        [TestCase("VictoryPanel")]
        [TestCase("DefeatPanel")]
        public void EachEndScreenPanel_OwnsAnOpaqueBackdropBehindItsContent(string panelName)
        {
            GameObject panel = OpenAndFind(panelName);

            Transform backdrop = panel.transform.Find("Backdrop");
            Assert.IsNotNull(
                backdrop,
                panelName + " has no Backdrop child. Without one the HUD shows around the panel, "
                + "because the panel is centred rather than full-screen.");

            Assert.AreEqual(
                0,
                backdrop.GetSiblingIndex(),
                "The backdrop must be the first child so it draws BEHIND the panel's own content.");

            var image = backdrop.GetComponent<Image>();
            Assert.IsNotNull(image, "The backdrop needs an Image or it covers nothing.");
            Assert.AreEqual(
                1f,
                image.color.a,
                0.001f,
                "The backdrop must be fully opaque; a translucent one still shows the HUD through it.");

            var rect = backdrop.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, rect.anchorMin, "Backdrop must stretch to its panel.");
            Assert.AreEqual(Vector2.one, rect.anchorMax, "Backdrop must stretch to its panel.");
            Assert.Less(
                rect.offsetMin.x, -500f,
                "The backdrop must extend well past the panel, or the HUD shows around a centred "
                + "panel at aspect ratios taller or wider than the reference.");
            Assert.Greater(rect.offsetMax.y, 500f, "Same, on the opposite edge.");
        }

        /// <summary>
        /// The actual regression guard: EndScreenCanvas must own no always-on full-screen graphic.
        /// Its children are the two panels, both of which their controllers deactivate on Awake.
        /// </summary>
        [Test]
        public void EndScreenCanvas_HasNoAlwaysOnFullScreenGraphic()
        {
            GameObject canvas = OpenAndFind("EndScreenCanvas");

            foreach (Transform child in canvas.transform)
            {
                if (!child.gameObject.activeSelf)
                    continue;

                var graphic = child.GetComponent<Graphic>();
                Assert.IsNull(
                    graphic,
                    "'" + child.name + "' is an active graphic parented directly to EndScreenCanvas, "
                    + "which renders above the HUD from scene load and blocks gameplay. A backdrop "
                    + "belongs inside the panel it backs, so it is shown and hidden with it.");
            }
        }

        private static GameObject OpenAndFind(string name)
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name)
                        return t.gameObject;

            Assert.Fail(name + " not found in " + GameplayScenePath + ".");
            return null;
        }
    }
}
