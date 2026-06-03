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
            Assert.IsFalse(LevelTutorialProgress.ShouldShowForLevelNumber(3));
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenLevelTwoAndNotSeen_ReturnsTrue()
        {
            Assert.IsTrue(LevelTutorialProgress.ShouldShowForLevelNumber(2));
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenSeen_ReturnsFalse()
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();

            Assert.IsFalse(LevelTutorialProgress.ShouldShowForLevelNumber(1));
        }

        [Test]
        public void ShouldShowForLevelNumber_WhenLevelTwoSeen_ReturnsFalse()
        {
            LevelTutorialProgress.MarkLevel2TutorialSeen();

            Assert.IsFalse(LevelTutorialProgress.ShouldShowForLevelNumber(2));
        }

        [Test]
        public void MarkLevel1TutorialSeen_PersistsSeenFlag()
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();

            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel1Tutorial());
            Assert.AreEqual(1, PlayerPrefs.GetInt("salinlahi.tutorial.level1_ftue_seen", 0));
        }

        [Test]
        public void MarkLevel2TutorialSeen_PersistsSeparateSeenFlag()
        {
            LevelTutorialProgress.MarkLevel2TutorialSeen();

            Assert.IsTrue(LevelTutorialProgress.HasSeenLevel2Tutorial());
            Assert.AreEqual(1, PlayerPrefs.GetInt("salinlahi.tutorial.level2_advanced_focus_chain_v3_seen", 0));
            Assert.IsFalse(LevelTutorialProgress.HasSeenLevel1Tutorial());
        }

        [Test]
        public void Level1FtueSeenKey_MatchesPersistedContract()
        {
            Assert.AreEqual("salinlahi.tutorial.level1_ftue_seen", LevelTutorialProgress.Level1FtueSeenKey);
            Assert.AreEqual(ProgressManager.Level1FtueSeenKey, LevelTutorialProgress.Level1FtueSeenKey);
        }

        [Test]
        public void Level2AdvancedSeenKey_MatchesPersistedContract()
        {
            Assert.AreEqual("salinlahi.tutorial.level2_advanced_focus_chain_v3_seen", LevelTutorialProgress.Level2AdvancedSeenKey);
            Assert.AreEqual(ProgressManager.Level2AdvancedSeenKey, LevelTutorialProgress.Level2AdvancedSeenKey);
        }
    }
}
