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
/// WHY THE BEAT HAS NO BANNER COPY. The instant-win beat exists to teach one rule:
/// filling every slot of the target text wins the level, and surviving a wave is not a
/// win condition (level-01 design plan, §2 beat B10). The original banner stated that in
/// words ("the text is whole, the wave no longer matters"); product chose to drop the
/// wording and let the staging carry it — enemies frozen mid-stride, then dissolved, is
/// the rule shown rather than said. The continue prompt below is the beat's only text.
///
/// D-006 ("tracing" -> "drawing" in prose and UI strings) is satisfied by construction:
/// no string here contains "Trac*".
/// ============================================================================
/// </summary>
public static class InstantWinCopy
{
    /// <summary>
    /// Shown once the frozen hold's arming pause passes, while the beat holds for the
    /// player's tap. Verbatim CutscenePlayer's _continuePromptMessage — every
    /// text surface in the game shares this affordance, so the wording stays identical.
    /// </summary>
    public const string ContinuePromptLabel = "Tap anywhere to continue";
}
