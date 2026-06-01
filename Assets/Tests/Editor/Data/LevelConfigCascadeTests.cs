using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    public class LevelConfigCascadeTests
    {
        [Test]
        public void ReconcileWavesToRoster_DropsCharactersAndEnemiesNotInRoster()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO keepChar = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            BaybayinCharacterSO dropChar = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            EnemyDataSO keepEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();
            EnemyDataSO dropEnemy = ScriptableObject.CreateInstance<EnemyDataSO>();

            level.allowedCharacters = new List<BaybayinCharacterSO> { keepChar };
            level.allowedEnemyTypes = new List<EnemyDataSO> { keepEnemy };

            WaveDefinition wave = new()
            {
                characters = new List<BaybayinCharacterSO> { keepChar, dropChar },
                enemyTypes = new List<EnemyDataSO> { keepEnemy, dropEnemy },
            };
            level.waves = new List<WaveDefinition> { wave };

            try
            {
                level.ReconcileWavesToRoster();

                Assert.Contains(keepChar, wave.characters);
                Assert.IsFalse(wave.characters.Contains(dropChar), "dropChar should be pruned");
                Assert.Contains(keepEnemy, wave.enemyTypes);
                Assert.IsFalse(wave.enemyTypes.Contains(dropEnemy), "dropEnemy should be pruned");
            }
            finally
            {
                Object.DestroyImmediate(level);
                Object.DestroyImmediate(keepChar);
                Object.DestroyImmediate(dropChar);
                Object.DestroyImmediate(keepEnemy);
                Object.DestroyImmediate(dropEnemy);
            }
        }

        [Test]
        public void ReconcileWavesToRoster_RemovesNullEntries()
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            BaybayinCharacterSO keepChar = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            level.allowedCharacters = new List<BaybayinCharacterSO> { keepChar };
            level.allowedEnemyTypes = new List<EnemyDataSO>();
            WaveDefinition wave = new() { characters = new List<BaybayinCharacterSO> { keepChar, null } };
            level.waves = new List<WaveDefinition> { wave };

            try
            {
                level.ReconcileWavesToRoster();
                Assert.AreEqual(1, wave.characters.Count);
                Assert.Contains(keepChar, wave.characters);
            }
            finally
            {
                Object.DestroyImmediate(level);
                Object.DestroyImmediate(keepChar);
            }
        }
    }
}
