using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class SpotlightOverlayGraphicTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void SetCutout_StoresRectAndMarksGraphicVisible()
        {
            SpotlightOverlayGraphic graphic = CreateGraphic();
            Rect cutout = new Rect(10f, 20f, 100f, 80f);

            graphic.SetCutout(cutout);

            Assert.AreEqual(cutout, graphic.CutoutRect);
            Assert.IsTrue(graphic.HasCutout);
            Assert.IsFalse(graphic.raycastTarget);
        }

        [Test]
        public void ClearCutout_RemovesCutout()
        {
            SpotlightOverlayGraphic graphic = CreateGraphic();
            graphic.SetCutout(new Rect(10f, 20f, 100f, 80f));

            graphic.ClearCutout();

            Assert.IsFalse(graphic.HasCutout);
            Assert.AreEqual(Rect.zero, graphic.CutoutRect);
        }

        [Test]
        public void SetCutout_WithNegativeSize_NormalizesToPositiveSize()
        {
            SpotlightOverlayGraphic graphic = CreateGraphic();

            graphic.SetCutout(new Rect(100f, 80f, -40f, -20f));

            Assert.AreEqual(new Rect(60f, 60f, 40f, 20f), graphic.CutoutRect);
        }

        private SpotlightOverlayGraphic CreateGraphic()
        {
            _root = new GameObject("SpotlightOverlayGraphic_Test", typeof(RectTransform));
            RectTransform rect = _root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(320f, 480f);
            SpotlightOverlayGraphic graphic = _root.AddComponent<SpotlightOverlayGraphic>();
            graphic.color = new Color(0f, 0f, 0f, 0.78f);
            return graphic;
        }
    }
}
