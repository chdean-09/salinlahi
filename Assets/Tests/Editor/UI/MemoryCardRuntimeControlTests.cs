using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-240 — the memory card's runtime-built controls.
    ///
    /// WHY THIS IS NOT A COPY OF CampaignSaveNoticeSceneWiringTests. Those fixtures read
    /// fields that were AUTHORED INTO A SCENE. This ticket authors nothing into any scene —
    /// the card builds itself — so a scene-shaped guard here would assert nothing and report
    /// a false green, which is the exact failure it would be meant to prevent. The guard that
    /// matters for a self-building surface is that Present() actually produces every control,
    /// that each is layoutable, and that no two fields collapsed onto one object.
    ///
    /// The failure this guards against has shipped on this project: Gameplay.unity carried
    /// _starCountText: {fileID: 0} and the Results screen rendered no stars at all while the
    /// EditMode and PlayMode suites and both validator profiles stayed green. Nothing but
    /// manual Play Mode inspection could tell it from success.
    ///
    /// Fields are private [SerializeField], so they are read through SerializedObject rather
    /// than widening the runtime API for a test.
    /// </summary>
    [TestFixture]
    public sealed class MemoryCardRuntimeControlTests
    {
        private readonly List<Object> _created = new();
        private GameObject _cardObject;

        [TearDown]
        public void TearDown()
        {
            // The card reparents itself under a canvas it creates, so destroying the card
            // object alone would leave that canvas behind and leak it into the next test.
            if (_cardObject != null)
            {
                Transform root = _cardObject.transform.root;
                Object.DestroyImmediate(root != null ? root.gameObject : _cardObject);
                _cardObject = null;
            }

            foreach (Object asset in _created)
                if (asset != null)
                    Object.DestroyImmediate(asset);
            _created.Clear();
        }

        [Test]
        public void Present_BuildsBothCardFaces()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);

            Assert.IsNotNull(Reference(serialized, "_frontRoot"), "The card has no front face.");
            Assert.IsNotNull(
                Reference(serialized, "_backRoot"),
                "The card has no back face, so AC-2's flip has nothing to flip to.");
        }

        [Test]
        public void Present_BuildsEveryTextControl()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);

            foreach (string field in new[] { "_numberText", "_titleText", "_wordsText", "_loreText" })
            {
                Assert.IsNotNull(
                    Reference(serialized, field),
                    field + " was never built, so that part of the card renders nothing at all "
                    + "and no other check in this project would notice.");
            }
        }

        [Test]
        public void Present_BuildsBothButtons()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);

            var flip = Reference(serialized, "_flipButton") as Button;
            var close = Reference(serialized, "_closeButton") as Button;

            Assert.IsNotNull(flip, "The Flip Card control was never built (AC-2).");
            Assert.IsNotNull(close, "The card can never be dismissed.");
            Assert.IsNotNull(
                flip.targetGraphic,
                "The flip button has no target Graphic, so it presents no raycast surface and "
                + "can never be clicked however it is wired.");
            Assert.IsTrue(flip.targetGraphic.raycastTarget);
        }

        /// <summary>
        /// NOTE on falsifying this guard: uGUI silently repairs a missing RectTransform on an
        /// object that carries a Graphic, so removing only the transform from a control with
        /// an Image does NOT make this fail — it is repaired and reported as passing.
        /// Removing only the Graphic is likewise not the control. The negative control has to
        /// remove BOTH, which is precisely the shape of the defect that shipped unnoticed.
        /// </summary>
        [Test]
        public void Present_EveryBuiltControlCarriesARectTransform()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);

            foreach (string field in ControlFields)
            {
                GameObject target = GameObjectOf(Reference(serialized, field));
                Assert.IsNotNull(target, field + " is unassigned after Present().");
                Assert.IsNotNull(
                    target.GetComponent<RectTransform>(),
                    "'" + target.name + "' (" + field + ") has a plain Transform, not a "
                    + "RectTransform, so uGUI cannot lay it out or draw it. It is invisible "
                    + "with no error and no failing test.");
            }
        }

        [Test]
        public void Present_ControlFieldsAreAllDistinctObjects()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);

            var seen = new Dictionary<GameObject, string>();
            foreach (string field in ControlFields)
            {
                GameObject target = GameObjectOf(Reference(serialized, field));
                if (target == null)
                    continue;

                Assert.IsFalse(
                    seen.ContainsKey(target),
                    field + " and " + (seen.TryGetValue(target, out string other) ? other : "?")
                    + " share one object (" + target.name + "). Render() writes the fields in "
                    + "order, so the earlier one is overwritten and never seen — the SALIN-272 "
                    + "defect, where three serialized fields pointed at a single object.");
                seen[target] = field;
            }
        }

        [Test]
        public void Present_ReturnsFalseWhenTheMemoryContentIsNotAuthored()
        {
            MemoryCardUI card = NewCard();

            bool presented = card.Present(UnauthoredEntry(), 5, null);

            Assert.IsFalse(
                presented,
                "Levels 6-15 carry rewardIds: [] under D-015. Present must report 'nothing to "
                + "show' so the caller carries on; a caller that waited on this would hang.");
            Assert.IsFalse(card.IsPresented);
        }

        [Test]
        public void Flip_ShowsExactlyOneFaceAtATime()
        {
            MemoryCardUI card = PresentAuthoredCard();
            var serialized = new SerializedObject(card);
            var front = Reference(serialized, "_frontRoot") as GameObject;
            var back = Reference(serialized, "_backRoot") as GameObject;

            Assert.IsTrue(front.activeSelf, "The card must open on its front.");
            Assert.IsFalse(back.activeSelf);

            card.Flip();
            Assert.IsFalse(front.activeSelf);
            Assert.IsTrue(back.activeSelf, "Flipping must reveal the lore face (AC-2, AC-5).");

            card.Flip();
            Assert.IsTrue(front.activeSelf, "Flipping back must restore the front face.");
            Assert.IsFalse(back.activeSelf);
        }

        /// <summary>
        /// D-021 and owner ruling R1 cut the displayed accuracy statistic; D-006 requires
        /// player-facing prose to say "drawing", never "tracing". Nothing else in this project
        /// would catch a percentage quietly reappearing on a new surface — the campaign
        /// validator reads content assets and is blind to every line this ticket adds.
        /// </summary>
        [Test]
        public void Present_ShowsNoAccuracyOrTracingText()
        {
            MemoryCardUI card = PresentAuthoredCard();

            foreach (TMP_Text label in card.GetComponentsInChildren<TMP_Text>(true))
            {
                string text = label.text ?? string.Empty;
                Assert.IsFalse(text.Contains("%"), "A percentage appeared on the memory card: " + text);
                Assert.IsFalse(
                    text.ToLowerInvariant().Contains("tracing"),
                    "D-006: player-facing prose says 'drawing', never 'tracing'. Found: " + text);
                Assert.IsFalse(
                    text.ToLowerInvariant().Contains("accuracy"),
                    "D-021 / ruling R1 removed the accuracy readout. Found: " + text);
            }
        }

        // ----- helpers -------------------------------------------------------------------

        private static readonly string[] ControlFields =
        {
            "_frontRoot", "_backRoot", "_numberText", "_titleText",
            "_wordsText", "_loreText", "_glyphRow", "_flipButton", "_closeButton"
        };

        private MemoryCardUI NewCard()
        {
            _cardObject = new GameObject("MemoryCardUnderTest");
            return _cardObject.AddComponent<MemoryCardUI>();
        }

        private MemoryCardUI PresentAuthoredCard()
        {
            MemoryCardUI card = NewCard();
            Assert.IsTrue(
                card.Present(AuthoredEntry(), 5, null),
                "Present returned false for fully authored content — the card built no surface.");
            return card;
        }

        private MemoryArchiveEntry AuthoredEntry()
        {
            var symbol = Track(ScriptableObject.CreateInstance<BaybayinCharacterSO>());
            var words = new List<MemoryArchiveWord>
            {
                new("INA", "mother", new List<BaybayinCharacterSO> { symbol }),
                new("AMA", "father", new List<BaybayinCharacterSO> { symbol })
            };

            return new MemoryArchiveEntry(
                "Ugat", 1, 1, 1, "level.test.1", "memory.ugat.01", true,
                "Ang Unang Tinig", words,
                "Sa liwanag ng gabing iyon.");
        }

        private static MemoryArchiveEntry UnauthoredEntry() =>
            new("Ugnayan", 2, 1, 6, "level.test.6", null, false,
                "Mula Awa sa Gawa", new List<MemoryArchiveWord>(), string.Empty);

        private static Object Reference(SerializedObject serialized, string fieldName)
        {
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                "MemoryCardUI no longer serializes '" + fieldName + "'. If the field was "
                + "renamed, add [FormerlySerializedAs] so any existing wiring survives, then "
                + "update this guard rather than deleting it.");
            return property.objectReferenceValue;
        }

        private static GameObject GameObjectOf(Object value)
        {
            if (value == null)
                return null;
            if (value is GameObject gameObject)
                return gameObject;
            return value is Component component ? component.gameObject : null;
        }

        private T Track<T>(T asset) where T : Object
        {
            _created.Add(asset);
            return asset;
        }
    }
}
