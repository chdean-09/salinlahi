using System;
using System.Collections.Generic;

/// <summary>
/// Splits a restoration objective into the words a player restores one at a time when
/// <see cref="RestorationObjectiveDefinition.oneWordAtATime"/> is set.
///
/// <para>
/// A word is a whitespace-delimited run of the unit's reconstructed text, so the boundaries are
/// the ones the player reads: "KAniYANG" is one word of three boxes (KA, YA, NGA) even though a
/// literal "ni" sits between its first two targets, and "nag-iNGAt" stays whole across its hyphen.
/// A word with no target occurrences ("ni", "sa", "mga") has no boxes and is not a page. A unit
/// boundary always closes the current word.
/// </para>
///
/// <para>
/// Shared by the spawn director (which slots may be offered), the coordinator (which occurrences
/// may be credited) and the HUD rail (which boxes are on screen), so all three agree on what
/// "the current word" is. Free of UnityEngine types so it is a plain EditMode assertion.
/// </para>
/// </summary>
public sealed class RestorationWordPages
{
    public const int NoPage = -1;

    private readonly List<List<string>> _pages = new List<List<string>>();
    private readonly Dictionary<string, int> _pageByOccurrence =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _indexInPage =
        new Dictionary<string, int>(StringComparer.Ordinal);

    public int PageCount => _pages.Count;

    /// <summary>True when <paramref name="definition"/> opts into one-word-at-a-time play.</summary>
    public static bool IsEnabled(RestorationObjectiveDefinition definition)
        => definition != null && definition.oneWordAtATime && definition.HasTargets;

    public static RestorationWordPages Build(RestorationObjectiveDefinition definition)
    {
        var pages = new RestorationWordPages();
        if (definition?.units == null)
            return pages;

        var current = new List<string>();
        for (int unitIndex = 0; unitIndex < definition.units.Count; unitIndex++)
        {
            RestorationObjectiveUnit unit = definition.units[unitIndex];
            pages.Close(ref current);
            if (unit?.tokens == null)
                continue;

            for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
            {
                RestorationObjectiveToken token = unit.tokens[tokenIndex];
                if (token == null)
                    continue;

                if (token.IsTarget)
                {
                    if (!string.IsNullOrEmpty(token.occurrenceId))
                        current.Add(token.occurrenceId);
                    continue;
                }

                string text = token.literalText;
                if (string.IsNullOrEmpty(text))
                    continue;

                for (int charIndex = 0; charIndex < text.Length; charIndex++)
                {
                    if (char.IsWhiteSpace(text[charIndex]))
                        pages.Close(ref current);
                }
            }
        }

        pages.Close(ref current);
        return pages;
    }

    /// <summary>The page holding <paramref name="occurrenceId"/>, or <see cref="NoPage"/>.</summary>
    public int PageOf(string occurrenceId)
        => !string.IsNullOrEmpty(occurrenceId)
            && _pageByOccurrence.TryGetValue(occurrenceId, out int page)
                ? page
                : NoPage;

    /// <summary>Left-to-right position of <paramref name="occurrenceId"/> inside its page, or -1.</summary>
    public int IndexInPage(string occurrenceId)
        => !string.IsNullOrEmpty(occurrenceId)
            && _indexInPage.TryGetValue(occurrenceId, out int index)
                ? index
                : -1;

    /// <summary>How many boxes <paramref name="page"/> carries.</summary>
    public int SlotCountOf(int page)
        => page >= 0 && page < _pages.Count ? _pages[page].Count : 0;

    /// <summary>The widest page's box count, which sizes the rail.</summary>
    public int MaxSlotCount
    {
        get
        {
            int max = 0;
            for (int page = 0; page < _pages.Count; page++)
                max = Math.Max(max, _pages[page].Count);
            return max;
        }
    }

    /// <summary>
    /// The first page with an unrestored occurrence: the word the player is working on. Once every
    /// occurrence is restored this is the last page, so a finished objective keeps its final word
    /// on screen. <see cref="NoPage"/> only when there are no pages at all.
    /// </summary>
    public int CurrentPage(Func<string, bool> isRestored)
    {
        for (int page = 0; page < _pages.Count; page++)
        {
            List<string> occurrences = _pages[page];
            for (int index = 0; index < occurrences.Count; index++)
            {
                if (isRestored == null || !isRestored(occurrences[index]))
                    return page;
            }
        }

        return _pages.Count - 1;
    }

    /// <summary>
    /// How many words have every box restored. Counted per word rather than up to
    /// <see cref="CurrentPage"/>, so the figure stays honest even if a later word's box were ever
    /// credited out of order.
    /// </summary>
    public int CompletedPageCount(Func<string, bool> isRestored)
    {
        if (isRestored == null)
            return 0;

        int completed = 0;
        for (int page = 0; page < _pages.Count; page++)
        {
            List<string> occurrences = _pages[page];
            bool whole = true;
            for (int index = 0; index < occurrences.Count && whole; index++)
                whole = isRestored(occurrences[index]);

            if (whole)
                completed++;
        }

        return completed;
    }

    private void Close(ref List<string> current)
    {
        if (current.Count == 0)
            return;

        int page = _pages.Count;
        for (int index = 0; index < current.Count; index++)
        {
            _pageByOccurrence[current[index]] = page;
            _indexInPage[current[index]] = index;
        }

        _pages.Add(current);
        current = new List<string>();
    }
}
