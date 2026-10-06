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
        public void AuthoredGameText_ContainsNoEmDashes()
        {
            foreach (string path in UnityEditor.AssetDatabase.GetAllAssetPaths())
            {
                if (!path.StartsWith("Assets/", System.StringComparison.Ordinal)
                    || path.StartsWith("Assets/TextMesh Pro/", System.StringComparison.Ordinal))
                    continue;

                string extension = System.IO.Path.GetExtension(path);
                if (extension != ".asset" && extension != ".unity" && extension != ".prefab"
                    && extension != ".txt" && extension != ".json")
                    continue;

                string text = System.IO.File.ReadAllText(path);
                StringAssert.DoesNotContain("\u2014", text, path);
                StringAssert.DoesNotMatch(@"\\u2014|&#(?:8212|x2014);|&mdash;", text, path);
            }
        }

        [TestCase("AWA: malasakit na nadarama para sa kapwa.")]
        [TestCase("AWA\u2014malasakit na nadarama para sa kapwa.")]
        public void Build_DefinitionSeparatorDoesNotLeakIntoHint(string descriptor)
        {
            LevelConfigSO level = CreateLevel(FocusWord("awa", "AWA", "compassion", descriptor));
            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);
            Assert.AreEqual(new[] { "malasakit na nadarama para sa kapwa." }, entries[0].Lines);
        }

        [Test]
        public void Build_NullLevel_ReturnsEmpty()
        {
            Assert.AreEqual(0, SentenceHintContent.Build(null).Count);
        }

        [Test]
        public void Build_PrefersTheMatchedObjectiveClueWithoutRepeatingTheDialogue()
        {
            LevelConfigSO level = CreateLevel(FocusWord("father", "AMA", "father",
                "AMA: ang haligi ng tahanan. Binubuo ito ng dalawang titik: A at MA."));
            RestorationObjectiveUnit unit = ObjectiveUnit("ama", "ang haligi ng tahanan", Target("a"), Target("ma"));
            unit.displayLabel = "AMA";
            level.restorationObjective = new RestorationObjectiveDefinition
            {
                units = new List<RestorationObjectiveUnit> { unit },
            };

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("_ _ _", entries[0].Label);
            Assert.AreEqual(new[] { "ang haligi ng tahanan" }, entries[0].Lines);
        }

        [Test]
        public void Build_ExplicitHintKeepsTutorialInstructionOutOfTheScroll()
        {
            FocusWordDefinition word = FocusWord("child", "BATA", "child",
                "BATA: nabakas mo na ito noon. Ngayon, gagamitin mo ito sa isang pangungusap.");
            word.hintText = "ang musmos na sumisibol, ang simula ng bawat alaala.";
            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(CreateLevel(word));
            Assert.AreEqual(new[] { word.hintText }, entries[0].Lines);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        [TestCase(6)] [TestCase(7)] [TestCase(8)] [TestCase(9)] [TestCase(10)]
        [TestCase(11)] [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void Build_AuthoredCampaignHasOneCluePerFocusWord(int levelNumber)
        {
            LevelConfigSO level = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset");
            Assert.IsNotNull(level);
            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);
            int wordHints = 0;
            foreach (SentenceHintContent.Entry entry in entries)
            {
                if (entry.Label == "Konteksto ng pangungusap")
                    continue;
                FocusWordDefinition focus = level.focusWords[wordHints];
                string spelling = string.IsNullOrWhiteSpace(focus.latinSpelling)
                    ? focus.displayLabel : focus.latinSpelling;
                Assert.AreEqual(spelling.Trim().Length, entry.Label.Replace(" ", string.Empty).Length);
                StringAssert.IsMatch(@"^_( _)*$", entry.Label);
                wordHints++;
                Assert.AreEqual(1, entry.Lines.Count);
                StringAssert.DoesNotMatch(@"^_+\s*[\u2014:-]", entry.Lines[0]);
            }
            Assert.AreEqual(level.focusWords.Count, wordHints);
        }

        [Test]
        public void Build_EmptyLevel_ReturnsEmpty()
        {
            LevelConfigSO level = CreateLevel();

            Assert.AreEqual(0, SentenceHintContent.Build(level).Count);
        }

        [Test]
        public void Build_KeepsAnswerBlanksAndDescriptorOnly()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugnayan.01.focus.01", "AWA", "compassion",
                    "AWA: malasakit na nadarama para sa kapwa.",
                    "A + WA. Ang awa ang damdaming gumigising sa akin."),
                FocusWord("level.ugnayan.01.focus.02", "GAWA", "action",
                    "GAWA: ang malasakit na isinasakatuparan."));
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
            Assert.AreEqual("_ _ _", entries[0].Label);
            Assert.AreEqual(
                new[] { "malasakit na nadarama para sa kapwa." },
                entries[0].Lines);
            Assert.AreEqual("_ _ _ _", entries[1].Label);
            Assert.AreEqual(
                new[] { "ang malasakit na isinasakatuparan." },
                entries[1].Lines);
        }

        [Test]
        public void Build_ExcludesPromptsAndInstructionLines()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.ugat.01.focus.01", "INA", "mother",
                    "INA: ang nagluwal at nag-aruga.",
                    "Bakasin mo ang bawat titik upang maibalik ang alaala ni Ina."));
            level.challengeSequence = CreateSequence(
                Unit(ChallengeMode.GuidedTracing, "Draw E/I. Follow the guide.", ""),
                Unit(ChallengeMode.SentenceRestoration,
                    "Ibalik ang INA sa alaala.", "level.ugat.01.focus.01"));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("_ _ _", entries[0].Label);
            Assert.AreEqual(
                new[] { "ang nagluwal at nag-aruga." },
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
                    "INA: ang nagluwal at nag-aruga. Binubuo ito ng dalawang titik: I at NA."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(
                new[] { "ang nagluwal at nag-aruga." },
                entries[0].Lines);
        }

        [Test]
        public void Build_DedupesIdenticalDescriptors()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", "AWA", "compassion",
                    "AWA: malasakit na nadarama para sa kapwa."),
                FocusWord("level.x.focus.02", "GAWA", "action",
                    "AWA: malasakit na nadarama para sa kapwa."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            // The second descriptor cleans to the same line, so it is dropped and
            // the empty entry never appears.
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("_ _ _", entries[0].Label);
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
            Assert.AreEqual("Konteksto ng pangungusap", entries[0].Label);
            Assert.AreEqual(new[] { "__ __: ilaw ng tahanan" }, entries[0].Lines);
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
                    "AWA: malasakit na nadarama para sa kapwa."));

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual("_ _ _", entries[0].Label);
        }

        [Test]
        public void Build_WithoutMeaning_UsesAnswerLength()
        {
            LevelConfigSO level = CreateLevel(
                FocusWord("level.x.focus.01", null, null, "hint line"));
            level.focusWords[0].latinSpelling = "KASAMA";

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(level);

            Assert.AreEqual("_ _ _ _ _ _", entries[0].Label);
        }

        [TestCase("INA", "_ _ _")]
        [TestCase("BATA", "_ _ _ _")]
        [TestCase("MAHALAGA", "_ _ _ _ _ _ _ _")]
        [TestCase(" INA ", "_ _ _")]
        public void Build_UsesLatinAnswerLengthInsteadOfMeaningOrDisplayLabel(string spelling, string expected)
        {
            FocusWordDefinition word = FocusWord("word", "Different label", "mother");
            word.latinSpelling = spelling;
            word.hintText = "ilaw ng tahanan";

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(CreateLevel(word));

            Assert.AreEqual(expected, entries[0].Label);
            Assert.AreEqual(new[] { "ilaw ng tahanan" }, entries[0].Lines);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void Build_WithoutLatinSpelling_UsesDisplayAnswerLength(string spelling)
        {
            FocusWordDefinition word = FocusWord("word", "INA", "mother");
            word.latinSpelling = spelling;
            word.hintText = "ilaw ng tahanan";

            Assert.AreEqual("_ _ _", SentenceHintContent.Build(CreateLevel(word))[0].Label);
        }

        [Test]
        public void Build_WithoutAnswerSpelling_LeavesClueUnlabeled()
        {
            FocusWordDefinition word = FocusWord("word", null, "mother");
            word.hintText = "ilaw ng tahanan";

            List<SentenceHintContent.Entry> entries = SentenceHintContent.Build(CreateLevel(word));

            Assert.AreEqual(string.Empty, entries[0].Label);
            Assert.AreEqual(new[] { "ilaw ng tahanan" }, entries[0].Lines);
        }

        [TestCase("KASAMA", "companion", "kapiling, kaagapay")]
        [TestCase("INA", "mother", "nanay")]
        public void ChallengeHint_UsesFilipinoSynonymsWithoutAnswerOrEnglishMeaning(
            string answer, string meaning, string synonyms)
        {
            FocusWordDefinition word = FocusWord("word", answer, meaning);
            word.latinSpelling = answer;
            word.hintSynonyms = synonyms;

            string hint = SentenceHintContent.BuildChallengeHint(word);

            Assert.AreEqual(synonyms, hint);
            StringAssert.DoesNotContain(answer, hint);
            StringAssert.DoesNotContain(meaning, hint);
        }

        [Test]
        public void ChallengeHint_DoesNotUseTheSentenceDescriptor()
        {
            FocusWordDefinition word = FocusWord("word", "KASAMA", "companion", "Bakasin ang KA at SA at MA.");
            word.hintText = "taong kapiling; hindi iniiwan ang kasama sa hirap";
            word.hintSynonyms = "kapiling, kaagapay";

            Assert.AreEqual("kapiling, kaagapay",
                SentenceHintContent.BuildChallengeHint(word));
        }

        [Test]
        public void ChallengeHint_MissingSynonymsDoesNotFallBackToSentenceOrMeaning()
        {
            Assert.AreEqual(string.Empty, SentenceHintContent.BuildChallengeHint(null));
            FocusWordDefinition word = FocusWord("word", "KASAMA", "companion",
                "KASAMA: taong kapiling sa gawain at paglalakbay.");
            word.hintText = "taong kapiling sa gawain";
            Assert.AreEqual(string.Empty,
                SentenceHintContent.BuildChallengeHint(word));
            word.hintSynonyms = "KASAMA";
            Assert.AreEqual(string.Empty, SentenceHintContent.BuildChallengeHint(word));
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
