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
            Assert.GreaterOrEqual(panel.anchorMax.y, 0.28f);
            Assert.IsTrue(body.enableAutoSizing);
            Assert.GreaterOrEqual(body.fontSizeMax, 58f);
            Assert.GreaterOrEqual(body.fontSizeMin, 36f);
            Assert.Less(body.rectTransform.anchorMin.x, body.rectTransform.anchorMax.x);
            Assert.Greater(body.rectTransform.anchorMax.y, body.rectTransform.anchorMin.y);
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

            public void Dispose()
            {
                Object.DestroyImmediate(_root);
            }
        }
    }
}
