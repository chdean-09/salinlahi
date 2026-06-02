using NUnit.Framework;
using static TutorialIntroPlayer;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class TutorialIntroPlayerTests
    {
        [Test]
        public void SelectMode_WhenVideoClipAssigned_ReturnsVideo()
            => Assert.AreEqual(PlaybackMode.Video, SelectMode(hasVideoClip: true, hasAnimationClip: false));

        [Test]
        public void SelectMode_WhenOnlyAnimationClipAssigned_ReturnsAnimation()
            => Assert.AreEqual(PlaybackMode.Animation, SelectMode(hasVideoClip: false, hasAnimationClip: true));

        [Test]
        public void SelectMode_WhenBothAssigned_PrefersVideo()
            => Assert.AreEqual(PlaybackMode.Video, SelectMode(hasVideoClip: true, hasAnimationClip: true));

        [Test]
        public void SelectMode_WhenNeitherAssigned_ReturnsNone()
            => Assert.AreEqual(PlaybackMode.None, SelectMode(hasVideoClip: false, hasAnimationClip: false));
    }
}
