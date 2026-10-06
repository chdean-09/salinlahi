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
    /// pause button it parks under — the same parchment background —
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
            Assert.AreEqual(Color.white, chipBackground.color);
            Assert.IsNotNull(chipBackground.sprite, "The parchment HUD background must resolve.");
            Assert.AreEqual("PanelBackground", chipBackground.sprite.texture.name);
            Assert.AreEqual(Image.Type.Sliced, chipBackground.type);
            Assert.Greater(chipBackground.sprite.border.x, 0f,
                "The background frame must preserve its corners.");

            Image icon = chip.transform.Find("[Runtime] SentenceHintChipIcon")?
                .GetComponent<Image>();
            Assert.IsNotNull(icon, "The chip carries the hint icon when the art resolves.");
            Assert.IsNotNull(icon.sprite,
                "Resources/Art/UI/Almanac/Questionmark should resolve to a sprite.");
            Assert.IsFalse(icon.raycastTarget,
                "The icon must not swallow the chip button's taps.");
            Assert.AreEqual(ScrollPanelArt.InkColor, icon.color);
        }

        [Test]
        public void Hud_PauseUsesTheSameParchmentBackgroundAsTheHintChip()
        {
            GameObject hudRoot = Track(new GameObject("HudTest", typeof(RectTransform)));
            hudRoot.SetActive(false);
            Button pause = Track(new GameObject("PauseButton", typeof(RectTransform), typeof(Image), typeof(Button)))
                .GetComponent<Button>();
            pause.transform.SetParent(hudRoot.transform, false);
            HUD hud = hudRoot.AddComponent<HUD>();
            typeof(HUD).GetField("_pauseButton", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hud, pause);
            hudRoot.SetActive(true);
            typeof(HUD).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(hud, null);
            Assert.IsNotNull(pause.GetComponent<Image>().sprite);
            Assert.AreEqual("PanelBackground", pause.GetComponent<Image>().sprite.texture.name);
            Assert.AreEqual(Image.Type.Sliced, pause.GetComponent<Image>().type);
        }

        [Test]
        public void ScrollActions_KeepClicksAndReadableLabels_WhenStyledAndInked()
        {
            Button button = Track(new GameObject("ScrollAction", typeof(RectTransform), typeof(Image), typeof(Button)))
                .GetComponent<Button>();
            GameObject labelObject = Track(new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)));
            labelObject.transform.SetParent(button.transform, false);
            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "Continue";
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            ScrollPanelArt.StylePrimaryButton(button);
            Image image = button.GetComponent<Image>();
            Assert.IsNotNull(image.sprite);
            Assert.AreEqual("PanelBackground", image.sprite.texture.name,
                "Scroll actions must not borrow the main menu artwork.");
            Assert.AreEqual(Image.Type.Sliced, image.type);
            Assert.AreSame(image, button.targetGraphic);
            ScrollPanelArt.Inkify(label);
            Assert.AreEqual(ScrollPanelArt.InkColor, label.color);
            Assert.AreEqual(Selectable.Transition.ColorTint, button.transition);
            Assert.AreNotEqual(button.colors.normalColor, button.colors.pressedColor);
            Assert.AreNotEqual(button.colors.normalColor, button.colors.disabledColor);
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
            button.interactable = false;
            ScrollPanelArt.StyleSecondaryButton(button);
            Assert.IsFalse(button.interactable, "Styling must preserve gameplay gating.");
            Assert.AreEqual(ScrollPanelArt.InkColor, label.color);
        }

        private T Track<T>(T created) where T : Object
        {
            _objectsToDestroy.Add(created);
            return created;
        }

        [Test]
        public void Scrollbar_RemainsVisibleAtRestWhileContentOverflows_AndHidesWhenItFits()
        {
            ScrollRect scroll = CreateScrollableContent(600f);
            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            RefreshScrollbar(scroll);
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);

            scroll.verticalNormalizedPosition = 0f;
            RefreshScrollbar(scroll);
            RefreshScrollbar(scroll);
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf,
                "Reaching the bottom or remaining idle must not hide an overflowing scrollbar.");

            scroll.content.sizeDelta = new Vector2(0f, 100f);
            RefreshScrollbar(scroll);
            Assert.IsFalse(scroll.verticalScrollbar.gameObject.activeSelf);

            scroll.content.sizeDelta = new Vector2(0f, 600f);
            RefreshScrollbar(scroll);
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf,
                "A reopened or resized scroll must show its scrollbar when content grows.");
        }

        [Test]
        public void Scrollbar_RepeatedSetupKeepsOneBarAndContentClearOfItsTrack()
        {
            ScrollRect scroll = CreateScrollableContent(600f);
            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            Scrollbar scrollbar = scroll.verticalScrollbar;
            Vector2 inset = scroll.content.offsetMax;
            int children = scroll.viewport.childCount;

            ScrollPanelArt.EnsureVerticalScrollbar(scroll);
            RefreshScrollbar(scroll);

            Assert.AreSame(scrollbar, scroll.verticalScrollbar);
            Assert.AreEqual(inset, scroll.content.offsetMax);
            Assert.AreEqual(children, scroll.viewport.childCount);
            Assert.AreSame(scrollbar.handleRect.GetComponent<Image>(), scrollbar.targetGraphic);
            Assert.AreEqual(Scrollbar.Direction.BottomToTop, scrollbar.direction);
            Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                scroll.viewport, scroll.content);
            Bounds trackBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                scroll.viewport, scrollbar.transform);
            Assert.Less(contentBounds.max.x, trackBounds.min.x,
                "The scrollbar must have its own lane, outside the text/card width.");
        }

        [Test]
        public void Scrollbar_PreservesExistingAuthoredBarAndViewportPolicy()
        {
            ScrollRect scroll = CreateScrollableContent(600f);
            Scrollbar authored = Track(new GameObject("AuthoredScrollbar", typeof(RectTransform), typeof(Scrollbar)))
                .GetComponent<Scrollbar>();
            authored.transform.SetParent(scroll.transform, false);
            scroll.verticalScrollbar = authored;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            Vector2 inset = scroll.content.offsetMax;

            ScrollPanelArt.EnsureVerticalScrollbar(scroll);

            Assert.AreSame(authored, scroll.verticalScrollbar);
            Assert.AreEqual(ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport,
                scroll.verticalScrollbarVisibility);
            Assert.AreEqual(inset, scroll.content.offsetMax);
        }

        [Test]
        public void Scrollbar_DoesNotEnableScrollingOnANonScrollableSurface()
        {
            ScrollRect scroll = CreateScrollableContent(600f);
            scroll.vertical = false;

            ScrollPanelArt.EnsureVerticalScrollbar(scroll);

            Assert.IsNull(scroll.verticalScrollbar);
            Assert.IsFalse(scroll.vertical);
        }

        private ScrollRect CreateScrollableContent(float contentHeight)
        {
            GameObject canvas = Track(new GameObject("ScrollbarTestCanvas", typeof(RectTransform), typeof(Canvas)));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect));
            viewport.transform.SetParent(canvas.transform, false);
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.sizeDelta = new Vector2(200f, 200f);
            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = Vector2.one;
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, contentHeight);
            ScrollRect scroll = viewport.GetComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            return scroll;
        }

        private static void RefreshScrollbar(ScrollRect scroll)
        {
            Canvas.ForceUpdateCanvases();
            scroll.Rebuild(CanvasUpdate.PostLayout);
            typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(scroll, null);
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
                .SetValue(controller, "<b>_ _ _</b>\nilaw ng tahanan\n\n<b>_ _ _</b>\nang haligi ng tahanan");
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
            Assert.GreaterOrEqual(body.fontSize, 64f,
                "The hint reading size must remain larger than the general UI body floor.");
            StringAssert.Contains("<b>_ _ _</b>\nilaw ng tahanan", body.text);
            StringAssert.Contains("<b>_ _ _</b>\nang haligi ng tahanan", body.text);
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

        private static IEnumerable<TestCaseData> CampaignReadingCases()
        {
            for (int level = 1; level <= 15; level++)
            {
                foreach (int height in new[] { 1620, 1920, 2340 })
                    yield return new TestCaseData(level, height)
                        .SetName($"SentenceHints_Level{level:00}_1080x{height}");
            }
        }

        [TestCaseSource(nameof(CampaignReadingCases))]
        public void EnsureOverlay_AuthoredCampaignKeepsReadableCopyInsideScroll(int levelNumber, int height)
        {
            LevelConfigSO level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset");
            Assert.IsNotNull(level);
            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);
            Assert.IsNotEmpty(entries, "Every campaign level must offer readable hint content.");

            GameObject canvasObject = Track(new GameObject("CampaignHintCanvas", typeof(RectTransform), typeof(Canvas)));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            canvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, height);
            GameObject host = Track(new GameObject("HintController"));
            host.transform.SetParent(canvasObject.transform, false);
            SentenceHintController controller = host.AddComponent<SentenceHintController>();
            controller.ApplyLevel(level);
            typeof(SentenceHintController).GetMethod("EnsureOverlay", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, null);
            GameObject overlay = (GameObject)typeof(SentenceHintController)
                .GetField("_overlayRoot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Track(overlay);
            overlay.SetActive(true);
            Canvas.ForceUpdateCanvases();

            ScrollRect scroll = overlay.GetComponentInChildren<ScrollRect>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            TextMeshProUGUI body = scroll.content.GetComponent<TextMeshProUGUI>();
            body.ForceMeshUpdate();
            Assert.IsFalse(body.enableAutoSizing, "Longer levels must scroll, never shrink.");
            Assert.GreaterOrEqual(body.fontSize, 64f);
            Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>());
            foreach (SentenceHintContent.Entry entry in entries)
            {
                StringAssert.Contains(entry.Label, body.text);
                foreach (string line in entry.Lines)
                    StringAssert.Contains(line, body.text);
            }
            for (int line = 0; line < body.textInfo.lineCount; line++)
                Assert.LessOrEqual(body.textInfo.lineInfo[line].maxAdvance, scroll.content.rect.width + 1f,
                    "Every clue must wrap inside the reading area.");
            Assert.GreaterOrEqual(scroll.content.rect.height + 1f, body.preferredHeight,
                "The scrolling content must contain the entire hint, including its final line.");

            Transform panel = scroll.viewport.parent;
            TextMeshProUGUI title = panel.Find("[Runtime] SentenceHintTitle").GetComponent<TextMeshProUGUI>();
            title.ForceMeshUpdate();
            Assert.GreaterOrEqual(title.fontSize, 64f);
            Assert.IsFalse(title.isTextOverflowing);
            RectTransform close = (RectTransform)panel.Find("[Runtime] SentenceHintClose");
            TextMeshProUGUI closeLabel = close.GetComponentInChildren<TextMeshProUGUI>();
            closeLabel.ForceMeshUpdate();
            Assert.GreaterOrEqual(closeLabel.fontSize, 64f);
            Assert.IsFalse(closeLabel.isTextOverflowing);
            Bounds closeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel, close);
            // Measure the mask itself, not its children: long content deliberately
            // extends beyond the viewport and is clipped by RectMask2D.
            float readingBottom = panel.InverseTransformPoint(scroll.viewport.TransformPoint(
                new Vector3(0f, scroll.viewport.rect.yMin, 0f))).y;
            Assert.Less(closeBounds.max.y, readingBottom, "Close must stay below the masked reading area.");

            scroll.verticalNormalizedPosition = 0f;
            Canvas.ForceUpdateCanvases();
            Bounds contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, scroll.content);
            Assert.GreaterOrEqual(contentBounds.min.y, scroll.viewport.rect.yMin - 1f,
                "Scrolling to the bottom must reveal the final clue.");
        }
    }
}
