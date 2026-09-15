using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class ResponsiveUILayoutTests
    {
        [Test]
        public void SafeAreaHandler_CalculateAnchors_CanIntersectSafeAreaWithPlayColumn()
        {
            Rect safeArea = new(0f, 24f, 1000f, 676f);
            Rect playColumn = new(220f, 0f, 560f, 700f);

            SafeAreaHandler.CalculateAnchors(
                safeArea,
                screenWidth: 1000,
                screenHeight: 700,
                includePlayColumn: true,
                playColumnRect: playColumn,
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.AreEqual(0.22f, anchorMin.x, 0.001f);
            Assert.AreEqual(24f / 700f, anchorMin.y, 0.001f);
            Assert.AreEqual(0.78f, anchorMax.x, 0.001f);
            Assert.AreEqual(1f, anchorMax.y, 0.001f);
        }

        [Test]
        public void SafeAreaHandler_CalculateAnchors_CanUseFullSafeAreaForCutscenePrompt()
        {
            Rect safeArea = new(0f, 24f, 1000f, 676f);
            Rect playColumn = new(220f, 0f, 560f, 700f);

            SafeAreaHandler.CalculateAnchors(
                safeArea,
                screenWidth: 1000,
                screenHeight: 700,
                includePlayColumn: false,
                playColumnRect: playColumn,
                out Vector2 anchorMin,
                out Vector2 anchorMax);

            Assert.AreEqual(0f, anchorMin.x, 0.001f);
            Assert.AreEqual(24f / 700f, anchorMin.y, 0.001f);
            Assert.AreEqual(1f, anchorMax.x, 0.001f);
            Assert.AreEqual(1f, anchorMax.y, 0.001f);
        }

        [Test]
        public void TutorialGuide_ResponsiveTextLayout_KeepsInstructionsAboveGifPreviewBand()
        {
            using TestObjects objects = new();
            TextMeshProUGUI prompt = objects.CreateText("PromptText");
            TextMeshProUGUI feedback = objects.CreateText("FeedbackText");

            Level1TutorialGuideUI.ApplyResponsiveTextLayout(prompt, feedback);

            RectTransform promptRect = prompt.rectTransform;
            RectTransform feedbackRect = feedback.rectTransform;

            Assert.GreaterOrEqual(promptRect.anchorMin.y, 0.84f);
            Assert.GreaterOrEqual(feedbackRect.anchorMin.y, 0.75f);
            Assert.Greater(feedbackRect.anchorMin.y, 0.42f, "Feedback must sit above the bottom-centered GIF preview band.");
            Assert.AreEqual(0.06f, promptRect.anchorMin.x, 0.001f);
            Assert.AreEqual(0.94f, promptRect.anchorMax.x, 0.001f);
            Assert.IsTrue(prompt.enableAutoSizing);
            Assert.IsTrue(feedback.enableAutoSizing);
            Assert.GreaterOrEqual(feedback.fontSizeMax, 42f);
        }

        [Test]
        public void DialogueController_ResponsiveLayout_ExpandsPanelAndReadableBodyText()
        {
            using TestObjects objects = new();
            RectTransform panel = objects.CreateRect("DialoguePanel");
            TextMeshProUGUI speaker = objects.CreateText("SpeakerText", panel);
            TextMeshProUGUI body = objects.CreateText("BodyText", panel);
            Image portrait = objects.CreateImage("PortraitImage", panel);

            DialogueController.ApplyResponsiveDialogueLayout(panel, speaker, body, portrait, hasPortrait: true);

            Assert.AreEqual(0f, panel.anchorMin.y, 0.001f);
            Assert.AreEqual(0.5f, panel.anchorMax.y, 0.001f, "The story scroll fills half the screen.");

            // Everything fits at half height, so the copy renders at a fixed readable size
            // rather than being auto-shrunk to fit a cramped band.
            Assert.IsFalse(body.enableAutoSizing);
            Assert.IsFalse(speaker.enableAutoSizing);
            Assert.Greater(speaker.fontSize, body.fontSize, "The speaker name is the title.");

            Assert.Less(body.rectTransform.anchorMin.x, body.rectTransform.anchorMax.x);
            Assert.Greater(body.rectTransform.anchorMax.y, body.rectTransform.anchorMin.y);
            Assert.LessOrEqual(body.rectTransform.anchorMax.y, speaker.rectTransform.anchorMin.y);
            AssertAnchorsInside(speaker.rectTransform, ScrollPanelArt.TopSafeArea);
            AssertAnchorsInside(body.rectTransform, ScrollPanelArt.TopSafeArea);

            // The body hangs from just under the title and grows downward. Centering it in
            // the tall half-screen panel leaves it adrift in the middle of the paper with a
            // gap under the title, so it is top-aligned and its band starts near the title.
            Assert.AreEqual(TextAlignmentOptions.Top, body.alignment);
            Assert.Less(
                speaker.rectTransform.anchorMin.y - body.rectTransform.anchorMax.y, 0.04f,
                "The body should start just under the title, not float below a gap.");
        }

        [Test]
        public void DialogueController_Title_ClearsTheFixedScrollRod()
        {
            using TestObjects objects = new();
            RectTransform panel = objects.CreateRect("DialoguePanel");
            TextMeshProUGUI speaker = objects.CreateText("SpeakerText", panel);
            TextMeshProUGUI body = objects.CreateText("BodyText", panel);

            DialogueController.ApplyResponsiveDialogueLayout(
                panel, speaker, body, null, hasPortrait: false);

            // The scroll's rod is a 9-slice border: a fixed pixel height that does not shrink
            // with the panel. Shrinking the panel is what used to push the speaker name onto
            // the rod, so the guard is stated against the rod's real share of the panel.
            float rodTop = 1f - DialogueController.ScrollRodFraction;
            Assert.Less(DialogueController.ScrollRodFraction, 0.25f,
                "At the design height the rod must not swallow a quarter of the panel.");
            Assert.Less(speaker.rectTransform.anchorMax.y, rodTop,
                "The speaker name must sit below the rod, not on it.");
            Assert.Less(body.rectTransform.anchorMax.y, rodTop);
        }

        [Test]
        public void DialogueController_SlideOffset_RisesFromBelowTheScreenToRest()
        {
            const float height = 960f;

            Assert.AreEqual(-height, DialogueController.SlideOffsetY(0f, height), 0.01f,
                "At rest the scroll is parked one full panel below the screen.");
            Assert.AreEqual(0f, DialogueController.SlideOffsetY(1f, height), 0.01f,
                "It settles flush with the bottom of the screen.");

            // Eased out: most of the travel happens early, so it is already past halfway.
            Assert.Greater(DialogueController.SlideOffsetY(0.5f, height), -height * 0.5f);

            // Monotonic — it never dips back down on the way up.
            float previous = DialogueController.SlideOffsetY(0f, height);
            for (int step = 1; step <= 10; step++)
            {
                float current = DialogueController.SlideOffsetY(step / 10f, height);
                Assert.GreaterOrEqual(current, previous);
                previous = current;
            }
        }

        [Test]
        public void ScrollPanelArt_SafeAreas_StayInsideNormalizedPanelBounds()
        {
            Assert.Greater(ScrollPanelArt.FullSafeArea.xMin, 0f);
            Assert.Greater(ScrollPanelArt.FullSafeArea.yMin, 0f);
            Assert.Less(ScrollPanelArt.FullSafeArea.xMax, 1f);
            Assert.Less(ScrollPanelArt.FullSafeArea.yMax, 1f);
            Assert.Greater(ScrollPanelArt.TopSafeArea.xMin, 0f);
            Assert.Greater(ScrollPanelArt.TopSafeArea.yMin, 0f);
            Assert.Less(ScrollPanelArt.TopSafeArea.xMax, 1f);
            Assert.Less(ScrollPanelArt.TopSafeArea.yMax, 1f);
        }

        [Test]
        public void PauseMenu_ParchmentLayout_KeepsPromptAndButtonsInsidePaper()
        {
            using TestObjects objects = new();
            RectTransform card = objects.CreateRect("PauseCard");
            TMP_Text prompt = objects.CreateText("Prompt", card);
            Button confirm = objects.CreateButton("Confirm", card);
            Button cancel = objects.CreateButton("Cancel", card);

            PauseMenuUI.ApplyParchmentConfirmationLayout(card, prompt, confirm, cancel);

            Assert.AreEqual(new Vector2(760f, 680f), card.sizeDelta);
            AssertAnchorsInside(prompt.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(confirm.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(cancel.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            Assert.LessOrEqual(cancel.GetComponent<RectTransform>().anchorMax.y,
                confirm.GetComponent<RectTransform>().anchorMin.y);
        }

        [Test]
        public void FocusWordPreview_ParchmentLayout_KeepsCopyAndButtonInsidePaper()
        {
            using TestObjects objects = new();
            RectTransform panel = objects.CreateRect("FocusWordPanel");
            TMP_Text preview = objects.CreateText("Preview", panel);
            Button continueButton = objects.CreateButton("Continue", panel);

            FocusWordPreviewController.ApplyParchmentLayout(panel, preview, continueButton);

            Assert.AreEqual(new Vector2(560f, 520f), panel.sizeDelta);
            AssertAnchorsInside(preview.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(continueButton.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            Assert.LessOrEqual(continueButton.GetComponent<RectTransform>().anchorMax.y,
                preview.rectTransform.anchorMin.y);
        }

        [Test]
        public void WaveCleared_ParchmentLayout_KeepsBannerHeartsAndButtonInsidePaper()
        {
            using TestObjects objects = new();
            RectTransform card = objects.CreateRect("BannerCard");
            TMP_Text banner = objects.CreateText("BannerText", card);
            TMP_Text hearts = objects.CreateText("HeartsText", card);
            Button continueButton = objects.CreateButton("ContinueButton", card);
            AsBuiltText(banner);
            AsBuiltText(hearts);
            AsBuiltButton(continueButton);

            WaveClearedScreenUI.ApplyParchmentLayout(card, banner, hearts, continueButton);

            AssertAnchorsInside(banner.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(hearts.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(
                continueButton.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            Assert.LessOrEqual(hearts.rectTransform.anchorMax.y, banner.rectTransform.anchorMin.y);
            Assert.LessOrEqual(
                continueButton.GetComponent<RectTransform>().anchorMax.y,
                hearts.rectTransform.anchorMin.y);
        }

        [Test]
        public void MemoryClaim_ParchmentLayout_StacksButtonsInsidePaper()
        {
            using TestObjects objects = new();
            RectTransform card = objects.CreateRect("ClaimCard");
            TMP_Text body = objects.CreateText("BodyText", card);
            Button claim = objects.CreateButton("ClaimButton", card);
            Button dismiss = objects.CreateButton("DismissButton", card);
            AsBuiltText(body);
            AsBuiltButton(claim);
            AsBuiltButton(dismiss);

            MemoryClaimPanel.ApplyParchmentLayout(card, body, claim, dismiss);

            AssertAnchorsInside(body.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(claim.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(dismiss.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            Assert.LessOrEqual(
                claim.GetComponent<RectTransform>().anchorMax.y, body.rectTransform.anchorMin.y);
            Assert.LessOrEqual(
                dismiss.GetComponent<RectTransform>().anchorMax.y,
                claim.GetComponent<RectTransform>().anchorMin.y);
        }

        [Test]
        public void MemoryCard_ParchmentLayout_KeepsFaceContentAndButtonsInsidePaper()
        {
            using TestObjects objects = new();
            RectTransform card = objects.CreateRect("MemoryCard");
            RectTransform frontFace = objects.CreateRect("FrontFace");
            RectTransform backFace = objects.CreateRect("BackFace");
            frontFace.SetParent(card, false);
            backFace.SetParent(card, false);
            TMP_Text number = objects.CreateText("NumberText", frontFace);
            TMP_Text title = objects.CreateText("TitleText", frontFace);
            TMP_Text words = objects.CreateText("WordsText", frontFace);
            RectTransform glyphRow = objects.CreateRect("GlyphRow");
            glyphRow.SetParent(frontFace, false);
            TMP_Text lore = objects.CreateText("LoreText", backFace);
            Button flip = objects.CreateButton("FlipButton", card);
            Button close = objects.CreateButton("CloseButton", card);
            frontFace.offsetMin = new Vector2(0f, 120f);
            backFace.offsetMin = new Vector2(0f, 120f);
            AsBuiltText(number);
            AsBuiltText(title);
            AsBuiltText(words);
            AsBuiltRow(glyphRow);
            AsBuiltText(lore);
            AsBuiltButton(flip);
            AsBuiltButton(close);

            MemoryCardUI.ApplyParchmentLayout(
                card, frontFace, backFace, number, title, words, glyphRow, lore, flip, close);

            // Both faces stretch edge to edge, so their children's anchors read against the
            // card itself rather than a box inset above the old outside-the-paper button row.
            Assert.AreEqual(Vector2.zero, frontFace.offsetMin);
            Assert.AreEqual(Vector2.zero, frontFace.offsetMax);
            Assert.AreEqual(Vector2.zero, backFace.offsetMin);
            Assert.AreEqual(Vector2.zero, backFace.offsetMax);

            AssertAnchorsInside(number.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(title.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(words.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(glyphRow, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(lore.rectTransform, ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(flip.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);
            AssertAnchorsInside(close.GetComponent<RectTransform>(), ScrollPanelArt.FullSafeArea);

            // Reading order down the paper, then the button row under everything.
            Assert.LessOrEqual(title.rectTransform.anchorMax.y, number.rectTransform.anchorMin.y);
            Assert.LessOrEqual(words.rectTransform.anchorMax.y, title.rectTransform.anchorMin.y);
            Assert.LessOrEqual(
                flip.GetComponent<RectTransform>().anchorMax.y, glyphRow.anchorMin.y);

            // The two buttons sit side by side without overlapping.
            Assert.LessOrEqual(
                flip.GetComponent<RectTransform>().anchorMax.x,
                close.GetComponent<RectTransform>().anchorMin.x);
        }

        [Test]
        public void MemoryCard_GlyphSize_ShrinksSoAFullRowFitsThePaper()
        {
            // Five glyphs is the real worst case in Ugat (Level 5: IBA + MANA).
            float rowWidth = 820f * (0.83f - 0.17f);

            float five = MemoryCardUI.ResolveGlyphSize(5, rowWidth);
            float rowSpan = (five * 5) + (MemoryCardUI.GlyphGap * 4);

            // Tolerance is for float rounding only — the bug this guards against overflows
            // by 3px (five glyphs at the authored 96 plus gaps is 544 against 541).
            Assert.LessOrEqual(rowSpan, rowWidth + 0.01f);
            Assert.Greater(five, 0f);

            // A short row is not blown up past the authored size.
            Assert.AreEqual(
                MemoryCardUI.MaxGlyphSize, MemoryCardUI.ResolveGlyphSize(2, rowWidth), 0.01f);
        }

        [Test]
        public void MemoryCard_GlyphRow_StillFitsWhenTheCardHasNotLaidOutYet()
        {
            // Present() renders the glyphs before it activates the overlay root, and Hide()
            // leaves that root inactive, so from the second card on the row measures zero.
            // The fit has to come from the card's own width, not from a layout pass.
            float unmeasured = MemoryCardUI.ResolveGlyphRowWidth(0f);
            Assert.Greater(unmeasured, 0f);

            float size = MemoryCardUI.ResolveGlyphSize(5, unmeasured);
            Assert.LessOrEqual(
                (size * 5) + (MemoryCardUI.GlyphGap * 4), unmeasured + 0.01f);
            Assert.Less(size, MemoryCardUI.MaxGlyphSize);

            // A real measurement still wins when there is one.
            Assert.AreEqual(500f, MemoryCardUI.ResolveGlyphRowWidth(500f), 0.01f);
        }

        [Test]
        public void CutscenePlayer_ContinuePromptLayout_UsesCenteredTopSafeAreaBand()
        {
            using TestObjects objects = new();
            TextMeshProUGUI prompt = objects.CreateText("ContinuePromptText");

            CutscenePlayer.ApplyContinuePromptLayout(prompt, topPadding: 52f);

            RectTransform rect = prompt.rectTransform;
            Assert.AreEqual(0.04f, rect.anchorMin.x, 0.001f);
            Assert.AreEqual(0.96f, rect.anchorMax.x, 0.001f);
            Assert.AreEqual(1f, rect.anchorMin.y, 0.001f);
            Assert.AreEqual(1f, rect.anchorMax.y, 0.001f);
            Assert.AreEqual(new Vector2(0.5f, 1f), rect.pivot);
            Assert.AreEqual(0f, rect.anchoredPosition.x, 0.001f);
            Assert.Less(rect.anchoredPosition.y, 0f);
            Assert.IsTrue(prompt.enableAutoSizing);
            Assert.GreaterOrEqual(prompt.fontSizeMax, 54f);
        }

        /// <summary>
        /// The anchors the runtime builders leave behind: text pinned across the full card
        /// width at the top edge, buttons pinned to the bottom edge. Both sit outside the
        /// parchment safe area, which is the defect these layout helpers exist to correct —
        /// seeding them here keeps the assertions from passing against a default RectTransform.
        /// </summary>
        private static RectTransform AsBuiltText(TMP_Text text)
        {
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            return rect;
        }

        private static RectTransform AsBuiltButton(Button button)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            return rect;
        }

        private static RectTransform AsBuiltRow(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            return rect;
        }

        private static void AssertAnchorsInside(RectTransform rect, Rect area)
        {
            Assert.GreaterOrEqual(rect.anchorMin.x, area.xMin);
            Assert.GreaterOrEqual(rect.anchorMin.y, area.yMin);
            Assert.LessOrEqual(rect.anchorMax.x, area.xMax);
            Assert.LessOrEqual(rect.anchorMax.y, area.yMax);
        }

        private sealed class TestObjects : System.IDisposable
        {
            private readonly GameObject _root = new("ResponsiveUILayoutTests_Root", typeof(RectTransform));

            public RectTransform CreateRect(string name)
            {
                GameObject go = new(name, typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                return go.GetComponent<RectTransform>();
            }

            public TextMeshProUGUI CreateText(string name, RectTransform parent = null)
            {
                GameObject go = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent != null ? parent : _root.transform, false);
                return go.GetComponent<TextMeshProUGUI>();
            }

            public Image CreateImage(string name, RectTransform parent = null)
            {
                GameObject go = new(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent != null ? parent : _root.transform, false);
                return go.GetComponent<Image>();
            }

            public Button CreateButton(string name, RectTransform parent = null)
            {
                GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Button));
                go.transform.SetParent(parent != null ? parent : _root.transform, false);
                return go.GetComponent<Button>();
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
            }
        }
    }
}
