using System;
using System.Collections.Generic;

/// <summary>
/// Flattens a level's focus words into the slot list the player is restoring, and classifies
/// a drawn syllable against it.
///
/// <para>The slot list is kept in visual reading order: word 0's syllables left to right, then word
/// 1's, and so on. Completion order breaks ties between repeated symbols; every eligible
/// unfinished occurrence can restore regardless of that order.</para>
///
/// <para>Deliberately free of UnityEngine types apart from the content assets it reads, following
/// <see cref="ActiveClueSelector"/> and <see cref="DrawTargetResolver"/>, so every classification
/// rule is an EditMode assertion rather than a scene rehearsal. Restoration state arrives as a
/// predicate so the caller — not this type — owns the dependency on the HUD that tracks it.</para>
/// </summary>
public static class TargetTextSlotMap
{
    /// <summary>One syllable of the target text, flattened out of its word.</summary>
    public struct Slot
    {
        public string WordStableId;

        /// <summary>Position inside the word, which is what the restoration state is keyed on.</summary>
        public int SlotIndexInWord;

        /// <summary>Combat character id, so a drawn glyph can be compared without a content lookup.</summary>
        public string CharacterId;

        /// <summary>
        /// Objective completion order. Legacy focus-word slots use their visual order; authored
        /// objectives may teach a later visual token first while the rail remains in reading order.
        /// </summary>
        public int CompletionOrder;

        /// <summary>True while a separate beat gates this occurrence.</summary>
        public bool Locked;

        public bool Restored;
    }

    /// <summary>
    /// Rebuilds <paramref name="slots"/> from <paramref name="words"/> in reading order.
    /// </summary>
    /// <param name="isRestored">
    /// Answers "is this word's slot already filled". Injected rather than read from the HUD directly,
    /// so the flattening can be tested with no scene and no presenter.
    /// </param>
    public static void Build(
        IReadOnlyList<FocusWordDefinition> words,
        Func<FocusWordDefinition, int, bool> isRestored,
        List<Slot> slots)
    {
        if (slots == null)
            return;

        slots.Clear();

        if (words == null)
            return;

        int flatOrder = 0;
        for (int w = 0; w < words.Count; w++)
        {
            FocusWordDefinition word = words[w];
            if (word?.decomposition == null)
                continue;

            for (int s = 0; s < word.decomposition.Count; s++)
            {
                BaybayinCharacterSO symbol = word.decomposition[s]?.symbol;
                if (symbol == null)
                    continue;

                slots.Add(new Slot
                {
                    WordStableId = word.stableId,
                    SlotIndexInWord = s,
                    CharacterId = symbol.characterID,
                    CompletionOrder = flatOrder++,
                    Restored = isRestored != null && isRestored(word, s),
                });
            }
        }
    }

    /// <summary>Builds the same classification map from a data-driven restoration objective.</summary>
    public static void Build(
        RestorationObjectiveDefinition definition,
        RestorationObjectiveState state,
        List<Slot> slots,
        Func<string, bool> canRestoreOccurrence = null)
    {
        if (slots == null)
            return;

        slots.Clear();
        if (definition?.units == null)
            return;

        for (int unitIndex = 0; unitIndex < definition.units.Count; unitIndex++)
        {
            RestorationObjectiveUnit unit = definition.units[unitIndex];
            if (unit?.tokens == null)
                continue;

            for (int tokenIndex = 0; tokenIndex < unit.tokens.Count; tokenIndex++)
            {
                RestorationObjectiveToken token = unit.tokens[tokenIndex];
                BaybayinCharacterSO symbol = token?.target?.symbol;
                if (token?.IsTarget != true || symbol == null)
                    continue;

                slots.Add(new Slot
                {
                    WordStableId = unit.stableId,
                    SlotIndexInWord = tokenIndex,
                    CharacterId = symbol.characterID,
                    CompletionOrder = token.EffectiveCompletionOrder(tokenIndex),
                    Locked = canRestoreOccurrence != null
                        && !canRestoreOccurrence(token.occurrenceId),
                    Restored = state != null && state.IsOccurrenceRestored(token.occurrenceId),
                });
            }
        }
    }

    /// <summary>
    /// Visual index of the next unrestored slot by completion order, or -1 when every slot is filled.
    /// </summary>
    public static int FindCursorIndex(IReadOnlyList<Slot> slots)
    {
        if (slots == null)
            return -1;

        int cursorIndex = -1;
        int cursorOrder = int.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Restored || slots[i].Locked)
                continue;

            if (slots[i].CompletionOrder < cursorOrder)
            {
                cursorIndex = i;
                cursorOrder = slots[i].CompletionOrder;
            }
        }

        return cursorIndex;
    }

    /// <summary>
    /// Classifies <paramref name="drawnCharacterId"/> against the flattened text.
    /// </summary>
    /// <param name="slotIndex">
    /// The earliest eligible matching occurrence, a gated occurrence if none is eligible, an
    /// already filled occurrence if none remains, or -1 when the syllable is absent.
    /// </param>
    /// <param name="cursorIndex">The cursor at the moment of the draw, for the slot that pulses.</param>
    public static DrawTextRelation Classify(
        IReadOnlyList<Slot> slots,
        string drawnCharacterId,
        out int slotIndex,
        out int cursorIndex)
    {
        slotIndex = -1;
        cursorIndex = FindCursorIndex(slots);

        if (slots == null || slots.Count == 0 || string.IsNullOrEmpty(drawnCharacterId))
            return DrawTextRelation.Unknown;

        // Any unfinished eligible occurrence can fill. Completion order only breaks ties when
        // a glyph occurs more than once; it never makes a correctly drawn glyph ineligible.
        int matchingIndex = -1;
        int matchingOrder = int.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].Restored
                && !slots[i].Locked
                && Matches(slots[i], drawnCharacterId)
                && slots[i].CompletionOrder < matchingOrder)
            {
                matchingIndex = i;
                matchingOrder = slots[i].CompletionOrder;
            }
        }

        if (matchingIndex >= 0)
        {
            slotIndex = matchingIndex;
            cursorIndex = matchingIndex;
            return DrawTextRelation.FillsCursorSlot;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].Restored && slots[i].Locked && Matches(slots[i], drawnCharacterId))
            {
                slotIndex = i;
                return DrawTextRelation.LaterNeeded;
            }
        }

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].Restored && Matches(slots[i], drawnCharacterId))
            {
                slotIndex = i;
                return DrawTextRelation.AlreadyFilled;
            }
        }

        return DrawTextRelation.NotInTargetText;
    }

    private static bool Matches(Slot slot, string drawnCharacterId)
        => string.Equals(slot.CharacterId, drawnCharacterId, StringComparison.Ordinal);
}
