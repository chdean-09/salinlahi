using NUnit.Framework;

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
        }
    }
}
