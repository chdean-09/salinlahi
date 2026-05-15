using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class PhaserEnemyTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator IsPhaser_True_TogglesVisibilityOnCoroutine()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            Assert.IsTrue(phaser.IsVisible);
            Assert.IsTrue(renderer.enabled);

            float timeout = 0.2f;
            float elapsed = 0f;
            while (phaser.IsVisible && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.IsFalse(phaser.IsVisible);
            Assert.Less(renderer.color.a, 0.05f);
        }

        [UnityTest]
        public IEnumerator IsPhaser_False_DoesNotToggleVisibility()
        {
            Enemy enemy = CreateEnemy(isPhaser: false, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            yield return new WaitForSeconds(0.08f);
            Assert.IsTrue(phaser.IsVisible);
            Assert.Greater(renderer.color.a, 0.95f);
        }

        [UnityTest]
        public IEnumerator DisableEnable_ResetsToVisibleStateForPoolSafety()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 0.02f);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SpriteRenderer renderer = enemy.GetComponent<SpriteRenderer>();

            float timeout = 0.2f;
            float elapsed = 0f;
            while (phaser.IsVisible && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.IsFalse(phaser.IsVisible, "Sanity: test should reach an invisible state first.");
            enemy.gameObject.SetActive(false);
            yield return null;

            enemy.gameObject.SetActive(true);
            yield return null;

            Assert.IsTrue(phaser.IsVisible);
            Assert.Greater(renderer.color.a, 0.95f);
        }

        [Test]
        public void InvisiblePhaser_IgnoresTakeDamage()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 5f, maxHealth: 2);
            PhaserEnemy phaser = enemy.GetComponent<PhaserEnemy>();
            SetPrivateField(phaser, "_isVisible", false);

            enemy.TakeDamage(1);

            Assert.AreEqual(2, enemy.CurrentHealth);
        }

        [Test]
        public void VisiblePhaser_TakesDamageNormally()
        {
            Enemy enemy = CreateEnemy(isPhaser: true, phaserInterval: 5f, maxHealth: 2);
            enemy.TakeDamage(1);
            Assert.AreEqual(1, enemy.CurrentHealth);
        }

        private Enemy CreateEnemy(bool isPhaser, float phaserInterval, int maxHealth = 1)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.syllable = "ba";
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "fraile_test";
            data.maxHealth = maxHealth;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;
            data.isPhaser = isPhaser;
            data.phaserInterval = phaserInterval;
            _objectsToDestroy.Add(data);

            GameObject go = new GameObject("Enemy_Phaser_Component_Test");
            go.SetActive(false);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<BoxCollider2D>();
            go.AddComponent<EnemyMover>();
            go.AddComponent<PhaserEnemy>();
            Enemy enemy = go.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            go.SetActive(true);
            _objectsToDestroy.Add(go);

            Assert.IsTrue(enemy.Initialize(data));
            return enemy;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
