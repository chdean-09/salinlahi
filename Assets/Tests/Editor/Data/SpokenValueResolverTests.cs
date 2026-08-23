using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-157: the spoken-value clip/label resolution that lets E/I, O/U, and
    /// DA/RA cards follow the approved level context, with the documented
    /// fallbacks when no per-value data exists.
    /// </summary>
    public sealed class SpokenValueResolverTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [Test]
        public void ResolveClip_UsesTheMatchingSpokenValuesClip()
        {
            AudioClip valueClip = CreateClip("value-clip");
            AudioClip characterClip = CreateClip("character-clip");
            BaybayinCharacterSO symbol = CreateSymbol("da", characterClip,
                SpokenValue("value.test-da", "da", null),
                SpokenValue("value.test-ra", "ra", valueClip));

            Assert.AreSame(valueClip,
                SpokenValueResolver.ResolveClip(symbol, "value.test-ra"),
                "The per-value clip must win so DA/RA can diverge with an asset edit only.");
        }

        [Test]
        public void ResolveClip_FallsBackToCharacterClip_WhenTheValueHasNone()
        {
            AudioClip characterClip = CreateClip("character-clip");
            BaybayinCharacterSO symbol = CreateSymbol("da", characterClip,
                SpokenValue("value.test-da", "da", null));

            Assert.AreSame(characterClip,
                SpokenValueResolver.ResolveClip(symbol, "value.test-da"));
        }

        [Test]
        public void ResolveClip_FallsBackToCharacterClip_WhenTheIdDoesNotResolve()
        {
            AudioClip characterClip = CreateClip("character-clip");
            BaybayinCharacterSO symbol = CreateSymbol("da", characterClip,
                SpokenValue("value.test-da", "da", null));

            Assert.AreSame(characterClip,
                SpokenValueResolver.ResolveClip(symbol, "value.unknown"));
        }

        [Test]
        public void ResolveClip_ReturnsNull_WhenNoApprovedAudioExistsAnywhere()
        {
            // Level 1's four learning requirements ship exactly this shape today
            // (Level1AssetReadinessTests records the clips as manifest MISSING).
            BaybayinCharacterSO symbol = CreateSymbol("a", null,
                SpokenValue("value.test-a", "a", null));

            Assert.IsNull(SpokenValueResolver.ResolveClip(symbol, "value.test-a"));
        }

        [Test]
        public void ResolveClip_NullSymbol_ReturnsNull()
        {
            Assert.IsNull(SpokenValueResolver.ResolveClip(null, "value.test-a"));
        }

        [Test]
        public void ResolveLabel_FollowsTheSpokenValueId()
        {
            BaybayinCharacterSO symbol = CreateSymbol("da", null,
                SpokenValue("value.test-da", "da", null),
                SpokenValue("value.test-ra", "ra", null));

            Assert.AreEqual("ra",
                SpokenValueResolver.ResolveLabel(symbol, "value.test-ra"),
                "The visible label must follow the approved level context, not the glyph default.");
            Assert.AreEqual("da",
                SpokenValueResolver.ResolveLabel(symbol, "value.test-da"));
        }

        [Test]
        public void ResolveLabel_FallsBackToLegacySyllable_WhenTheIdDoesNotResolve()
        {
            BaybayinCharacterSO symbol = CreateSymbol("da", null,
                SpokenValue("value.test-da", "da", null));

            Assert.AreEqual("da", SpokenValueResolver.ResolveLabel(symbol, "value.unknown"));
        }

        [Test]
        public void ResolveLabel_FallsBackToTheIdSuffix_WhenTheSymbolHasNoText()
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            _objectsToDestroy.Add(symbol);

            Assert.AreEqual("ra", SpokenValueResolver.ResolveLabel(symbol, "value.ra"));
            Assert.AreEqual("ra", SpokenValueResolver.ResolveLabel(null, "value.ra"));
        }

        [Test]
        public void ResolveLabel_LastResort_IsAPlaceholderNeverEmpty()
        {
            Assert.AreEqual("?", SpokenValueResolver.ResolveLabel(null, null));
            Assert.AreEqual("?", SpokenValueResolver.ResolveLabel(null, "not-a-value-id"));
        }

        private static SpokenValueDefinition SpokenValue(
            string stableId, string displayValue, AudioClip clip)
        {
            return new SpokenValueDefinition
            {
                stableId = stableId,
                displayValue = displayValue,
                pronunciationClip = clip,
            };
        }

        private BaybayinCharacterSO CreateSymbol(
            string syllable, AudioClip characterClip, params SpokenValueDefinition[] values)
        {
            BaybayinCharacterSO symbol = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            _objectsToDestroy.Add(symbol);
            symbol.syllable = syllable;
            symbol.pronunciationClip = characterClip;
            symbol.spokenValues = new List<SpokenValueDefinition>(values);
            return symbol;
        }

        private AudioClip CreateClip(string name)
        {
            AudioClip clip = AudioClip.Create(name, 441, 1, 44100, false);
            _objectsToDestroy.Add(clip);
            return clip;
        }
    }
}
