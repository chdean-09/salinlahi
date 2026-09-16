using System.Collections.Generic;

/// <summary>
/// Decides WHICH flattened slot a gated-finale level withholds until its final wave.
///
/// <para>
/// The obvious answer - the last slot - is wrong, and wrong silently. Restoration is by SYMBOL, not
/// by slot: <c>ActiveCluePresenter.ActiveClueRestorationState.Apply</c> fills every slot matching a
/// defeated carrier's symbol across every focus word. So gating a slot whose symbol also appears
/// earlier in the level gates nothing at all - clearing the earlier occurrence fills the gated slot
/// for free, and the level completes before its final wave exactly as if the feature were switched
/// off.
/// </para>
///
/// <para>
/// Level 2 is precisely that shape: BATA + MATA flatten to <c>[ba, ta, ma, ta]</c>, so the last slot
/// is <c>ta@MATA</c> and defeating any TA carrier for BATA's slot 1 also restored it. Levels 3 and 4
/// end on a symbol that occurs once, which is why the defect survived every per-level review.
/// </para>
///
/// <para>
/// The gate therefore lands on the LAST slot whose symbol occurs EXACTLY ONCE among the level's
/// flattened slots. That slot cannot be restored by any other slot's carrier, so withholding it
/// genuinely withholds the level's completion. For Level 2 that is <c>ma@MATA</c>; for Levels 3 and
/// 4 it is still the last slot, so their behaviour is unchanged.
/// </para>
///
/// <para>
/// A level whose every symbol repeats cannot support a gated finale at all: no single slot can be
/// withheld, because whichever one you pick, another slot's carrier fills it. That is an authoring
/// error, reported by <c>CampaignConfigValidator.ValidateGatedFinale</c> as
/// <see cref="ContentValidationCode.GatedFinaleUnwinnable"/>, and left ungated at runtime rather
/// than gated somewhere useless.
/// </para>
///
/// <para>
/// Pure and static so the choice is an EditMode test rather than something only a full play session
/// could surface, and so the runtime coordinator and the author-time validator answer the question
/// with the same code instead of two drifting copies.
/// </para>
/// </summary>
public static class DerivedFinaleGate
{
    /// <summary>No slot in this level can carry the derived finale gate.</summary>
    public const int NoSlot = -1;

    /// <summary>
    /// The index of the last entry in <paramref name="symbolStableIds"/> whose value occurs exactly
    /// once in the list, or <see cref="NoSlot"/> when every value repeats (or the list is null or
    /// empty). Null and empty entries are never candidates: a slot with no symbol cannot be
    /// restored by a carrier either way.
    /// </summary>
    public static int LastUniquelyOccurringIndex(IReadOnlyList<string> symbolStableIds)
    {
        if (symbolStableIds == null || symbolStableIds.Count == 0)
            return NoSlot;

        var counts = new Dictionary<string, int>();
        for (int index = 0; index < symbolStableIds.Count; index++)
        {
            string symbol = symbolStableIds[index];
            if (string.IsNullOrEmpty(symbol))
                continue;

            counts.TryGetValue(symbol, out int seen);
            counts[symbol] = seen + 1;
        }

        for (int index = symbolStableIds.Count - 1; index >= 0; index--)
        {
            string symbol = symbolStableIds[index];
            if (!string.IsNullOrEmpty(symbol) && counts[symbol] == 1)
                return index;
        }

        return NoSlot;
    }
}
