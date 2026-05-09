using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class BossControllerTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private int _onDrawingFailedCount;
        private int _onBossDefeatedCount;
        private int _onLevelCompleteCount;
        private int _onPhaseStartedCount;
        private int _onPhaseClearedCount;

        // Named delegates so subscribe/unsubscribe match correctly.
        private System.Action _onDrawingFailed;
        private System.Action _onBossDefeated;
        private System.Action _onLevelComplete;
        private System.Action<int> _onBossPhaseStarted;
        private System.Action<int> _onBossPhaseCleared;

        [SetUp]
        public void SetUp()
        {
            _onDrawingFailedCount = 0;
            _onBossDefeatedCount = 0;
            _onLevelCompleteCount = 0;
            _onPhaseStartedCount = 0;
            _onPhaseClearedCount = 0;

            _onDrawingFailed = () => _onDrawingFailedCount++;
            _onBossDefeated = () => _onBossDefeatedCount++;
            _onLevelComplete = () => _onLevelCompleteCount++;
            _onBossPhaseStarted = _ => _onPhaseStartedCount++;
            _onBossPhaseCleared = _ => _onPhaseClearedCount++;

            EventBus.OnDrawingFailed += _onDrawingFailed;
            EventBus.OnBossDefeated += _onBossDefeated;
            EventBus.OnLevelComplete += _onLevelComplete;
            EventBus.OnBossPhaseStarted += _onBossPhaseStarted;
            EventBus.OnBossPhaseCleared += _onBossPhaseCleared;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnDrawingFailed -= _onDrawingFailed;
            EventBus.OnBossDefeated -= _onBossDefeated;
            EventBus.OnLevelComplete -= _onLevelComplete;
            EventBus.OnBossPhaseStarted -= _onBossPhaseStarted;
            EventBus.OnBossPhaseCleared -= _onBossPhaseCleared;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        // ---- Test 1 — §10.1 ----
        [UnityTest]
        public IEnumerator NonRequiredDraw_ReturnsNotRouted_NoAdvance()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            // Advance frames so Intro (0s) elapses and PhaseActive begins.
            yield return null;
            yield return null;

            BossRouteResult result = boss.TryRouteDraw(ka.characterID);
            Assert.AreEqual(BossRouteResult.NotRouted, result);
        }

        // ---- Test 2 — §10.2 ----
        [UnityTest]
        public IEnumerator ThreeRequiredChars_ClearsWhenAllDrawn_AnyOrder()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            BaybayinCharacterSO ga = CreateChar("GA");
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba, ka, ga }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("GA"));
            Assert.AreEqual(0, _onPhaseClearedCount);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            Assert.AreEqual(0, _onPhaseClearedCount);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("KA"));
            Assert.AreEqual(1, _onPhaseClearedCount);
        }

        // ---- Test 3 — §10.3 ----
        [UnityTest]
        public IEnumerator DuplicateRequiredDraw_RaisesOnDrawingFailed_Consumed()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba, ka }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            BossRouteResult dup = boss.TryRouteDraw("BA");
            Assert.AreEqual(BossRouteResult.Duplicate, dup);
            Assert.AreEqual(1, _onDrawingFailedCount);
            Assert.AreEqual(0, _onPhaseClearedCount, "Duplicate must not clear the phase.");
        }

        // ---- Test 4 — §10.4 ----
        [UnityTest]
        public IEnumerator LastPhaseCleared_RaisesOnBossDefeated_AndOnLevelComplete()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0.05f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));

            // Wait outroDuration + a frame.
            float t = 0f;
            while (t < 0.2f) { yield return null; t += Time.deltaTime; }

            Assert.AreEqual(1, _onBossDefeatedCount);
            Assert.AreEqual(1, _onLevelCompleteCount);
        }

        // ---- Test 5 — §10.5 ----
        [UnityTest]
        public IEnumerator Intro_IsTargetableFalse_TryRouteDrawReturnsNotRouted()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BossConfigSO config = CreateConfig(introDuration: 0.2f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null;

            Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during Intro.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"));
        }

        // ---- Test 6 — §10.6 ----
        [UnityTest]
        public IEnumerator Intermission_IsTargetableFalse_TryRouteDrawReturnsNotRouted()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            WaveConfigSO intermission = ScriptableObject.CreateInstance<WaveConfigSO>();
            _objectsToDestroy.Add(intermission);

            BossPhase phase1 = CreatePhase(new[] { ba });
            phase1.intermissionWave = intermission;
            phase1.postIntermissionDelay = 1f;

            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { phase1, CreatePhase(new[] { ka }) });

            (BossController boss, FakeWaveSpawner spawner) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            yield return null;

            Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during intermission.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("KA"));
            Assert.AreEqual(1, spawner.SpawnWaveCallCount,
                "Intermission must spawn the configured wave exactly once.");
        }

        // ---- Test 7 — §10.7 ----
        [UnityTest]
        public IEnumerator IntermissionSpawning_UsesInjectedSpawner()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            WaveConfigSO intermission = ScriptableObject.CreateInstance<WaveConfigSO>();
            _objectsToDestroy.Add(intermission);

            BossPhase phase1 = CreatePhase(new[] { ba });
            phase1.intermissionWave = intermission;

            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { phase1, CreatePhase(new[] { ka }) });

            (BossController boss, FakeWaveSpawner spawner) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            boss.TryRouteDraw("BA");
            yield return null;

            Assert.AreSame(intermission, spawner.LastSpawnedWave,
                "BossController must call SpawnWave on the injected spawner with the configured wave.");
        }

        // ---- Test 8 — §10.8 ----
        [UnityTest]
        public IEnumerator IsDefeated_FlipsAtStartOfOutro()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 1f, phases:
                new List<BossPhase> { CreatePhase(new[] { ba }) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return null; yield return null;

            Assert.IsFalse(boss.IsDefeated);
            boss.TryRouteDraw("BA");
            yield return null;
            Assert.IsTrue(boss.IsDefeated, "IsDefeated must flip true at the start of Outro.");
        }

        // ---- Helpers ----

        private BaybayinCharacterSO CreateChar(string id)
        {
            BaybayinCharacterSO so = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            so.characterID = id;
            _objectsToDestroy.Add(so);
            return so;
        }

        private BossPhase CreatePhase(IReadOnlyList<BaybayinCharacterSO> required)
        {
            BossPhase phase = new BossPhase();
            phase.requiredCharacters = new List<BaybayinCharacterSO>(required);
            phase.movementPattern = BossMovementPattern.Hover;
            phase.movementSpeed = 0f;
            return phase;
        }

        private BossConfigSO CreateConfig(float introDuration, float outroDuration, List<BossPhase> phases)
        {
            BossConfigSO config = ScriptableObject.CreateInstance<BossConfigSO>();
            config.bossName = "TestBoss";
            config.bossID = "TEST";
            config.introDuration = introDuration;
            config.outroDuration = outroDuration;
            config.phases = phases;

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "test_boss";
            data.maxHealth = 1;
            data.moveSpeed = 0f;
            config.bossEnemyData = data;
            _objectsToDestroy.Add(data);

            _objectsToDestroy.Add(config);
            return config;
        }

        private FakeWaveSpawner _fakeSpawner;
        private GameObject _spawnerGO;

        private (BossController, FakeWaveSpawner) CreateBossWithFakeSpawner()
        {
            GameObject bossGO = new GameObject("Boss_Test");
            bossGO.SetActive(false);
            bossGO.AddComponent<SpriteRenderer>();
            bossGO.AddComponent<BoxCollider2D>();
            bossGO.AddComponent<EnemyMover>();
            BossEnemy enemy = bossGO.AddComponent<BossEnemy>();
            // Suppress debug label creation during Awake in the Editor test runner.
            SetField(enemy, "_showDebugLabels", false);
            BossController controller = bossGO.AddComponent<BossController>();
            bossGO.SetActive(true);
            _objectsToDestroy.Add(bossGO);

            _spawnerGO = new GameObject("FakeWaveSpawner");
            _fakeSpawner = _spawnerGO.AddComponent<FakeWaveSpawner>();
            _objectsToDestroy.Add(_spawnerGO);

            return (controller, _fakeSpawner);
        }

        private WaveSpawner GetFakeSpawner() => _fakeSpawner;

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo f = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(f, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            f.SetValue(target, value);
        }

        // ---- Test double ----

        private class FakeWaveSpawner : WaveSpawner
        {
            public int SpawnWaveCallCount;
            public WaveConfigSO LastSpawnedWave;

            public override IEnumerator SpawnWave(WaveConfigSO wave, System.Action onEnemySpawned = null, int spawnOffset = 0)
            {
                SpawnWaveCallCount++;
                LastSpawnedWave = wave;
                yield break;
            }
        }
    }
}
