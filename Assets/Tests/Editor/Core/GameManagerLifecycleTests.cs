using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Core
{
    [TestFixture]
    public class GameManagerLifecycleTests
    {
        private GameObject _gameObject;
        private Action _pauseHandler;
        private Action _resumeHandler;
        private Action _abortHandler;

        [SetUp]
        public void SetUp()
        {
            ClearSingletonInstance<GameManager>();
            PlayerPrefs.DeleteKey("salinlahi.test.committed_progress");
            Time.timeScale = 1f;
            _gameObject = new GameObject("GameManager_Lifecycle_Test");
            _gameObject.AddComponent<GameManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_pauseHandler != null) EventBus.OnGamePaused -= _pauseHandler;
            if (_resumeHandler != null) EventBus.OnGameResumed -= _resumeHandler;
            if (_abortHandler != null) EventBus.OnLevelAttemptAborted -= _abortHandler;

            if (_gameObject != null)
                UnityEngine.Object.DestroyImmediate(_gameObject);

            ClearSingletonInstance<GameManager>();
            Time.timeScale = 1f;
        }

        [Test]
        public void PauseAndResumeAreIdempotent()
        {
            int pauseCount = 0;
            int resumeCount = 0;
            _pauseHandler = () => pauseCount++;
            _resumeHandler = () => resumeCount++;
            EventBus.OnGamePaused += _pauseHandler;
            EventBus.OnGameResumed += _resumeHandler;

            GameManager.Instance.StartGame();
            GameManager.Instance.PauseGame();
            GameManager.Instance.PauseGame();
            GameManager.Instance.ResumeGame();
            GameManager.Instance.ResumeGame();

            Assert.AreEqual(GameState.Playing, GameManager.Instance.CurrentState);
            Assert.AreEqual(1, pauseCount);
            Assert.AreEqual(1, resumeCount);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void AbortClearsPausedSnapshotAndLeavesCommittedPrefsUntouched()
        {
            const string committedKey = "salinlahi.test.committed_progress";
            PlayerPrefs.SetInt(committedKey, 7);

            int abortCount = 0;
            _abortHandler = () => abortCount++;
            EventBus.OnLevelAttemptAborted += _abortHandler;

            GameManager.Instance.StartGame();
            GameManager.Instance.CachePausedRunSnapshot(1, 2, 0, 1);
            Assert.IsTrue(GameManager.Instance.TryGetPausedRunLevelId(out _));

            GameManager.Instance.AbortCurrentLevelAttempt();
            GameManager.Instance.AbortCurrentLevelAttempt();

            Assert.AreEqual(GameState.Idle, GameManager.Instance.CurrentState);
            Assert.IsFalse(GameManager.Instance.TryGetPausedRunLevelId(out _));
            Assert.IsTrue(GameManager.Instance.IsAttemptAbortInProgress);
            Assert.AreEqual(1, abortCount);
            Assert.AreEqual(7, PlayerPrefs.GetInt(committedKey));

            PlayerPrefs.DeleteKey(committedKey);
        }

        [Test]
        public void StartingNextAttemptClearsAbortGate()
        {
            GameManager.Instance.StartGame();
            GameManager.Instance.AbortCurrentLevelAttempt();
            Assert.IsTrue(GameManager.Instance.IsAttemptAbortInProgress);

            GameManager.Instance.StartGame();

            Assert.IsFalse(GameManager.Instance.IsAttemptAbortInProgress);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.CurrentState);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            setter?.Invoke(null, new object[] { null });
        }
    }
}
