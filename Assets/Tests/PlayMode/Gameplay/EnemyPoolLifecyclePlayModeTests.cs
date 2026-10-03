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

        [UnityTest]
        public IEnumerator DefeatThenRetry_ReturnsPoolBeforeTheNextAttempt()
        {
            Enemy prefab = CreateEnemyPrefab();
            EnemyPool pool = CreateEnemyPool(prefab);
            EnemyDataSO data = CreateEnemyDataWithoutDeathAnimation();
            WaveManager waveManager = CreateTerminalWaveManager("WaveManager_RetryTeardown_Test");

            Enemy firstAttempt = pool.Get(data);
            Assert.IsNotNull(firstAttempt);
            Assert.AreEqual(1, _tracker.ActiveCount);

            InvokePrivate<object>(waveManager, "HandleGameOver");
            yield return null;

            Assert.IsFalse(pool.IsCheckedOut(firstAttempt));
            Assert.AreEqual(0, _tracker.ActiveCount,
                "The defeated attempt must leave no active enemy before retry starts.");

            Enemy retryAttempt = pool.Get(data);
            Assert.IsNotNull(retryAttempt);
            Assert.AreEqual(1, _tracker.ActiveCount,
                "Retry should check out a clean enemy, not inherit the previous attempt.");

            InvokePrivate<object>(waveManager, "HandleGameOver");
            yield return null;

            Assert.IsFalse(pool.IsCheckedOut(retryAttempt));
            Assert.AreEqual(0, _tracker.ActiveCount,
                "Retry teardown must return its own checked-out enemies exactly once.");
        }

        [UnityTest]
        public IEnumerator HatiSplit_QueuesSecondPieceOneSecondAfterFirstAdmission()
        {
            EnemyPool pool = CreateEnemyPool(CreateEnemyPrefab());
            WaveManager waveManager = CreateTerminalWaveManager("WaveManager_HatiQueue_Test");
            LevelConfigSO level = GameManager.CurrentLevelConfig;
            level.levelNumber = 7;
            level.maxActiveEnemies = 2;
            SetPrivateField(waveManager, "_levelConfig", level);
            WaveSpawner spawner = waveManager.gameObject.AddComponent<WaveSpawner>();
            SetPrivateField(waveManager, "_spawner", spawner);
            GameManager.Instance.StartGame();

            EnemyDataSO sourceData = CreateEnemyDataWithDeathAnimation();
            EnemyDataSO pieceData = CreateEnemyDataWithoutDeathAnimation();
            sourceData.splitsOnDefeat = true;
            sourceData.splitCount = 2;
            sourceData.splitSpawnData = pieceData;
            sourceData.splitOffsetX = 1f;
            Sprite holdFrame = sourceData.deathFrames[0];
            sourceData.deathFrames = new[] { holdFrame, holdFrame, holdFrame };
            sourceData.deathAnimationFps = 0.25f;

            EnemyDataSO occupantData = CreateEnemyDataWithoutDeathAnimation();
            Enemy occupant = pool.Get(occupantData);
            Enemy source = pool.Get(sourceData);
            source.transform.position = new Vector3(4f, 2f, 0f);
            Assert.IsNotNull(occupant);
            Assert.IsNotNull(source);
            Assert.AreEqual(2, _tracker.ActiveCount);

            source.Defeat();
            Assert.IsTrue(source.IsDying);
            Assert.AreEqual(1, waveManager.PendingGeneratedSpawnCount,
                "The first piece waits while the dying source and another enemy fill the cap.");

            yield return new WaitForSeconds(1.1f);
            Assert.AreEqual(2, _tracker.ActiveCount,
                "Dying enemies retain their slot until the pool returns them.");
            Assert.AreEqual(1, waveManager.PendingGeneratedSpawnCount);

            pool.Return(occupant);
            waveManager.ProcessPendingGeneratedEnemySpawns();
            Assert.AreEqual(2, _tracker.ActiveCount,
                "Returning one slot admits the first Hati piece.");
            Assert.AreEqual(1, waveManager.PendingGeneratedSpawnCount,
                "The second piece is queued only after the first is actually admitted.");

            yield return new WaitForSeconds(0.25f);
            pool.Return(source);
            waveManager.ProcessPendingGeneratedEnemySpawns();
            Assert.AreEqual(1, waveManager.PendingGeneratedSpawnCount,
                "Free capacity cannot bypass the one-scaled-second delay after first admission.");

            yield return new WaitForSeconds(0.85f);
            waveManager.ProcessPendingGeneratedEnemySpawns();
            Assert.AreEqual(0, waveManager.PendingGeneratedSpawnCount);
            var active = _tracker.GetActiveEnemiesSnapshot();
            int pieces = 0;
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] == null || active[i].Data != pieceData)
                    continue;
                pieces++;
                Assert.AreEqual(1, active[i].CurrentHealth, "Every fragment is a one-hit enemy.");
                Assert.IsFalse(active[i].Data.splitsOnDefeat, "Fragments must not split recursively.");
            }
            Assert.AreEqual(2, pieces);
            pool.ReturnAllCheckedOut();
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

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator Level13_NeededSaDraw_AdvancesRestorationAndTheNextSpawn()
        {
            var authored = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                "Assets/ScriptableObjects/Levels/Level13_Config.asset");
            Assert.IsNotNull(authored);
            var level = Object.Instantiate(authored);
            _objectsToDestroy.Add(level);
            // Keep the real words and roster; remove pacing delays for a deterministic draw.
            level.spawnAssignmentPolicy = new SpawnAssignmentPolicy
            {
                minSpawnsBeforeNeeded = 0,
                neededWeight = 1f,
                choiceMomentSlotIndex = -1,
                activeSlotWindow = 1
            };

            var directorGo = new GameObject("Level13_ClueDirector_Test");
            _objectsToDestroy.Add(directorGo);
            var director = directorGo.AddComponent<ActiveClueDirector>();
            director.SetObjectiveSource(new TestClueObjective());
            var canvasGo = new GameObject("Level13_Hud_Test", typeof(Canvas));
            _objectsToDestroy.Add(canvasGo);
            var presenterGo = new GameObject("Level13_Presenter_Test");
            presenterGo.transform.SetParent(canvasGo.transform);
            var presenter = presenterGo.AddComponent<ActiveCluePresenter>();
            presenter.ApplyLevel(level);
            var mark = GameObject.Find("[Runtime] ActiveClueMark");
            if (mark != null) _objectsToDestroy.Add(mark);

            var coordinatorGo = new GameObject("Level13_Assignment_Test");
            _objectsToDestroy.Add(coordinatorGo);
            var coordinator = coordinatorGo.AddComponent<SpawnAssignmentCoordinator>();
            coordinator.ApplyLevel(level, presenter);
            var spawnerGo = new GameObject("Level13_Spawner_Test");
            _objectsToDestroy.Add(spawnerGo);
            var spawner = spawnerGo.AddComponent<WaveSpawner>();
            SetPrivateField(spawner, "_spawnPoints", new[] { spawnerGo.transform, spawnerGo.transform });
            SetPrivateField(spawner, "_assignmentCoordinator", coordinator);
            var pool = CreateEnemyPool(CreateEnemyPrefab());
            var resolverGo = new GameObject("Level13_CombatResolver_Test");
            _objectsToDestroy.Add(resolverGo);
            resolverGo.AddComponent<CombatResolver>();
            yield return null;

            var wave = new WaveDefinition
            {
                enemyCount = 1,
                spawnInterval = 1f,
                enemyTypes = authored.waves[0].enemyTypes,
                characters = authored.waves[0].characters
            };
            yield return spawner.SpawnWave(wave);
            var enemies = new List<Enemy>();
            _tracker.FillActiveEnemiesSnapshot(enemies);
            Assert.AreEqual(1, enemies.Count);
            Enemy carrier = enemies[0];
            Assert.AreEqual("SA", carrier.Character.characterID);
            Assert.IsFalse(carrier.IsDecoy, "Needed SA must not resolve to Salungat.");
            director.Reevaluate();
            Assert.AreSame(carrier, director.CurrentClue);
            int before = presenter.RestorationState.RestoredSlotCount;
            EventBus.RaiseCharacterRecognized("SA");
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(presenter.RestorationState.RestoredSlotCount, before,
                "The actual accepted SA draw must restore its slot.");
            Assert.AreEqual("symbol.nga", presenter.RestorationState.NextTargetSymbolStableId);

            pool.ReturnAllCheckedOut();
            yield return spawner.SpawnWave(wave);
            _tracker.FillActiveEnemiesSnapshot(enemies);
            Assert.AreEqual(1, enemies.Count);
            Assert.AreEqual("NGA", enemies[0].Character.characterID,
                "The scheduler must move past SA instead of repeating Salungat forever.");
            pool.ReturnAllCheckedOut();
        }

        private sealed class TestClueObjective : IClueObjectiveSource
        {
            public bool IsClueCombatActive => true;
            public IReadOnlyCollection<string> CurrentObjectiveContentIds => System.Array.Empty<string>();
        }
#endif

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
