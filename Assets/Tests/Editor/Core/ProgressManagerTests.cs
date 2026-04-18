using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Core
{
    [TestFixture]
    public class ProgressManagerTests
    {
        private GameObject _gameObject;
        private ProgressManager _manager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("ProgressManager_Test");
            _manager = _gameObject.AddComponent<ProgressManager>();
            _manager.ClearAllProgress();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
                Object.DestroyImmediate(_gameObject);
            PlayerPrefs.DeleteKey(ProgressManager.SelectedLevelKey);
            PlayerPrefs.DeleteKey(ProgressManager.EndlessModeKey);
            for (int i = 1; i <= 5; i++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{i}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{i}");
            }
            PlayerPrefs.Save();
        }

        [Test]
        public void IsLevelUnlocked_Level1_AlwaysReturnsTrue()
        {
            Assert.IsTrue(_manager.IsLevelUnlocked(1));
        }

        [Test]
        public void IsLevelUnlocked_Level2_NotUnlockedInitially()
        {
            Assert.IsFalse(_manager.IsLevelUnlocked(2));
        }

        [Test]
        public void MarkLevelComplete_UnlocksNextLevel()
        {
            _manager.MarkLevelComplete(1, 3);
            Assert.IsTrue(_manager.IsLevelUnlocked(2));
        }

        [Test]
        public void MarkLevelComplete_NeverDowngradesStars()
        {
            _manager.MarkLevelComplete(1, 3);
            Assert.AreEqual(3, _manager.GetStars(1));

            _manager.MarkLevelComplete(1, 1);
            Assert.AreEqual(3, _manager.GetStars(1), "Stars should not be downgraded.");
        }

        [Test]
        public void MarkLevelComplete_UpdatesStarsWhenHigher()
        {
            _manager.MarkLevelComplete(1, 1);
            Assert.AreEqual(1, _manager.GetStars(1));

            _manager.MarkLevelComplete(1, 3);
            Assert.AreEqual(3, _manager.GetStars(1));
        }

        [Test]
        public void GetStars_UncompletedLevel_ReturnsZero()
        {
            Assert.AreEqual(0, _manager.GetStars(2));
        }

        [Test]
        public void GetTotalStars_SumsAllLevels()
        {
            _manager.MarkLevelComplete(1, 3);
            _manager.MarkLevelComplete(2, 2);
            Assert.AreEqual(5, _manager.GetTotalStars());
        }

        [Test]
        public void GetTotalStars_NoCompletedLevels_ReturnsZero()
        {
            Assert.AreEqual(0, _manager.GetTotalStars());
        }

        [Test]
        public void ClearAllProgress_ResetsStarsAndUnlocks()
        {
            _manager.MarkLevelComplete(1, 3);
            _manager.MarkLevelComplete(2, 2);
            Assert.AreEqual(5, _manager.GetTotalStars());

            _manager.ClearAllProgress();
            Assert.AreEqual(0, _manager.GetStars(1));
            Assert.AreEqual(0, _manager.GetStars(2));
            Assert.IsTrue(_manager.IsLevelUnlocked(1));
            Assert.IsFalse(_manager.IsLevelUnlocked(2));
        }

        [Test]
        public void ClearAllProgress_DoesNotAffectOtherPlayerPrefs()
        {
            PlayerPrefs.SetString("unrelated_key", "test_value");
            PlayerPrefs.Save();

            _manager.ClearAllProgress();

            Assert.AreEqual("test_value", PlayerPrefs.GetString("unrelated_key", ""));
            PlayerPrefs.DeleteKey("unrelated_key");
        }

        [Test]
        public void IsEndlessModeUnlocked_FalseInitially()
        {
            Assert.IsFalse(_manager.IsEndlessModeUnlocked());
        }

        [Test]
        public void UnlockEndlessMode_SetsKey()
        {
            _manager.UnlockEndlessMode();
            Assert.IsTrue(_manager.IsEndlessModeUnlocked());
        }

        [Test]
        public void MarkLevelComplete_Level5_UnlocksEndlessMode()
        {
            for (int i = 1; i <= 5; i++)
                _manager.MarkLevelComplete(i, 3);

            Assert.IsTrue(_manager.IsEndlessModeUnlocked());
        }

        [Test]
        public void IsLevelUnlocked_InvalidLevel_ReturnsFalse()
        {
            Assert.IsFalse(_manager.IsLevelUnlocked(0));
            Assert.IsFalse(_manager.IsLevelUnlocked(6));
            Assert.IsFalse(_manager.IsLevelUnlocked(-1));
        }

        [Test]
        public void GetStars_InvalidLevel_ReturnsZero()
        {
            Assert.AreEqual(0, _manager.GetStars(0));
            Assert.AreEqual(0, _manager.GetStars(6));
        }

        [Test]
        public void MarkLevelComplete_InvalidLevel_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _manager.MarkLevelComplete(0, 3));
            Assert.DoesNotThrow(() => _manager.MarkLevelComplete(6, 3));
        }

        [Test]
        public void IsLevelCompleted_ReturnsTrueWhenStarsGreaterThanZero()
        {
            _manager.MarkLevelComplete(1, 1);
            Assert.IsTrue(_manager.IsLevelCompleted(1));
        }

        [Test]
        public void IsLevelCompleted_ReturnsFalseWhenZeroStars()
        {
            Assert.IsFalse(_manager.IsLevelCompleted(1));
        }
    }
}