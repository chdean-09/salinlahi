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
            Assert.IsFalse(level.focusModeEnabled);
            Assert.IsFalse(level.multiKillChainEnabled);
        }

        [Test]
        public void LevelTwoConfig_EnablesAdvancedCombatAndAssignsTutorial()
        {
            LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level2_Config.asset");

            Assert.IsNotNull(level);
            Assert.IsTrue(level.focusModeEnabled);
            Assert.IsTrue(level.multiKillChainEnabled);
            Assert.IsNotNull(level.onboardingSequence,
                "Level 2 must have the advanced onboarding sequence assigned or the tutorial flow will not start.");
            Assert.Contains(OnboardingBeatType.ComboTeach, level.onboardingSequence.beatOrder);
            Assert.Contains(OnboardingBeatType.FocusModeTeach, level.onboardingSequence.beatOrder);
        }

        [Test]
        public void DefaultGameConfig_ActivatesFocusAtFiveStreaks()
        {
            GameConfigSO config = AssetDatabase.LoadAssetAtPath<GameConfigSO>(
                "Assets/ScriptableObjects/GameConfig_Default.asset");

            Assert.IsNotNull(config);
            Assert.AreEqual(5, config.focusModeThreshold,
                "Focus Mode should activate when the visible streak reaches 5.");
        }
#endif
    }
}
