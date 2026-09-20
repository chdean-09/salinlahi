using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public sealed class LevelReadyAndNameLossTests
    {
        [SetUp]
        public void SetUp()
        {
            NameLossEffectRegistry.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            NameLossEffectRegistry.ResetForTests();
        }

        [Test]
        public void ReadyTitle_UsesChapterLevelNumberAndAuthoredName()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.chapterName = "Ugat";
            config.levelNumber = 1;
            config.levelName = "Ang Unang Tinig";

            Assert.AreEqual(
                "Ugat · Level 1: Ang Unang Tinig",
                LevelReadyScreenController.BuildTitle(config));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ReadyObjective_DoesNotRepeatFocusWords()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.focusWords.Add(new FocusWordDefinition
            {
                displayLabel = "INA",
                meaning = "mother"
            });
            config.focusWords.Add(new FocusWordDefinition
            {
                latinSpelling = "AMA",
                meaning = "father"
            });

            string copy = LevelReadyScreenController.BuildObjectiveText(config);

            Assert.AreEqual("Learn the symbols, then defend the shrine.", copy);
            StringAssert.DoesNotContain("INA", copy);
            StringAssert.DoesNotContain("AMA", copy);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ReadyObjective_ExplainsMarkedSentenceRestoration()
        {
            BaybayinCharacterSO ma = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ma.stableId = "symbol.test.ma.ready";
            ma.syllable = "ma";

            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.restorationObjective = new RestorationObjectiveDefinition
            {
                displayMode = RestorationDisplayMode.MarkedContext,
                units = new System.Collections.Generic.List<RestorationObjectiveUnit>
                {
                    new RestorationObjectiveUnit
                    {
                        stableId = "sentence",
                        tokens = new System.Collections.Generic.List<RestorationObjectiveToken>
                        {
                            new RestorationObjectiveToken
                            {
                                kind = RestorationTokenKind.Target,
                                occurrenceId = "sentence.ma",
                                target = new SymbolValueReference { symbol = ma },
                            },
                        },
                    },
                },
            };

            string copy = LevelReadyScreenController.BuildObjectiveText(config);

            Assert.AreEqual("Restore the marked syllables in the sentence.", copy);

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(ma);
        }

        [Test]
        public void ReadyObjective_ExplainsLegacyFocusWordRestoration()
        {
            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            config.activeClueRestorationEnabled = true;
            config.focusWords.Add(new FocusWordDefinition
            {
                displayLabel = "BATA",
                meaning = "bunga ng pagmamahalan",
            });

            Assert.AreEqual(
                "Restore the focus words, then defend the shrine.",
                LevelReadyScreenController.BuildObjectiveText(config));

            Object.DestroyImmediate(config);
        }

        [TestCase(0, 4, "Symbol 1 of 4")]
        [TestCase(3, 4, "Symbol 4 of 4")]
        public void SymbolCardProgress_UsesOneBasedPosition(int index, int total, string expected)
        {
            Assert.AreEqual(expected, SymbolLearningCardController.BuildProgressText(index, total));
        }

        [Test]
        public void CombatRetryIntent_IsConsumedOnce()
        {
            LevelRetryIntent.Clear();
            LevelRetryIntent.RequestCombatOnly();

            Assert.IsTrue(LevelRetryIntent.ConsumeCombatOnly());
            Assert.IsFalse(LevelRetryIntent.ConsumeCombatOnly());
        }

        [Test]
        public void ReviewLesson_ClearsCombatRetryIntent()
        {
            LevelRetryIntent.RequestCombatOnly();
            LevelRetryIntent.Clear();

            Assert.IsFalse(LevelRetryIntent.ConsumeCombatOnly());
        }

        [Test]
        public void NameLossRegistry_StaysActiveUntilEverySourceLeaves()
        {
            object first = new object();
            object second = new object();

            NameLossEffectRegistry.Register(first);
            NameLossEffectRegistry.Register(second);
            NameLossEffectRegistry.Unregister(first);

            Assert.IsTrue(NameLossEffectRegistry.IsActive);

            NameLossEffectRegistry.Unregister(second);
            Assert.IsFalse(NameLossEffectRegistry.IsActive);
        }
    }
}
