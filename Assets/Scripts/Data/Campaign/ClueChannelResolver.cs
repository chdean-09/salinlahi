/// <summary>
/// Pure channel maths for active-clue presentation. Kept free of UnityEngine types so the
/// audio-fallback rule is covered by fast EditMode tests.
/// </summary>
public static class ClueChannelResolver
{
    /// <summary>Channels a player can read without audio.</summary>
    public const ClueChannels VisualChannels =
        ClueChannels.Glyph
        | ClueChannels.LatinText
        | ClueChannels.ContextImage
        | ClueChannels.IncompleteWord;

    public static bool HasReadableVisual(ClueChannels channels)
        => (channels & VisualChannels) != ClueChannels.None;

    /// <summary>
    /// Adds the configured visual fallback when the clue is audio-only. Non-visual bits in the
    /// fallback are masked out, so a non-visual fallback cannot satisfy validation.
    /// </summary>
    public static ClueChannels Resolve(ClueChannels channels, ClueChannels audioVisualFallback)
    {
        if (HasReadableVisual(channels))
            return channels;

        if ((channels & ClueChannels.SpokenAudio) == ClueChannels.None)
            return channels;

        return channels | (audioVisualFallback & VisualChannels);
    }
}
