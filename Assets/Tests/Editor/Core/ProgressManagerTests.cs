using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            for (int i = 1; i <= 15; i++)
            {
                PlayerPrefs.DeleteKey($"salinlahi.progress.unlocked.{i}");
                PlayerPrefs.DeleteKey($"salinlahi.progress.stars.{i}");
            }
            CharacterUnlockProgress.ClearAllUnlocked();
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
            LevelTutorialProgress.MarkLevel1TutorialSeen();
            Assert.AreEqual(5, _manager.GetTotalStars());
            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel1Tutorial());

            _manager.ClearAllProgress();
            Assert.AreEqual(0, _manager.GetStars(1));
            Assert.AreEqual(0, _manager.GetStars(2));
            Assert.IsTrue(_manager.IsLevelUnlocked(1));
            Assert.IsFalse(_manager.IsLevelUnlocked(2));
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial());
        }

        [Test]
        public void ClearAllProgress_ResetsEnemyDiscovery()
        {
            EnemyDataSO data = ScriptableObject.CreateInstance<EnemyDataSO>();
            data.enemyID = "soldado";

            EnemyDiscoveryProgress.TryMarkDiscovered(data, out _);
            Assert.IsTrue(EnemyDiscoveryProgress.HasDiscovered(data));

            _manager.ClearAllProgress();

            Assert.IsFalse(EnemyDiscoveryProgress.HasDiscovered(data));
            Object.DestroyImmediate(data);
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
            for (int i = 1; i <= 15; i++)
                _manager.MarkLevelComplete(i, 3);

            Assert.IsTrue(_manager.IsEndlessModeUnlocked());
        }

        [Test]
        public void IsLevelUnlocked_InvalidLevel_ReturnsFalse()
        {
            Assert.IsFalse(_manager.IsLevelUnlocked(0));
            Assert.IsFalse(_manager.IsLevelUnlocked(16));
            Assert.IsFalse(_manager.IsLevelUnlocked(-1));
        }

        [Test]
        public void GetStars_InvalidLevel_ReturnsZero()
        {
            Assert.AreEqual(0, _manager.GetStars(0));
            Assert.AreEqual(0, _manager.GetStars(16));
        }

        [Test]
        public void MarkLevelComplete_InvalidLevel_DoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[Salinlahi] ProgressManager: Invalid levelID 0. Must be between 1 and 15.");
            LogAssert.Expect(LogType.Error, "[Salinlahi] ProgressManager: Invalid levelID 16. Must be between 1 and 15.");
            Assert.DoesNotThrow(() => _manager.MarkLevelComplete(0, 3));
            Assert.DoesNotThrow(() => _manager.MarkLevelComplete(16, 3));
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

        /// <summary>
        /// SALIN-202: the accuracy-aware star formula belongs to revised saves only.
        /// A legacy PlayerPrefs save must keep the hearts-only formula even when the
        /// level flow has computed and handed over its results.
        /// </summary>
        [Test]
        public void CommitCurrentLevelOutcome_LegacySave_KeepsTheHeartsOnlyStarFormula()
        {
            GameObject heartHost = new GameObject("HeartSystem_Test");
            try
            {
                HeartSystem hearts = heartHost.AddComponent<HeartSystem>();
                // EditMode does not run Awake on AddComponent, so seed a full run directly.
                FieldInfo currentHearts = typeof(HeartSystem).GetField(
                    "_currentHearts", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(currentHearts, "HeartSystem must carry a current-heart field.");
                currentHearts.SetValue(hearts, hearts.GetMaxHearts());
                _manager.RegisterHeartSystem(hearts);
                _manager.TrySetSelectedLevelNumber(1);

                var evidence = new LearningEvidenceBatch { levelId = "level.ugat.01" };
                evidence.entries.Add(new LearningEvidenceEntry
                {
                    contentId = "symbol.na",
                    contentKind = LearningContentKind.Symbol,
                    dimension = MasteryDimension.Form,
                    attemptCount = 4,
                    successCount = 3,
                });
                LevelResults pending = LevelResultsCalculator.Compute(
                    evidence,
                    heartsRemaining: hearts.GetMaxHearts(),
                    maxHearts: hearts.GetMaxHearts(),
                    hintsUsed: 0,
                    emergencyHintPenalty: 0f);
                Assert.AreEqual(2, pending.Stars,
                    "Fixture check: weak tracing caps the revised formula at two stars.");

                _manager.SetPendingLevelResults(pending);
                _manager.CommitCurrentLevelOutcome();

                Assert.AreEqual(3, _manager.GetStars(1),
                    "Full hearts is three stars on a legacy save; pending results must not apply.");
            }
            finally
            {
                Object.DestroyImmediate(heartHost);
            }
        }

        [Test]
        public void ClearAllProgress_ClearsCharacterUnlocks()
        {
            BaybayinCharacterSO ba = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ba.characterID = "BA";
            CharacterUnlockProgress.TryMarkUnlocked(ba, out _);
            Assert.IsTrue(CharacterUnlockProgress.HasUnlocked(ba));

            _manager.ClearAllProgress();

            Assert.IsFalse(CharacterUnlockProgress.HasUnlocked(ba),
                "ClearAllProgress should also clear character unlocks");
            Object.DestroyImmediate(ba);
        }
    }
}
