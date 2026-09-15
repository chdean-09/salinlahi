/// <summary>
/// ============================================================================
/// PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the instant-win beat can show lives here, and nowhere else, so
/// product/content can rewrite the wording without touching flow logic. Mirrors
/// <see cref="WaveClearedCopy"/>, <see cref="LevelResultsCopy"/> and
/// <see cref="LevelLockNoticeCopy"/>, which shipped under the same banner.
///
/// LANGUAGE: English. The project splits by role — UI chrome is English (this class,
/// WaveClearedCopy, LevelResultsCopy, LevelContentMissingPanel), narrative content is
/// Filipino (Assets/ScriptableObjects/Dialogue/*). That split was already in force; it
/// is followed here rather than re-decided. The level-01 design plan's §6 "dual Filipino
/// + English prompt lines" row is a HUD prompt affordance, not this banner.
///
/// WHAT THE BANNER HAS TO SAY, AND WHY THE WORDING IS NOT FREE. The instant-win beat
/// exists to teach one rule: filling every slot of the target text wins the level, and
/// surviving a wave is not a win condition (level-01 design plan, §2 beat B10). The
/// banner is the only place that rule is ever stated in words, so it must name BOTH
/// halves — the text being whole, and the wave having stopped mattering. A banner that
/// says only "Level Complete" leaves the player to credit the wave clear they can still
/// see on screen, which is the exact misreading the frozen hold is staged to prevent.
///
/// D-006 ("tracing" -> "drawing" in prose and UI strings) is satisfied by construction:
/// no string here contains "Trac*".
/// ============================================================================
/// </summary>
public static class InstantWinCopy
{
    /// <summary>
    /// The instant-win banner (level-01 design plan §2 B10: "banner: the text is whole,
    /// the wave no longer matters"). Two sentences rather than one clause, because the
    /// second half is the rule being taught and a reader skims the first half.
    /// </summary>
    public const string BannerLabel = "The text is whole.\nThe wave no longer matters.";

    /// <summary>
    /// Fallback for the restored-text line when the level's focus words carry no authored
    /// display label or Latin spelling. Never expected in authored content — every
    /// FocusWordDefinition in the campaign has a latinSpelling — but a blank line above
    /// the banner would read as a rendering fault rather than as missing data.
    /// </summary>
    public const string RestoredTextUnavailableLabel = "Restored";
}
