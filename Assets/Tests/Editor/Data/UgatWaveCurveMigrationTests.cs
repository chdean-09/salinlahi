using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    public class UgatWaveCurveMigrationTests
    {
        private static LevelConfigSO Load(int level)
        {
            string path = $"Assets/ScriptableObjects/Levels/Level{level}_Config.asset";
            var config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            Assert.IsNotNull(config, path);
            return config;
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void MigratedUgatLevel_HasNoAuthoredWaves_AndUsesCurveUgat(int levelNumber)
        {
            LevelConfigSO level = Load(levelNumber);

            Assert.IsEmpty(level.AuthoredWaves, $"Level {levelNumber} must not carry authored waves any more");
            Assert.IsNotNull(level.waveCurve, $"Level {levelNumber} must reference a wave curve");
            Assert.AreEqual("Curve_Ugat", level.waveCurve.name);
            Assert.IsTrue(level.UsesWaveCurve);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void MigratedUgatLevel_ResolvesFiveWavesCarryingItsWholeRoster(int levelNumber)
        {
            LevelConfigSO level = Load(levelNumber);

            List<WaveDefinition> waves = level.waves;

            Assert.AreEqual(5, waves.Count);
            CollectionAssert.AreEqual(new[] { 2, 4, 5, 6, 7 }, waves.ConvertAll(w => w.enemyCount));
            for (int i = 0; i < waves.Count; i++)
            {
                Assert.IsFalse(waves[i].isIntermissionWave);
                CollectionAssert.AreEqual(level.allowedCharacters, waves[i].characters, $"wave {i + 1} glyphs");
                CollectionAssert.AreEqual(level.allowedEnemyTypes, waves[i].enemyTypes, $"wave {i + 1} enemies");
                Assert.IsNotEmpty(waves[i].characters, $"Level {levelNumber} wave {i + 1} would fall back to EnemyDataSO.assignedCharacter");
            }
        }

        [TestCase(1)]
        [TestCase(5)]
        public void ReferenceLevels_KeepAuthoredWaves(int levelNumber)
        {
            LevelConfigSO level = Load(levelNumber);

            Assert.IsNotEmpty(level.AuthoredWaves);
            Assert.IsFalse(level.UsesWaveCurve);
        }

        [TestCase(10)]
        [TestCase(15)]
        public void BossLevels_HaveNeitherWavesNorCurve(int levelNumber)
        {
            LevelConfigSO level = Load(levelNumber);

            Assert.IsNotNull(level.bossConfig);
            Assert.IsEmpty(level.AuthoredWaves);
            Assert.IsNull(level.waveCurve);
            Assert.IsEmpty(level.waves);
        }
    }
}
