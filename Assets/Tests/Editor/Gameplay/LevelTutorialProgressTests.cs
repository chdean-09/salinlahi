using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class LevelTutorialProgressTests
    {
        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenLevelOneAndNotSeen_ReturnsTrue()
        {
            Assert.IsTrue(LevelTutorialProgress.ShouldShowForLevelNumber(1));
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenLevelIsNotOne_ReturnsFalse()
        {
            Assert.IsFalse(LevelTutorialProgress.ShouldShowForLevelNumber(2));
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenSeen_ReturnsFalse()
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();

            Assert.IsFalse(LevelTutorialProgress.ShouldShowForLevelNumber(1));
        }

        [Test]
        public void MarkLevel1TutorialSeen_PersistsSeenFlag()
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();

            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel1Tutorial());
            Assert.AreEqual(1, PlayerPrefs.GetInt("salinlahi.tutorial.level1_ftue_seen", 0));
        }

        [Test]
        public void Level1FtueSeenKey_MatchesPersistedContract()
        {
            Assert.AreEqual("salinlahi.tutorial.level1_ftue_seen", LevelTutorialProgress.Level1FtueSeenKey);
            Assert.AreEqual(ProgressManager.Level1FtueSeenKey, LevelTutorialProgress.Level1FtueSeenKey);
        }

        [Test]
        public void GuidedMomentKeys_MatchPersistedContract()
        {
            Assert.AreEqual("salinlahi.tutorial.level1.first_enemy_guided", LevelTutorialProgress.Level1FirstEnemyGuidedKey);
            Assert.AreEqual("salinlahi.tutorial.level1.first_enemy_defeated", LevelTutorialProgress.Level1FirstEnemyDefeatedKey);
            Assert.AreEqual("salinlahi.tutorial.level1.base_hp_explained", LevelTutorialProgress.Level1BaseHpExplainedKey);
            Assert.AreEqual("salinlahi.tutorial.level1.wave1_clear_explained", LevelTutorialProgress.Level1Wave1ClearExplainedKey);
            Assert.AreEqual("salinlahi.tutorial.level1.world_intro_seen", LevelTutorialProgress.Level1WorldIntroSeenKey);
            Assert.AreEqual("salinlahi.tutorial.level1.onboarding_complete", LevelTutorialProgress.Level1OnboardingCompleteKey);
            Assert.AreEqual("salinlahi.tutorial.level1.trace_assist_shown_count", LevelTutorialProgress.Level1TraceAssistShownCountKey);
            Assert.AreEqual("salinlahi.tutorial.level1.recent_draw_failures", LevelTutorialProgress.Level1RecentDrawFailuresKey);
        }

        [Test]
        public void MarkLevel1FirstEnemyGuided_PersistsGuidedFlag()
        {
            LevelTutorialProgress.MarkLevel1FirstEnemyGuided();

            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel1FirstEnemyGuided());
            Assert.AreEqual(1, PlayerPrefs.GetInt(LevelTutorialProgress.Level1FirstEnemyGuidedKey, 0));
        }

        [Test]
        public void MarkLevel1WorldIntroSeen_PersistsWorldIntroFlag()
        {
            LevelTutorialProgress.MarkLevel1WorldIntroSeen();

            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel1WorldIntro());
            Assert.AreEqual(1, PlayerPrefs.GetInt(LevelTutorialProgress.Level1WorldIntroSeenKey, 0));
        }

        [Test]
        public void MarkLevel1OnboardingComplete_PersistsCompletionFlag()
        {
            LevelTutorialProgress.MarkLevel1OnboardingComplete();

            Assert.IsTrue(LevelTutorialProgress.HasCompletedLevel1Onboarding());
            Assert.AreEqual(1, PlayerPrefs.GetInt(LevelTutorialProgress.Level1OnboardingCompleteKey, 0));
        }

        [Test]
        public void TraceAssistCounters_PersistAndReset()
        {
            Assert.AreEqual(1, LevelTutorialProgress.IncrementLevel1TraceAssistShownCount());
            Assert.AreEqual(2, LevelTutorialProgress.IncrementLevel1TraceAssistShownCount());
            Assert.AreEqual(1, LevelTutorialProgress.IncrementLevel1RecentDrawFailures());

            LevelTutorialProgress.ResetLevel1RecentDrawFailures();

            Assert.AreEqual(2, LevelTutorialProgress.GetLevel1TraceAssistShownCount());
            Assert.AreEqual(0, LevelTutorialProgress.GetLevel1RecentDrawFailures());
        }

        [Test]
        public void ResetLevel1TutorialForTests_RemovesAllLevelOneTutorialFlags()
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();
            LevelTutorialProgress.MarkLevel1FirstEnemyGuided();
            LevelTutorialProgress.MarkLevel1FirstEnemyDefeated();
            LevelTutorialProgress.MarkLevel1BaseHpExplained();
            LevelTutorialProgress.MarkLevel1Wave1ClearExplained();
            LevelTutorialProgress.MarkLevel1WorldIntroSeen();
            LevelTutorialProgress.MarkLevel1OnboardingComplete();
            LevelTutorialProgress.IncrementLevel1TraceAssistShownCount();
            LevelTutorialProgress.IncrementLevel1RecentDrawFailures();

            LevelTutorialProgress.ResetLevel1TutorialForTests();

            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial());
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1FirstEnemyGuided());
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1FirstEnemyDefeated());
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1BaseHpExplained());
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Wave1ClearExplained());
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1WorldIntro());
            Assert.IsFalse(LevelTutorialProgress.HasCompletedLevel1Onboarding());
            Assert.AreEqual(0, LevelTutorialProgress.GetLevel1TraceAssistShownCount());
            Assert.AreEqual(0, LevelTutorialProgress.GetLevel1RecentDrawFailures());
        }
    }
}
