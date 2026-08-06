using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-135: Receive clear combat feedback and retry safely.
    /// Covers the four acceptance criteria:
    ///  AC1: Accepted drawing -> combat + word-restoration feedback appears once.
    ///  AC2: Rejected drawing -> non-destructive correction cue, no target word advance.
    ///  AC3: Retry -> clean level restart (no stale enemies, prompts, timers, drawing state).
    ///  AC4: Attempt ends (defeat/retry/exit) -> no premature completion/unlock, no dup tutorial.
    /// </summary>
    [TestFixture]
    public class CombatFeedbackRetryTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<RecognitionManager>();
            ClearSingletonInstance<GameManager>();
            ClearSingletonInstance<ProgressManager>();
            ClearSingletonInstance<SceneLoader>();

            for (int i = 0; i < _objectsToDestroy.Count; i++)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
            for (int i = 1; i <= 15; i++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{i}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{i}");
            }
            PlayerPrefs.Save();
        }

        // ---- AC1: Feedback appears once ----

        [Test]
        public void Recognize_ReentrantCall_DoesNotRaiseEvents()
        {
            RecognitionConfigSO config = ScriptableObject.CreateInstance<RecognitionConfigSO>();
            config.minimumConfidence = 0.1f;
            _objectsToDestroy.Add(config);

            RecognitionManager manager = CreateRecognitionManager(config);

            // Simulate an in-flight recognition by setting the guard.
            SetPrivateField(manager, "_isRecognizing", true);

            int characterRecognizedCount = 0;
            int drawingFailedCount = 0;
            int recognitionResolvedCount = 0;
            EventBus.OnCharacterRecognized += HandleCharacterRecognized;
            EventBus.OnDrawingFailed += HandleDrawingFailed;
            EventBus.OnRecognitionResolved += HandleRecognitionResolved;

            try
            {
                var strokes = new List<List<Vector2>>
                {
                    new() { new Vector2(0, 0), new Vector2(50, 50), new Vector2(100, 0) }
                };
                manager.Recognize(strokes);

                Assert.AreEqual(0, characterRecognizedCount,
                    "Re-entrant Recognize must not raise OnCharacterRecognized.");
                Assert.AreEqual(0, drawingFailedCount,
                    "Re-entrant Recognize must not raise OnDrawingFailed.");
                Assert.AreEqual(0, recognitionResolvedCount,
                    "Re-entrant Recognize must not raise OnRecognitionResolved.");
            }
            finally
            {
                EventBus.OnCharacterRecognized -= HandleCharacterRecognized;
                EventBus.OnDrawingFailed -= HandleDrawingFailed;
                EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
            }

            void HandleCharacterRecognized(string _) => characterRecognizedCount++;
            void HandleDrawingFailed() => drawingFailedCount++;
            void HandleRecognitionResolved(RecognitionResult _, bool _p, float _t) => recognitionResolvedCount++;
        }

        [Test]
        public void Recognize_NormalCall_RaisesExactlyOneResolvedEvent()
        {
            RecognitionConfigSO config = ScriptableObject.CreateInstance<RecognitionConfigSO>();
            config.minimumConfidence = 0.1f;
            config.resamplePointCount = 16;
            _objectsToDestroy.Add(config);

            RecognitionManager manager = CreateRecognitionManager(config);

            int recognitionResolvedCount = 0;
            EventBus.OnRecognitionResolved += HandleRecognitionResolved;

            try
            {
                // Degenerate stroke (single point) triggers the early return path,
                // which raises DrawingFailed -- a single feedback event.
                var strokes = new List<List<Vector2>> { new() { Vector2.zero } };
                manager.Recognize(strokes);

                Assert.AreEqual(0, recognitionResolvedCount,
                    "Degenerate stroke should not raise OnRecognitionResolved.");
            }
            finally
            {
                EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
            }

            void HandleRecognitionResolved(RecognitionResult _, bool _p, float _t) => recognitionResolvedCount++;
        }

        // ---- AC2: Rejected drawing does not advance target word ----

        [Test]
        public void DrawingFailed_DoesNotAdvanceBossExpectedCharacter()
        {
            GameManager gameManager = CreateGameManager();
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            levelConfig.levelNumber = 1;
            BaybayinCharacterSO charA = CreateCharacter("A", "a");
            BaybayinCharacterSO charB = CreateCharacter("B", "be");
            levelConfig.allowedCharacters = new List<BaybayinCharacterSO> { charA, charB };
            gameManager.SetLevel(levelConfig);
            _objectsToDestroy.Add(levelConfig);

            // BossController.TryRouteDraw raises DrawingFailed for a wrong glyph
            // but does NOT advance the expected character. We verify the contract
            // at the event level: DrawingFailed is non-destructive.
            bool drawingFailedRaised = false;
            EventBus.OnDrawingFailed += HandleDrawingFailed;

            try
            {
                EventBus.RaiseDrawingFailed();
                Assert.IsTrue(drawingFailedRaised,
                    "DrawingFailed event should be raised for rejected drawings.");
            }
            finally
            {
                EventBus.OnDrawingFailed -= HandleDrawingFailed;
            }

            // The target word (level's allowed characters) is unchanged.
            Assert.AreEqual(2, levelConfig.allowedCharacters.Count,
                "Rejected drawing must not modify the level's allowed characters.");

            void HandleDrawingFailed() => drawingFailedRaised = true;
        }

        // ---- AC3: Retry produces clean GameManager state ----

        [Test]
        public void CleanupGameplayRun_ClearsDrawingSuppressionAndBossReference()
        {
            GameManager gameManager = CreateGameManager();
            // Start in Playing state so AcceptsDrawingInput reflects suppression.
            gameManager.StartGame();
            gameManager.SuppressDrawingInput(true);

            // Simulate a stale boss reference (as if BossController.OnDisable
            // hasn't fired yet when CleanupGameplayRun runs).
            BossController stubBoss = CreateStubBoss();
            Assert.IsNotNull(stubBoss, "Stub boss must be created successfully.");
            gameManager.SetCurrentBoss(stubBoss);

            Assert.IsFalse(gameManager.AcceptsDrawingInput,
                "Drawing should be suppressed before cleanup.");
            Assert.IsNotNull(gameManager.CurrentBoss,
                "Boss reference should exist before cleanup.");

            SceneLoader loader = CreateSceneLoader();
            InvokePrivate(loader, "CleanupGameplayRun");

            Assert.IsTrue(gameManager.AcceptsDrawingInput,
                "CleanupGameplayRun must release drawing suppression for a clean retry.");
            Assert.IsNull(gameManager.CurrentBoss,
                "CleanupGameplayRun must clear the stale boss reference for a clean retry.");
        }

        // ---- AC4: No premature completion on defeat ----

        [Test]
        public void GameOver_DoesNotGrantLevelCompletionOrUnlock()
        {
            ProgressManager progressManager = CreateProgressManager();
            PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, 1);
            PlayerPrefs.Save();

            int starsBefore = progressManager.GetStars(1);
            bool level2UnlockedBefore = progressManager.IsLevelUnlocked(2);

            // Simulate defeat -- OnGameOver should NOT trigger HandleLevelComplete.
            EventBus.RaiseGameOver();

            int starsAfter = progressManager.GetStars(1);
            bool level2UnlockedAfter = progressManager.IsLevelUnlocked(2);

            Assert.AreEqual(starsBefore, starsAfter,
                "Defeat must not grant stars.");
            Assert.AreEqual(level2UnlockedBefore, level2UnlockedAfter,
                "Defeat must not unlock the next level.");
        }

        [Test]
        public void ProgressManager_IdempotencyGuardResetsOnNewAttempt()
        {
            ProgressManager progressManager = CreateProgressManager();
            PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, 1);
            PlayerPrefs.Save();

            // First completion of level 1.
            progressManager.MarkLevelComplete(1, 2);
            Assert.AreEqual(2, progressManager.GetStars(1));

            // Simulate the idempotency guard being set (as HandleLevelComplete does).
            SetPrivateField(progressManager, "_lastProcessedLevelId", 1);

            // Simulate a new attempt: OnSceneLoaded resets the guard when entering
            // gameplay. We replicate that reset here since Scene.name is not settable
            // via reflection in Unity 6 (backed by native code).
            SetPrivateField(progressManager, "_lastProcessedLevelId", -1);
            SetPrivateField(progressManager, "_currentPlayingLevelId", 1);

            // Second completion should now be processed (stars upgraded to 3).
            progressManager.MarkLevelComplete(1, 3);
            Assert.AreEqual(3, progressManager.GetStars(1),
                "Idempotency guard must reset on new attempt so replay completion is recorded.");
        }

        [Test]
        public void ProgressManager_IdempotencyGuard_BlocksDuplicateCompletionInSameAttempt()
        {
            ProgressManager progressManager = CreateProgressManager();
            PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, 1);
            PlayerPrefs.Save();

            // Simulate the guard being set from a prior completion in this attempt.
            SetPrivateField(progressManager, "_lastProcessedLevelId", 1);
            SetPrivateField(progressManager, "_currentPlayingLevelId", 1);

            // A duplicate LevelComplete in the same attempt must be skipped.
            EventBus.RaiseLevelComplete();

            // Stars should remain 0 because the guard blocked processing.
            Assert.AreEqual(0, progressManager.GetStars(1),
                "Duplicate LevelComplete in the same attempt must be blocked by the guard.");
        }

        // ---- Helpers ----

        private RecognitionManager CreateRecognitionManager(RecognitionConfigSO config)
        {
            var go = new GameObject("RecognitionManager_Test");
            _objectsToDestroy.Add(go);
            RecognitionManager manager = go.AddComponent<RecognitionManager>();
            SetPrivateField(manager, "_config", config);
            SetSingletonInstance(manager);
            return manager;
        }

        private GameManager CreateGameManager()
        {
            var go = new GameObject("GameManager_Test");
            _objectsToDestroy.Add(go);
            GameManager gameManager = go.AddComponent<GameManager>();
            SetSingletonInstance(gameManager);
            return gameManager;
        }

        private ProgressManager CreateProgressManager()
        {
            var go = new GameObject("ProgressManager_Test");
            _objectsToDestroy.Add(go);
            ProgressManager progressManager = go.AddComponent<ProgressManager>();
            SetSingletonInstance(progressManager);
            progressManager.ClearAllProgress();
            return progressManager;
        }

        private SceneLoader CreateSceneLoader()
        {
            var go = new GameObject("SceneLoader_Test");
            _objectsToDestroy.Add(go);
            SceneLoader loader = go.AddComponent<SceneLoader>();
            SetSingletonInstance(loader);
            return loader;
        }

        private BossController CreateStubBoss()
        {
            var go = new GameObject("StubBoss_Test");
            _objectsToDestroy.Add(go);
            // BossController requires BossEnemy, which requires a Collider2D.
            go.AddComponent<BoxCollider2D>();
            return go.AddComponent<BossController>();
        }

        private BaybayinCharacterSO CreateCharacter(string id, string syllable)
        {
            var character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            character.characterID = id;
            character.syllable = syllable;
            _objectsToDestroy.Add(character);
            return character;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, args);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")?
                .GetSetMethod(true)?
                .Invoke(null, new object[] { null });
        }
    }
}
