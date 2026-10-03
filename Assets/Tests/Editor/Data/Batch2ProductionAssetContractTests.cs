using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine;

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
        public void EnemyCapAndPacing_AreScopedToLevels7Through15()
        {
            LevelConfigSO freshConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            Assert.AreEqual(0, freshConfig.maxActiveEnemies,
                "The serialized field default must preserve unlimited legacy spawning.");
            Object.DestroyImmediate(freshConfig);

            for (int levelNumber = 1; levelNumber <= 6; levelNumber++)
                Assert.AreEqual(0, Load(levelNumber).maxActiveEnemies,
                    $"Level {levelNumber} must keep the unlimited default.");

            int[][] expectedEnemyCounts =
            {
                new[] { 5, 5, 6, 7 },
                new[] { 5, 6, 6, 7, 8 },
                new[] { 8, 9, 9, 10, 12 },
                new[] { 6, 7, 8, 9, 10 },
                new[] { 4, 5, 6, 7 },
                new[] { 5, 5, 6, 6, 7 },
                new[] { 5, 6, 6, 7, 8 },
                new[] { 7, 8, 8, 9, 10 },
                new[] { 8, 9, 10, 11, 12 },
            };
            float[][] expectedSpawnIntervals =
            {
                new[] { 2.2f, 2.1f, 2f, 2f },
                new[] { 2f, 2f, 2f, 2f, 2f },
                new[] { 2f, 2f, 2f, 2f, 2f },
                new[] { 2.2f, 2.05f, 2f, 2f, 2f },
                new[] { 3f, 2.8f, 2.5f, 2.2f },
                new[] { 5.6f, 5f, 3f, 3f, 3f },
                new[] { 5f, 4.6f, 3f, 3f, 3f },
                new[] { 3.6f, 3f, 3f, 3f, 3f },
                new[] { 2.2f, 2.05f, 2f, 2f, 2f },
            };

            for (int levelNumber = 7; levelNumber <= 15; levelNumber++)
            {
                LevelConfigSO level = Load(levelNumber);
                Assert.AreEqual(4, level.maxActiveEnemies,
                    $"Level {levelNumber} must opt into the four-enemy cap.");
                if (levelNumber >= 12 && levelNumber <= 14)
                    Assert.AreEqual(0.65f, level.enemySpeedMultiplier, 0.0001f,
                        "Keep upstream Era Three movement pacing alongside the active cap.");
                Assert.AreEqual(expectedEnemyCounts[levelNumber - 7].Length, level.waves.Count,
                    $"Level {levelNumber} wave count must remain unchanged.");
                CollectionAssert.AreEqual(expectedEnemyCounts[levelNumber - 7],
                    level.waves.Select(wave => wave.enemyCount).ToArray(),
                    $"Level {levelNumber} authored enemy counts must remain unchanged.");
                CollectionAssert.AreEqual(expectedSpawnIntervals[levelNumber - 7],
                    level.waves.Select(wave => wave.spawnInterval).ToArray(),
                    $"Level {levelNumber} schedule must be preserved except for its 2-second floor.");
                Assert.IsTrue(level.waves.All(wave => wave.spawnInterval >= 2f),
                    $"Every Level {levelNumber} wave must respect the spawn interval floor.");
            }
        }

        [TestCase(10, "A,EI,BA,MA,NA,TA,OU,KA,GA,SA,WA,YA", "value.a,value.wa,value.ga,value.sa,value.ma,value.ka,value.o,value.na")]
        [TestCase(15, "A,EI,BA,MA,NA,TA,OU,KA,GA,SA,WA,YA,DA,HA,LA,NGA,RA,PA", "value.da,value.la,value.ma,value.pa,value.ya,value.ha,value.nga,value.ga,value.sa,value.ra")]
        public void EraFinales_RestoreTheirParagraphsWithCumulativeRegularEnemies(
            int levelNumber, string roster, string restorationValues)
        {
            LevelConfigSO level = Load(levelNumber);
            Assert.IsNull(level.bossConfig, "Era finales use regular waves, never a boss encounter.");
            Assert.IsTrue(level.restorationObjective.HasTargets);
            Assert.AreEqual(RestorationDisplayMode.HiddenContext, level.restorationObjective.displayMode);
            Assert.AreEqual(3, level.restorationObjective.units.Count);
            CollectionAssert.AreEquivalent(restorationValues.Split(','), TargetSequence(level).Distinct());
            CollectionAssert.AreEquivalent(roster.Split(','),
                level.allowedCharacters.Select(character => character.characterID));
            CollectionAssert.AreEquivalent(roster.Split(','),
                level.allowedEnemyTypes.Select(enemy => enemy.assignedCharacter.characterID).Distinct());
            CollectionAssert.AreEquivalent(roster.Split(','),
                level.waves.SelectMany(wave => wave.guaranteedCharacters)
                    .Select(character => character.characterID).Distinct());
            foreach (WaveDefinition wave in level.waves)
            {
                Assert.LessOrEqual(wave.guaranteedCharacters.Count, wave.enemyCount);
                CollectionAssert.AreEquivalent(roster.Split(','),
                    wave.enemyTypes.Select(enemy => enemy.assignedCharacter.characterID).Distinct());
            }
            Assert.AreEqual(3, level.flowSegments.Count);
            Assert.IsTrue(level.flowSegments.All(segment => segment.waveCount > 0));
            Assert.AreEqual(level.waves.Count, level.flowSegments.Sum(segment => segment.waveCount));
            Assert.IsFalse(LevelPhasePlan.FromConfig(level).SegmentPlanInvalid);
            Assert.IsTrue(level.suppressSymbolLearningCards);
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            var targets = level.restorationObjective.units.SelectMany(unit => unit.tokens)
                .Where(token => token.IsTarget).OrderBy(token => token.completionOrder).ToArray();
            CollectionAssert.AllItemsAreUnique(targets.Select(token => token.occurrenceId));
            Assert.AreEqual(level.finalRestorationValue.spokenValueId, targets.Last().SpokenValueId);
            var state = new RestorationObjectiveState();
            state.Configure(level.restorationObjective);
            foreach (var token in targets)
                Assert.IsTrue(state.TryRestore(token.SymbolStableId, token.SpokenValueId).Applied);
            Assert.IsTrue(state.IsComplete, "Every authored occurrence must restore without dead slots.");
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
