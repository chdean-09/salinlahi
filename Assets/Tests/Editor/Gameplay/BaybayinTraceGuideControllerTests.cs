using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class BaybayinTraceGuideControllerTests
    {
        private GameObject _root;
        private BaybayinTraceGuideController _guide;
        private CanvasGroup _group;
        private Image _glyphImage;
        private Image _tracePathImage;
        private Image _startMarkerImage;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Trace Guide Test Root");
            _guide = _root.AddComponent<BaybayinTraceGuideController>();
            _group = _root.AddComponent<CanvasGroup>();
            _glyphImage = AddImage("Glyph");
            _tracePathImage = AddImage("Trace Path");
            _startMarkerImage = AddImage("Start Marker");

            SetPrivateField("_guideGroup", _group);
            SetPrivateField("_glyphImage", _glyphImage);
            SetPrivateField("_tracePathImage", _tracePathImage);
            SetPrivateField("_startMarkerImage", _startMarkerImage);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Show_LightAssist_IsVisibleWithoutBlockingRaycasts()
        {
            _glyphImage.raycastTarget = true;
            _tracePathImage.raycastTarget = true;
            _startMarkerImage.raycastTarget = true;

            _guide.Show(null, TraceAssistStrength.Light);

            Assert.IsTrue(_guide.IsVisible);
            Assert.AreEqual(TraceAssistStrength.Light, _guide.CurrentStrength);
            Assert.IsFalse(_group.interactable);
            Assert.IsFalse(_group.blocksRaycasts);
            Assert.IsFalse(_glyphImage.raycastTarget);
            Assert.IsFalse(_tracePathImage.raycastTarget);
            Assert.IsFalse(_startMarkerImage.raycastTarget);
            Assert.AreEqual(1f, _tracePathImage.fillAmount);
        }

        [Test]
        public void Hide_DisablesGuideAndClearsTracePath()
        {
            _guide.Show(null, TraceAssistStrength.Light);

            _guide.Hide();

            Assert.IsFalse(_guide.IsVisible);
            Assert.AreEqual(TraceAssistStrength.Hidden, _guide.CurrentStrength);
            Assert.AreEqual(0f, _group.alpha);
            Assert.IsFalse(_group.blocksRaycasts);
            Assert.AreEqual(0f, _tracePathImage.fillAmount);
        }

        private Image AddImage(string name)
        {
            GameObject child = new(name);
            child.transform.SetParent(_root.transform);
            return child.AddComponent<Image>();
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = typeof(BaybayinTraceGuideController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, $"Expected field {fieldName} to exist.");
            field.SetValue(_guide, value);
        }
    }
}
