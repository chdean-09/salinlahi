using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class EnemyPoolUnregisterLifecycleTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<EnemyPool>();
            var trackerGo = new GameObject("ActiveEnemyTracker_Unregister_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<EnemyPool>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        [UnityTest]
        public IEnumerator Defeat_ReturnsToPool_AndUnregistersCleanly()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithDeathAnimation();

            Enemy enemy = pool.Get(data);
            Assert.IsNotNull(enemy);
            Assert.AreEqual(1, _tracker.ActiveCount);

            enemy.Defeat();
            Assert.IsTrue(enemy.IsDying);

            float timeout = 0.3f;
            float elapsed = 0f;
            while (pool.IsCheckedOut(enemy) && elapsed < timeout)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.IsFalse(pool.IsCheckedOut(enemy));
            Assert.AreEqual(0, _tracker.ActiveCount);
            Assert.IsFalse(enemy.gameObject.activeInHierarchy);
        }

        private Enemy CreateEnemyPrefab()
        {
            var prefabGo = new GameObject("EnemyPrefab_Unregister_Test");
            prefabGo.SetActive(false);
            _objectsToDestroy.Add(prefabGo);

            prefabGo.AddComponent<SpriteRenderer>();
            prefabGo.AddComponent<BoxCollider2D>();
            prefabGo.AddComponent<EnemyMover>();
            Enemy enemy = prefabGo.AddComponent<Enemy>();
            SetPrivateField(enemy, "_showDebugLabels", false);
            return enemy;
        }

        private EnemyPool CreateEnemyPool(Enemy prefab)
        {
            var poolGo = new GameObject("EnemyPool_Unregister_Test");
            poolGo.SetActive(false);
            _objectsToDestroy.Add(poolGo);

            EnemyPool pool = poolGo.AddComponent<EnemyPool>();
            SetPrivateField(pool, "_enemyPrefab", prefab);
            SetPrivateField(pool, "_defaultCapacity", 0);
            SetPrivateField(pool, "_maxSize", 8);
            poolGo.SetActive(true);
            InvokePrivate<object>(pool, "Awake");
            return pool;
        }

        private EnemyDataSO CreateEnemyDataWithDeathAnimation()
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.syllable = "ba";
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "soldado";
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;
            data.deathFrames = new[] { CreateSprite(Color.red) };
            data.deathAnimationFps = 120f;
            _objectsToDestroy.Add(data);
            return data;
        }

        private Sprite CreateSprite(Color color)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new[] { color, color, color, color });
            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
            _objectsToDestroy.Add(tex);
            _objectsToDestroy.Add(sprite);
            return sprite;
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            FieldInfo instanceField = typeof(Singleton<T>).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            instanceField?.SetValue(null, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }
    }
}
