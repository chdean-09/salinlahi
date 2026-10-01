using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// The hint chip is a compact icon control matching the authored 80x80
    /// pause button it parks under — the same flat translucent square —
    /// carrying the almanac "?" glyph when the icon art resolves.
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
            Assert.AreEqual(new Vector2(80f, 80f), chipRect.sizeDelta,
                "The chip matches the authored 80x80 pause button it parks under.");
            Assert.AreEqual(new Vector2(-20f, -112f), chipRect.anchoredPosition,
                "The chip sits flush under the pause button's right edge.");
            Assert.IsNotNull(chip.GetComponent<Button>(),
                "The chip stays a clickable button.");

            Image chipBackground = chip.GetComponent<Image>();
            Assert.AreEqual(new Color(0f, 0f, 0f, 0.45f), chipBackground.color,
                "The chip wears the pause button's flat translucent fill, not the scroll skin.");
            Assert.IsNull(chipBackground.sprite,
                "The chip keeps the pause button's sprite-free flat look.");

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

        [Test]
        public void EnsureOverlay_KeepsBothHintsInAMaskedReadingAreaAboveClose()
        {
            GameObject canvasObject = Track(new GameObject("HintTestCanvas", typeof(RectTransform), typeof(Canvas)));
            // Fixed reference geometry keeps the assertion independent of the Editor window size.
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1080f, 1920f);
            GameObject host = Track(new GameObject("HintController"));
            host.transform.SetParent(canvasObject.transform, false);
            SentenceHintController controller = host.AddComponent<SentenceHintController>();
            typeof(SentenceHintController).GetField("_bodyText", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(controller, "<b>mother</b>\nilaw ng tahanan\n\n<b>father</b>\nang haligi ng tahanan");
            typeof(SentenceHintController).GetMethod("EnsureOverlay", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);
            GameObject overlay = (GameObject)typeof(SentenceHintController)
                .GetField("_overlayRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Track(overlay);
            overlay.SetActive(true);
            Assert.IsNotNull(canvasObject.GetComponent<GraphicRaycaster>(),
                "An existing canvas must receive a raycaster so the modal can be dismissed.");

            ScrollRect scroll = overlay.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll);
            Assert.IsFalse(scroll.horizontal);
            Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>());
            TextMeshProUGUI body = scroll.content.GetComponent<TextMeshProUGUI>();
            Assert.AreEqual(TextAlignmentOptions.TopLeft, body.alignment);
            Assert.IsFalse(body.enableAutoSizing);
            Assert.AreEqual(UITextScale.Body, body.fontSize);
            StringAssert.Contains("mother", body.text);
            StringAssert.Contains("father", body.text);
            Assert.IsNotNull(scroll.content.GetComponent<ContentSizeFitter>());
            Transform close = scroll.viewport.parent.Find("[Runtime] SentenceHintClose");
            Assert.IsNotNull(close);
            Assert.AreSame(close.GetComponent<Image>(), close.GetComponent<Button>().targetGraphic);
            Assert.IsFalse(close.IsChildOf(scroll.content), "Close stays outside the scrolling content.");
            Assert.Less(((RectTransform)close).anchorMax.y, scroll.viewport.anchorMin.y);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height,
                "The two short introductory clues fit without scrolling.");
        }
    }
}
