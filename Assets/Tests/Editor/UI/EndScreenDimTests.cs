using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// The end-screen translucent dim. The authored layers stay opaque in the scenes —
    /// EndScreenBackdropSceneTests pins that — so these exercise the runtime retint
    /// against bare panels shaped like the two authored variants: Gameplay's
    /// Backdrop+Background pair and the tutorial scene's Background-only panel.
    /// </summary>
    [TestFixture]
    public sealed class EndScreenDimTests
    {
        private GameObject _panelObject;

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null)
                Object.DestroyImmediate(_panelObject);
        }

        [Test]
        public void Apply_DimsFirstBackdropLayer_AndDisablesExtras()
        {
            GameObject panel = CreatePanel();
            Image backdrop = AddLayer(panel, "Backdrop", new Color(0.07f, 0.08f, 0.10f, 1f));
            Image background = AddLayer(panel, "Background", new Color(0.1f, 0.1f, 0.15f, 0.85f));

            EndScreenDim.Apply(panel, EndScreenDim.VictoryTint);

            Assert.That(backdrop.color, Is.EqualTo(EndScreenDim.VictoryTint),
                "The Backdrop layer must take the dim tint so the world shows through at ~80%.");
            Assert.IsTrue(backdrop.enabled, "The dim layer must render.");
            Assert.IsTrue(backdrop.raycastTarget,
                "The dim must keep eating clicks — a translucent backdrop still blocks the world.");
            Assert.IsFalse(background.enabled,
                "The second layer must be disabled — stacked alphas would compound past 80%.");
        }

        [Test]
        public void Apply_BackgroundOnlyPanel_DimsThatLayer()
        {
            // Level_01_Tutorial.unity's panels author no Backdrop — Background is the only layer.
            GameObject panel = CreatePanel();
            Image background = AddLayer(panel, "Background", new Color(0.1f, 0.1f, 0.15f, 0.85f));

            EndScreenDim.Apply(panel, EndScreenDim.DefeatTint);

            Assert.That(background.color, Is.EqualTo(EndScreenDim.DefeatTint));
            Assert.IsTrue(background.enabled);
        }

        [Test]
        public void Apply_PanelWithoutLayers_CreatesRuntimeDim()
        {
            GameObject panel = CreatePanel();
            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(panel.transform, false);

            EndScreenDim.Apply(panel, EndScreenDim.VictoryTint);

            Transform dim = panel.transform.Find(EndScreenDim.RuntimeDimName);
            Assert.IsNotNull(dim, "A panel with no authored layers still needs the dim.");
            Assert.AreEqual(0, dim.GetSiblingIndex(),
                "The dim must draw behind the panel's own content.");
            var image = dim.GetComponent<Image>();
            Assert.That(image.color, Is.EqualTo(EndScreenDim.VictoryTint));
            Assert.IsTrue(image.raycastTarget);
            var rect = dim.GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
        }

        [Test]
        public void Apply_PrefersBackdropName_WhenBackgroundIsListedFirst()
        {
            GameObject panel = CreatePanel();
            Image background = AddLayer(panel, "Background", Color.black);
            Image backdrop = AddLayer(panel, "Backdrop", Color.black);

            EndScreenDim.Apply(panel, EndScreenDim.VictoryTint);

            Assert.IsTrue(backdrop.enabled,
                "Backdrop is the authored scrim; sibling order must not decide which layer dims.");
            Assert.IsFalse(background.enabled);
        }

        [Test]
        public void Apply_IsIdempotent_NoDuplicateRuntimeDim()
        {
            GameObject panel = CreatePanel();

            EndScreenDim.Apply(panel, EndScreenDim.VictoryTint);
            EndScreenDim.Apply(panel, EndScreenDim.VictoryTint);

            int dims = 0;
            foreach (Transform child in panel.transform)
                if (child.name == EndScreenDim.RuntimeDimName)
                    dims++;
            Assert.AreEqual(1, dims,
                "Show() runs on every presentation — a second Apply must not stack another dim.");
        }

        [Test]
        public void VictoryAndDefeatTints_AreMoodGraded_AtTheChosenOpacity()
        {
            Assert.AreEqual(0.80f, EndScreenDim.VictoryTint.a, 0.001f,
                "User direction: ~80% dim — world visible but pushed back.");
            Assert.AreEqual(0.80f, EndScreenDim.DefeatTint.a, 0.001f);
            Assert.Greater(EndScreenDim.DefeatTint.r, EndScreenDim.VictoryTint.r,
                "Defeat leans red to sit under the DEFEAT banner; victory stays neutral navy.");
        }

        private GameObject CreatePanel()
        {
            _panelObject = new GameObject("Panel_Test", typeof(RectTransform));
            return _panelObject;
        }

        private static Image AddLayer(GameObject panel, string name, Color color)
        {
            var layer = new GameObject(name, typeof(RectTransform), typeof(Image));
            layer.transform.SetParent(panel.transform, false);
            var image = layer.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }
    }
}
