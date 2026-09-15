using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    public class LevelConfigWaveResolutionTests
    {
        private static WaveCurveSO Curve(int waveCount)
        {
            var curve = ScriptableObject.CreateInstance<WaveCurveSO>();
            curve.waveCount = waveCount;
            return curve;
        }

        private static LevelConfigSO Level()
        {
            var level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.allowedCharacters = new List<BaybayinCharacterSO>
            {
                ScriptableObject.CreateInstance<BaybayinCharacterSO>(),
                ScriptableObject.CreateInstance<BaybayinCharacterSO>(),
            };
            level.allowedEnemyTypes = new List<EnemyDataSO>
            {
                ScriptableObject.CreateInstance<EnemyDataSO>(),
            };
            return level;
        }

        [Test]
        public void NoAuthoredWaves_NoCurve_ResolvesToTheEmptyAuthoredList()
        {
            LevelConfigSO level = Level();

            Assert.IsNotNull(level.waves);
            Assert.IsEmpty(level.waves);
            Assert.AreSame(level.AuthoredWaves, level.waves, "tests Add to this list; it must be the live one");
            Assert.IsFalse(level.UsesWaveCurve);
        }

        [Test]
        public void AuthoredWaves_TakePrecedenceOverTheCurve()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(5);
            // With a curve set and nothing authored, `waves` is the resolved cache, so author
            // through AuthoredWaves: that is the list the inspector's Add Wave button writes.
            level.AuthoredWaves.Add(new WaveDefinition { enemyCount = 99 });

            Assert.AreEqual(1, level.waves.Count);
            Assert.AreEqual(99, level.waves[0].enemyCount);
            Assert.IsFalse(level.UsesWaveCurve);
        }

        [Test]
        public void EmptyAuthored_WithCurve_ResolvesTheFullRosterPerWave()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(3);

            List<WaveDefinition> resolved = level.waves;

            Assert.IsTrue(level.UsesWaveCurve);
            Assert.AreEqual(3, resolved.Count);
            CollectionAssert.AreEqual(level.allowedCharacters, resolved[1].characters);
            CollectionAssert.AreEqual(level.allowedEnemyTypes, resolved[1].enemyTypes);
            Assert.IsEmpty(level.AuthoredWaves, "resolution must not write into the authored list");
        }

        [Test]
        public void ResolvedList_IsCachedAcrossReads()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(3);

            Assert.AreSame(level.waves, level.waves, "WaveManager indexes the same list across frames");
        }

        [Test]
        public void ChangingTheCurve_RebuildsTheCache()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(3);
            Assert.AreEqual(3, level.waves.Count);

            level.waveCurve = Curve(4);

            Assert.AreEqual(4, level.waves.Count);
        }

        [Test]
        public void Invalidate_RebuildsAgainstTheCurrentRoster()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(2);
            Assert.AreEqual(2, level.waves[0].characters.Count);

            level.allowedCharacters.Add(ScriptableObject.CreateInstance<BaybayinCharacterSO>());
            Assert.AreEqual(2, level.waves[0].characters.Count, "stale until invalidated, by design");

            level.InvalidateResolvedWaves();

            Assert.AreEqual(3, level.waves[0].characters.Count);
        }

        [Test]
        public void Setter_ReplacesAuthoredAndDropsTheCache()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(3);
            Assert.AreEqual(3, level.waves.Count);

            level.waves = new List<WaveDefinition> { new WaveDefinition { enemyCount = 1 } };

            Assert.AreEqual(1, level.waves.Count);
            Assert.AreEqual(1, level.AuthoredWaves.Count);
            Assert.IsFalse(level.UsesWaveCurve);

            level.waves = new List<WaveDefinition>();

            Assert.AreEqual(3, level.waves.Count, "empty authored list falls back to the curve again");
        }

        [Test]
        public void ReadingResolvedWaves_NeverTouchesTheSerializedField()
        {
            LevelConfigSO level = Level();
            level.waveCurve = Curve(5);
            Assert.AreEqual(5, level.waves.Count);

            var so = new SerializedObject(level);
            SerializedProperty authored = so.FindProperty("_authoredWaves");

            Assert.IsNotNull(authored, "serialized field must be named _authoredWaves");
            Assert.AreEqual(0, authored.arraySize, "the resolved cache must never persist");
            Assert.IsNull(so.FindProperty("waves"), "the old name must no longer be a serialized field");
        }

        [Test]
        public void ReconcileWavesToRoster_PrunesAuthoredOnly_AndInvalidates()
        {
            LevelConfigSO level = Level();
            var outsider = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            level.waves.Add(new WaveDefinition
            {
                characters = new List<BaybayinCharacterSO> { level.allowedCharacters[0], outsider },
            });

            level.ReconcileWavesToRoster();

            CollectionAssert.AreEqual(new[] { level.allowedCharacters[0] }, level.waves[0].characters);
        }
    }
}
