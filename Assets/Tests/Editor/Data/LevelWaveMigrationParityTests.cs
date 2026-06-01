using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    // TEMPORARY: validates migration parity. Deleted in Task 11 when WaveConfigSO is removed.
    public class LevelWaveMigrationParityTests
    {
        [Test]
        public void MigrateLevel_ProducesWaveDefinitionsMatchingLegacyWaves()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level1_Config.asset");
            Assert.NotNull(level, "Level1_Config not found");
            Assert.NotNull(level.waves, "legacy waves missing");
            int legacyCount = level.waves.Count(w => w != null);

            // Work on a clone so we don't dirty the real asset during the test.
            LevelConfigSO clone = Object.Instantiate(level);
            try
            {
                LevelWaveMigrator.MigrateLevel(clone);

                Assert.AreEqual(legacyCount, clone.embeddedWaves.Count, "wave count mismatch");
                for (int i = 0; i < clone.embeddedWaves.Count; i++)
                {
                    WaveConfigSO src = level.waves[i];
                    WaveDefinition dst = clone.embeddedWaves[i];
                    Assert.AreEqual(src.enemyCount, dst.enemyCount, $"wave {i} enemyCount");
                    Assert.AreEqual(src.spawnInterval, dst.spawnInterval, $"wave {i} spawnInterval");
                    Assert.AreEqual(src.waveStartDelay, dst.waveStartDelay, $"wave {i} waveStartDelay");
                    CollectionAssert.AreEquivalent(
                        src.charactersInWave.Where(c => c != null), dst.characters, $"wave {i} characters");
                    CollectionAssert.AreEquivalent(
                        src.enemyTypesInWave.Where(e => e != null), dst.enemyTypes, $"wave {i} enemyTypes");
                }
                // Every wave enemy type ended up in the roster.
                foreach (WaveDefinition w in clone.embeddedWaves)
                    foreach (EnemyDataSO e in w.enemyTypes)
                        Assert.Contains(e, clone.allowedEnemyTypes);
            }
            finally
            {
                Object.DestroyImmediate(clone);
            }
        }
    }
}
