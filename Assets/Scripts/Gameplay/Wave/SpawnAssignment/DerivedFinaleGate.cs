using System.Collections.Generic;

/// <summary>
/// Decides WHICH flattened slot a gated-finale level withholds until its final wave: the last one.
///
/// <para>
/// <b>It was not always this simple, and the history is the reason this class still exists.</b>
/// Restoration used to be by SYMBOL — one defeated carrier filled every slot matching its symbol,
/// across every focus word. Gating the last slot therefore withheld nothing whenever its symbol
/// appeared earlier in the level: clearing the earlier occurrence filled the gated slot for free
/// and the level completed before its final wave exactly as if the feature were off. Level 2 is
/// precisely that shape — BATA + MATA flatten to <c>[ba, ta, ma, ta]</c> — and the rule had to be
/// "the last slot whose symbol occurs EXACTLY ONCE", which for Level 2 meant <c>ma@MATA</c>: not
/// the last slot, and not the syllable the level was teaching.
/// </para>
///
/// <para>
/// <b>2026-09-17: one carrier now restores one slot</b> (see
/// <c>ActiveClueRestorationState.Apply</c>), so no slot can be filled by another slot's carrier and
/// the last slot is always genuinely withholdable. The uniqueness analysis, and the
/// <c>GATED_FINALE_UNWINNABLE</c> case that reported levels with no unique symbol, both became
/// unnecessary. Level 2 now ends on its last slot, <c>ta@MATA</c>, which is what it was always
/// meant to end on.
/// </para>
///
/// <para>
/// Pure and static so the choice stays an EditMode test, and so the runtime coordinator and the
/// author-time validator answer the question with the same code rather than two drifting copies.
/// </para>
/// </summary>
public static class DerivedFinaleGate
{
    /// <summary>No slot in this level can carry the finale gate.</summary>
    public const int NoSlot = -1;

    /// <summary>
    /// The slot withheld until the final wave: the last one, or <see cref="NoSlot"/> when the level
    /// has fewer than two slots. Gating the only slot would withhold the level's sole win
    /// condition, which <c>CampaignConfigValidator.ValidateGatedFinale</c> reports at author time.
    /// </summary>
    public static int LastSlotIndex(IReadOnlyList<string> symbolStableIds)
    {
        if (symbolStableIds == null || symbolStableIds.Count < 2)
            return NoSlot;

        return symbolStableIds.Count - 1;
    }
}
