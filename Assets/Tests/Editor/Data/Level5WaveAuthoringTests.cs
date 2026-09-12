using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// SALIN-247: pins the authored Level 5 (Ugat 05) combat data.
    ///
    /// Level 5 ships <b>unarmored mixed waves</b>. The "two-hit enemy" acceptance
    /// clause was deferred by owner ruling R1 to the Level 10 follow-up, because
    /// every enemy legal at Level 5 is maxHealth: 1 and every multi-hit enemy in
    /// the project is bound to a symbol first taught at Level 6 or later.
    ///
    /// Two assertions here look backwards and are deliberate:
    ///   * <see cref="Level5_StillDeclaresItsBossConfig"/> — D-026 holds bossConfig
    ///     until SALIN-273 clears it. Clearing it early strands the level.
    ///   * <see cref="Level5_DeclaresNoFlowSegmentsYet"/> — owner ruling R3 moved
    ///     flowSegments to SALIN-273 so segments and the bossConfig clear land in
    ///     one commit; authoring segments while bossConfig is set would make the
    ///     level run its boss twice (WaveManager.cs:433-437).
    /// When SALIN-273 lands, both assertions should be DELETED, not inverted.
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
            List<BaybayinCharacterSO> carried = level.allowedEnemyTypes
                .Select(enemy => enemy.assignedCharacter)
                .ToList();

            CollectionAssert.AreEquivalent(rosterCharacters, carried,
                "Every glyph on the Level 5 roster needs an enemy that carries it, or the glyph " +
                "can never be practised in combat and its wave entries are silently stripped.");
        }

        [Test]
        public void Level5_EveryEnemyOnTheRosterIsUnarmored()
        {
            LevelConfigSO level = LoadLevelFive();

            foreach (EnemyDataSO enemy in level.allowedEnemyTypes)
            {
                Assert.AreEqual(1, enemy.maxHealth,
                    $"{enemy.name}: owner ruling R1 ships Level 5 unarmored. Raising maxHealth on a " +
                    "shared Ugat EnemyData would silently re-tune the polished Levels 1-4.");
            }
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
        public void Level5_StillDeclaresItsBossConfig()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsNotNull(level.bossConfig,
                "D-026 LOCKED: clearing Level 5's bossConfig belongs to SALIN-273, held until these " +
                "waves land. Delete this assertion when SALIN-273 lands; do not invert it.");
        }

        [Test]
        public void Level5_DeclaresNoFlowSegmentsYet()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsEmpty(level.flowSegments,
                "Owner ruling R3: flowSegments move to SALIN-273 so they land in the same commit as the " +
                "bossConfig clear. Authoring them now would run the boss twice (WaveManager.cs:433-437). " +
                "Delete this assertion when SALIN-273 lands; do not invert it.");

            Assert.IsFalse(level.challengePrototypeEnabled,
                "challengePrototypeEnabled must stay false or SALIN-273's segment plan is rejected " +
                "outright (LevelPhasePlan.cs:200-204).");
        }
    }
}
