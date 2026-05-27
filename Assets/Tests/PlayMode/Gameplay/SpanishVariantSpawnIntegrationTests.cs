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

        [UnityTest]
        public IEnumerator Capitan_ShieldBreak_PausesThenResumesMovement()
        {
            EnemyDataSO capitanData = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(
                "Assets/ScriptableObjects/EnemyData_Capitan.asset");

            GameObject go = new GameObject("Capitan_ShieldBreak_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            EnemyMover mover = go.AddComponent<EnemyMover>();
            go.AddComponent<EnemyHurtFeedback>();
            Enemy enemy = go.AddComponent<Enemy>();
            go.SetActive(true);

            Assert.IsTrue(enemy.Initialize(capitanData));
            Assert.IsTrue(mover.IsMoving, "Sanity: mover should be active after Initialize.");

            enemy.TakeDamage(1);
            Assert.AreEqual(1, enemy.CurrentHealth);
            Assert.IsFalse(enemy.IsDying);
            yield return null;
            Assert.IsFalse(mover.IsMoving,
                "Capitan should stop while shield-break hurt animation is playing.");

            float waited = 0f;
            while (waited < 0.4f)
            {
                yield return null;
                waited += Time.deltaTime;
            }

            Assert.IsTrue(mover.IsMoving,
                "Capitan should resume movement after shield-break animation completes.");

            Object.Destroy(go);
        }
    }
}
