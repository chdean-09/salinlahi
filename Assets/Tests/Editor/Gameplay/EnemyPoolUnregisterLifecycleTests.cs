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

        // Defeat_ReturnsToPool_AndUnregistersCleanly (the death-animation
        // variant) moved to EnemyPoolLifecyclePlayModeTests: the pool return
        // rides on the death-animation coroutine completing across frames,
        // which EditMode never advances.

        [Test]
        public void Defeat_WithoutDeathAnimation_ReturnsToPool_AndUnregistersCleanly()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();

            Enemy enemy = pool.Get(data);
            Assert.IsNotNull(enemy);
            Assert.AreEqual(1, _tracker.ActiveCount);

            enemy.Defeat();

            Assert.IsFalse(pool.IsCheckedOut(enemy));
            Assert.AreEqual(0, _tracker.ActiveCount);
            Assert.IsFalse(enemy.gameObject.activeInHierarchy);
        }

        [Test]
        public void ApplyDecoyPenalty_ReturnsToPool_AndUnregistersCleanly()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();
            data.isDecoy = true;

            Enemy enemy = pool.Get(data);
            Assert.IsNotNull(enemy);
            Assert.AreEqual(1, _tracker.ActiveCount);

            enemy.ApplyDecoyPenalty();

            Assert.IsFalse(pool.IsCheckedOut(enemy));
            Assert.AreEqual(0, _tracker.ActiveCount);
            Assert.IsFalse(enemy.gameObject.activeInHierarchy);
        }

        [Test]
        public void ReturnAllCheckedOut_SkipsEntriesReturnedByDisableCascade()
        {
            Enemy prefab = CreateEnemyPrefab();
            prefab.gameObject.AddComponent<EnemyPoolCascadeReturnOnDisable>();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();

            Enemy source = pool.Get(data);
            Enemy dependent = pool.Get(data);
            Assert.IsNotNull(source);
            Assert.IsNotNull(dependent);
            source.GetComponent<EnemyPoolCascadeReturnOnDisable>().Target = dependent;

            var duplicateReturns = new List<string>();
            Application.LogCallback callback = (message, stackTrace, type) =>
            {
                if (message != null && message.Contains("was already returned"))
                    duplicateReturns.Add(message);
            };
            Application.logMessageReceived += callback;
            try
            {
                pool.ReturnAllCheckedOut();
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }

            CollectionAssert.IsEmpty(duplicateReturns,
                "Bulk cleanup must tolerate an OnDisable cascade returning a later snapshot entry.");
            Assert.IsFalse(pool.IsCheckedOut(source));
            Assert.IsFalse(pool.IsCheckedOut(dependent));
            Assert.AreEqual(0, _tracker.ActiveCount);
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

        private EnemyDataSO CreateEnemyDataWithoutDeathAnimation()
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
            data.deathFrames = System.Array.Empty<Sprite>();
            data.deathAnimationFps = 0f;
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

    /// <summary>
    /// Test-only stand-in for MirrorDecoyController's OnDisable cascade. The production decoy
    /// returns itself when its source shell is released, which is the ordering that used to make
    /// ReturnAllCheckedOut call Return on an already-returned snapshot entry.
    /// </summary>
    public sealed class EnemyPoolCascadeReturnOnDisable : MonoBehaviour
    {
        public Enemy Target;

        private void OnDisable()
        {
            if (Target != null && Target.gameObject.activeInHierarchy)
                Target.ReturnToPool();
        }
    }
}
