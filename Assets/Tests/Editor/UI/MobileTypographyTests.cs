using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    public class MobileTypographyTests
    {
        [TestCase(320f, 568f, 8, 1, false)]
        [TestCase(360f, 800f, 8, 1, false)]
        [TestCase(430f, 932f, 8, 1, false)]
        [TestCase(320f, 568f, 5, 0, true)]
        [TestCase(360f, 800f, 5, 0, true)]
        [TestCase(430f, 932f, 5, 0, true)]
        public void ShortAuthoredChallenge_FitsWithoutScrollingOrChangingTextSize(
            float width, float height, int levelNumber, int unitIndex, bool showFeedback)
        {
            GameObject root = new("Short challenge layout", typeof(RectTransform), typeof(Canvas));
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            try
            {
                SetPhoneCanvas(root, width, height);
                LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                    $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset");
                sequence.units = new[] { level.challengeSequence.units[unitIndex] };
                ChallengeSession session = new(sequence, policy: level.challengePolicy);
                session.Enter();
                GameObject host = new("Challenge", typeof(RectTransform));
                host.transform.SetParent(root.transform, false);
                ChallengeModeUI board = host.AddComponent<ChallengeModeUI>();
                board.Render(session);
                if (showFeedback) board.ShowFeedback("Tama! Susunod na salita.");
                ScrollRect scroll = host.GetComponentInChildren<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                TMP_Text prompt = scroll.content.GetComponent<TMP_Text>();
                prompt.ForceMeshUpdate();
                Assert.AreEqual(UITextScale.Title, prompt.fontSize);
                Assert.LessOrEqual(scroll.content.rect.height, scroll.viewport.rect.height + 1f,
                    "The reported short prompt should fit using the available parchment space.");
                if (showFeedback)
                    Assert.AreEqual(UITextScale.Body, host.transform.Find("Status").GetComponent<TMP_Text>().fontSize);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sequence); }
        }

        [TestCase(320f, 568f, 1)]
        [TestCase(320f, 568f, 2)]
        [TestCase(320f, 568f, 3)]
        [TestCase(320f, 568f, 4)]
        [TestCase(360f, 800f, 3)]
        [TestCase(360f, 800f, 4)]
        [TestCase(430f, 932f, 3)]
        [TestCase(430f, 932f, 4)]
        public void ChallengeLayout_ShowsMorePromptAndCentersEachAnswerRow(float width, float height, int count)
        {
            GameObject root = new("Challenge layout test", typeof(RectTransform), typeof(Canvas));
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            try
            {
                SetPhoneCanvas(root, width, height);
                string[] words = { "IBA", "MANA", "MATA", "AMA" };
                var tokens = new ChallengeTokenDefinition[count];
                var candidates = new string[count];
                for (int i = 0; i < count; i++)
                {
                    candidates[i] = words[i];
                    tokens[i] = new ChallengeTokenDefinition { tokenId = words[i], occurrenceId = words[i], displayText = words[i] };
                }
                sequence.units = new[] { new ChallengeUnitDefinition
                {
                    unitId = "layout-test", mode = ChallengeMode.WordPlacement,
                    prompt = "Kapag naalala ang ugat at pinili ang sariling landas, nagiging ______ ang tinig.",
                    tokens = tokens, candidateOccurrenceIds = candidates,
                    slots = new[] { new ChallengeSlotDefinition { slotId = "answer", expectedOccurrenceId = words[0] } }
                } };
                ChallengeSession session = new(sequence);
                session.Enter();
                GameObject host = new("Challenge", typeof(RectTransform));
                host.transform.SetParent(root.transform, false);
                ChallengeModeUI board = host.AddComponent<ChallengeModeUI>();
                board.Render(session);
                Canvas.ForceUpdateCanvases();
                RectTransform viewport = host.transform.Find("PromptViewport").GetComponent<RectTransform>();
                TMP_Text prompt = viewport.GetComponentInChildren<TMP_Text>();
                Assert.GreaterOrEqual(viewport.rect.height, prompt.fontSize * 6f,
                    "An empty feedback band should make room for six prompt lines.");
                RectTransform choices = host.transform.Find("AnswerChoices").GetComponent<RectTransform>();
                Button[] buttons = choices.GetComponentsInChildren<Button>();
                Assert.AreEqual(count, buttons.Length);
                for (int row = 0; row < (count + 1) / 2; row++)
                {
                    RectTransform first = buttons[row * 2].GetComponent<RectTransform>();
                    RectTransform last = buttons[Mathf.Min(row * 2 + 1, count - 1)].GetComponent<RectTransform>();
                    Assert.AreEqual(0f, (first.anchoredPosition.x + last.anchoredPosition.x) / 2f, 1f,
                        "Each row, including a single last answer, must be centered.");
                }
                float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
                RectTransform actions = host.transform.Find("ChallengeActions").GetComponent<RectTransform>();
                LayoutRebuilder.ForceRebuildLayoutImmediate(actions);
                RectTransform hint = actions.GetComponentInChildren<Button>().GetComponent<RectTransform>();
                Assert.GreaterOrEqual(hint.rect.height * scale, 44f);
                Assert.LessOrEqual(hint.rect.height * scale, 72f, "Gabay should remain a compact action.");
                float hintTop = host.transform.InverseTransformPoint(hint.TransformPoint(new Vector3(0f, hint.rect.yMax))).y;
                float choiceBottom = float.PositiveInfinity;
                foreach (Button button in buttons)
                {
                    RectTransform tile = button.GetComponent<RectTransform>();
                    choiceBottom = Mathf.Min(choiceBottom, host.transform.InverseTransformPoint(
                        tile.TransformPoint(new Vector3(0f, tile.rect.yMin))).y);
                }
                Assert.GreaterOrEqual((choiceBottom - hintTop) * scale, 12f,
                    "Keep a clear gap between answer tiles and Gabay.");
                board.ShowFeedback("Tama! Susunod na salita.");
                RectTransform status = host.transform.Find("Status").GetComponent<RectTransform>();
                Assert.Less(status.anchorMax.y, viewport.anchorMin.y,
                    "Visible feedback must have its own space below the prompt.");
                Assert.GreaterOrEqual(viewport.rect.height, prompt.fontSize * 4f);
                Canvas.ForceUpdateCanvases();
                TMP_Text feedback = status.GetComponent<TMP_Text>();
                feedback.ForceMeshUpdate();
                Assert.IsFalse(feedback.isTextOverflowing, "Ordinary feedback must remain fully readable.");
                board.Render(session);
                Assert.GreaterOrEqual(viewport.rect.height, prompt.fontSize * 6f,
                    "Returning to empty feedback must restore the larger reading area.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(sequence); }
        }

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
        public void LongChallengePrompt_KeepsItsInkInsideTheScrollbarLane(float width, float height)
        {
            GameObject root = new("Prompt typography test", typeof(RectTransform), typeof(Canvas));
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            try
            {
                SetPhoneCanvas(root, width, height);
                sequence.units = new[] { new ChallengeUnitDefinition
                {
                    unitId = "prompt-test", mode = ChallengeMode.WordPlacement,
                    prompt = "Kapag naalala ang ugat at pinili ang sariling landas, nagiging ______ ang tinig.",
                    tokens = new[] { new ChallengeTokenDefinition { tokenId = "answer", occurrenceId = "answer", displayText = "MALAYA" } },
                    candidateOccurrenceIds = new[] { "answer" },
                    slots = new[] { new ChallengeSlotDefinition { slotId = "answer", expectedOccurrenceId = "answer" } }
                } };
                ChallengeSession session = new(sequence);
                session.Enter();
                GameObject host = new("Challenge", typeof(RectTransform));
                host.transform.SetParent(root.transform, false);
                host.AddComponent<ChallengeModeUI>().Render(session);
                TMP_Text prompt = host.transform.Find("PromptViewport/Prompt").GetComponent<TMP_Text>();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(prompt.rectTransform);
                prompt.ForceMeshUpdate();
                Assert.LessOrEqual(prompt.textBounds.size.x, prompt.rectTransform.rect.width);
                Assert.IsFalse(prompt.isTextOverflowing);
                Assert.GreaterOrEqual(prompt.fontSize, UITextScale.Body);
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

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void BoldContinueLabel_KeepsGlyphInkInsideItsButton(float width, float height)
        {
            GameObject root = new("Bold button typography test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
                Button button = CreateAction(root.transform, "Magpatuloy");
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale * 0.92f * 0.40f, 138f);
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                label.fontStyle = FontStyles.Bold;
                ScrollPanelArt.SizeButtonLabel(button);
                Canvas.ForceUpdateCanvases();
                label.ForceMeshUpdate();
                Assert.IsFalse(label.isTextOverflowing);
                Assert.LessOrEqual(label.textBounds.size.x, label.rectTransform.rect.width);
                Assert.GreaterOrEqual(label.fontSize, UITextScale.Body);
            }
            finally { Object.DestroyImmediate(root); }
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

        [Test]
        public void AuthoredReadingScroll_KeepsGlyphsClearOfItsExistingTrack()
        {
            GameObject root = new("Authored scrollbar test", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 600f);
                GameObject viewport = new("Viewport", typeof(RectTransform), typeof(ScrollRect));
                viewport.transform.SetParent(root.transform, false);
                ScrollPanelArt.SetAnchors(viewport.GetComponent<RectTransform>(), Rect.MinMaxRect(0f, 0f, 1f, 1f));
                ScrollRect scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewport.GetComponent<RectTransform>();
                scroll.horizontal = false;
                GameObject content = new("Content", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                ScrollPanelArt.SetAnchors(content.GetComponent<RectTransform>(), Rect.MinMaxRect(0f, 0f, 1f, 1f));
                scroll.content = content.GetComponent<RectTransform>();
                GameObject bar = new("AuthoredBar", typeof(RectTransform), typeof(Scrollbar));
                bar.transform.SetParent(viewport.transform, false);
                RectTransform track = bar.GetComponent<RectTransform>();
                track.anchorMin = new Vector2(1f, 0f);
                track.anchorMax = Vector2.one;
                track.pivot = new Vector2(1f, 0.5f);
                track.sizeDelta = new Vector2(28f, 0f);
                scroll.verticalScrollbar = bar.GetComponent<Scrollbar>();
                TMP_Text body = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
                body.transform.SetParent(content.transform, false);
                TutorialFontProvider.ApplyTo(body);
                body.alignment = TextAlignmentOptions.TopLeft;
                body.fontSize = UITextScale.Body;
                body.text = "Ang mga alaala ay patuloy niyang iingatan.";
                Assert.AreSame(scroll, ScrollPanelArt.MakeReadingScroll(body));
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                body.ForceMeshUpdate();
                Vector3 inkRight = body.transform.TransformPoint(new Vector3(body.textBounds.max.x, 0f, 0f));
                Vector3 trackLeft = track.TransformPoint(new Vector3(track.rect.xMin, 0f, 0f));
                Assert.Less(inkRight.x, trackLeft.x, "Prose must stay clear of the authored scrollbar.");
                Assert.AreEqual(1, root.GetComponentsInChildren<ScrollRect>().Length);
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

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void ResetHeadings_FitAboveTheReadingViewportInEveryState(float width, float height)
        {
            GameObject root = new("Reset typography test", typeof(RectTransform));
            try
            {
                ResetJourneyConfirmationPanel panel = root.AddComponent<ResetJourneyConfirmationPanel>();
                panel.Present(() => ResetJourneyOutcome.RetryableFailure, null);
                SetPhoneCanvas(root, width, height);
                TMP_Text title = root.transform.Find("Overlay/Card/Title").GetComponent<TMP_Text>();
                ScrollRect body = root.GetComponentInChildren<ScrollRect>();
                Button confirm = root.transform.Find("Overlay/Card/ConfirmButton").GetComponent<Button>();
                AssertHeadingFits();
                confirm.onClick.Invoke();
                AssertHeadingFits();
                panel.Present(() => ResetJourneyOutcome.Succeeded, null);
                confirm.onClick.Invoke();
                AssertHeadingFits();

                void AssertHeadingFits()
                {
                    Canvas.ForceUpdateCanvases();
                    title.ForceMeshUpdate();
                    Assert.IsFalse(title.isTextOverflowing, title.text);
                    Assert.GreaterOrEqual(title.fontSize, UITextScale.Body);
                    Assert.Less(body.viewport.anchorMax.y, title.rectTransform.anchorMin.y,
                        "The body must not overlap the heading.");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(320f, 568f)]
        [TestCase(360f, 800f)]
        [TestCase(430f, 932f)]
        public void EndingSummary_FitsAboveTheCreditsWithoutShrinking(float width, float height)
        {
            GameObject root = new("Ending typography test", typeof(RectTransform));
            try
            {
                root.AddComponent<CampaignEndingScreenUI>().Present();
                SetPhoneCanvas(root, width, height);
                Transform paper = root.transform.Find("EndingBackdrop/SafeArea/EndingScroll");
                TMP_Text summary = paper.Find("Summary").GetComponent<TMP_Text>();
                Canvas.ForceUpdateCanvases();
                summary.ForceMeshUpdate();
                Assert.IsFalse(summary.isTextOverflowing);
                Assert.GreaterOrEqual(summary.fontSize, UITextScale.Body);
                Assert.Less(paper.Find("CreditsViewport").GetComponent<RectTransform>().anchorMax.y,
                    summary.rectTransform.anchorMin.y);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void SetPhoneCanvas(GameObject root, float width, float height)
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            float scale = Mathf.Sqrt(width / 1080f * height / 1920f);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(width / scale, height / scale);
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
