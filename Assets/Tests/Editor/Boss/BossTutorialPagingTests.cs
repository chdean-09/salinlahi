using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Boss
{
    [TestFixture]
    public class BossTutorialPagingTests
    {
        [Test]
        public void FirstPage_LeftDisabled_RightEnabled()
        {
            var p = new BossTutorialPaging(3);
            Assert.AreEqual(0, p.Index);
            Assert.IsTrue(p.IsValid);
            Assert.IsFalse(p.CanGoLeft);
            Assert.IsTrue(p.CanGoRight);
        }

        [Test]
        public void MiddlePage_BothEnabled()
        {
            var p = new BossTutorialPaging(3);
            p.Next();
            Assert.AreEqual(1, p.Index);
            Assert.IsTrue(p.CanGoLeft);
            Assert.IsTrue(p.CanGoRight);
        }

        [Test]
        public void LastPage_RightDisabled_AndNextClamps()
        {
            var p = new BossTutorialPaging(3);
            p.Next(); p.Next();
            Assert.AreEqual(2, p.Index);
            Assert.IsFalse(p.CanGoRight);
            p.Next(); // clamp
            Assert.AreEqual(2, p.Index);
        }

        [Test]
        public void Prev_ClampsAtZero()
        {
            var p = new BossTutorialPaging(3);
            p.Prev();
            Assert.AreEqual(0, p.Index);
        }

        [Test]
        public void SinglePage_BothArrowsDisabled()
        {
            var p = new BossTutorialPaging(1);
            Assert.IsTrue(p.IsValid);
            Assert.IsFalse(p.CanGoLeft);
            Assert.IsFalse(p.CanGoRight);
        }

        [Test]
        public void ZeroOrNegativeCount_IsInvalid()
        {
            Assert.IsFalse(new BossTutorialPaging(0).IsValid);
            Assert.IsFalse(new BossTutorialPaging(-5).IsValid);
        }
    }
}
