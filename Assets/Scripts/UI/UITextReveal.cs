using System;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Reveal-in-place typewriter. The full string is assigned to the text and laid out
/// once, then maxVisibleCharacters reveals glyphs left to right. Because the layout
/// never changes while text "types", words cannot re-wrap or jump — unlike the old
/// substring-append approach, which rebuilt the whole layout every character.
///
/// Drivers: coroutine callers use <see cref="Play"/>; frame-driven callers
/// (e.g. an Update loop) use <see cref="Begin"/>/<see cref="SetProgress"/>/
/// <see cref="Complete"/> directly.
/// </summary>
public static class UITextReveal
{
    /// <summary>
    /// Lays out the text as currently assigned, hides every glyph, and returns the
    /// number of visible characters (rich-text tags don't count). Callers must assign
    /// <see cref="TMP_Text.text"/> before calling this.
    /// </summary>
    public static int Begin(TMP_Text text)
    {
        if (text == null)
            return 0;

        text.ForceMeshUpdate();
        int count = text.textInfo.characterCount;
        text.maxVisibleCharacters = count > 0 ? 0 : int.MaxValue;
        return count;
    }

    /// <summary>Shows the first <paramref name="visibleCharacters"/> glyphs, in place.</summary>
    public static void SetProgress(TMP_Text text, int visibleCharacters)
    {
        if (text != null)
            text.maxVisibleCharacters = Mathf.Max(visibleCharacters, 0);
    }

    /// <summary>Shows every glyph — the finished and skip-to-end state.</summary>
    public static void Complete(TMP_Text text)
    {
        if (text != null)
            text.maxVisibleCharacters = int.MaxValue;
    }

    /// <summary>
    /// Coroutine driver: reveals at <paramref name="charactersPerSecond"/> in unscaled
    /// time, matching the pacing of the old substring routines. If
    /// <paramref name="interrupted"/> returns true the reveal completes immediately.
    /// </summary>
    public static IEnumerator Play(TMP_Text text, float charactersPerSecond, Func<bool> interrupted = null)
    {
        int total = Begin(text);
        if (total <= 0 || charactersPerSecond <= 0f)
        {
            Complete(text);
            yield break;
        }

        float delay = 1f / charactersPerSecond;
        for (int i = 0; i < total; i++)
        {
            if (interrupted != null && interrupted())
            {
                Complete(text);
                yield break;
            }
            SetProgress(text, i + 1);
            yield return new WaitForSecondsRealtime(delay);
        }
        Complete(text);
    }
}
