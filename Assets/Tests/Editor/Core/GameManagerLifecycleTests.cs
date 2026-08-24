using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Core
{
    /// <summary>
    /// SALIN-141. EditMode coverage for the two-source pause split and the level-attempt
    /// abort transaction on <see cref="GameManager"/>.
    ///
    /// Everything here drives GameManager through direct method calls. EditMode never
    /// runs Awake/OnEnable on a runtime-created GameObject, so the EventBus handlers are
    /// not subscribed in this fixture and are invoked through reflection where they are
    /// under test. The end-to-end event routing lives in the PlayMode fixtures.
    /// </summary>
    [TestFixture]
    public sealed class GameManagerLifecycleTests
    {
        private GameObject _host;
        private GameManager _gameManager;
        private int _abortRaiseCount;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("GameManager_LifecycleTest");
            _gameManager = _host.AddComponent<GameManager>();
            _abortRaiseCount = 0;
            EventBus.OnLevelAttemptAborted += CountAbort;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnLevelAttemptAborted -= CountAbort;
            if (_host != null)
                Object.DestroyImmediate(_host);
            Time.timeScale = 1f;
        }

        private void CountAbort() => _abortRaiseCount++;

        // ------------------------------------------------------------------
        // AC-1 / AC-2 — the two pause sources are independent
        // ------------------------------------------------------------------

        [Test]
        public void PauseGame_FromPlaying_StopsTheClockAndClosesDrawingInput()
        {
            _gameManager.StartGame();

            _gameManager.PauseGame();

            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState);
            Assert.AreEqual(0f, Time.timeScale, "A user pause must stop gameplay time.");
            Assert.IsFalse(_gameManager.AcceptsDrawingInput,
                "Drawing input must close together with the rest of gameplay.");
        }

        [Test]
        public void PauseGame_DuringADialoguePause_IsRejected()
        {
            _gameManager.StartGame();
            _gameManager.EnterDialoguePause();

            _gameManager.PauseGame();
            // The dialogue still owns the pause, so its own exit must still work.
            _gameManager.ExitDialoguePause();

            Assert.AreEqual(GameState.Playing, _gameManager.CurrentState,
                "A rejected user pause must not leave a latch that survives the dialogue.");
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void ResumeGame_DuringADialoguePause_DoesNotRestartGameplay()
        {
            _gameManager.StartGame();
            _gameManager.EnterDialoguePause();

            _gameManager.ResumeGame();

            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState,
                "Resume must never lift a dialogue-driven pause.");
            Assert.AreEqual(0f, Time.timeScale,
                "Resuming under an open dialogue box would restart combat behind it.");
        }

        [Test]
        public void ExitDialoguePause_DuringAUserPause_DoesNotLiftIt()
        {
            _gameManager.StartGame();
            _gameManager.PauseGame();

            _gameManager.ExitDialoguePause();

            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState,
                "A beat unwinding must not lift the player's own pause.");
            Assert.AreEqual(0f, Time.timeScale);
        }

        [Test]
        public void ResumeGame_AfterAUserPause_ContinuesTheSameAttemptExactlyOnce()
        {
            int resumeRaises = 0;
            void CountResume() => resumeRaises++;
            EventBus.OnGameResumed += CountResume;
            try
            {
                _gameManager.StartGame();
                _gameManager.PauseGame();

                _gameManager.ResumeGame();
                _gameManager.ResumeGame(); // Double-tap.

                Assert.AreEqual(GameState.Playing, _gameManager.CurrentState);
                Assert.AreEqual(1f, Time.timeScale);
                Assert.AreEqual(1, resumeRaises,
                    "A second Resume must be inert: subscribers re-arm on every raise.");
            }
            finally
            {
                EventBus.OnGameResumed -= CountResume;
            }
        }

        [Test]
        public void StartGame_AfterATeachingBeatPause_ClearsBothPauseLatches()
        {
            _gameManager.StartGame();
            _gameManager.EnterDialoguePause();

            // Onboarding beats end their pause with StartGame rather than
            // ExitDialoguePause; a stranded latch here would block every later pause.
            _gameManager.StartGame();
            _gameManager.PauseGame();

            Assert.AreEqual(GameState.Paused, _gameManager.CurrentState);
            Assert.AreEqual(0f, Time.timeScale);
        }

        // ------------------------------------------------------------------
        // AC-3 / AC-4 — the abort transaction
        // ------------------------------------------------------------------

        [Test]
        public void AbortCurrentLevelAttempt_RestoresTheClockAndRaisesOnceOnly()
        {
            _gameManager.StartGame();
            _gameManager.PauseGame();

            _gameManager.AbortCurrentLevelAttempt();
            _gameManager.AbortCurrentLevelAttempt(); // Double-tap on Confirm.

            Assert.IsTrue(_gameManager.IsAttemptAbortInProgress);
            Assert.AreEqual(GameState.Idle, _gameManager.CurrentState);
            Assert.AreEqual(1f, Time.timeScale, "The abort must hand back an unpaused clock.");
            Assert.AreEqual(1, _abortRaiseCount,
                "Subscribers tear down per raise; a second raise would double the teardown.");
        }

        [Test]
        public void AbortCurrentLevelAttempt_ReleasesDrawingSuppression()
        {
            _gameManager.StartGame();
            _gameManager.SuppressDrawingInput(true);

            _gameManager.AbortCurrentLevelAttempt();
            _gameManager.StartGame();

            Assert.IsTrue(_gameManager.AcceptsDrawingInput,
                "A suppression held when the attempt was abandoned must not survive into the next one.");
        }

        [Test]
        public void AbortCurrentLevelAttempt_LeavesThePausedRunSnapshotToTheCaller()
        {
            _gameManager.StartGame();
            _gameManager.CachePausedRunSnapshot(levelId: 2, currentHearts: 2);

            _gameManager.AbortCurrentLevelAttempt();

            Assert.IsTrue(_gameManager.TryGetPausedRunLevelId(out int levelId),
                "Snapshot policy differs between Restart and Leave, so the abort must not decide it.");
            Assert.AreEqual(2, levelId);
        }

        [Test]
        public void GameOver_AfterAnAbort_IsIgnored()
        {
            _gameManager.StartGame();
            _gameManager.AbortCurrentLevelAttempt();

            InvokePrivate(_gameManager, "HandleGameOver");

            Assert.AreEqual(GameState.Idle, _gameManager.CurrentState,
                "A straggler defeat must not reopen an attempt that is being discarded.");
        }

        [Test]
        public void LevelComplete_AfterAnAbort_IsIgnored()
        {
            _gameManager.StartGame();
            _gameManager.AbortCurrentLevelAttempt();

            InvokePrivate(_gameManager, "HandleLevelComplete");

            Assert.AreEqual(GameState.Idle, _gameManager.CurrentState,
                "An aborted attempt can never complete.");
        }

        [Test]
        public void StartGame_ClearsTheAbortLatchSoTheNextAttemptCanFinish()
        {
            _gameManager.StartGame();
            _gameManager.AbortCurrentLevelAttempt();

            _gameManager.StartGame();

            Assert.IsFalse(_gameManager.IsAttemptAbortInProgress);

            InvokePrivate(_gameManager, "HandleLevelComplete");

            Assert.AreEqual(GameState.LevelComplete, _gameManager.CurrentState,
                "A stuck abort latch would silently swallow the next attempt's completion.");
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, System.Array.Empty<object>());
        }
    }
}
