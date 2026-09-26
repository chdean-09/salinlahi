using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// The five-wave Ugat curve supplies Level 5; Levels 1-4 use shorter pacing.
    /// </summary>
    public class WaveCurveGoldenTests
    {
        private const string LevelFivePath = "Assets/ScriptableObjects/Levels/Level5_Config.asset";
        private const string UgatCurvePath = "Assets/ScriptableObjects/Levels/WaveCurves/Curve_Ugat.asset";

        [Test]
        public void UgatCurve_ExpandedAgainstLevelFiveRoster_PreservesFiveWaveShape()
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelFivePath);
            var curve = AssetDatabase.LoadAssetAtPath<WaveCurveSO>(UgatCurvePath);
            Assert.IsNotNull(level, LevelFivePath);
            Assert.IsNotNull(curve, UgatCurvePath);

            Assert.IsTrue(level.UsesWaveCurve);
            List<WaveDefinition> generated = WaveCurveExpander.Expand(
                curve, level.allowedCharacters, level.allowedEnemyTypes);

            Assert.AreEqual(5, generated.Count, "wave count");
            CollectionAssert.AreEqual(new[] { 2, 4, 5, 6, 7 },
                generated.ConvertAll(w => w.enemyCount));
            for (int i = 0; i < generated.Count; i++)
            {
                Assert.IsFalse(generated[i].isIntermissionWave);
                CollectionAssert.AreEqual(level.allowedCharacters, generated[i].characters);
                CollectionAssert.AreEqual(level.allowedEnemyTypes, generated[i].enemyTypes);
            }
        }

        [Test]
        public void UgatCurveAsset_CarriesTheFiveWaveCadence()
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
