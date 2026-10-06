using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    public class MobileTypographyTests
    {
        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void LongChallengeAnswers_FitInsideSeparateReadableTiles(float width, float height)
        {
            GameObject root = new("Challenge typography test", typeof(RectTransform), typeof(Canvas));
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                string[] answers = { "ALAALA", "MAHALAGA", "HALAGA", "DALA" };
                var tokens = new ChallengeTokenDefinition[answers.Length];
                for (int i = 0; i < answers.Length; i++)
                    tokens[i] = new ChallengeTokenDefinition { tokenId = answers[i], occurrenceId = answers[i], displayText = answers[i] };
                sequence.units = new[] { new ChallengeUnitDefinition
                {
                    unitId = "long-answer-test", mode = ChallengeMode.WordPlacement,
                    tokens = tokens, candidateOccurrenceIds = answers,
                    slots = new[] { new ChallengeSlotDefinition { slotId = "answer", expectedOccurrenceId = "MAHALAGA" } }
                } };
                ChallengeSession session = new(sequence);
                session.Enter();
                GameObject host = new("Challenge", typeof(RectTransform));
                host.transform.SetParent(root.transform, false);
                ChallengeModeUI board = host.AddComponent<ChallengeModeUI>();
                board.Render(session);
                Canvas.ForceUpdateCanvases();
                RectTransform choices = host.transform.Find("AnswerChoices").GetComponent<RectTransform>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(choices);
                foreach (Button button in choices.GetComponentsInChildren<Button>())
                {
                    TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                    label.ForceMeshUpdate();
                    Assert.GreaterOrEqual(label.fontSize, UITextScale.Body);
                    Assert.LessOrEqual(label.textBounds.size.x, label.rectTransform.rect.width);
                    Assert.IsFalse(label.isTextOverflowing);
                    Assert.AreEqual(1, label.textInfo.lineCount, "An answer word should fit intact.");
                    Assert.GreaterOrEqual(button.GetComponent<RectTransform>().rect.height * scale, 44f);
                }
                Assert.AreEqual(answers.Length, choices.GetComponentsInChildren<Button>().Length);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sequence); }
        }

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void EmptyArchive_LongLockedLabelsAndInstructionsFit(float width, float height)
        {
            GameObject root = new("Archive typography test", typeof(RectTransform), typeof(Canvas));
            CampaignConfigSO campaign = ScriptableObject.CreateInstance<CampaignConfigSO>();
            EraConfigSO era = ScriptableObject.CreateInstance<EraConfigSO>();
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                era.eraName = "Ugnayan";
                era.levels.Add(level);
                level.levelNumber = 10;
                level.eraLocalOrder = 5;
                campaign.eras.Add(era);
                GameObject host = new("Archive", typeof(RectTransform));
                host.transform.SetParent(root.transform, false);
                MemoryArchiveController archive = host.AddComponent<MemoryArchiveController>();
                Assert.IsTrue(archive.Present(campaign, null, null));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(host.GetComponent<RectTransform>());
                foreach (TMP_Text text in host.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextOverflowing, text.text + " must remain fully readable.");
                    Assert.GreaterOrEqual(text.rectTransform.rect.height, text.preferredHeight);
                }
                ScrollRect scroll = host.GetComponentInChildren<ScrollRect>();
                TMP_Text instructions = host.transform.Find("EmptyStateText").GetComponent<TMP_Text>();
                Assert.LessOrEqual(scroll.viewport.offsetMax.y, instructions.rectTransform.offsetMin.y);
                Assert.GreaterOrEqual(host.GetComponentInChildren<Button>().GetComponent<RectTransform>().rect.height * scale, 44f);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(campaign);
                Object.DestroyImmediate(era);
                Object.DestroyImmediate(level);
            }
        }

        [Test]
        public void LongActionLabel_EnablesWrappingWithinItsButton()
        {
            GameObject root = new("Button typography test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                Button button = CreateAction(root.transform, "Pumasok sa Susunod na Panahon");
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, 176f);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.textWrappingMode = TextWrappingModes.NoWrap;
                ScrollPanelArt.SizeButtonLabel(button);
                Canvas.ForceUpdateCanvases();
                label.ForceMeshUpdate();
                Assert.AreEqual(TextWrappingModes.Normal, label.textWrappingMode);
                Assert.IsFalse(label.isTextOverflowing);
                Assert.LessOrEqual(label.textBounds.size.x, label.rectTransform.rect.width);
                Assert.Greater(label.textInfo.lineCount, 1);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredReadingScroll_ReusesItsContentLayoutWithoutNesting()
        {
            GameObject root = new("Authored reading test", typeof(RectTransform), typeof(ScrollRect));
            try
            {
                ScrollRect authored = root.GetComponent<ScrollRect>();
                GameObject viewport = new("Viewport", typeof(RectTransform), typeof(RectMask2D));
                viewport.transform.SetParent(root.transform, false);
                authored.viewport = viewport.GetComponent<RectTransform>();
                GameObject content = new("Content", typeof(RectTransform), typeof(ContentSizeFitter));
                content.transform.SetParent(viewport.transform, false);
                authored.content = content.GetComponent<RectTransform>();
                TMP_Text body = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                body.transform.SetParent(content.transform, false);
                TutorialFontProvider.ApplyTo(body);
                body.text = "Ang mga alaala ay patuloy niyang iingatan at ipapasa sa susunod na salinlahi.";
                root.SetActive(false);
                Assert.AreSame(authored, ScrollPanelArt.MakeReadingScroll(body));
                Assert.AreSame(content.transform, body.transform.parent);
                Assert.IsNotNull(content.GetComponent<VerticalLayoutGroup>());
                Assert.AreEqual(1, root.GetComponentsInChildren<ScrollRect>(true).Length);
                root.SetActive(true);
                LayoutRebuilder.ForceRebuildLayoutImmediate(authored.content);
                Assert.Greater(body.rectTransform.rect.height, 0f, "Authored prose must have a real scrollable height.");
                Assert.IsFalse(body.enableAutoSizing);
                Assert.GreaterOrEqual(body.fontSize, UITextScale.Body);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void MemoryClaim_LocalizedButtonsFitAtPhoneSizes(float width, float height)
        {
            GameObject root = new("Claim typography test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                GameObject paper = new("Paper", typeof(RectTransform));
                paper.transform.SetParent(root.transform, false);
                TMP_Text body = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                body.transform.SetParent(paper.transform, false);
                Button claim = CreateAction(paper.transform, MemoryCardCopy.ClaimLabel);
                Button dismiss = CreateAction(paper.transform, MemoryCardCopy.CloseLabel);
                MemoryClaimPanel.ApplyParchmentLayout(paper.GetComponent<RectTransform>(), body, claim, dismiss);
                Canvas.ForceUpdateCanvases();
                foreach (Button button in new[] { claim, dismiss })
                {
                    TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                    label.ForceMeshUpdate();
                    Assert.IsFalse(label.isTextOverflowing, label.text + " must fit without clipping.");
                    Assert.GreaterOrEqual(label.fontSize, UITextScale.Body);
                    Assert.GreaterOrEqual(button.GetComponent<RectTransform>().rect.height * scale, 44f);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static Button CreateAction(Transform parent, string copy)
        {
            GameObject host = new(copy, typeof(RectTransform), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);
            Button button = host.GetComponent<Button>();
            button.targetGraphic = host.GetComponent<Image>();
            GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(host.transform, false);
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            TutorialFontProvider.ApplyTo(label);
            label.text = copy;
            label.fontSize = UITextScale.Title;
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }

        [Test]
        public void ReadingFont_PreservesAuthoredFamilyAndMobileReadingFloor()
        {
            TMP_FontAsset font = TutorialFontProvider.FontAsset;
            Assert.IsNotNull(font);
            Assert.AreEqual("VT323", font.faceInfo.familyName,
                "Mobile readability polish must preserve the authored font family.");
            Assert.GreaterOrEqual(UITextScale.Body * 320f / 1080f, 16f);
        }

        [Test]
        public void LongReferenceCopy_ScrollsWithoutShrinkingOrNestingViewports()
        {
            GameObject root = new GameObject("Reading test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                RectTransform panel = root.GetComponent<RectTransform>();
                panel.sizeDelta = new Vector2(1080f, 576f);
                GameObject host = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
                host.transform.SetParent(panel, false);
                TMP_Text body = host.GetComponent<TMP_Text>();
                TutorialFontProvider.ApplyTo(body);
                body.rectTransform.anchorMin = new Vector2(0.17f, 0.16f);
                body.rectTransform.anchorMax = new Vector2(0.84f, 0.54f);
                body.rectTransform.offsetMin = body.rectTransform.offsetMax = Vector2.zero;
                body.fontSizeMax = UITextScale.Body;
                body.text = "Sa dulo ng Ugat, hinarap ni Juan ang Paglimot, ang anino na kumain sa mga alaala ng kanyang pamilya. "
                    + "Ang mga alaala ay patuloy niyang iingatan at ipapasa sa susunod na salinlahi.";
                ScrollRect scroll = ScrollPanelArt.MakeReadingScroll(body);
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.viewport);
                Canvas.ForceUpdateCanvases();

                Assert.IsFalse(body.enableAutoSizing);
                Assert.GreaterOrEqual(body.fontSize, UITextScale.Body);
                Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
                Assert.IsNotNull(scroll.viewport.GetComponent<RectMask2D>());
                scroll.verticalNormalizedPosition = 0f;

                Vector2 originalBand = scroll.viewport.anchorMin;
                ScrollPanelArt.MakeReadingScroll(body);
                Assert.AreEqual(originalBand, scroll.viewport.anchorMin,
                    "Opening another entry must preserve the reading band instead of collapsing it.");

                body.rectTransform.anchorMin = new Vector2(0.2f, 0.3f);
                body.rectTransform.anchorMax = new Vector2(0.84f, 0.66f);
                ScrollRect updated = ScrollPanelArt.MakeReadingScroll(body);
                Assert.AreSame(scroll, updated);
                Assert.AreSame(panel, updated.viewport.parent);
                Assert.AreEqual(new Vector2(0.2f, 0.3f), updated.viewport.anchorMin);
                Assert.AreEqual(1, root.GetComponentsInChildren<ScrollRect>().Length,
                    "Changing a portrait or dialogue band must not nest reading viewports.");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
