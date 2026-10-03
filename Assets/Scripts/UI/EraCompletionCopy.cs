/// <summary>
/// ============================================================================
/// SALIN-253 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Era Completion screen can show lives here, and nowhere else, so
/// product/content can rewrite the wording without touching presentation logic. Mirrors
/// <see cref="LevelResultsCopy"/> (SALIN-234), <see cref="MemoryCardCopy"/> (SALIN-240),
/// <see cref="CampaignLevelLabel"/> (SALIN-258), CampaignSaveNoticeCopy (SALIN-272) and
/// LevelLockNoticeCopy (SALIN-137), which all shipped under this banner.
///
/// LANGUAGE. The project splits by role and that split is followed, not re-decided: UI
/// chrome is English, narrative content is Filipino. Every string below is English chrome.
/// The ERA NAME, the LEVEL TITLES and the LORE are NOT here — they are read verbatim from
/// the authored assets (EraConfigSO.eraName, LevelConfigSO.levelName, and each level's
/// restored-memory cutscene panels). No narrative text is drafted in this ticket.
///
/// PROVENANCE. Nothing below is invented from nothing:
///   EnterNextEraLabel        verbatim, spec BTN-NEXT-ERA "Enter Next Era"
///                            (docs/audit/AUDIT.md:155)
///   CloseLabel / BackLabel   the wording already shipping in MemoryCardCopy, reused so the
///                            button register across overlays stays consistent
///   EraCompleteHeadingFormat the shipped "{0} ..." chrome-around-an-authored-name shape
///                            settled by CampaignLevelLabel.cs:32-34
///   MemoriesHeading          the register of MemoryCardCopy.WordsHeading ("Words Restored")
///
/// ⚠️ THE ERA ENDING LINE IS NOT HERE, AND MUST NOT BE ADDED HERE.
/// AC-2 of SALIN-253 asks for the Ugat ending line. It does not exist and its cited source
/// does not exist either: docs/audit/BACKLOG.md:563 names "Era sheets 'Era Ending Line'",
/// and the frozen workbook has ten sheets, none of which is an Era sheet.
/// docs/audit/AUDIT.md:115 states it plainly — "No era scene, no paragraph, no ending line."
/// EraConfigSO.cs:10-31 has no field to hold one. It is Filipino NARRATIVE, not chrome, so
/// the drafting precedent that covers this file does not extend to it. The screen ships with
/// the slot empty and the authoring ticket supplies both the line and the field. Drafting a
/// placeholder here would put invented lore in front of a demo player as the last thing they
/// read, which is worse than its absence.
///
/// NO MASTERY SUMMARY, AND NO ACCURACY / STREAK / SCORE STRING — DELIBERATE.
/// The Jira prose asks for a "mastery summary". It is not in the normative acceptance
/// (workbook cell I41; docs/audit/BACKLOG.md:565) — it appears only in cell F41, which on the
/// Jira Alignment sheet is the PROBLEM column, not Final Agreed Behavior. D-021 (LOCKED) cut
/// the accuracy/streak statistic outright, and owner ruling R1 implemented that cut on the
/// sibling Results screen (LevelResultsCopy.cs:22-30). A mastery summary showing accuracy
/// would violate D-021; one showing only restored words would duplicate the Results screen's
/// "Restored:" line. If product wants it back it needs a definition that survives D-021.
///
/// D-006: player-facing prose says "drawing", never "tracing".
///
/// ACTION REQUIRED: product/content review of the wording before release.
/// Approval follow-up: SALIN-291.
/// ============================================================================
/// </summary>
public static class EraCompletionCopy
{
    /// <summary>
    /// {0} = EraConfigSO.eraName, read verbatim. The era names (Ugat, Ugnayan, Pamana) are
    /// authored Filipino; the chrome around them is English, which is the split
    /// CampaignLevelLabel.cs:32-34 already settled.
    /// </summary>
    public const string EraCompleteHeadingFormat = "{0} Complete";

    /// <summary>Spec BTN-NEXT-ERA, verbatim (docs/audit/AUDIT.md:155).</summary>
    public const string EnterNextEraLabel = "Enter Next Era";

    /// <summary>Shown instead of Enter Next Era on the final era. Matches MemoryCardCopy.</summary>
    public const string CloseLabel = "Close";

    /// <summary>Header above the era's memory tiles. Register follows MemoryCardCopy.WordsHeading.</summary>
    public const string MemoriesHeading = "Memories Restored";

    public static string EraCompleteHeading(string eraName) =>
        string.IsNullOrEmpty(eraName) ? string.Empty : string.Format(EraCompleteHeadingFormat, eraName);
}
