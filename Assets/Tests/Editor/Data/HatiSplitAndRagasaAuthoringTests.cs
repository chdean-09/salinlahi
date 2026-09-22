using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Data
{
    /// <summary>
    /// Guards the authored data behind the 2026-09-14 character-art delivery: Hati splits into its
    /// minion pieces, and Ragasa exists as the RA enemy on RA's introduction level. Reads assets as
    /// they sit on disk; never edits them.
    /// </summary>
    [TestFixture]
    public sealed class HatiSplitAndRagasaAuthoringTests
    {
        private const string EnemyDir = "Assets/ScriptableObjects/Enemies/";
        private const string CharDir = "Assets/ScriptableObjects/Characters/";
        private const string Level13Path = "Assets/ScriptableObjects/Levels/Level13_Config.asset";
        private const string AlmanacPath = "Assets/ScriptableObjects/Almanac/AlmanacEnemyRegistry_Default.asset";

        private static T Load<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"Missing asset at {path}");
            return asset;
        }

        [Test]
        public void Hati_SplitsIntoTwoMinionPieces()
        {
            var hati = Load<EnemyDataSO>(EnemyDir + "EnemyData_Hati.asset");
            var minion = Load<EnemyDataSO>(EnemyDir + "EnemyData_HatiMinion.asset");

            Assert.IsTrue(hati.splitsOnDefeat, "Hati's description says it splits; the data must say so too");
            Assert.AreSame(minion, hati.splitSpawnData);
            Assert.AreEqual(2, hati.splitCount, "'splits into two smaller enemies'");
            Assert.AreSame(minion, HatiSplitController.ResolveSpawnData(hati));
        }

        [Test]
        public void HatiMinion_IsASmallerNonSplittingHaEnemy_ThatNeverRaisesDiscovery()
        {
            var minion = Load<EnemyDataSO>(EnemyDir + "EnemyData_HatiMinion.asset");
            var ha = Load<BaybayinCharacterSO>(CharDir + "Char_HA.asset");

            Assert.AreEqual("hati-minion", minion.enemyID);
            Assert.IsFalse(minion.splitsOnDefeat, "pieces never split again");
            Assert.Less(minion.spriteScale, 1f, "'smaller enemies'");
            Assert.AreSame(ha, minion.assignedCharacter);
            Assert.IsTrue(minion.suppressDiscovery, "the pieces are part of Hati's discovery, not their own");
            Assert.AreEqual(4, minion.walkFrames.Length);
            CollectionAssert.AllItemsAreNotNull(minion.walkFrames);
        }

        [Test]
        public void Ragasa_IsTheRaEnemy_WithWalkFrames()
        {
            var ragasa = Load<EnemyDataSO>(EnemyDir + "EnemyData_Ragasa.asset");
            var ra = Load<BaybayinCharacterSO>(CharDir + "Char_RA.asset");

            Assert.AreEqual("ragasa", ragasa.enemyID);
            Assert.AreEqual("Ragasa", ragasa.displayName);
            Assert.AreSame(ra, ragasa.assignedCharacter);
            Assert.AreEqual(4, ragasa.walkFrames.Length);
            CollectionAssert.AllItemsAreNotNull(ragasa.walkFrames);
            Assert.Greater(ragasa.maxHealth, 0);
        }

        [Test]
        public void Ragasa_HasAnAlmanacDescription()
        {
            var ragasa = Load<EnemyDataSO>(EnemyDir + "EnemyData_Ragasa.asset");

            Assert.IsFalse(string.IsNullOrWhiteSpace(ragasa.description),
                "Ragasa's Almanac entry must not render with an empty description area.");
        }

        [Test]
        public void Ragasa_SpawnsOnLevel13_WhereRaIsIntroduced()
        {
            var ragasa = Load<EnemyDataSO>(EnemyDir + "EnemyData_Ragasa.asset");
            var ra = Load<BaybayinCharacterSO>(CharDir + "Char_RA.asset");
            var level = Load<LevelConfigSO>(Level13Path);

            CollectionAssert.Contains(level.allowedCharacters, ra, "Level 13 is RA's introduction level");
            CollectionAssert.Contains(level.allowedEnemyTypes, ragasa);
            Assert.IsTrue(level.waves.Any(w => w.enemyTypes != null && w.enemyTypes.Contains(ragasa)),
                "at least one Level 13 wave must be able to spawn Ragasa");
        }

        [Test]
        public void Ragasa_IsDiscoverableInTheAlmanac()
        {
            var ragasa = Load<EnemyDataSO>(EnemyDir + "EnemyData_Ragasa.asset");
            var registry = Load<AlmanacEnemyRegistrySO>(AlmanacPath);

            Assert.IsTrue(registry.entries.Any(e => e.enemyData == ragasa && !e.IsBoss));
        }

        [Test]
        public void ReplacedWalkSheets_StillResolveFourFramesEach()
        {
            foreach (string name in new[] { "Hati", "Iligaw", "Uhaw", "Kadena" })
            {
                var enemy = Load<EnemyDataSO>(EnemyDir + $"EnemyData_{name}.asset");
                Assert.AreEqual(4, enemy.walkFrames.Length, name);
                CollectionAssert.AllItemsAreNotNull(enemy.walkFrames, name);
                Assert.AreEqual(1024f, enemy.walkFrames[0].rect.width, name);
            }
        }
    }
}
