using System;
using System.Collections.Generic;

/// <summary>
/// Flattens a level's focus words into the ordered slot list the player is restoring, and classifies
/// a drawn syllable against it.
///
/// <para>The slot list is kept in visual reading order: word 0's syllables left to right, then word
/// 1's, and so on. An authored objective may separately assign a completion order, so the cursor
/// follows that order while returned indices still address the visual rail.</para>
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

        /// <summary>True when the objective has not activated this slot's unit yet.</summary>
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
        List<Slot> slots)
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
                    Locked = state != null
                        && !string.IsNullOrEmpty(state.ActiveUnitId)
                        && !string.Equals(state.ActiveUnitId, unit.stableId, StringComparison.Ordinal),
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
    /// The slot this syllable belongs to: the cursor for a fill, the earliest matching future slot
    /// for later-needed, the earliest matching filled slot for a duplicate, and -1 when the syllable
    /// is in no slot at all. It is what the badge flies to, so "earliest" matters: a syllable with
    /// two slots left should promise the player the nearer one.
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

        if (cursorIndex >= 0 && Matches(slots[cursorIndex], drawnCharacterId))
        {
            slotIndex = cursorIndex;
            return DrawTextRelation.FillsCursorSlot;
        }

        // Unrestored slots are scanned before restored ones on purpose. A syllable can legitimately
        // occupy both — Level 1 has no repeat, but INA/AMA-shaped texts generally will — and in that
        // case the honest answer is the one that still has a future: "that one comes later" is true
        // and useful, while "already restored" would be true of a different instance of the same
        // syllable and read as a flat contradiction of the empty slot the player can see. Compare
        // completion order rather than list position because Level 2 teaches TA before the BA that
        // appears to its left in the visible word.
        int cursorOrder = cursorIndex >= 0 ? slots[cursorIndex].CompletionOrder : int.MinValue;
        int laterIndex = -1;
        int laterOrder = int.MaxValue;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].Restored
                && !slots[i].Locked
                && slots[i].CompletionOrder > cursorOrder
                && Matches(slots[i], drawnCharacterId)
                && slots[i].CompletionOrder < laterOrder)
            {
                laterIndex = i;
                laterOrder = slots[i].CompletionOrder;
            }
        }

        if (laterIndex >= 0)
        {
            slotIndex = laterIndex;
            return DrawTextRelation.LaterNeeded;
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
