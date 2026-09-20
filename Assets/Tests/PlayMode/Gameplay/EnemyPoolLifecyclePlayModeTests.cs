using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// The frame-driven half of the pool-unregister coverage: a defeat WITH a
    /// death animation returns to the pool only when that animation's coroutine
    /// completes, which EditMode never advances. The synchronous variants stay
    /// in the EditMode EnemyPoolUnregisterLifecycleTests.
    /// </summary>
    [TestFixture]
    public class EnemyPoolLifecyclePlayModeTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private ActiveEnemyTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ProgressManager>();
            ClearSingletonInstance<EnemyPool>();
            var trackerGo = new GameObject("ActiveEnemyTracker_Unregister_Test");
            _tracker = trackerGo.AddComponent<ActiveEnemyTracker>();
            _objectsToDestroy.Add(trackerGo);
            SetSingletonInstance(_tracker);
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ProgressManager>();
            ClearSingletonInstance<ActiveEnemyTracker>();
            ClearSingletonInstance<EnemyPool>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.Destroy(_objectsToDestroy[i]);
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

            float timeout = 2f;
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

        [UnityTest]
        public IEnumerator GameOver_StopsWaveOwnedCoroutines_AndReturnsCheckedOutEnemies()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();
            Enemy first = pool.Get(data);
            Enemy second = pool.Get(data);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(2, _tracker.ActiveCount);

            WaveManager waveManager = CreateTerminalWaveManager("WaveManager_TerminalTeardown_Test");

            bool resumedAfterTerminal = false;
            waveManager.StartCoroutine(ResumeAfterDelay(() => resumedAfterTerminal = true));
            InvokePrivate<object>(waveManager, "HandleGameOver");

            yield return new WaitForSecondsRealtime(0.1f);

            Assert.IsFalse(resumedAfterTerminal,
                "A coroutine owned by WaveManager resumed after Game Over.");
            Assert.IsFalse(pool.IsCheckedOut(first));
            Assert.IsFalse(pool.IsCheckedOut(second));
            Assert.AreEqual(0, _tracker.ActiveCount);
        }

        [UnityTest]
        public IEnumerator AbortRun_StopsWaveOwnedCoroutines_AndReturnsCheckedOutEnemies()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();
            Enemy first = pool.Get(data);
            Enemy second = pool.Get(data);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(2, _tracker.ActiveCount);

            WaveManager waveManager = CreateTerminalWaveManager("WaveManager_AbortTeardown_Test");

            bool resumedAfterAbort = false;
            waveManager.StartCoroutine(ResumeAfterDelay(() => resumedAfterAbort = true));
            InvokePrivate<object>(waveManager, "AbortRun");

            yield return new WaitForSecondsRealtime(0.1f);

            Assert.IsFalse(resumedAfterAbort,
                "A coroutine owned by WaveManager resumed after abort.");
            Assert.IsFalse(pool.IsCheckedOut(first));
            Assert.IsFalse(pool.IsCheckedOut(second));
            Assert.AreEqual(0, _tracker.ActiveCount);
        }

        private static IEnumerator ResumeAfterDelay(System.Action onResume)
        {
            yield return new WaitForSecondsRealtime(0.05f);
            onResume?.Invoke();
        }

        private WaveManager CreateTerminalWaveManager(string objectName)
        {
            LevelConfigSO level = ScriptableObject.CreateInstance<LevelConfigSO>();
            level.levelNumber = 1;
            _objectsToDestroy.Add(level);

            GameObject gameManagerObject = new GameObject("GameManager_TerminalTeardown_Test");
            _objectsToDestroy.Add(gameManagerObject);
            GameManager gameManager = gameManagerObject.AddComponent<GameManager>();
            gameManager.SetLevel(level);

            GameObject waveObject = new GameObject(objectName);
            waveObject.SetActive(false);
            _objectsToDestroy.Add(waveObject);
            WaveManager waveManager = waveObject.AddComponent<WaveManager>();
            SetPrivateField(waveManager, "_waitForExternalStart", true);
            waveObject.SetActive(true);
            return waveManager;
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
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            return (T)method.Invoke(target, args);
        }
    }
}
