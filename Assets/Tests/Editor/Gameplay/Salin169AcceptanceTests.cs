using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// Acceptance tests for TW-SPK-003 / SALIN-169 — Validate the three
    /// Paglimot mastery encounters. These tests verify the acceptance examples
    /// in docs/backlog/technical-work.md §4 against the existing BossController
    /// state machine. They confirm the framework supports the three-phase
    /// encounter structure for Levels 5, 10, and 15 without code changes.
    /// </summary>
    [TestFixture]
    public class Salin169AcceptanceTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        // Event counters
        private int _onBossStartedCount;
        private int _onBossDefeatedCount;
        private int _onLevelCompleteCount;
        private int _onBossDamagedCount;
        private int _onBossVulnerabilityExpiredCount;
        private int _onDrawingFailedCount;
        private int _lastDamagedHpRemaining = -1;

        // Named delegates for clean subscribe/unsubscribe
        private System.Action<BossConfigSO> _onBossStarted;
        private System.Action _onBossDefeated;
        private System.Action _onLevelComplete;
        private System.Action<int, int> _onBossDamaged;
        private System.Action<int> _onBossVulnerabilityExpired;
        private System.Action _onDrawingFailed;

        [SetUp]
        public void SetUp()
        {
            ResetCounters();

            _onBossStarted = _ => _onBossStartedCount++;
            _onBossDefeated = () => _onBossDefeatedCount++;
            _onLevelComplete = () => _onLevelCompleteCount++;
            _onBossDamaged = (phase, hp) => { _onBossDamagedCount++; _lastDamagedHpRemaining = hp; };
            _onBossVulnerabilityExpired = _ => _onBossVulnerabilityExpiredCount++;
            _onDrawingFailed = () => _onDrawingFailedCount++;

            EventBus.OnBossStarted += _onBossStarted;
            EventBus.OnBossDefeated += _onBossDefeated;
            EventBus.OnLevelComplete += _onLevelComplete;
            EventBus.OnBossDamaged += _onBossDamaged;
            EventBus.OnBossVulnerabilityExpired += _onBossVulnerabilityExpired;
            EventBus.OnDrawingFailed += _onDrawingFailed;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnBossStarted -= _onBossStarted;
            EventBus.OnBossDefeated -= _onBossDefeated;
            EventBus.OnLevelComplete -= _onLevelComplete;
            EventBus.OnBossDamaged -= _onBossDamaged;
            EventBus.OnBossVulnerabilityExpired -= _onBossVulnerabilityExpired;
            EventBus.OnDrawingFailed -= _onDrawingFailed;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            _objectsToDestroy.Clear();

            if (GameManager.Instance != null)
                GameManager.Instance.SetCurrentBoss(null);
        }

        private void ResetCounters()
        {
            _onBossStartedCount = 0;
            _onBossDefeatedCount = 0;
            _onLevelCompleteCount = 0;
            _onBossDamagedCount = 0;
            _onBossVulnerabilityExpiredCount = 0;
            _onDrawingFailedCount = 0;
            _lastDamagedHpRemaining = -1;
        }

        // ---- §4.1 Phase entry ----
        [UnityTest]
        public IEnumerator PhaseEntry_BossStarts_SetsCurrentBossAndRaisesBossStarted()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0f,
                phases: new List<BossPhase> { CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();

            Assert.IsNull(GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null,
                "CurrentBoss must be null before StartBoss.");

            boss.StartBoss(config, GetFakeSpawner());

            Assert.AreEqual(1, _onBossStartedCount,
                "OnBossStarted must be raised exactly once on StartBoss.");
            Assert.IsNotNull(GameManager.Instance != null ? GameManager.Instance.CurrentBoss : null,
                "GameManager.CurrentBoss must be set after StartBoss.");
            Assert.AreEqual(config.phases.Count, boss.HPRemaining,
                "HPRemaining must equal phases.Count after StartBoss.");

            yield return null;
        }

        // ---- §4.2 Phase failure (vulnerability timer expiry) ----
        [UnityTest]
        public IEnumerator PhaseFailure_TimerExpires_NoHPLossRepeatsSamePhase()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            // 2-phase boss: phase 0 will fail, HP must stay at 2.
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0f,
                phases: new List<BossPhase>
                {
                    CreatePhase(requiredCount: 3, vulnerabilityTimer: 0.3f),
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f)
                });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            yield return WaitUntilTargetable(boss, timeout: 2f);

            int phaseAtVulnerable = boss.CurrentPhaseIndex;
            int hpAtVulnerable = boss.HPRemaining;

            // Wait for the vulnerability timer to expire (0.3s + margin).
            float elapsed = 0f;
            while (elapsed < 0.6f) { yield return null; elapsed += Time.deltaTime; }

            Assert.AreEqual(1, _onBossVulnerabilityExpiredCount,
                "OnBossVulnerabilityExpired must be raised exactly once on timer expiry.");
            Assert.AreEqual(0, _onBossDamagedCount,
                "No damage must be applied on timer expiry.");
            Assert.AreEqual(phaseAtVulnerable, boss.CurrentPhaseIndex,
                "Phase index must not advance on failure — same phase repeats.");
            Assert.AreEqual(hpAtVulnerable, boss.HPRemaining,
                "HP must not change on failure.");
        }

        // ---- §4.3 Phase retry (after failure) ----
        [UnityTest]
        public IEnumerator PhaseRetry_AfterFailure_SamePhaseSameRequirements()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0f,
                phases: new List<BossPhase>
                {
                    CreatePhase(requiredCount: 2, vulnerabilityTimer: 0.3f),
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f)
                });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            // First attempt: let it fail.
            yield return WaitUntilTargetable(boss, timeout: 2f);
            int phaseBeforeFailure = boss.CurrentPhaseIndex;
            int requiredBeforeFailure = boss.RequiredCharactersForCurrentPhase;

            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }
            Assert.AreEqual(1, _onBossVulnerabilityExpiredCount,
                "First attempt must have failed.");

            // Second attempt: boss re-enters vulnerable for the same phase.
            yield return WaitUntilTargetable(boss, timeout: 5f);

            Assert.AreEqual(phaseBeforeFailure, boss.CurrentPhaseIndex,
                "Retry must be on the same phase index.");
            Assert.AreEqual(requiredBeforeFailure, boss.RequiredCharactersForCurrentPhase,
                "Retry must have the same requiredCharacterCount.");

            // Clear the phase on retry to confirm it's winnable.
            int required = boss.RequiredCharactersForCurrentPhase;
            for (int i = 0; i < required; i++)
            {
                Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"),
                    $"Correct draw #{i + 1} on retry must be a Hit.");
            }

            yield return null;
            yield return null;
            Assert.AreEqual(1, _onBossDamagedCount,
                "Phase must be cleared on retry — boss takes damage.");
        }

        // ---- §4.6 Completion (all phases cleared) ----
        [UnityTest]
        public IEnumerator Completion_AllPhasesCleared_RaisesBossDefeatedAndLevelComplete()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0.1f,
                phases: new List<BossPhase>
                {
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f),
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f),
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f)
                });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            // Phase 1
            yield return WaitUntilTargetable(boss, timeout: 2f);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            yield return null; yield return null;
            Assert.AreEqual(1, _onBossDamagedCount, "Phase 1 must be cleared.");

            // Phase 2
            yield return WaitUntilTargetable(boss, timeout: 5f);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            yield return null; yield return null;
            Assert.AreEqual(2, _onBossDamagedCount, "Phase 2 must be cleared.");

            // Phase 3 (final)
            yield return WaitUntilTargetable(boss, timeout: 5f);
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            yield return null; yield return null;
            Assert.AreEqual(3, _onBossDamagedCount, "Phase 3 must be cleared.");
            Assert.AreEqual(0, _lastDamagedHpRemaining,
                "HP must be 0 after the final phase is cleared.");

            // Wait for outro to complete.
            float outroElapsed = 0f;
            while (outroElapsed < 0.5f && _onBossDefeatedCount == 0)
            {
                yield return null;
                outroElapsed += Time.deltaTime;
            }

            Assert.AreEqual(1, _onBossDefeatedCount,
                "OnBossDefeated must be raised after all phases are cleared.");
            Assert.AreEqual(1, _onLevelCompleteCount,
                "OnLevelComplete must be raised after all phases are cleared.");
            Assert.IsTrue(boss.IsDefeated,
                "BossController.IsDefeated must be true after outro.");
        }

        // ---- Wrong glyph during vulnerable ----
        [UnityTest]
        public IEnumerator WrongGlyph_DuringVulnerable_RaisesDrawingFailedNoDamage()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            BaybayinCharacterSO ka = CreateChar("KA");
            CreateLevelConfig(ba); // Pool contains only BA → KA is always wrong.
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0f,
                phases: new List<BossPhase> { CreatePhase(requiredCount: 2, vulnerabilityTimer: 100f) });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());
            yield return WaitUntilTargetable(boss, timeout: 2f);

            string expectedBefore = boss.CurrentExpectedCharacterID;
            int failedBefore = _onDrawingFailedCount;

            BossRouteResult result = boss.TryRouteDraw("KA");

            Assert.AreEqual(BossRouteResult.WrongGlyph, result,
                "Wrong glyph must return WrongGlyph.");
            Assert.AreEqual(0, _onBossDamagedCount,
                "Wrong glyph must not damage the boss.");
            Assert.AreEqual(expectedBefore, boss.CurrentExpectedCharacterID,
                "Wrong glyph must not advance the expected character.");
            Assert.AreEqual(failedBefore + 1, _onDrawingFailedCount,
                "Wrong glyph must raise OnDrawingFailed exactly once.");
        }

        // ---- Three-phase encounter end-to-end (validates the 3-phase structure) ----
        [UnityTest]
        public IEnumerator ThreePhaseEncounter_AllPhasesCleared_BossDefeated()
        {
            // Simulates the Paglimot encounter structure: 3 phases, each = 1 HP.
            // Validates that the BossController state machine handles the
            // 3-phase loop correctly — the same structure used by all three
            // Paglimot encounters (Levels 5, 10, 15).
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0.1f,
                phases: new List<BossPhase>
                {
                    CreatePhase(requiredCount: 3, vulnerabilityTimer: 100f),  // Phase 1
                    CreatePhase(requiredCount: 3, vulnerabilityTimer: 100f),  // Phase 2
                    CreatePhase(requiredCount: 4, vulnerabilityTimer: 100f),  // Phase 3
                });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            Assert.AreEqual(3, boss.HPRemaining,
                "3-phase boss must start with HP = 3.");

            // Clear all 3 phases (3 + 3 + 4 = 10 draws total, matching Level 5).
            int[] drawsPerPhase = { 3, 3, 4 };
            for (int phase = 0; phase < 3; phase++)
            {
                yield return WaitUntilTargetable(boss, timeout: 5f);
                Assert.AreEqual(phase, boss.CurrentPhaseIndex,
                    $"Must be on phase {phase} before clearing it.");

                for (int d = 0; d < drawsPerPhase[phase]; d++)
                {
                    Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"),
                        $"Phase {phase + 1} draw #{d + 1} must be a Hit.");
                }

                yield return null; yield return null;
                Assert.AreEqual(phase + 1, _onBossDamagedCount,
                    $"Phase {phase + 1} must register exactly {phase + 1} damage events.");
                Assert.AreEqual(3 - (phase + 1), boss.HPRemaining,
                    $"HP must be {3 - (phase + 1)} after clearing phase {phase + 1}.");
            }

            // Wait for outro → defeated.
            float elapsed = 0f;
            while (elapsed < 0.5f && _onBossDefeatedCount == 0)
            {
                yield return null;
                elapsed += Time.deltaTime;
            }

            Assert.AreEqual(1, _onBossDefeatedCount,
                "OnBossDefeated must fire after all 3 phases are cleared.");
            Assert.AreEqual(1, _onLevelCompleteCount,
                "OnLevelComplete must fire after all 3 phases are cleared.");
            Assert.IsTrue(boss.IsDefeated,
                "Boss must be defeated after clearing all 3 phases.");
        }

        // ---- §4.1 IsTargetable false during Intro and SummoningPhase ----
        [UnityTest]
        public IEnumerator PhaseEntry_IsTargetableFalseDuringIntroAndSummoning()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossPhase phase = CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f,
                summonPhaseDuration: 0.5f);
            BossConfigSO config = CreateConfig(
                introDuration: 0.3f, outroDuration: 0f,
                phases: new List<BossPhase> { phase });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            yield return null;
            Assert.IsFalse(boss.IsTargetable,
                "IsTargetable must be false during Intro.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"),
                "Draws during Intro must not be routed to the boss.");

            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }
            Assert.IsFalse(boss.IsTargetable,
                "IsTargetable must be false during SummoningPhase.");
            Assert.AreEqual(BossRouteResult.NotRouted, boss.TryRouteDraw("BA"),
                "Draws during SummoningPhase must not be routed to the boss.");
        }

        // ---- §4.2/4.3 Retry resets correct-draw count ----
        [UnityTest]
        public IEnumerator PhaseFailure_CorrectDrawCountResetsOnRetry()
        {
            BaybayinCharacterSO ba = CreateChar("BA");
            CreateLevelConfig(ba);
            BossConfigSO config = CreateConfig(
                introDuration: 0f, outroDuration: 0f,
                phases: new List<BossPhase>
                {
                    CreatePhase(requiredCount: 3, vulnerabilityTimer: 0.3f),
                    CreatePhase(requiredCount: 1, vulnerabilityTimer: 100f)
                });

            (BossController boss, _) = CreateBossWithFakeSpawner();
            boss.StartBoss(config, GetFakeSpawner());

            // Enter vulnerable, draw 1 correct glyph, then let timer expire.
            yield return WaitUntilTargetable(boss, timeout: 2f);
            Assert.AreEqual(0, boss.CorrectDrawsThisWindow,
                "CorrectDrawsThisWindow must start at 0.");
            Assert.AreEqual(BossRouteResult.Hit, boss.TryRouteDraw("BA"));
            Assert.AreEqual(1, boss.CorrectDrawsThisWindow,
                "CorrectDrawsThisWindow must be 1 after one correct draw.");

            // Wait for timer expiry.
            float elapsed = 0f;
            while (elapsed < 0.5f) { yield return null; elapsed += Time.deltaTime; }
            Assert.AreEqual(1, _onBossVulnerabilityExpiredCount,
                "Vulnerability must have expired.");

            // Re-enter vulnerable — correct draws must reset.
            yield return WaitUntilTargetable(boss, timeout: 5f);
            Assert.AreEqual(0, boss.CorrectDrawsThisWindow,
                "CorrectDrawsThisWindow must reset to 0 on retry.");
        }

        // ====================================================================
        // Helpers (mirrors BossControllerTests.cs patterns)
        // ====================================================================

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
            config.bossName = "SALIN169TestBoss";
            config.bossID = "SALIN169_TEST";
            config.introDuration = introDuration;
            config.outroDuration = outroDuration;
            config.phases = phases;

            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "salin169_test_boss";
            data.maxHealth = 1;
            data.moveSpeed = 0f;
            data.deathFrames = new Sprite[0]; // No death frames → PlayDeathAnimationFrames yields immediately.
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

            // Wire GameManager.CurrentLevel so BossController.SampleNextExpectedCharacter resolves.
            if (GameManager.Instance != null)
                GameManager.Instance.SetLevel(lc);

            return lc;
        }

        private FakeWaveSpawner _fakeSpawner;
        private GameObject _spawnerGO;

        private (BossController, FakeWaveSpawner) CreateBossWithFakeSpawner()
        {
            GameObject bossGO = new GameObject("Boss_SALIN169_Test");
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

            _spawnerGO = new GameObject("FakeWaveSpawner_SALIN169");
            _fakeSpawner = _spawnerGO.AddComponent<FakeWaveSpawner>();
            _objectsToDestroy.Add(_spawnerGO);

            return (controller, _fakeSpawner);
        }

        private WaveSpawner GetFakeSpawner() => _fakeSpawner;

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
                return null;
            }
        }
    }
}
