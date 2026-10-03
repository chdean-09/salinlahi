/// <summary>
/// ============================================================================
/// SALIN-240 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Memory Card and Memory Archive can show lives here, and nowhere else,
/// so product/content can rewrite the wording without touching presentation logic. Mirrors
/// <see cref="LevelResultsCopy"/> (SALIN-234), <see cref="CampaignSaveNoticeCopy"/>
/// (SALIN-272) and <see cref="LevelLockNoticeCopy"/> (SALIN-137), which shipped under the
/// same banner.
///
/// LANGUAGE. The project splits by role and that split is followed, not re-decided: UI
/// chrome is English (MainMenu.unity's "Play", "Level Select", "Settings", "Almanac"), and
/// narrative content is Filipino (Assets/ScriptableObjects/Dialogue/*). So every string in
/// this class is English chrome. The card's TITLE, WORD MEANINGS and LORE are NOT here --
/// they are read verbatim from the authored assets (LevelConfigSO.levelName,
/// focusWords[*].meaning, and the restored-memory cutscene's panels) and no narrative text
/// is drafted anywhere in this ticket.
///
/// PROVENANCE. Nothing below is invented:
///   ArchiveTitle             verbatim, spec BTN-ARCHIVE "Memory Archive"
///   ClaimLabel               verbatim, spec BTN-CLAIM "Claim Memory"
///   FlipLabel / BackLabel    verbatim, spec UF-29 "Flip Card"
///   EarnInLevelFormat        was verbatim from SALIN-240's criterion ("Earn in Level n");
///                            re-pointed by SALIN-258 onto the era-relative label, per the
///                            versioned ruling in docs/design/spec-rulings-2026-09.md
///   CollectibleNumberFormat  the shipped n/max shape (LevelResultsCopy)
///   EmptyArchiveBody         the one genuinely drafted sentence; register copied from
///                            LevelContentMissingPanel.Render, which deliberately does not
///                            blame the player and does not imply anything was lost.
///
/// THE SALIN-258 BOUNDARY IS NOW CLOSED. SALIN-240 declared a conflict here: its acceptance
/// criterion's literal wording was "Earn in Level n" with the GLOBAL number, while SALIN-258's
/// ruling is "three eras of five levels; never show a global 1-15". SALIN-240 shipped the
/// criterion as written and isolated it to this constant plus one call site
/// (MemoryArchiveController.BuildEntryRow) so that SALIN-258 could flip one constant and one
/// argument rather than unpicking a screen.
///
/// That isolation held exactly as designed. SALIN-258 resolved the conflict in favour of the
/// ruling (docs/design/spec-rulings-2026-09.md, "How are levels numbered for the player?"):
/// the format no longer carries the word "Level" or a number at all, and takes a pre-rendered
/// era-relative label instead, so the row now reads "Locked · Earn in Ugnayan Level 2" where
/// it used to read "Locked · Earn in Level 7". A reviewer seeing a global level number on this
/// surface is now looking at a REGRESSION, not a recorded choice.
///
/// NO ACCURACY READOUT — D-021 / owner ruling R1. No percentage, no streak, no score
/// appears on the card or the archive, and no string here offers one. D-006: player-facing
/// prose says "drawing", never "tracing".
///
/// NOT BUILT, DELIBERATELY: "Hear Words" (UF-29) is CONTENT-BLOCKED -- every Level 1 focus
/// word has narrationClip: {fileID: 0}, and a disabled button promising audio that does not
/// exist is worse than its absence. "Add to Favorites" (UF-29) and filters (UF-39) are out
/// of scope by ruling: favorites needs a new persisted field and the save schema has already
/// moved twice this sprint.
///
/// ACTION REQUIRED: product/content review of the wording before release. The language
/// split above is settled; the sentences themselves are not sacred.
/// ============================================================================
/// </summary>
public static class MemoryCardCopy
{
    /// <summary>Spec BTN-ARCHIVE, verbatim. Also the main-menu button label.</summary>
    public const string ArchiveTitle = "Memory Archive";

    /// <summary>Spec BTN-CLAIM, verbatim.</summary>
    public const string ClaimLabel = "Claim Memory";

    /// <summary>Spec UF-29, verbatim.</summary>
    public const string FlipLabel = "Flip Card";

    /// <summary>Returns the card to its front face. Title Case matches the shipped register.</summary>
    public const string BackLabel = "Back";

    public const string CloseLabel = "Close";

    /// <summary>Shown under a silhouette slot instead of the level title.</summary>
    public const string LockedLabel = "Locked";

    /// <summary>
    /// SALIN-258. {0} is the ERA-RELATIVE LABEL ("Ugnayan Level 2"), not a number — the word
    /// "Level" moved into the label, which is why it is no longer in this format string.
    /// See the boundary note on this class.
    /// </summary>
    public const string EarnInLevelFormat = "Earn in {0}";

    /// <summary>{0} = this memory's position in its era, {1} = memories in the era.</summary>
    public const string CollectibleNumberFormat = "{0}/{1}";

    /// <summary>Header above the words on the card front.</summary>
    public const string WordsHeading = "Words Restored";

    /// <summary>Header above the lore on the card back.</summary>
    public const string LoreHeading = "The Memory";

    /// <summary>
    /// Shown when the archive has nothing unlocked yet. Register follows
    /// LevelContentMissingPanel.Render: it does not blame the player and does not suggest
    /// anything was lost, because nothing was -- no level has granted a memory yet.
    /// </summary>
    public const string EmptyArchiveBody =
        "No memories have been restored yet. Finish a level to earn its memory, and it will "
        + "appear here.";

    /// <summary>Shown on the claim overlay above the Claim Memory control.</summary>
    public const string ClaimPromptBody = "You restored a memory.";

    /// <param name="levelLabel">
    /// SALIN-258: an era-relative label from <see cref="CampaignLevelLabel"/>, never a global
    /// 1-15 number.
    /// </param>
    public static string EarnInLevel(string levelLabel) =>
        string.Format(EarnInLevelFormat, levelLabel);

    public static string CollectibleNumber(int index, int total) =>
        string.Format(CollectibleNumberFormat, index, total);
}
