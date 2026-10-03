using System;
using UnityEngine;

/// <summary>
/// SALIN-157: resolves the approved per-level-context pronunciation clip and
/// visible label for a symbol's spoken value. E/I, O/U, and DA/RA share one
/// glyph but carry distinct <see cref="SpokenValueDefinition"/> entries, so
/// both audio and label must follow the requirement's spokenValueId rather
/// than the character-level defaults. Pure logic — EditMode tested.
/// </summary>
public static class SpokenValueResolver
{
    /// <summary>
    /// The clip for the given spoken value, falling back to the legacy
    /// character-level clip, or null when no approved audio exists. A null
    /// result is the visual-only path: callers keep every essential element
    /// readable and simply do not offer playback.
    /// </summary>
    public static AudioClip ResolveClip(BaybayinCharacterSO symbol, string spokenValueId)
    {
        if (symbol == null)
            return null;

        if (symbol.TryGetSpokenValue(spokenValueId, out SpokenValueDefinition value)
            && value.pronunciationClip != null)
        {
            return value.pronunciationClip;
        }

        return symbol.pronunciationClip;
    }

    /// <summary>
    /// The level-context label for the given spoken value (e.g. "ra" for
    /// value.ra on the DA/RA character), falling back to the legacy syllable,
    /// then the id suffix — the same last-resort chain the focus-word preview
    /// uses — so a card always has readable text.
    /// </summary>
    public static string ResolveLabel(BaybayinCharacterSO symbol, string spokenValueId)
    {
        if (symbol != null
            && symbol.TryGetSpokenValue(spokenValueId, out SpokenValueDefinition value)
            && !string.IsNullOrEmpty(value.displayValue))
        {
            return value.displayValue;
        }

        if (symbol != null && !string.IsNullOrEmpty(symbol.syllable))
            return symbol.syllable;

        if (!string.IsNullOrEmpty(spokenValueId)
            && spokenValueId.StartsWith("value.", StringComparison.Ordinal))
        {
            return spokenValueId.Substring("value.".Length);
        }

        return "?";
    }
}
