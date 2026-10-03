using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-247: pins the authored Level 5 (Ugat 05) combat data.
    ///
    /// Level 5 ships mixed waves with the existing Walang-Awa armored enemy. Its
    /// maxHealth: 3 is the current armor implementation. Walang-Awa's canonical
    /// character is WA, but Level 5 supplies an explicit Ugat character list per
    /// wave, so the spawner uses the level's taught glyphs while retaining the
    /// enemy's shared armored behavior.
    ///
    /// Level 5 is the Era 1 mastery finale: a hidden, three-line paragraph restored
    /// through the existing scene-scoped objective and five authored waves.
    /// </summary>
    [TestFixture]
    public sealed class Level5WaveAuthoringTests
    {
        private const string LevelPath = "Assets/ScriptableObjects/Levels/Level5_Config.asset";

        private static LevelConfigSO LoadLevelFive()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
            Assert.IsNotNull(level, $"{LevelPath} must load as a LevelConfigSO.");
            return level;
        }

        [Test]
        public void Level5_AuthorsAtLeastThreeWaves()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.GreaterOrEqual(level.waves.Count, 3,
                "SALIN-226's Level 5 exemplar needs a wave 3 (waves 1-2 open paragraph line 1, " +
                "correct restoration resumes wave 3).");
        }

        [Test]
        public void Level5_AllowedEnemyTypesCoverTheUgatRoster()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsNotEmpty(level.allowedEnemyTypes,
                "allowedEnemyTypes has no validator rule; if it is empty, ReconcileWavesToRoster " +
                "silently strips every authored wave's enemies (LevelConfigSO.cs:104-118,129-132).");

            List<BaybayinCharacterSO> rosterCharacters = level.allowedCharacters
                .Where(c => c != null)
                .ToList();

            foreach (EnemyDataSO enemy in level.allowedEnemyTypes)
            {
                Assert.IsNotNull(enemy, "allowedEnemyTypes must not carry a null entry.");
                if (enemy.enemyID == "walang-awa")
                    continue;

                Assert.Contains(enemy.assignedCharacter, rosterCharacters,
                    $"{enemy.name} carries a glyph outside the Level 5 pool (roster invariant D-020/A40).");
            }

            CollectionAssert.AllItemsAreUnique(level.allowedEnemyTypes,
                "A duplicated enemy on the roster is an authoring slip.");

            // Coverage has to be asserted in BOTH directions. Checking only that every
            // rostered enemy carries an in-pool glyph is not enough: dropping an enemy
            // from allowedEnemyTypes makes ReconcileWavesToRoster prune it out of every
            // wave on import, which leaves "waves are a subset of the roster" trivially
            // true. Verified by negative control — removing Takip left the suite green
            // at 948/948 until this assertion was added.
            // Walang-Awa is the shared armored asset and carries its canonical WA
            // character. Its wave entry is intentionally driven by the explicit
            // Level 5 character list, so it is not part of the Ugat glyph coverage.
            List<BaybayinCharacterSO> carried = level.allowedEnemyTypes
                .Where(enemy => enemy != null && enemy.enemyID != "walang-awa")
                .Select(enemy => enemy.assignedCharacter)
                .ToList();

            CollectionAssert.AreEquivalent(rosterCharacters, carried,
                "Every glyph on the Level 5 roster needs an enemy that carries it, or the glyph " +
                "can never be practised in combat and its wave entries are silently stripped.");
        }

        [Test]
        public void Level5_AuthorsTheExistingWalangAwaAsItsArmoredEnemy()
        {
            LevelConfigSO level = LoadLevelFive();

            EnemyDataSO armored = level.allowedEnemyTypes
                .SingleOrDefault(enemy => enemy != null && enemy.enemyID == "walang-awa");

            Assert.IsNotNull(armored,
                "Level 5 must use the existing Walang-Awa enemy asset for its armored wave.");
            Assert.AreEqual("Assets/ScriptableObjects/Enemies/EnemyData_Walang-Awa.asset",
                AssetDatabase.GetAssetPath(armored),
                "The Level 5 armored entry must reuse the shared Walang-Awa asset.");
            Assert.AreEqual(3, armored.maxHealth,
                "Walang-Awa's existing maxHealth: 3 is the authored armor behavior.");
            Assert.IsTrue(level.waves.Any(wave => wave != null && wave.enemyTypes != null &&
                                                  wave.enemyTypes.Contains(armored)),
                "The armored enemy must be assigned to at least one authored Level 5 wave.");
        }

        [Test]
        public void Level5_WaveRostersAreNonEmptySubsetsOfTheLevelRosters()
        {
            LevelConfigSO level = LoadLevelFive();

            for (int i = 0; i < level.waves.Count; i++)
            {
                WaveDefinition wave = level.waves[i];
                Assert.IsNotNull(wave, $"Wave {i + 1} must not be null.");

                Assert.IsNotEmpty(wave.characters, $"Wave {i + 1} lost every character to the roster prune.");
                Assert.IsNotEmpty(wave.enemyTypes, $"Wave {i + 1} lost every enemy to the roster prune.");

                foreach (BaybayinCharacterSO character in wave.characters)
                {
                    Assert.Contains(character, level.allowedCharacters,
                        $"Wave {i + 1} names a character outside allowedCharacters.");
                }

                foreach (EnemyDataSO enemy in wave.enemyTypes)
                {
                    Assert.Contains(enemy, level.allowedEnemyTypes,
                        $"Wave {i + 1} names an enemy outside allowedEnemyTypes.");
                }
            }
        }

        [Test]
        public void Level5_WavesAreMixed()
        {
            LevelConfigSO level = LoadLevelFive();

            for (int i = 0; i < level.waves.Count; i++)
            {
                WaveDefinition wave = level.waves[i];
                Assert.GreaterOrEqual(wave.enemyTypes.Count, 2,
                    $"Wave {i + 1} must draw from more than one enemy — F31 specifies mixed waves.");
                Assert.Greater(wave.enemyCount, 0, $"Wave {i + 1} must spawn something.");
                Assert.Greater(wave.spawnInterval, 0f, $"Wave {i + 1} needs a positive spawn interval.");
                Assert.IsFalse(wave.isIntermissionWave,
                    $"Wave {i + 1} must not be a boss intermission wave — Level 5 has no boss phase (D-002).");
            }
        }

        [Test]
        public void Level5_AllowedCharactersEqualsCumulativeSymbolPool()
        {
            LevelConfigSO level = LoadLevelFive();

            List<BaybayinCharacterSO> pool = level.cumulativeSymbolPool
                .Select(reference => reference.symbol)
                .ToList();

            CollectionAssert.AreEquivalent(pool, level.allowedCharacters,
                "Acceptance criterion 3: every glyph asked for must be in the Ugat pool.");
        }

        [Test]
        public void Level5_DeclaresNoBossConfig()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsNull(level.bossConfig,
                "D-002: Level 5 has no boss phase. A non-null bossConfig is not merely cosmetic here — " +
                "the boss branch at WaveManager.cs:433-437 returns before the segment's wave range is " +
                "read (that clamp is computed later, at WaveManager.cs:452-454), so the segment slice is " +
                "discarded and the flow machine's backward edge (LevelFlowMachine.cs:90-95) re-enters " +
                "Defense and runs the whole boss encounter a second time.");
        }

        [Test]
        public void Level5_AuthorsItsThreeParagraphFlowSegments()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsFalse(level.challengePrototypeEnabled,
                "challengePrototypeEnabled must stay false or the segment plan is rejected " +
                "outright (LevelPhasePlan.cs:200-204).");

            Assert.AreEqual(3, level.flowSegments.Count,
                "The paragraph is restored in three authored units after wave groups 2, 2, and 1.");

            Assert.AreEqual(2, level.flowSegments[0].waveCount,
                "Segment 0 runs waves 1-2 before the first paragraph line.");
            CollectionAssert.AreEqual(
                new[] { "ugat05-restore-line-01" }, level.flowSegments[0].challengeUnitIds,
                "Segment 0 restores exactly the first paragraph line.");

            Assert.AreEqual(2, level.flowSegments[1].waveCount,
                "Segment 1 runs waves 3-4 before the second paragraph line.");
            CollectionAssert.AreEqual(
                new[] { "ugat05-restore-line-02" }, level.flowSegments[1].challengeUnitIds,
                "Segment 1 restores exactly the second paragraph line.");

            Assert.AreEqual(1, level.flowSegments[2].waveCount,
                "Segment 2 runs wave 5 before the final paragraph line.");
            CollectionAssert.AreEqual(
                new[] { "ugat05-restore-line-03" }, level.flowSegments[2].challengeUnitIds,
                "Segment 2 restores exactly the final paragraph line.");

            // NOT OPTIONAL. This is the ONLY guard in the repository against under-consumption.
            // The segment list partitions the flat waves list, and both rejection paths test
            // ONLY for overrun: LevelPhasePlan.cs:247-251 and CampaignConfigValidator.cs:532-538
            // each compare `consumedWaves > waveBudget`. Authoring 2 + 0 instead of 2 + 1 is
            // therefore accepted in silence — wave 3 never runs, the validator's warning count
            // stays at its 90 baseline, SegmentPlanInvalid stays false, and the process exits 0.
            // Negative control NC-3 confirmed this assertion is the only thing that fires.
            Assert.AreEqual(level.waves.Count, level.flowSegments.Sum(segment => segment.waveCount),
                "The segments must consume EVERY authored wave. Under-consumption is rejected by " +
                "neither LevelPhasePlan.PlanSegments nor CampaignConfigValidator.ValidateFlowSegments, " +
                "so a short count silently drops the trailing waves with nothing else failing.");

            Assert.IsFalse(LevelPhasePlan.FromConfig(level).SegmentPlanInvalid,
                "The authored segment list must survive all six PlanSegments rejections " +
                "(LevelPhasePlan.cs:191-263); an invalid list collapses Level 5 back to a single pass.");
        }

        [Test]
        public void Level5_AuthorsHiddenEraMasteryParagraph()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsNotNull(level.restorationObjective);
            Assert.AreEqual(RestorationDisplayMode.HiddenContext, level.restorationObjective.displayMode);
            Assert.IsTrue(level.restorationObjective.HasTargets);
            Assert.AreEqual(3, level.restorationObjective.units.Count);

            string[] expectedUnits =
            {
                "level.ugat.05.paragraph.line.01",
                "level.ugat.05.paragraph.line.02",
                "level.ugat.05.paragraph.line.03",
            };
            CollectionAssert.AreEqual(expectedUnits,
                level.restorationObjective.units.Select(unit => unit.stableId).ToArray());

            string[] expectedSequence = { "value.ma", "value.na", "value.i", "value.ma", "value.ma", "value.ba", "value.ma", "value.ma", "value.na", "value.a", "value.ma", "value.ma", "value.na", "value.ta" };
            var targets = level.restorationObjective.units
                .SelectMany(unit => unit.tokens ?? new List<RestorationObjectiveToken>())
                .Where(token => token != null && token.IsTarget)
                .OrderBy(token => token.completionOrder)
                .ToList();
            Assert.AreEqual(expectedSequence.Length, targets.Count);
            CollectionAssert.AreEqual(expectedSequence, targets.Select(token => token.target.spokenValueId).ToArray());
            CollectionAssert.AllItemsAreUnique(targets.Select(token => token.occurrenceId).ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(0, expectedSequence.Length),
                targets.Select(token => token.completionOrder).ToArray());

            Assert.IsTrue(level.suppressSymbolLearningCards,
                "Level 5 is cumulative recall and must not enter reference-form learning cards.");
            Assert.IsTrue(level.activeClueRestorationEnabled);
            Assert.IsNotEmpty(level.focusWords);
            Assert.IsTrue(level.spawnAssignmentPolicy.gateFinalSlotToFinalWave);
            Assert.AreEqual("value.ta", level.finalRestorationValue.spokenValueId);
            Assert.IsTrue(level.spawnAssignmentPolicy.allowFinalWaveOverflow,
                "Bounded overflow remains the recovery path for misses or unlucky filler selection.");
            Assert.Greater(level.spawnAssignmentPolicy.maxOverflowBatches, 0);
            Assert.LessOrEqual(level.spawnAssignmentPolicy.maxOverflowBatches, 12);
        }

        [Test]
        public void Level5_AllowedCharactersAreExactlyEraOne()
        {
            LevelConfigSO level = LoadLevelFive();

            string[] expected = { "A", "EI", "BA", "MA", "NA", "TA" };
            CollectionAssert.AreEquivalent(expected,
                level.allowedCharacters.Where(character => character != null).Select(character => character.characterID));
            Assert.IsFalse(level.allowedCharacters.Any(character => character != null &&
                                                                       !expected.Contains(character.characterID)));
        }
    }
}
