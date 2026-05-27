using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    [TestFixture]
    public class SpanishVariantSpawnIntegrationTests
    {
        [UnityTest]
        public IEnumerator GuardiaAndCapitanPrefabs_HaveEnemyAndMoverComponents()
        {
            GameObject guardiaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Enemies/[Enemy] Guardia.prefab");
            GameObject capitanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Enemies/[Enemy] Capitan.prefab");

            Assert.NotNull(guardiaPrefab);
            Assert.NotNull(capitanPrefab);
            Assert.NotNull(guardiaPrefab.GetComponent<Enemy>());
            Assert.NotNull(guardiaPrefab.GetComponent<EnemyMover>());
            Assert.NotNull(capitanPrefab.GetComponent<Enemy>());
            Assert.NotNull(capitanPrefab.GetComponent<EnemyMover>());
            yield break;
        }

        [UnityTest]
        public IEnumerator Capitan_RequiresTwoHitsBeforeDefeat()
        {
            EnemyDataSO capitanData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Capitan.asset");

            GameObject go = new GameObject("Capitan_Test");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            Enemy enemy = go.AddComponent<Enemy>();

            Assert.IsTrue(enemy.Initialize(capitanData));
            Assert.AreEqual(2, enemy.CurrentHealth);

            enemy.TakeDamage(1);
            Assert.AreEqual(1, enemy.CurrentHealth);
            Assert.IsFalse(enemy.IsDying);

            enemy.TakeDamage(1);
            Assert.IsTrue(enemy.IsDying);

            Object.Destroy(go);
            yield break;
        }
    }
}
