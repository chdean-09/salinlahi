/// <summary>
/// Presentation channels a level may use to cue the active clue. Channels are composable so
/// an audio clue can declare a readable visual equivalent in the same value.
/// </summary>
[System.Flags]
public enum ClueChannels
{
    None           = 0,
    Glyph          = 1 << 0,
    SpokenAudio    = 1 << 1,
    LatinText      = 1 << 2,
    ContextImage   = 1 << 3,
    IncompleteWord = 1 << 4,
}
