using NUnit.Framework;
using UnityEngine;
using static TutorialIntroPlayer;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class TutorialIntroPlayerTests
    {
        [Test]
        public void SelectMode_WhenVideoClipAssigned_ReturnsVideo()
            => Assert.AreEqual(PlaybackMode.Video, SelectMode(hasVideoClip: true, hasGifTexture: false, hasAnimationClip: false));

        [Test]
        public void SelectMode_WhenOnlyGifTextureAssigned_ReturnsGif()
            => Assert.AreEqual(PlaybackMode.Gif, SelectMode(hasVideoClip: false, hasGifTexture: true, hasAnimationClip: false));

        [Test]
        public void SelectMode_WhenOnlyGifFramesAssigned_ReturnsGif()
        {
            Texture2D texture = new(1, 1);
            Sprite frame = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));

            try
            {
                Assert.AreEqual(
                    PlaybackMode.Gif,
                    SelectMode(videoClip: null, gifTexture: null, gifFrames: new[] { frame }, animationClip: null));
            }
            finally
            {
                Object.DestroyImmediate(frame);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SelectMode_WhenOnlyAnimationClipAssigned_ReturnsAnimation()
            => Assert.AreEqual(PlaybackMode.Animation, SelectMode(hasVideoClip: false, hasGifTexture: false, hasAnimationClip: true));

        [Test]
        public void SelectMode_WhenVideoAndGifAssigned_PrefersVideo()
            => Assert.AreEqual(PlaybackMode.Video, SelectMode(hasVideoClip: true, hasGifTexture: true, hasAnimationClip: false));

        [Test]
        public void SelectMode_WhenGifAndAnimationAssigned_PrefersGif()
            => Assert.AreEqual(PlaybackMode.Gif, SelectMode(hasVideoClip: false, hasGifTexture: true, hasAnimationClip: true));

        [Test]
        public void SelectMode_WhenNeitherAssigned_ReturnsNone()
            => Assert.AreEqual(PlaybackMode.None, SelectMode(hasVideoClip: false, hasGifTexture: false, hasAnimationClip: false));

        [Test]
        public void Play_WhenNoMediaAssigned_ReturnsWithoutStartingOverlay()
        {
            GameObject host = new("TutorialIntroPlayerTestHost");
            try
            {
                TutorialIntroPlayer player = host.AddComponent<TutorialIntroPlayer>();
                System.Collections.IEnumerator routine = player.Play(new OnboardingVideoTemplate());

                Assert.IsFalse(routine.MoveNext());
                Assert.IsFalse(player.IsPlaying);
                Assert.AreEqual(PlaybackMode.None, player.CurrentMode);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
