using NUnit.Framework;

namespace Salinlahi.Tests.Editor.UI
{
    [TestFixture]
    public class AlmanacControllerTests
    {
        // ---- AlmanacCell decision logic ----

        [Test]
        public void ShouldShowBossBorder_OnlyWhenBossAndRevealed()
        {
            Assert.IsTrue(AlmanacCell.ShouldShowBossBorder(isBoss: true, isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: true, isRevealed: false),
                "a locked boss must read as a plain '?', no red border");
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: false, isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldShowBossBorder(isBoss: false, isRevealed: false));
        }

        [Test]
        public void ShouldBeInteractable_OnlyWhenRevealed()
        {
            Assert.IsTrue(AlmanacCell.ShouldBeInteractable(isRevealed: true));
            Assert.IsFalse(AlmanacCell.ShouldBeInteractable(isRevealed: false));
        }
    }
}
