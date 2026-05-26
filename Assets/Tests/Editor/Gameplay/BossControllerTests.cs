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
        private int _onBossDamagedCount;
        private int _onBossVulnerabilityExpiredCount;
        private int _lastDamagedHpRemaining = -1;

        // Named delegates so subscribe/unsubscribe match correctly.
        private System.Action _onDrawingFailed;
        private System.Action _onBossDefeated;
        private System.Action _onLevelComplete;
        private System.Action<int, int> _onBossDamaged;
        private System.Action<int> _onBossVulnerabilityExpired;

        private LevelConfigSO _testLevelConfig;

        [SetUp]
        public void SetUp()
        {
            _onDrawingFailedCount = 0;
            _onBossDefeatedCount = 0;
            _onLevelCompleteCount = 0;
            _onBossDamagedCount = 0;
            _onBossVulnerabilityExpiredCount = 0;
            _lastDamagedHpRemaining = -1;

            _onDrawingFailed = () => _onDrawingFailedCount++;
            _onBossDefeated = () => _onBossDefeatedCount++;
            _onLevelComplete = () => _onLevelCompleteCount++;
            _onBossDamaged = (phase, hp) => { _onBossDamagedCount++; _lastDamagedHpRemaining = hp; };
            _onBossVulnerabilityExpired = _ => _onBossVulnerabilityExpiredCount++;

            EventBus.OnDrawingFailed += _onDrawingFailed;
            EventBus.OnBossDefeated += _onBossDefeated;
            EventBus.OnLevelComplete += _onLevelComplete;
            EventBus.OnBossDamaged += _onBossDamaged;
            EventBus.OnBossVulnerabilityExpired += _onBossVulnerabilityExpired;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnDrawingFailed -= _onDrawingFailed;
            EventBus.OnBossDefeated -= _onBossDefeated;
            EventBus.OnLevelComplete -= _onLevelComplete;
            EventBus.OnBossDamaged -= _onBossDamaged;
            EventBus.OnBossVulnerabilityExpired -= _onBossVulnerabilityExpired;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();
        }

        // ---- Test 1 — spec §11 ----
        [UnityTest]
        public IEnumerator Vulnerable_NCorrectDrawsCompleted_TransitionsToDamaged()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 3, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            yield return WaitUntilTargetable(boss, timeout: 2f);

            Assert.IsTrue(boss.IsTargetable);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));

            yield return null;
            yield return null;
            Assert.AreEqual(1, _onBossDamagedCount);
            Assert.AreEqual(0, _lastDamagedHpRemaining,
                "Single-phase boss: HP after the damaged event must be 0.");
        }

        // ---- Test 2 — spec §11 ----
        [UnityTest]
        public IEnumerator Vulnerable_TimerExpiresWithFewerThanNCorrect_ReturnsToSummoningSamePhase()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 3, vulnerabilityTimer: 0.3f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            yield return WaitUntilTargetable(boss, timeout: 2f);
            int phaseAtVulnerable = boss.CurrentPhaseIndex;
            int hpAtVulnerable = boss.HPRemaining;

            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }

            Assert.AreEqual(1, _onBossVulnerabilityExpiredCount);
            Assert.AreEqual(0, _onBossDamagedCount);
            Assert.AreEqual(phaseAtVulnerable, boss.CurrentPhaseIndex,
                "Forgiving timeout must keep the same phase index.");
            Assert.AreEqual(hpAtVulnerable, boss.HPRemaining, "HP must be unchanged after timeout.");
        }

        // ---- Test 3 — spec §11 ----
        [UnityTest]
        public IEnumerator Vulnerable_WrongGlyph_DoesNotAdvanceQueueOrAffectTimer()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            CreateLevelConfig(ba); // Pool contains only BA -> KA is always "wrong".
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 2, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return WaitUntilTargetable(boss, timeout: 2f);

            string expectedBefore = boss.CurrentExpectedCharacterID;
            int failedBefore = _onDrawingFailedCount;

            BossRouteResult result = boss.TryRouteDraw("KA");
            Assert.AreEqual(BossRouteResult.WrongGlyph, result);
            Assert.AreEqual(0, _onBossDamagedCount, "Wrong glyph must not damage.");
            Assert.AreEqual(expectedBefore, boss.CurrentExpectedCharacterID,
                "Wrong glyph must not advance the expected character.");
            Assert.AreEqual(failedBefore + 1, _onDrawingFailedCount,
                "Wrong glyph must raise OnDrawingFailed exactly once (spec §7).");
        }

        // ---- Test 4 — spec §11 ----
        [UnityTest]
        public IEnumerator Vulnerable_AfterCorrectDraw_NextExpectedGlyphIsFromAllowedPool()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba); // Single-element pool — every sample yields BA.
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 3, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return WaitUntilTargetable(boss, timeout: 2f);

            Assert.AreEqual("BA", boss.CurrentExpectedCharacterID);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            Assert.AreEqual("BA", boss.CurrentExpectedCharacterID,
                "After a correct draw, the next expected character must be sampled from the pool.");
        }

        // ---- Test 4b — regression: UI must read the NEWLY sampled glyph ----
        // Repro for the bug where TryRouteDraw fired OnDrawnThisPhaseChanged
        // BEFORE sampling the next character, leaving the UI stuck on the
        // just-matched glyph while the boss internally expected a different one.
        [UnityTest]
        public IEnumerator Vulnerable_AfterCorrectDraw_SubscriberSeesNewlySampledGlyph()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            CreateLevelConfig(ba, ka); // Multi-character pool: new sample may differ.
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 5, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return WaitUntilTargetable(boss, timeout: 2f);

            string observedInsideHandler = null;
            System.Action handler = () => observedInsideHandler = boss.CurrentExpectedCharacterID;
            boss.OnDrawnThisPhaseChanged += handler;
            try
            {
                string matched = boss.CurrentExpectedCharacterID;
                Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw(matched));

                Assert.IsNotNull(observedInsideHandler,
                    "OnDrawnThisPhaseChanged must fire after a Hit.");
                Assert.AreEqual(boss.CurrentExpectedCharacterID, observedInsideHandler,
                    "Subscribers reading CurrentExpectedCharacter inside the event must "
                    + "see the newly sampled glyph, not the one that was just matched. "
                    + "If this fails the boss glyph UI will desync from the expected char.");
            }
            finally
            {
                boss.OnDrawnThisPhaseChanged -= handler;
            }
        }

        // ---- Test 5 — spec §11 ----
        [UnityTest]
        public IEnumerator SummoningPhase_TicksFireAtSummonIntervalCadence()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossPhase phase = CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f, summonPhaseDuration: 2f);
            phase.delayBetweenSummons = 0.5f;
            phase.minionsPerSummonMin = 1;
            phase.minionsPerSummonMax = 1;
            EnemyDataSO summonData = ScriptableObject.CreateInstance<EnemyDataSO>();
            summonData.enemyID = "summon";
            summonData.maxHealth = 1;
            _objectsToDestroy.Add(summonData);
            phase.summonEnemyTypes = new List<EnemyDataSO> { summonData };

            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { phase });

            (BossController boss, FakeWaveSpawner spawner) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            float elapsed = 0f;
            while (elapsed < 2.3f) { yield return null; elapsed += Time.deltaTime; }

            Assert.GreaterOrEqual(spawner.SpawnEnemyCallCount, 3,
                "Expected at least 3 summon ticks in ~2s at 0.5s cadence.");
            Assert.LessOrEqual(spawner.SpawnEnemyCallCount, 5,
                "Expected at most 5 summon ticks; cadence drift should not exceed one tick.");
        }

        // ---- Test 6 — spec §11 ----
        [UnityTest]
        public IEnumerator Damaged_HPReachesZero_TransitionsToOutroThenDefeated()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0.1f, phases:
                new List<BossPhase> { CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return WaitUntilTargetable(boss, timeout: 2f);

            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));

            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }

            Assert.AreEqual(1, _onBossDefeatedCount);
            Assert.AreEqual(1, _onLevelCompleteCount);
        }

        // ---- Test 7 — spec §11 ----
        [UnityTest]
        public IEnumerator IsTargetable_FalseDuringIntroAndSummoningPhase()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossPhase phase = CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f, summonPhaseDuration: 0.5f);
            BossConfigSO config = CreateConfig(introDuration: 0.3f, outroDuration: 0f, phases:
                new List<BossPhase> { phase });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            yield return null;
            Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during Intro.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"));

            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }
            Assert.IsFalse(boss.IsTargetable, "IsTargetable must be false during SummoningPhase.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"));
        }

        // ---- Test 8 — spec §11 ----
        [UnityTest]
        public IEnumerator BossPhase_MovementPatternIsPerPhase()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossPhase phase0 = CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f,
                movement: BossMovementPattern.Pace);
            BossPhase phase1 = CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f,
                movement: BossMovementPattern.Teleport);
            BossConfigSO config = CreateConfig(introDuration: 0f, outroDuration: 0f, phases:
                new List<BossPhase> { phase0, phase1 });

            (BossController boss, _) = CreateBossWithFakeSpawner();

            PhaseBasedMovement existing = boss.GetComponent<PhaseBasedMovement>();
            if (existing != null) Object.DestroyImmediate(existing);
            SpyPhaseBasedMovement spy = boss.gameObject.AddComponent<SpyPhaseBasedMovement>();

            boss.StartBoss(config, GetFakeSpawner());

            yield return WaitUntilTargetable(boss, timeout: 2f);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));

            float elapsed = 0f;
            while (elapsed < 2f && spy.StartedPatterns.Count < 2)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.GreaterOrEqual(spy.StartedPatterns.Count, 2,
                "PhaseBasedMovement.StartPattern must be called at least once per phase.");
            Assert.AreEqual(BossMovementPattern.Pace, spy.StartedPatterns[0],
                "Phase 0 should start with the Pace pattern.");
            Assert.AreEqual(BossMovementPattern.Teleport, spy.StartedPatterns[1],
                "Phase 1 should start with the Teleport pattern.");
        }

        // ---- Helper polling for Vulnerable active window ----
        private IEnumerator WaitUntilTargetable(BossController boss, float timeout)
        {
            float t = 0f;
            while (t < timeout && !boss.IsTargetable)
            {
                yield return null;
                t += Time.deltaTime;
            }
            if (!boss.IsTargetable)
                throw new AssertionException($"Boss did not become IsTargetable within {timeout}s.");
        }

        // ---- Helpers ----

        private BaybayinCharacterSO CreateChar(string id)
        {
            BaybayinCharacterSO so = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            so.characterID = id;
            _objectsToDestroy.Add(so);
            return so;
        }

        private BossPhase CreatePhase(
            int requiredCount,
            float vulnerabilityTimer = 100f,
            float summonPhaseDuration = 0f,
            BossMovementPattern movement = BossMovementPattern.Hover)
        {
            return new BossPhase
            {
                summonPhaseDuration = summonPhaseDuration,
                delayBetweenSummons = 1f,
                minionsPerSummonMin = 1,
                minionsPerSummonMax = 1,
                summonEnemyTypes = new List<EnemyDataSO>(),
                summonSpawnRange = Vector2.zero,
                requiredCharacterCount = requiredCount,
                vulnerabilityTimer = vulnerabilityTimer,
                movementPattern = movement,
                movementSpeed = 0f,
                paceHalfRange = 0f,
                teleportHalfRange = Vector2.zero,
            };
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

        private LevelConfigSO CreateLevelConfig(params BaybayinCharacterSO[] allowed)
        {
            LevelConfigSO lc = ScriptableObject.CreateInstance<LevelConfigSO>();
            lc.allowedCharacters = new List<BaybayinCharacterSO>(allowed);
            _objectsToDestroy.Add(lc);
            _testLevelConfig = lc;

            // Wire GameManager.CurrentLevel so BossController.SampleNextExpectedCharacter resolves.
            if (GameManager.Instance != null)
                GameManager.Instance.SetLevel(lc);

            return lc;
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
            // Walk the type hierarchy: private fields declared on a base type
            // are not returned by GetField on a derived type, even with
            // BindingFlags.NonPublic. Required so e.g. setting Enemy._showDebugLabels
            // on a BossEnemy instance resolves the inherited field.
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
            public EnemyDataSO LastSpawnedEnemyData;
            public Vector3 LastSpawnedEnemyPosition;

            public override Enemy SpawnEnemy(EnemyDataSO data)
            {
                SpawnEnemyCallCount++;
                LastSpawnedEnemyData = data;
                return null;
            }
        }

        private class SpyPhaseBasedMovement : PhaseBasedMovement
        {
            public readonly List<BossMovementPattern> StartedPatterns = new();

            public override void StartPattern(BossPhase phase)
            {
                if (phase != null) StartedPatterns.Add(phase.movementPattern);
            }

            public override void StopPattern() { }
            public override void TeleportNow(BossPhase phase) { }
        }
    }
}
