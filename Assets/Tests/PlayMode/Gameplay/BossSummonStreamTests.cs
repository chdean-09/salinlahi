using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    // Drives BossSummonTicker.PlayTickAndSpawn directly via a stub WaveSpawner
    // so we can assert per-spawn cadence without a full encounter rig.
    // Mirrors the test-double pattern in BossControllerTests.FakeWaveSpawner.
    [TestFixture]
    public class BossSummonStreamTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private FakeWaveSpawner _spawner;
        private BossSummonTicker _ticker;

        [SetUp]
        public void SetUp()
        {
            // Ticker requires BossController due to [RequireComponent].
            GameObject tickerGO = new GameObject("Boss_Test_Ticker");
            tickerGO.SetActive(false);
            tickerGO.AddComponent<SpriteRenderer>();
            tickerGO.AddComponent<BoxCollider2D>();
            tickerGO.AddComponent<EnemyMover>();
            BossEnemy enemy = tickerGO.AddComponent<BossEnemy>();
            SetField(enemy, "_showDebugLabels", false);  // suppress debug label child
            tickerGO.AddComponent<BossController>();
            _ticker = tickerGO.AddComponent<BossSummonTicker>();
            // No summon frames assigned → windup is a 0-frame no-op so the
            // stream starts immediately and the cadence is measurable.
            tickerGO.SetActive(true);
            _objectsToDestroy.Add(tickerGO);

            GameObject spawnerGO = new GameObject("FakeWaveSpawner");
            _spawner = spawnerGO.AddComponent<FakeWaveSpawner>();
            _objectsToDestroy.Add(spawnerGO);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        // ---- Test 1 — spec §"Testing strategy" #1 ----
        [UnityTest]
        public IEnumerator Stream_FixedCountAndCadence_SpawnsOneAtATime()
        {
            EnemyDataSO summonData = ScriptableObject.CreateInstance<EnemyDataSO>();
            summonData.enemyID = "stream_summon";
            summonData.maxHealth = 1;
            _objectsToDestroy.Add(summonData);

            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.fallbackEnemyTypes = new List<EnemyDataSO> { summonData };
            _objectsToDestroy.Add(config);

            BossPhase phase = new BossPhase
            {
                summonPhaseDuration = 10f,
                delayBetweenSummons = 5f,
                minionsPerSummonMin = 4,
                minionsPerSummonMax = 4,
                delayBetweenMinions = 0.5f,
                summonEnemyTypes = new List<EnemyDataSO>(),
                summonSpawnRange = Vector2.zero,
            };

            // Start the act and sample SpawnEnemyCallCount at known times.
            _ticker.StartCoroutine(_ticker.PlayTickAndSpawn(phase, config, _spawner));

            // After ~0.1s (post-windup, pre-first-yield), exactly 1 enemy.
            yield return new WaitForSeconds(0.1f);
            Assert.AreEqual(1, _spawner.SpawnEnemyCallCount,
                "First minion must spawn immediately after the windup.");

            // After ~0.6s total, the 0.5s yield should have elapsed → 2 spawns.
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(2, _spawner.SpawnEnemyCallCount,
                "Second minion must spawn after delayBetweenMinions elapses.");

            // After ~1.6s total, all 4 spawns done; tail is still running.
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(4, _spawner.SpawnEnemyCallCount,
                "All 4 minions must spawn within count × delayBetweenMinions seconds.");
        }

        // ---- Test 2 — spec §"Testing strategy" #2 ----
        [UnityTest]
        public IEnumerator IsPlayingSummonAnimation_StaysTrue_ThroughStreamAndTail()
        {
            EnemyDataSO summonData = ScriptableObject.CreateInstance<EnemyDataSO>();
            summonData.enemyID = "tail_summon";
            summonData.maxHealth = 1;
            _objectsToDestroy.Add(summonData);

            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.fallbackEnemyTypes = new List<EnemyDataSO> { summonData };
            _objectsToDestroy.Add(config);

            BossPhase phase = new BossPhase
            {
                summonPhaseDuration = 10f,
                delayBetweenSummons = 5f,
                minionsPerSummonMin = 3,
                minionsPerSummonMax = 3,
                delayBetweenMinions = 0.2f,
                summonEnemyTypes = new List<EnemyDataSO>(),
                summonSpawnRange = Vector2.zero,
            };

            _ticker.StartCoroutine(_ticker.PlayTickAndSpawn(phase, config, _spawner));

            // Sample at three moments during the stream.
            yield return new WaitForSeconds(0.05f);
            Assert.IsTrue(_ticker.IsPlayingSummonAnimation,
                "Flag must be true at the very start of the act.");

            yield return new WaitForSeconds(0.3f);  // mid-stream (~0.35s in, after 2 spawns)
            Assert.IsTrue(_ticker.IsPlayingSummonAnimation,
                "Flag must remain true mid-stream.");

            // Sample the tail deterministically: wait for the third spawn (the
            // stream's end) rather than a wall-clock offset — WaitForSeconds
            // overshoot under load could land past the 0.3s pose tail and turn
            // this assert into a flake.
            float guard = 0f;
            while (_spawner.SpawnEnemyCallCount < 3 && guard < 5f)
            {
                yield return null;
                guard += Time.deltaTime;
            }
            Assert.AreEqual(3, _spawner.SpawnEnemyCallCount,
                "All three stream spawns must arrive.");
            yield return null;
            Assert.IsTrue(_ticker.IsPlayingSummonAnimation,
                "Flag must remain true during the post-stream pose tail.");

            // The flag must clear once the _holdLastFrameDuration tail elapses;
            // poll with a generous guard instead of a fixed sleep.
            guard = 0f;
            while (_ticker.IsPlayingSummonAnimation && guard < 2f)
            {
                yield return null;
                guard += Time.deltaTime;
            }
            Assert.IsFalse(_ticker.IsPlayingSummonAnimation,
                "Flag must clear after _holdLastFrameDuration tail elapses.");
        }

        // ---- Test 3 — spec §"Testing strategy" #3 ----
        [UnityTest]
        public IEnumerator DelayBetweenMinionsZero_DegeneratesToBurst()
        {
            EnemyDataSO summonData = ScriptableObject.CreateInstance<EnemyDataSO>();
            summonData.enemyID = "burst_summon";
            summonData.maxHealth = 1;
            _objectsToDestroy.Add(summonData);

            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.fallbackEnemyTypes = new List<EnemyDataSO> { summonData };
            _objectsToDestroy.Add(config);

            BossPhase phase = new BossPhase
            {
                summonPhaseDuration = 10f,
                delayBetweenSummons = 5f,
                minionsPerSummonMin = 3,
                minionsPerSummonMax = 3,
                delayBetweenMinions = 0f,
                summonEnemyTypes = new List<EnemyDataSO>(),
                summonSpawnRange = Vector2.zero,
            };

            int callCountBefore = _spawner.SpawnEnemyCallCount;
            _ticker.StartCoroutine(_ticker.PlayTickAndSpawn(phase, config, _spawner));

            // One frame is enough for a 0-delay burst with no windup.
            yield return null;
            yield return null;

            Assert.AreEqual(callCountBefore + 3, _spawner.SpawnEnemyCallCount,
                "delayBetweenMinions = 0 must spawn the entire count in one frame.");
        }

        // ---- Test 4 — spec §"Testing strategy" #4 ----
        [UnityTest]
        public IEnumerator MinionsPerSummonZero_CleanNoOp()
        {
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.fallbackEnemyTypes = new List<EnemyDataSO>();
            _objectsToDestroy.Add(config);

            BossPhase phase = new BossPhase
            {
                summonPhaseDuration = 10f,
                delayBetweenSummons = 5f,
                minionsPerSummonMin = 0,
                minionsPerSummonMax = 0,
                delayBetweenMinions = 0.5f,
                summonEnemyTypes = new List<EnemyDataSO>(),
                summonSpawnRange = Vector2.zero,
            };

            int callCountBefore = _spawner.SpawnEnemyCallCount;
            _ticker.StartCoroutine(_ticker.PlayTickAndSpawn(phase, config, _spawner));

            yield return new WaitForSeconds(0.6f);  // past the tail
            Assert.AreEqual(callCountBefore, _spawner.SpawnEnemyCallCount,
                "Zero minions configured must spawn nothing.");
            Assert.IsFalse(_ticker.IsPlayingSummonAnimation,
                "Flag must clear after the tail even on a no-spawn act.");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            FieldInfo f = null;
            for (System.Type t = target.GetType(); t != null && f == null; t = t.BaseType)
                f = t.GetField(fieldName, flags);
            Assert.IsNotNull(f, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        // ---- Test doubles ----
        private class FakeWaveSpawner : WaveSpawner
        {
            public int SpawnEnemyCallCount;
            public override Enemy SpawnEnemy(EnemyDataSO data)
            {
                SpawnEnemyCallCount++;
                return null;  // BossSummonTicker tolerates a null summon (skips position assignment).
            }
        }
    }
}
