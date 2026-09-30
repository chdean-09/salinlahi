using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Campaign regression contracts read the shipped Level 2-15 assets directly. These are
    /// deliberately separate from synthetic coordinator tests: a passing engine fixture is not
    /// evidence that production assets carry the intended order, display mode, roster, boss
    /// topology, or finale policy.
    /// </summary>
    [TestFixture]
    public sealed class Batch2ProductionAssetContractTests
    {
        private static LevelConfigSO Load(int levelNumber)
        {
            string path = $"Assets/ScriptableObjects/Levels/Level{levelNumber}_Config.asset";
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            Assert.IsNotNull(level, $"Expected the shipped Level {levelNumber} asset at {path}.");
            return level;
        }

        private static string[] TargetSequence(LevelConfigSO level)
        {
            return level.restorationObjective.units
                .SelectMany(unit => unit?.tokens ?? new List<RestorationObjectiveToken>())
                .Where(token => token != null && token.IsTarget)
                .OrderBy(token => token.completionOrder)
                .Select(token => token.target.spokenValueId)
                .ToArray();
        }

        [Test]
        public void Level2_ProductionContract_IsCombatDiscoveryAndTaBaMaTa()
        {
            LevelConfigSO level = Load(2);

            Assert.IsNull(level.onboardingSequence);
            Assert.IsNull(level.tutorialSequence);
            Assert.IsTrue(level.suppressSymbolLearningCards);
            Assert.AreEqual(RestorationDisplayMode.ClueOnlyWords,
                level.restorationObjective.displayMode);
            CollectionAssert.AreEqual(
                new[] { "value.ta", "value.ba", "value.ma", "value.ta" },
                TargetSequence(level));
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            Assert.IsTrue(level.spawnAssignmentPolicy.OverflowIsUnbounded);
            Assert.AreEqual("value.ta", level.finalRestorationValue.spokenValueId);
        }

        [Test]
        public void Level3_ProductionContract_IsMarkedAndMaBaTaMaTaMa()
        {
            LevelConfigSO level = Load(3);

            Assert.AreEqual(RestorationDisplayMode.MarkedContext,
                level.restorationObjective.displayMode);
            CollectionAssert.AreEqual(
                new[] { "value.ma", "value.ba", "value.ta", "value.ma", "value.ta", "value.ma" },
                TargetSequence(level));
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            Assert.IsTrue(level.spawnAssignmentPolicy.OverflowIsUnbounded);
            Assert.AreEqual("value.ma", level.finalRestorationValue.spokenValueId);
        }

        [Test]
        public void Level4_ProductionContract_IsHiddenAndINaAMaNaNa()
        {
            LevelConfigSO level = Load(4);

            Assert.AreEqual(RestorationDisplayMode.HiddenContext,
                level.restorationObjective.displayMode);
            CollectionAssert.AreEqual(
                new[] { "value.i", "value.na", "value.a", "value.ma", "value.na", "value.na" },
                TargetSequence(level));
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            Assert.IsTrue(level.spawnAssignmentPolicy.OverflowIsUnbounded);
            Assert.AreEqual("value.ma", level.finalRestorationValue.spokenValueId);
        }

        [Test]
        public void Level5_ProductionContract_GatesFinalTaAndBoundsOverflow()
        {
            LevelConfigSO level = Load(5);

            Assert.AreEqual(RestorationDisplayMode.HiddenContext,
                level.restorationObjective.displayMode);
            Assert.IsTrue(level.suppressSymbolLearningCards);
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            Assert.IsTrue(level.spawnAssignmentPolicy.allowFinalWaveOverflow);
            Assert.Greater(level.spawnAssignmentPolicy.maxOverflowBatches, 0);
            Assert.LessOrEqual(level.spawnAssignmentPolicy.maxOverflowBatches, 12);
            Assert.AreEqual("value.ta", level.finalRestorationValue.spokenValueId);
            Assert.AreEqual(3, level.flowSegments.Count);
            Assert.AreEqual(level.waves.Count,
                level.flowSegments.Sum(segment => segment.waveCount));
            CollectionAssert.AreEqual(
                new[] { "value.ma", "value.na", "value.i", "value.ma", "value.ma", "value.ba",
                    "value.ma", "value.ma", "value.na", "value.a", "value.ma", "value.ma",
                    "value.na", "value.ta" },
                TargetSequence(level));
        }

        [Test]
        public void Levels6To15_ProductionContractsHaveStableIdentityContentAndChallengeData()
        {
            for (int levelNumber = 6; levelNumber <= 15; levelNumber++)
            {
                LevelConfigSO level = Load(levelNumber);

                Assert.AreEqual(levelNumber, level.levelNumber);
                Assert.AreEqual(ContentIdentity.RevisedLevelIds[levelNumber - 1], level.stableId,
                    $"Level {levelNumber} stable id must stay in the canonical campaign order.");
                if (levelNumber <= 14)
                {
                    Assert.IsNull(level.bossConfig,
                        $"Level {levelNumber} must remain a wave/challenge level; Level 15 is the "
                        + "only authored campaign boss.");
                }
                Assert.AreEqual(((levelNumber - 1) % ContentIdentity.RevisedLevelsPerEra) + 1,
                    level.eraLocalOrder,
                    $"Level {levelNumber} era-local order must match its campaign position.");
                Assert.AreEqual(2, level.focusWords.Count,
                    $"Level {levelNumber} must author its two focus words.");
                Assert.IsNotEmpty(level.cumulativeSymbolPool,
                    $"Level {levelNumber} must carry its cumulative symbol pool.");
                CollectionAssert.IsNotEmpty(level.rewardIds,
                    $"Level {levelNumber} must carry its save/result reward ids.");
                Assert.IsNotNull(level.challengeSequence,
                    $"Level {levelNumber} must assign a context challenge sequence.");
                Assert.IsNotEmpty(level.challengeSequence.units,
                    $"Level {levelNumber} challenge sequence must contain authored units.");
                Assert.IsTrue(ChallengeSequenceValidator.Validate(level.challengeSequence).IsValid,
                    $"Level {levelNumber} challenge sequence must pass its asset contract.");
                Assert.IsNotNull(level.finalRestorationValue,
                    $"Level {levelNumber} must assign a final restoration value.");
            }
        }

        [Test]
        public void Levels6To14_EveryAuthoredWaveCarriesEveryFocusSymbolAndNaturalEnemyCarrier()
        {
            for (int levelNumber = 6; levelNumber <= 14; levelNumber++)
            {
                LevelConfigSO level = Load(levelNumber);
                HashSet<string> required = FocusSymbols(level);
                Assert.IsNotEmpty(level.waves, $"Level {levelNumber} must author combat waves.");

                for (int waveIndex = 0; waveIndex < level.waves.Count; waveIndex++)
                {
                    WaveDefinition wave = level.waves[waveIndex];
                    Assert.IsNotNull(wave, $"Level {levelNumber} wave {waveIndex + 1} is null.");
                    if (wave.isIntermissionWave)
                        continue;

                    foreach (string symbolId in required)
                    {
                        Assert.IsTrue(wave.characters.Any(character =>
                                character != null && character.stableId == symbolId),
                            $"Level {levelNumber} wave {waveIndex + 1} must carry {symbolId} in characters.");
                        Assert.IsTrue(wave.enemyTypes.Any(enemy =>
                                enemy != null && enemy.assignedCharacter != null
                                && enemy.assignedCharacter.stableId == symbolId),
                            $"Level {levelNumber} wave {waveIndex + 1} must carry a natural enemy for {symbolId}.");
                    }
                }
            }
        }

        [Test]
        public void Level10_IsMixedWaveParagraphContentWithoutABoss_AndLevel15OwnsTheCampaignBoss()
        {
            LevelConfigSO level10 = Load(10);
            LevelConfigSO level15 = Load(15);

            Assert.IsNull(level10.bossConfig,
                "Level 10 is authored as mixed-wave paragraph restoration, not a boss level.");
            Assert.AreEqual(3, level10.flowSegments.Count);
            Assert.AreEqual(level10.waves.Count,
                level10.flowSegments.Sum(segment => segment.waveCount));
            Assert.AreEqual(RestorationDisplayMode.GuidedWords,
                level10.restorationObjective.displayMode);

            Assert.IsNotNull(level15.bossConfig,
                "Level 15 is the sole authored campaign boss level.");
            Assert.IsNotEmpty(level15.bossConfig.phases,
                "Level 15's boss asset must carry at least one authored phase.");
            Assert.IsNotNull(level15.bossConfig.bossEnemyData,
                "Level 15's boss asset must identify its boss enemy data.");
            Assert.AreEqual(3, level15.bossConfig.phases.Count,
                "The final Paglimot encounter has one phase for Ugat, Ugnayan, and all.");
            Assert.AreEqual(3, level15.flowSegments.Count,
                "Each boss phase must pair with one paragraph checkpoint.");
            Assert.AreEqual(0, level15.waves.Count,
                "Level 15's phase minions are authored in the boss config, not ordinary waves.");
            Assert.IsNotNull(level15.challengeSequence);
            CollectionAssert.AreEqual(
                new[] { "pamana15-restore-line-01", "pamana15-restore-line-03", "pamana15-restore-line-02" },
                level15.challengeSequence.units.Select(unit => unit.unitId).ToArray(),
                "The MALAYA paragraph checkpoint and its YA finale must be last.");
            Assert.AreEqual("MALAYA",
                level15.challengeSequence.units[2].tokens.Single(token =>
                    token.occurrenceId == level15.challengeSequence.units[2].slots[0].expectedOccurrenceId).displayText);
            CollectionAssert.AreEqual(new[] { "Ugat", "Ugnayan", "Lahat" },
                level15.bossConfig.phases.Select(phase => phase.displayName).ToArray());

            HashSet<string> permittedCharacters = new HashSet<string>(
                level15.allowedCharacters.Select(character => character.characterID));
            HashSet<string> guaranteedCharacters = new HashSet<string>(
                level15.bossConfig.phases.SelectMany(phase => phase.guaranteedSummonEnemyTypes)
                    .Where(enemy => enemy != null && enemy.assignedCharacter != null)
                    .Select(enemy => enemy.assignedCharacter.characterID));
            CollectionAssert.AreEquivalent(permittedCharacters, guaranteedCharacters,
                "Every permitted Level 15 glyph must have a guaranteed boss summon.");
            Assert.AreEqual("EnemyData_YaposngDilim",
                level15.bossConfig.phases[2].guaranteedSummonEnemyTypes.Last().name,
                "Yapos ng Dilim is the final guaranteed YA carrier.");
            CollectionAssert.AreEquivalent(permittedCharacters,
                level15.bossConfig.phases[2].summonEnemyTypes
                    .Where(enemy => enemy != null && enemy.assignedCharacter != null)
                    .Select(enemy => enemy.assignedCharacter.characterID).ToArray(),
                "The final phase's summon pool includes the full learned character set.");
        }

        [Test]
        public void Level14_GuaranteesAnRaCarrierInItsFirstWave()
        {
            LevelConfigSO level14 = Load(14);
            Assert.IsNotEmpty(level14.waves);

            WaveDefinition firstWave = level14.waves[0];
            Assert.IsTrue(firstWave.guaranteedCharacters.Any(character =>
                    character != null && character.characterID == "RA"),
                "Level 14 must guarantee RA on its first wave after RA is taught in Level 13.");
            Assert.IsTrue(firstWave.enemyTypes.Any(enemy =>
                    enemy != null && enemy.assignedCharacter != null
                    && enemy.assignedCharacter.characterID == "RA"),
                "The guaranteed RA glyph must use its matching enemy type.");
            Assert.AreEqual("RA", WaveSpawner.GuaranteedCharacterForSpawn(firstWave, 0).characterID);
            Assert.IsNull(WaveSpawner.GuaranteedCharacterForSpawn(firstWave, 1));
        }

        private static HashSet<string> FocusSymbols(LevelConfigSO level)
        {
            return new HashSet<string>(
                level.focusWords
                    .Where(focus => focus != null && focus.decomposition != null)
                    .SelectMany(focus => focus.decomposition)
                    .Where(reference => reference != null && reference.symbol != null)
                    .Select(reference => reference.symbol.stableId));
        }
    }
}
