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
    /// SALIN-283 (split out of SALIN-273) activated the level: it cleared bossConfig
    /// and authored flowSegments in one commit. The two scaffold assertions that held
    /// the pre-activation state — <c>Level5_StillDeclaresItsBossConfig</c> and
    /// <c>Level5_DeclaresNoFlowSegmentsYet</c> — were DELETED per this fixture's own
    /// standing instruction, not inverted, and replaced by
    /// <see cref="Level5_DeclaresNoBossConfig"/> and
    /// <see cref="Level5_AuthorsItsAlternatingFlowSegments"/>.
    ///
    /// Both changes had to land in one commit: authoring segments while bossConfig was
    /// still set would make the level run its boss twice, because the boss branch at
    /// WaveManager.cs:433-437 returns before the segment's wave range is ever read.
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
        public void Level5_AuthorsItsAlternatingFlowSegments()
        {
            LevelConfigSO level = LoadLevelFive();

            Assert.IsFalse(level.challengePrototypeEnabled,
                "challengePrototypeEnabled must stay false or the segment plan is rejected " +
                "outright (LevelPhasePlan.cs:200-204).");

            Assert.AreEqual(2, level.flowSegments.Count,
                "SALIN-283 authors a two-segment alternating flow: clear waves 1-2, restore IBA, " +
                "clear wave 3, restore MANA.");

            Assert.AreEqual(2, level.flowSegments[0].waveCount,
                "Segment 0 runs waves 1-2. Unit ugat05-complete-iba's decoy set includes MA, which " +
                "wave 2 introduces, so restoring after wave 1 would offer a decoy the player has " +
                "never met.");
            CollectionAssert.AreEqual(
                new[] { "ugat05-complete-iba" }, level.flowSegments[0].challengeUnitIds,
                "Segment 0 restores exactly the IBA line.");

            Assert.AreEqual(1, level.flowSegments[1].waveCount,
                "Segment 1 runs wave 3, which is where SALIN-247 first introduces NA.");
            CollectionAssert.AreEqual(
                new[] { "ugat05-complete-mana" }, level.flowSegments[1].challengeUnitIds,
                "Segment 1 restores MANA last: its answer NA is the level's finalRestorationValue, " +
                "which D-003/D-004 keep as a distinct ceremonial final syllable.");

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
    }
}
