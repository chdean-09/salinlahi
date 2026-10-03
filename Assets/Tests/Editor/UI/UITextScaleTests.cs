using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class UITextScaleTests
    {
        [Test]
        public void Floors_AreOrderedAndPositive()
        {
            Assert.Greater(UITextScale.Caption, 0f);
            Assert.Greater(UITextScale.Secondary, UITextScale.Caption);
            Assert.Greater(UITextScale.Body, UITextScale.Secondary);
            Assert.Greater(UITextScale.Title, UITextScale.Body);
            Assert.Greater(UITextScale.Display, UITextScale.Title);
            Assert.LessOrEqual(UITextScale.AutoSizeFloor, UITextScale.Caption,
                "Auto-sized text must never shrink below the caption floor.");
        }

        [Test]
        public void RaiseToFloor_NeverReturnsBelowFloor()
        {
            Assert.AreEqual(UITextScale.Body, UITextScale.RaiseToFloor(12f, UITextScale.Body));
            Assert.AreEqual(60f, UITextScale.RaiseToFloor(60f, UITextScale.Body),
                "Sizes already above the floor pass through unchanged.");
        }

        [Test]
        public void ApplyFloor_RaisesFixedTextToFloor()
        {
            using var scope = new TextScope(enableAutoSizing: false, size: 20f);

            UITextScale.ApplyFloor(scope.Text, UITextScale.Body);

            Assert.AreEqual(UITextScale.Body, scope.Text.fontSize);
        }

        [Test]
        public void ApplyFloor_RaisesAutosizeRangeWithoutCappingIt()
        {
            using var scope = new TextScope(enableAutoSizing: true, size: 30f);
            scope.Text.fontSizeMin = 18f;
            scope.Text.fontSizeMax = 34f;

            UITextScale.ApplyFloor(scope.Text, UITextScale.Body);

            Assert.AreEqual(UITextScale.AutoSizeFloor, scope.Text.fontSizeMin);
            Assert.AreEqual(UITextScale.Body, scope.Text.fontSizeMax,
                "fontSizeMax rises to the floor so the text can still reach a readable size.");
        }

        [Test]
        public void ApplyFloor_LeavesCompliantTextUntouched()
        {
            using var scope = new TextScope(enableAutoSizing: true, size: 48f);
            scope.Text.fontSizeMin = 30f;
            scope.Text.fontSizeMax = 56f;

            UITextScale.ApplyFloor(scope.Text, UITextScale.Body);

            Assert.AreEqual(30f, scope.Text.fontSizeMin);
            Assert.AreEqual(56f, scope.Text.fontSizeMax);
            Assert.AreEqual(48f, scope.Text.fontSize);
        }

        [Test]
        public void ApplyFloor_IsNullSafe()
        {
            Assert.DoesNotThrow(() => UITextScale.ApplyFloor(null, UITextScale.Body));
        }

        private sealed class TextScope : System.IDisposable
        {
            private readonly GameObject _go;
            public readonly TextMeshProUGUI Text;

            public TextScope(bool enableAutoSizing, float size)
            {
                _go = new GameObject("UITextScaleTests_Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                Text = _go.GetComponent<TextMeshProUGUI>();
                Text.enableAutoSizing = enableAutoSizing;
                Text.fontSize = size;
            }

            public void Dispose()
            {
                if (_go != null)
                    Object.DestroyImmediate(_go);
            }
        }
    }
}
