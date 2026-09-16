using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
    }
}
