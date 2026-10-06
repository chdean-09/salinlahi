using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Salinlahi.Tests.PlayMode.UI
{
    /// <summary>
    /// Q16: English is UI copy only — story dialogue, cutscenes and focus-word explanations stay
    /// Filipino, and there is no language setting. The focus-word preview used to append
    /// <c>" — " + focus.meaning</c>, so the card the player reads before Level 5's combat said
    /// "IBA — different" and "MANA — inheritance". Caught by playing the level, not by any test.
    ///
    /// The meaning is still authored and still content: it is what the Meaning mastery dimension
    /// matches on. It simply must not be printed beside the word. PlayMode because
    /// <see cref="FocusWordPreviewController.Present"/> is a coroutine.
    /// </summary>
    [TestFixture]
    public sealed class FocusWordPreviewLanguageTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator Preview_RendersTheWordAndItsSyllables_ButNeverTheEnglishMeaning()
        {
            var symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            symbol.characterID = "BA";
            symbol.stableId = "symbol.ba";
            _objectsToDestroy.Add(symbol);

            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords.Add(new FocusWordDefinition
            {
                stableId = "level.test.focus.iba",
                latinSpelling = "iba",
                displayLabel = "IBA",
                meaning = "different",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = symbol, spokenValueId = "value.ba" },
                },
            });
            _objectsToDestroy.Add(level);

            var go = new GameObject("FocusWordPreviewController_LanguageTest");
            var controller = go.AddComponent<FocusWordPreviewController>();
            _objectsToDestroy.Add(go);

            // Present blocks on Continue; one step is enough to compose the copy.
            IEnumerator present = controller.Present(level);
            present.MoveNext();
            yield return null;

            Assert.IsNotNull(controller.RenderedText, "the preview composed no copy at all.");
            StringAssert.Contains("IBA", controller.RenderedText,
                "the preview must still show the focus word itself.");
            Assert.That(
                controller.RenderedText,
                Does.Not.Contain("different"),
                "the preview printed the English meaning. Q16 allows English as UI copy only, and "
                + "this card is story-facing: the Filipino explanation reaches the player as "
                + "focus-word dialogue instead. Actual copy: " + controller.RenderedText);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Preview_WithGlyphArtwork_ShowsGlyphsAboveOriginalSyllableTextAndContinues(bool guidedObjective)
        {
            Texture2D texture = new Texture2D(2, 2);
            Sprite glyph = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f);
            _objectsToDestroy.Add(texture);
            _objectsToDestroy.Add(glyph);

            BaybayinCharacterSO ei = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ei.stableId = "symbol.ei";
            ei.syllable = "e/i";
            ei.spokenValues.Add(new SpokenValueDefinition { stableId = "value.i", displayValue = "i" });
            ei.almanacSprite = glyph;
            _objectsToDestroy.Add(ei);
            BaybayinCharacterSO na = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            na.stableId = "symbol.na";
            na.syllable = "na";
            na.glyphOutlineSprite = glyph;
            _objectsToDestroy.Add(na);

            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(level);
            var iValue = new SymbolValueReference { symbol = ei, spokenValueId = "value.i" };
            var naValue = new SymbolValueReference { symbol = na, spokenValueId = "value.na" };
            level.focusWords.Add(new FocusWordDefinition
            {
                displayLabel = "INA",
                meaning = "mother",
                decomposition = new List<SymbolValueReference> { iValue, naValue },
            });
            if (guidedObjective)
            {
                level.restorationObjective = new RestorationObjectiveDefinition
                {
                    displayMode = RestorationDisplayMode.GuidedWords,
                    units = new List<RestorationObjectiveUnit>
                    {
                        new RestorationObjectiveUnit
                        {
                            displayLabel = "INA",
                            clue = "ilaw ng tahanan",
                            tokens = new List<RestorationObjectiveToken>
                            {
                                new RestorationObjectiveToken
                                {
                                    kind = RestorationTokenKind.Target,
                                    occurrenceId = "ina.i", target = iValue,
                                },
                                new RestorationObjectiveToken
                                {
                                    kind = RestorationTokenKind.Target,
                                    occurrenceId = "ina.na", target = naValue,
                                },
                            },
                        },
                    },
                };
            }

            GameObject host = new GameObject("FocusWordPreviewController_SyllableTest");
            _objectsToDestroy.Add(host);
            FocusWordPreviewController controller = host.AddComponent<FocusWordPreviewController>();
            IEnumerator present = controller.Present(level);
            Assert.IsTrue(present.MoveNext());
            Assert.IsTrue(controller.IsPresenting);
            Assert.AreEqual("INA\ni · na", controller.RenderedText);
            TMP_Text preview = null;
            foreach (TMP_Text text in host.GetComponentsInChildren<TMP_Text>())
                if (text.text == controller.RenderedText)
                    preview = text;
            Assert.IsNotNull(preview);
            Assert.AreEqual(controller.RenderedText, preview.text);
            var glyphImages = new List<Image>();
            foreach (Image image in host.GetComponentsInChildren<Image>())
                if (image.sprite == glyph)
                    glyphImages.Add(image);
            Assert.AreEqual(2, glyphImages.Count, "Each syllable must retain its complementary Baybayin glyph.");
            foreach (Image image in glyphImages)
            {
                RectTransform row = image.transform.parent.GetComponent<RectTransform>();
                Assert.Greater(row.anchorMin.y, preview.rectTransform.anchorMax.y,
                    "The glyph row must sit above the word and syllable caption.");
            }

            host.GetComponentInChildren<Button>().onClick.Invoke();
            Assert.IsFalse(present.MoveNext());
            Assert.IsFalse(controller.IsPresenting);
            Assert.IsNull(host.GetComponentInChildren<TMP_Text>(), "Continue must hide the intro.");
        }

        [UnityTest]
        public IEnumerator Preview_UsesMarkedSentenceObjectiveInsteadOfLegacyFocusWords()
        {
            BaybayinCharacterSO ma = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ma.characterID = "MA";
            ma.stableId = "symbol.test.ma.marked";
            ma.syllable = "ma";
            _objectsToDestroy.Add(ma);

            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords.Add(new FocusWordDefinition
            {
                stableId = "legacy.bata",
                displayLabel = "BATA",
                decomposition = new List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = ma },
                },
            });
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.MarkedContext,
                units = new List<RestorationObjectiveUnit>
                {
                    new RestorationObjectiveUnit
                    {
                        stableId = "sentence.01",
                        tokens = new List<RestorationObjectiveToken>
                        {
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Literal,
                                literalText = "Ang ",
                            },
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Target,
                                occurrenceId = "sentence.ma.01",
                                target = new SymbolValueReference { symbol = ma },
                            },
                        },
                    },
                },
            };
            _objectsToDestroy.Add(level);

            var go = new GameObject("FocusWordPreviewController_ObjectiveTest");
            var controller = go.AddComponent<FocusWordPreviewController>();
            _objectsToDestroy.Add(go);

            IEnumerator present = controller.Present(level);
            present.MoveNext();
            yield return null;

            StringAssert.Contains("Ang", controller.RenderedText);
            StringAssert.Contains("<u>MA</u>", controller.RenderedText);
            StringAssert.DoesNotContain("BATA", controller.RenderedText);
        }

        [UnityTest]
        public IEnumerator Preview_HidesHiddenContextTargetsUntilRestored()
        {
            BaybayinCharacterSO i = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            i.characterID = "I";
            i.stableId = "symbol.test.i.hidden";
            i.syllable = "i";
            _objectsToDestroy.Add(i);

            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords.Add(new FocusWordDefinition
            {
                stableId = "legacy.ina",
                displayLabel = "INA",
            });
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.HiddenContext,
                units = new List<RestorationObjectiveUnit>
                {
                    new RestorationObjectiveUnit
                    {
                        stableId = "sentence.hidden",
                        tokens = new List<RestorationObjectiveToken>
                        {
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Literal,
                                literalText = "Ang ",
                            },
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Target,
                                occurrenceId = "sentence.hidden.i",
                                target = new SymbolValueReference { symbol = i },
                            },
                        },
                    },
                },
            };
            _objectsToDestroy.Add(level);

            GameObject go = new GameObject("FocusWordPreviewController_HiddenObjectiveTest");
            FocusWordPreviewController controller = go.AddComponent<FocusWordPreviewController>();
            _objectsToDestroy.Add(go);

            IEnumerator present = controller.Present(level);
            present.MoveNext();
            yield return null;

            StringAssert.Contains("Ang __", controller.RenderedText);
            StringAssert.DoesNotContain("INA", controller.RenderedText);
            StringAssert.DoesNotContain("AMA", controller.RenderedText);
            StringAssert.DoesNotContain("<u>I</u>", controller.RenderedText);
        }
    }
}
