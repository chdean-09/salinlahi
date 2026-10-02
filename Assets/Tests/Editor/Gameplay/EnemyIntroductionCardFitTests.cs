using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// The enemy introduction card grows to fit its ability line and the continue prompt under it.
    /// Regression for the Gapos card, whose six-row copy spilled past the card's bottom edge with
    /// "Tap to continue" drawn over its last rows. Layout mirrors the card in Gameplay.unity.
    /// </summary>
    [TestFixture]
    public sealed class EnemyIntroductionCardFitTests
    {
        private const string CardFontGuid = "b3a97f8bae7014bd4b31bc84b88a29b9";
        private const string GaposAbilityLine =
            "While visible, it locks one enemy not bound by another Gapos. Defeat Gapos to release "
            + "it; the next symbol in the current word stays open.";

        private readonly List<Object> _created = new List<Object>();
        private EnemyIntroductionCardView _view;
        private RectTransform _card;
        private TextMeshProUGUI _ability;

        [SetUp]
        public void SetUp()
        {
            var canvasGo = new GameObject("TestCardCanvas", typeof(RectTransform), typeof(Canvas));
            _created.Add(canvasGo);

            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(CanvasGroup));
            cardGo.transform.SetParent(canvasGo.transform, false);
            _card = cardGo.GetComponent<RectTransform>();
            _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(860f, 320f);
            _card.anchoredPosition = new Vector2(0f, 220f);

            var abilityGo = new GameObject("AbilityText", typeof(RectTransform));
            abilityGo.transform.SetParent(cardGo.transform, false);
            _ability = abilityGo.AddComponent<TextMeshProUGUI>();
            _ability.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                AssetDatabase.GUIDToAssetPath(CardFontGuid));
            _ability.fontSize = 38f;
            _ability.alignment = TextAlignmentOptions.TopLeft;
            RectTransform abilityRect = _ability.rectTransform;
            abilityRect.anchorMin = new Vector2(0f, 0f);
            abilityRect.anchorMax = new Vector2(1f, 0f);
            abilityRect.pivot = Vector2.zero;
            abilityRect.anchoredPosition = new Vector2(380f, 34f);
            abilityRect.sizeDelta = new Vector2(-420f, 130f);

            var host = new GameObject("TestIntroductionCardView");
            _created.Add(host);
            _view = host.AddComponent<EnemyIntroductionCardView>();
            SetPrivateField("_cardGroup", cardGo.GetComponent<CanvasGroup>());
            SetPrivateField("_cardRect", _card);
            SetPrivateField("_abilityText", _ability);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }
            _created.Clear();
        }

        [Test]
        public void LongAbilityLine_GrowsCardDownward_AndKeepsPromptBelowText()
        {
            Assume.That(_ability.font, Is.Not.Null, "setup: the card font must load");
            float authoredTop = CardTopEdge();

            _view.PrepareCard(null, "Gapos", null, GaposAbilityLine);
            _view.SetCardProgress(1f);
            _view.ShowAbilityLine(GaposAbilityLine);
            _view.ShowContinuePrompt();

            RectTransform prompt = FindRuntimePrompt();
            Assert.IsNotNull(prompt, "the card must build its own continue prompt");

            float width = _ability.rectTransform.rect.width;
            float textHeight = _ability.GetPreferredValues(GaposAbilityLine, width, 0f).y;

            Assert.Greater(_card.rect.height, 320f, "six rows of copy cannot fit the authored card");
            Assert.AreEqual(authoredTop, CardTopEdge(), 0.01f, "the card grows downward only");
            Assert.GreaterOrEqual(_ability.rectTransform.rect.height + 0.01f, textHeight,
                "the ability box must hold every row of its copy");
            Assert.GreaterOrEqual(BottomInCard(_ability.rectTransform), TopInCard(prompt),
                "the prompt must sit under the ability copy, not over it");
            Assert.GreaterOrEqual(BottomInCard(prompt), 0f, "the prompt must stay inside the card");
        }

        [Test]
        public void ShortAbilityLine_KeepsAuthoredCardSize()
        {
            Assume.That(_ability.font, Is.Not.Null, "setup: the card font must load");

            _view.PrepareCard(null, "Gapos", null, GaposAbilityLine);
            _view.PrepareCard(null, "Abong Simula", null, "It covers the first symbol of a word with ash.");
            _view.SetCardProgress(1f);

            Assert.AreEqual(320f, _card.rect.height, 0.01f,
                "a line that fits keeps the authored card, even after a taller card");
            Assert.AreEqual(220f, _card.anchoredPosition.y, 0.01f);
        }

        private float CardTopEdge()
        {
            return _card.anchoredPosition.y + (1f - _card.pivot.y) * _card.rect.height;
        }

        // Edges measured up from the card's bottom edge; both labels are direct children.
        private float BottomInCard(RectTransform child)
        {
            return child.localPosition.y + child.rect.yMin - _card.rect.yMin;
        }

        private float TopInCard(RectTransform child)
        {
            return child.localPosition.y + child.rect.yMax - _card.rect.yMin;
        }

        private RectTransform FindRuntimePrompt()
        {
            foreach (Transform child in _card)
            {
                if (child.name.StartsWith("[Runtime]"))
                    return (RectTransform)child;
            }
            return null;
        }

        private void SetPrivateField(string name, object value)
        {
            FieldInfo field = typeof(EnemyIntroductionCardView).GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(_view, value);
        }
    }
}
