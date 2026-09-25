using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// The hint chip is a compact icon control — roughly twice the authored
    /// 80x80 pause button it parks under — carrying the almanac "?" glyph when
    /// the parchment and icon art resolve.
    /// </summary>
    [TestFixture]
    public sealed class SentenceHintChipTests
    {
        private readonly List<Object> _objectsToDestroy = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objectsToDestroy.Count - 1; index >= 0; index--)
            {
                if (_objectsToDestroy[index] != null)
                    Object.DestroyImmediate(_objectsToDestroy[index]);
            }
            _objectsToDestroy.Clear();
        }

        [Test]
        public void EnsureChip_ParksASquareIconChip_UnderThePauseButton()
        {
            GameObject hud = Track(new GameObject("HUDLayer", typeof(RectTransform)));
            RectTransform pause = Track(new GameObject("PauseButton", typeof(RectTransform)))
                .GetComponent<RectTransform>();
            pause.SetParent(hud.transform, false);
            pause.pivot = Vector2.one;
            pause.sizeDelta = new Vector2(80f, 80f);
            pause.anchoredPosition = new Vector2(-20f, -20f);

            SentenceHintController controller =
                Track(new GameObject("[Test] HintController"))
                    .AddComponent<SentenceHintController>();

            typeof(SentenceHintController)
                .GetMethod("EnsureChip", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);

            GameObject chip = (GameObject)typeof(SentenceHintController)
                .GetField("_chipRoot", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(controller);
            Assert.IsNotNull(chip, "EnsureChip should build the chip.");
            Track(chip);

            Assert.AreEqual("HUDLayer", chip.transform.parent.name,
                "The chip parks on the HUD layer.");

            RectTransform chipRect = (RectTransform)chip.transform;
            Assert.AreEqual(new Vector2(160f, 160f), chipRect.sizeDelta,
                "The chip stays square at ~2x the authored 80x80 pause button.");
            Assert.AreEqual(new Vector2(-20f, -112f), chipRect.anchoredPosition,
                "The chip sits flush under the pause button's right edge.");
            Assert.IsNotNull(chip.GetComponent<Button>(),
                "The chip stays a clickable button.");

            Image icon = chip.transform.Find("[Runtime] SentenceHintChipIcon")?
                .GetComponent<Image>();
            Assert.IsNotNull(icon, "The chip carries the hint icon when the art resolves.");
            Assert.IsNotNull(icon.sprite,
                "Resources/Art/UI/Almanac/Questionmark should resolve to a sprite.");
            Assert.IsFalse(icon.raycastTarget,
                "The icon must not swallow the chip button's taps.");
        }

        private T Track<T>(T created) where T : Object
        {
            _objectsToDestroy.Add(created);
            return created;
        }
    }
}
