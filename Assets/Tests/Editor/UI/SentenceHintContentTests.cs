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
        public void Build_KeepsMeaningAndDescriptorOnly()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugnayan.01.focus.01", "AWA", "compassion",
                    "AWA—malasakit na nadarama para sa kapwa.",
                    "A + WA. Ang awa ang damdaming gumigising sa akin."),
                FocusWord("level.ugnayan.01.focus.02", "GAWA", "action",
                    "GAWA—ang malasakit na isinasakatuparan."));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.SentenceRestoration,
                    "Ang ______ ay malasakit na nadarama para sa kapwa.",
                    "level.ugnayan.01.focus.01"),
                Unit(ChallengeMode.SentenceRestoration,
                    "Sa ______ naipapakita kung tunay ang malasakit.",
                    "level.ugnayan.01.focus.02"));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            // Only the descriptor survives per word: usage/instruction lines and
            // challenge prompts are excluded so the scroll stays scannable.
            Assert.AreEqual(2, entries.Count);
            Assert.AreEqual("compassion", entries[0].Label);
            Assert.AreEqual(
                new[] { "______—malasakit na nadarama para sa kapwa." },
                entries[0].Lines);
            Assert.AreEqual("action", entries[1].Label);
            Assert.AreEqual(
                new[] { "______—ang malasakit na isinasakatuparan." },
                entries[1].Lines);
        }

        [Test]
        public void Build_ExcludesPromptsAndInstructionLines()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother",
                    "INA — ang nagluwal at nag-aruga.",
                    "Bakasin mo ang bawat titik upang maibalik ang alaala ni Ina."));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.GuidedTracing, "Draw E/I. Follow the guide.", ""),
                Unit(ChallengeMode.SentenceRestoration,
                    "Ibalik ang INA sa alaala.", "level.ugat.01.focus.01"));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("mother", entries[0].Label);
            Assert.AreEqual(
                new[] { "______ — ang nagluwal at nag-aruga." },
                entries[0].Lines);
        }

        [Test]
        public void Build_PromptOnlyLevel_ReturnsEmpty()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother"));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.WordPlacement,
                    "Ibalik ang INA sa alaala.", "level.ugat.01.focus.01"));

            // Prompts are never hint content — a level whose only material is a
            // prompt has no scroll and no chip.
            Assert.AreEqual(0, SentenceHintContent.Build(level).Count);
        }

        [Test]
        public void Build_RedactsAnswerWordCaseInsensitively()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother",
                    "Bakasin mo ang bawat titik upang maibalik ang alaala ni Ina."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(
                new[] { "Bakasin mo ang bawat titik upang maibalik ang alaala ni ______." },
                entries[0].Lines);
        }

        [Test]
        public void Build_StripsBinubuoSpellingRecipe()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother",
                    "INA — ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(
                new[] { "______ — ang nagluwal at nag-aruga." },
                entries[0].Lines);
        }

        [Test]
        public void Build_DedupesIdenticalDescriptors()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", "AWA", "compassion",
                    "AWA—malasakit na nadarama para sa kapwa."),
                FocusWord("level.x.focus.02", "GAWA", "action",
                    "AWA—malasakit na nadarama para sa kapwa."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            // The second descriptor cleans to the same line, so it is dropped and
            // the empty entry never appears.
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("compassion", entries[0].Label);
        }

        [Test]
        public void Build_RendersWordModeObjectiveWithClueAndHiddenTargets()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother"));
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.GuidedWords,
                units = new List<RestorationObjectiveUnit>
                {
                    ObjectiveUnit("u1", "ilaw ng tahanan",
                        Target("occ.1"), Target("occ.2")),
                },
            };

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(string.Empty, entries[0].Label);
            Assert.AreEqual(new[] { "__ __ — ilaw ng tahanan" }, entries[0].Lines);
        }

        [Test]
        public void Build_RendersContextModeObjectiveAsSentenceSkeleton()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.03.focus.01", "BATA", "child"),
                FocusWord("level.ugat.03.focus.02", "TAMA", "correct"));
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.MarkedContext,
                units = new List<RestorationObjectiveUnit>
                {
                    ObjectiveUnit("s1", null,
                        Literal("Ang "), Target("occ.1"), Literal("buting "),
                        Target("occ.2"), Target("occ.3")),
                    ObjectiveUnit("s2", null,
                        Literal(" ay gu"), Target("occ.4"), Literal("gawa ng "),
                        Target("occ.5"), Target("occ.6"), Literal(".")),
                },
            };

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(
                new[] { "Ang __buting __ __ ay gu__gawa ng __ __." },
                entries[0].Lines);
        }

        [Test]
        public void Build_SkipsFocusWordsWithNoContent()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", "EMPTY", null),
                FocusWord("level.x.focus.02", "AWA", "compassion",
                    "AWA—malasakit na nadarama para sa kapwa."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("compassion", entries[0].Label);
        }

        [Test]
        public void Build_WithoutMeaning_LeavesLabelUnlabeled()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", null, null, "hint line"));
            level.focusWords[0].latinSpelling = "KASAMA";

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            // The word itself is the answer, so it is never used as the heading.
            Assert.AreEqual(string.Empty, entries[0].Label);
        }

        private LevelConfigSO CreateLevel(params FocusWordDefinition[] words)
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.focusWords = new List<FocusWordDefinition>(words);
            _objectsToDestroy.Add(level);
            return level;
        }

        private FocusWordDefinition FocusWord(
            string stableId, string displayLabel, string meaning, params string[] dialogueLines)
        {
            return new FocusWordDefinition
            {
                stableId = stableId,
                displayLabel = displayLabel,
                meaning = meaning,
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

        private static RestorationObjectiveUnit ObjectiveUnit(
            string stableId, string clue, params RestorationObjectiveToken[] tokens)
        {
            return new RestorationObjectiveUnit
            {
                stableId = stableId,
                displayLabel = stableId,
                clue = clue,
                tokens = new List<RestorationObjectiveToken>(tokens),
            };
        }

        private static RestorationObjectiveToken Literal(string text)
        {
            return new RestorationObjectiveToken
            {
                kind = RestorationTokenKind.Literal,
                literalText = text,
            };
        }

        private static RestorationObjectiveToken Target(string occurrenceId)
        {
            return new RestorationObjectiveToken
            {
                kind = RestorationTokenKind.Target,
                occurrenceId = occurrenceId,
            };
        }
    }
}
