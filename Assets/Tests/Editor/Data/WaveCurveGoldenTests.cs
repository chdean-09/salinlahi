using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The Ugat curve must reproduce Level 1's hand-authored wave list exactly. This is the proof
    /// that the curve abstraction is faithful, not merely plausible: Level 1 stays authored, and
    /// if this test ever fails the curve has drifted from the reference it was seeded from.
    /// </summary>
    public class WaveCurveGoldenTests
    {
        private const string LevelOnePath = "Assets/ScriptableObjects/Levels/Level1_Config.asset";
        private const string UgatCurvePath = "Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat.asset";

        [Test]
        public void UgatCurve_ExpandedAgainstLevelOneRoster_EqualsLevelOneAuthoredWaves()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelOnePath);
            var curve = AssetDatabase.LoadAssetAtPath<WaveCurveSO>(UgatCurvePath);
            Assert.IsNotNull(level, LevelOnePath);
            Assert.IsNotNull(curve, UgatCurvePath);

            List<WaveDefinition> authored = level.waves;
            Assert.IsNotEmpty(authored, "Level 1 is the hand-authored reference and must keep its waves.");

            // Level 1's roster lists Hati in allowedEnemyTypes but no wave spawns it, so the
            // reference roster is what wave 1 actually carries, not the level roster.
            List<WaveDefinition> generated = WaveCurveExpander.Expand(
                curve, authored[0].characters, authored[0].enemyTypes);

            Assert.AreEqual(authored.Count, generated.Count, "wave count");
            for (int i = 0; i < authored.Count; i++)
            {
                string wave = $"wave {i + 1}";
                Assert.AreEqual(authored[i].isIntermissionWave, generated[i].isIntermissionWave, wave + " intermission");
                Assert.AreEqual(authored[i].enemyCount, generated[i].enemyCount, wave + " enemyCount");
                Assert.AreEqual(authored[i].spawnInterval, generated[i].spawnInterval, 1e-4f, wave + " spawnInterval");
                Assert.AreEqual(authored[i].waveStartDelay, generated[i].waveStartDelay, 1e-4f, wave + " waveStartDelay");
                CollectionAssert.AreEqual(authored[i].characters, generated[i].characters, wave + " characters");
                CollectionAssert.AreEqual(authored[i].enemyTypes, generated[i].enemyTypes, wave + " enemyTypes");
            }
        }

        [Test]
        public void UgatCurveAsset_CarriesTheLevelOneSeed()
        {
            var curve = AssetDatabase.LoadAssetAtPath<WaveCurveSO>(UgatCurvePath);
            Assert.IsNotNull(curve, UgatCurvePath);

            WaveCurveShape shape = curve.ToShape();
            Assert.AreEqual(5, shape.WaveCount);
            Assert.AreEqual(2, shape.OpeningEnemyCount);
            Assert.AreEqual(6f, shape.OpeningSpawnInterval, 1e-4f);
            Assert.AreEqual(3f, shape.OpeningWaveStartDelay, 1e-4f);
            Assert.AreEqual(4, shape.RampFirstEnemyCount);
            Assert.AreEqual(7, shape.RampLastEnemyCount);
            Assert.AreEqual(5f, shape.RampFirstSpawnInterval, 1e-4f);
            Assert.AreEqual(3.5f, shape.RampLastSpawnInterval, 1e-4f);
            Assert.AreEqual(2f, shape.RampWaveStartDelay, 1e-4f);
        }

        [Test]
        public void Expand_NullCurve_IsEmpty()
        {
            Assert.IsEmpty(WaveCurveExpander.Expand((WaveCurveSO)null,
                new List<BaybayinCharacterSO>(), new List<EnemyDataSO>()));
        }
    }
}
