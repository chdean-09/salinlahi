using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The Ugat curve keeps the Level 1 roster shape while owning its current cadence. Level 1
    /// remains authored; cadence is asserted against the current curve asset rather than an old
    /// seed snapshot.
    /// </summary>
    public class WaveCurveGoldenTests
    {
        private const string LevelOnePath = "Assets/ScriptableObjects/Levels/Level1_Config.asset";
        private const string UgatCurvePath = "Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat.asset";

        [Test]
        public void UgatCurve_ExpandedAgainstLevelOneRoster_PreservesWaveShape()
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
            Assert.AreEqual(3f, shape.OpeningSpawnInterval, 1e-4f);
            Assert.AreEqual(1.25f, shape.OpeningWaveStartDelay, 1e-4f);
            Assert.AreEqual(4, shape.RampFirstEnemyCount);
            Assert.AreEqual(7, shape.RampLastEnemyCount);
            Assert.AreEqual(2.75f, shape.RampFirstSpawnInterval, 1e-4f);
            Assert.AreEqual(2f, shape.RampLastSpawnInterval, 1e-4f);
            Assert.AreEqual(1f, shape.RampWaveStartDelay, 1e-4f);
        }

        [Test]
        public void Expand_NullCurve_IsEmpty()
        {
            Assert.IsEmpty(WaveCurveExpander.Expand((WaveCurveSO)null,
                new List<BaybayinCharacterSO>(), new List<EnemyDataSO>()));
        }
    }
}
