using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// The shipped [Manager] EnemyPool registers a dedicated pool for one enemyID while roughly
    /// twenty EnemyDataSOs exist, because most of them deliberately share the default
    /// '[Enemy] Corrupted' shell. These tests pin that "unregistered id" is the designed spawn
    /// path and not a fault.
    /// </summary>
    [TestFixture]
    public class EnemyPoolFallbackPoolTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<EnemyPool>();
            var trackerGo = new GameObject("ActiveEnemyTracker_Fallback_Test");
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

        [Test]
        public void Get_WithUnregisteredEnemyID_StillSpawnsFromTheSharedDefaultPool()
        {
            EnemyPool pool = CreateEnemyPool(CreateEnemyPrefab());
            EnemyDataSO data = CreateEnemyData("ashared_shell_enemy_with_no_dedicated_pool");

            Enemy enemy = pool.Get(data);

            Assert.IsNotNull(
                enemy,
                "An enemyID with no dedicated pool must still spawn from the shared default pool: "
                + "that is the design, not a misconfiguration.");
            Assert.IsTrue(pool.IsCheckedOut(enemy));
        }

        // Shared-shell spawning is normal and must not allocate an interpolated log per spawn.
        [Test]
        public void Get_WithUnregisteredEnemyID_DoesNotLogTheNormalSharedPoolPath()
        {
#if ENABLE_SALINLAHI_LOG
            EnemyPool pool = CreateEnemyPool(CreateEnemyPrefab());
            EnemyDataSO data = CreateEnemyData("shared_shell_enemy_with_no_dedicated_pool");

            int fallbackLogs = 0;
            void CountFallback(string message, string stack, LogType type)
            {
                if (message.Contains("has no dedicated pool")) fallbackLogs++;
            }
            Application.logMessageReceived += CountFallback;
            try
            {
                Assert.IsNotNull(pool.Get(data));
                Assert.AreEqual(0, fallbackLogs);
            }
            finally
            {
                Application.logMessageReceived -= CountFallback;
            }
#else
            Assert.Ignore(
                "Compiled out: ENABLE_SALINLAHI_LOG is Standalone-only. Run this suite with "
                + "-buildTarget StandaloneOSX to exercise it. Counted as NOT RUN, not as a pass.");
#endif
        }

        private Enemy CreateEnemyPrefab()
        {
            var prefabGo = new GameObject("EnemyPrefab_Fallback_Test");
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
            var poolGo = new GameObject("EnemyPool_Fallback_Test");
            poolGo.SetActive(false);
            _objectsToDestroy.Add(poolGo);

            EnemyPool pool = poolGo.AddComponent<EnemyPool>();
            SetPrivateField(pool, "_enemyPrefab", prefab);
            SetPrivateField(pool, "_defaultCapacity", 0);
            SetPrivateField(pool, "_maxSize", 8);
            poolGo.SetActive(true);
            InvokePrivate(pool, "Awake");
            return pool;
        }

        private EnemyDataSO CreateEnemyData(string enemyID)
        {
            BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = "BA";
            character.syllable = "ba";
            _objectsToDestroy.Add(character);

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = enemyID;
            data.maxHealth = 1;
            data.moveSpeed = 1f;
            data.assignedCharacter = character;
            data.deathFrames = System.Array.Empty<Sprite>();
            data.deathAnimationFps = 0f;
            _objectsToDestroy.Add(data);
            return data;
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            // Not null-conditional: if either lookup ever returns null the tracker is never
            // installed and the test quietly exercises a different scenario, which is the
            // failure mode where a test that asserts nothing still reports as a pass.
            PropertyInfo property = typeof(Singleton<T>).GetProperty("Instance");
            Assert.IsNotNull(property, "Singleton<T> no longer exposes an 'Instance' property.");
            MethodInfo setter = property.GetSetMethod(true);
            Assert.IsNotNull(setter, "Singleton<T>.Instance no longer has a setter to drive.");
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            FieldInfo instanceField = typeof(Singleton<T>).GetField(
                "<Instance>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(instanceField,
                "Singleton<T>'s auto-property backing field was renamed; the singleton is no "
                + "longer being cleared between tests, which leaks state into the next fixture.");
            instanceField.SetValue(null, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }
    }
}
