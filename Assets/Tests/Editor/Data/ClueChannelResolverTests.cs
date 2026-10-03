using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Data
{
    public sealed class ClueChannelResolverTests
    {
        [Test]
        public void HasReadableVisual_None_IsFalse()
        {
            Assert.IsFalse(ClueChannelResolver.HasReadableVisual(ClueChannels.None));
        }

        [Test]
        public void HasReadableVisual_SpokenAudioAlone_IsFalse()
        {
            Assert.IsFalse(ClueChannelResolver.HasReadableVisual(ClueChannels.SpokenAudio));
        }

        [Test]
        public void Resolve_AlreadyVisual_ReturnsChannelsUnchanged()
        {
            ClueChannels input = ClueChannels.Glyph | ClueChannels.LatinText;

            ClueChannels resolved = ClueChannelResolver.Resolve(input, ClueChannels.ContextImage);

            Assert.That(resolved, Is.EqualTo(input));
        }

        [Test]
        public void Resolve_SpokenAudioOnly_AddsConfiguredVisualFallback()
        {
            ClueChannels resolved = ClueChannelResolver.Resolve(
                ClueChannels.SpokenAudio, ClueChannels.IncompleteWord);

            Assert.That(resolved,
                Is.EqualTo(ClueChannels.SpokenAudio | ClueChannels.IncompleteWord));
            Assert.IsTrue(ClueChannelResolver.HasReadableVisual(resolved));
        }

        [Test]
        public void Resolve_SpokenAudioWithAudioOnlyFallback_StaysUnreadable()
        {
            ClueChannels resolved = ClueChannelResolver.Resolve(
                ClueChannels.SpokenAudio, ClueChannels.SpokenAudio);

            Assert.IsFalse(ClueChannelResolver.HasReadableVisual(resolved),
                "A non-visual fallback must not be able to satisfy the readable-visual rule.");
        }

        [Test]
        public void Resolve_NoAudioAndNoVisual_ReturnsNone()
        {
            ClueChannels resolved = ClueChannelResolver.Resolve(
                ClueChannels.None, ClueChannels.LatinText);

            Assert.That(resolved, Is.EqualTo(ClueChannels.None),
                "An empty channel set is not silently repaired; the validator rejects it instead.");
        }
    }
}
