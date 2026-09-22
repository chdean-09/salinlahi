using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public sealed class SentenceHintContentTests
    {
        private readonly List<Object> _objectsToDestroy = new();

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
        public void Build_NullLevel_ReturnsEmpty()
        {
            Assert.AreEqual(0, SentenceHintContent.Build(null).Count);
        }

        [Test]
        public void Build_EmptyLevel_ReturnsEmpty()
        {
            LevelConfigSO level = CreateLevel();

            Assert.AreEqual(0, SentenceHintContent.Build(level).Count);
        }

        [Test]
        public void Build_GroupsDialogueAndMatchedPromptPerFocusWord()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugnayan.01.focus.01", "AWA",
                    "AWA—malasakit na nadarama para sa kapwa.",
                    "A + WA. Ang awa ang damdaming gumigising sa akin."),
                FocusWord("level.ugnayan.01.focus.02", "GAWA",
                    "GAWA—ang malasakit na isinasakatuparan."));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.SentenceRestoration,
                    "Ang ______ ay malasakit na nadarama para sa kapwa.",
                    "level.ugnayan.01.focus.01"),
                Unit(ChallengeMode.SentenceRestoration,
                    "Sa ______ naipapakita kung tunay ang malasakit.",
                    "level.ugnayan.01.focus.02"));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("AWA", entries[0].Label);
            Assert.AreEqual(
                new[]
                {
                    "AWA—malasakit na nadarama para sa kapwa.",
                    "A + WA. Ang awa ang damdaming gumigising sa akin.",
                    "Ang ______ ay malasakit na nadarama para sa kapwa.",
                },
                entries[0].Lines);
            Assert.AreEqual("GAWA", entries[1].Label);
            Assert.AreEqual(
                new[]
                {
                    "GAWA—ang malasakit na isinasakatuparan.",
                    "Sa ______ naipapakita kung tunay ang malasakit.",
                },
                entries[1].Lines);
        }

        [Test]
        public void Build_SkipsGuidedTracingPrompts()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "INA — ang nagluwal at nag-aruga."));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.GuidedTracing, "Draw E/I. Follow the guide.", ""),
                Unit(ChallengeMode.SentenceRestoration,
                    "Restore the family words: choose INA, then AMA.", ""));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            // The word entry holds only its dialogue line; the restoration prompt has no
            // focus-word evidence id, so it lands in the trailing unlabeled entry.
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("INA", entries[0].Label);
            Assert.AreEqual(new[] { "INA — ang nagluwal at nag-aruga." }, entries[0].Lines);
            Assert.AreEqual(string.Empty, entries[1].Label);
            Assert.AreEqual(
                new[] { "Restore the family words: choose INA, then AMA." },
                entries[1].Lines);
        }

        [Test]
        public void Build_DedupesIdenticalLinesAcrossSources()
        {
            const string shared = "Ang ______ ay malasakit na nadarama para sa kapwa.";
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugnayan.01.focus.01", "AWA", shared));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.SentenceRestoration, shared, "level.ugnayan.01.focus.01"));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(new[] { shared }, entries[0].Lines);
        }

        [Test]
        public void Build_SkipsFocusWordsWithNoContent()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", "EMPTY"),
                FocusWord("level.x.focus.02", "AWA", "AWA—malasakit na nadarama para sa kapwa."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("AWA", entries[0].Label);
        }

        [Test]
        public void Build_FallsBackToLatinSpellingLabel()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", null, "hint line"));
            level.focusWords[0].latinSpelling = "KASAMA";

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual("KASAMA", entries[0].Label);
        }

        private LevelConfigSO CreateLevel(params FocusWordDefinition[] words)
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords = new List<FocusWordDefinition>(words);
            _objectsToDestroy.Add(level);
            return level;
        }

        private FocusWordDefinition FocusWord(string stableId, string label, params string[] dialogueLines)
        {
            return new FocusWordDefinition
            {
                stableId = stableId,
                displayLabel = label,
                media = new ContentMediaReferences
                {
                    dialogue = dialogueLines.Length == 0 ? null : CreateDialogue(dialogueLines),
                },
            };
        }

        private DialogueSO CreateDialogue(params string[] texts)
        {
            DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            dialogue.lines = new DialogueLine[texts.Length];
            for (int i = 0; i < texts.Length; i++)
                dialogue.lines[i] = new DialogueLine { speakerName = "Test", text = texts[i] };
            _objectsToDestroy.Add(dialogue);
            return dialogue;
        }

        private ChallengeSequenceSO CreateSequence(params ChallengeUnitDefinition[] units)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            sequence.units = units;
            _objectsToDestroy.Add(sequence);
            return sequence;
        }

        private static ChallengeUnitDefinition Unit(
            ChallengeMode mode, string prompt, string evidenceContentId)
        {
            return new ChallengeUnitDefinition
            {
                unitId = "unit",
                mode = mode,
                prompt = prompt,
                evidenceContentId = evidenceContentId,
            };
        }
    }
}
