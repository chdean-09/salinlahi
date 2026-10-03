using System.Collections.Generic;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor;
#endif
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

#if UNITY_EDITOR
        [Test]
        public void LevelOneConfig_DisablesAdvancedCombat()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level1_Config.asset");

            Assert.IsNotNull(level);
            Assert.IsFalse(level.multiKillChainEnabled);
        }

        [Test]
        public void LevelTwoConfig_UsesCombatDiscoveryWithoutOnboarding()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level2_Config.asset");

            Assert.IsNotNull(level);
            Assert.IsFalse(level.multiKillChainEnabled,
                "Level 2 keeps mass-clear disabled in the combat-discovery slice.");
            Assert.IsTrue(level.activeClueCombatEnabled,
                "Level 2 must enter the active-clue combat path.");
            Assert.IsTrue(level.suppressSymbolLearningCards,
                "Level 2 should discover symbols through combat instead of reference cards.");
            Assert.IsNull(level.onboardingSequence,
                "Level 2 intentionally has no onboarding sequence; null is not a flow error.");
        }
#endif
    }
}
