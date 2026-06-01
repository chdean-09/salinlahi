using NUnit.Framework;
using UnityEditor;
using System.Linq;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class SpanishEnemyVariantDataTests
    {
        [Test]
        public void Guardia_HasExpectedFastSpeedAndSpanishEra()
        {
            EnemyDataSO soldado = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Soldado.asset");
            EnemyDataSO guardia = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Guardia.asset");

            Assert.NotNull(soldado);
            Assert.NotNull(guardia);
            Assert.AreEqual("guardia", guardia.enemyID);
            Assert.AreEqual(Era.Spanish, guardia.era);
            Assert.AreEqual(1, guardia.maxHealth);
            Assert.That(guardia.moveSpeed, Is.EqualTo(soldado.moveSpeed * 1.5f).Within(0.001f));
        }

        [Test]
        public void Capitan_HasExpectedShieldedAndSlowStats()
        {
            EnemyDataSO soldado = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Soldado.asset");
            EnemyDataSO capitan = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Capitan.asset");

            Assert.NotNull(soldado);
            Assert.NotNull(capitan);
            Assert.AreEqual("capitan", capitan.enemyID);
            Assert.AreEqual(Era.Spanish, capitan.era);
            Assert.AreEqual(2, capitan.maxHealth);
            Assert.That(capitan.moveSpeed, Is.EqualTo(soldado.moveSpeed * 0.7f).Within(0.001f));
            Assert.IsTrue(capitan.useHurtFeedback);
            Assert.IsNotNull(capitan.hurtFrames);
            Assert.Greater(capitan.hurtFrames.Length, 0);
        }

        [Test]
        public void Level3Waves_ContainGuardia_AndLevel4Waves_ContainCapitan()
        {
            LevelConfigSO level3 = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level3_Config.asset");
            LevelConfigSO level4 = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level4_Config.asset");

            Assert.NotNull(level3);
            Assert.NotNull(level4);

            bool level3HasGuardia = level3.embeddedWaves
                .SelectMany(w => w.enemyTypes).Any(e => e != null && e.enemyID == "guardia");
            bool level4HasCapitan = level4.embeddedWaves
                .SelectMany(w => w.enemyTypes).Any(e => e != null && e.enemyID == "capitan");

            Assert.IsTrue(level3HasGuardia);
            Assert.IsTrue(level4HasCapitan);
        }

        [Test]
        public void EnemyPool_HasGuardiaAndCapitanPrefabRegistrations()
        {
            GameObject poolPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Managers/[Manager] EnemyPool.prefab");
            Assert.NotNull(poolPrefab);

            EnemyPool pool = poolPrefab.GetComponent<EnemyPool>();
            Assert.NotNull(pool);

            SerializedObject so = new SerializedObject(pool);
            SerializedProperty list = so.FindProperty("_registeredEnemyPrefabs");
            Assert.NotNull(list);

            bool hasGuardia = false;
            bool hasCapitan = false;

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty item = list.GetArrayElementAtIndex(i);
                string id = item.FindPropertyRelative("enemyID").stringValue?.Trim().ToLowerInvariant();
                Object prefab = item.FindPropertyRelative("prefab").objectReferenceValue;
                if (id == "guardia" && prefab != null) hasGuardia = true;
                if (id == "capitan" && prefab != null) hasCapitan = true;
            }

            Assert.IsTrue(hasGuardia, "EnemyPool is missing guardia prefab registration.");
            Assert.IsTrue(hasCapitan, "EnemyPool is missing capitan prefab registration.");
        }
    }
}
