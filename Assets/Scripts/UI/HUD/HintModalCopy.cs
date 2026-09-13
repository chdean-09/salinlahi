using UnityEngine;

/// <summary>
/// ============================================================================
/// SALIN-231 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// Approval is tracked on SALIN-291. Wording is a grounded draft, not a ruling.
/// ============================================================================
/// Every string the hint modal can show lives here and nowhere else, so
/// product/content can rewrite the wording without touching flow logic. Mirrors
/// <see cref="LevelResultsCopy"/> (SALIN-234), <see cref="CampaignSaveNoticeCopy"/>
/// (SALIN-272) and <see cref="LevelLockNoticeCopy"/> (SALIN-137).
///
/// LANGUAGE: English, matching the surface this attaches to. ChallengeModeUI ships
/// English throughout ("Hint", "Retry", "Exit", "Try again. Correct progress is safe.",
/// "Clues:", "No timer"), so the modal that opens from its Hint button is English too.
/// This is NOT a project-wide default: the in-lesson HUD ships Filipino
/// (SymbolLearningCardController.cs "Pakinggan", "Magpatuloy"). The register was matched
/// to the neighbours of this surface rather than decided globally.
///
/// ⚠️ SCORE, NOT STARS. docs/audit/BACKLOG.md:293 describes "a modal listing hint types
/// with their star cost". That is wrong about the unit and the copy here deliberately
/// does not follow it. LevelResultsCalculator derives stars from the hearts ratio and the
/// two accuracies alone; the emergency-hint penalty is applied to metric.score only. A
/// modal saying "costs 1 star" would lie to the player. The calculator is PINNED by
/// LevelResultsScoringWeightPinTests, so making the backlog's wording true by changing
/// the engine is a STOP AND ASK, not an edit. Flagged for a docs correction.
/// ============================================================================
/// </summary>
public static class HintModalCopy
{
    /// <summary>Modal title. Grounded in UF-25's own "Use This Hint" phrasing.</summary>
    public const string Title = "Use a Hint?";

    /// <summary>Confirm control. Verbatim from UF-25 (docs/audit/AUDIT.md:106).</summary>
    public const string ConfirmLabel = "Use This Hint";

    /// <summary>Cancel control. Verbatim from UF-25. Cancel is a no-op by AC-2.</summary>
    public const string CancelLabel = "Cancel";

    /// <summary>
    /// The one hint type the authored data can actually serve: the focus word's approved
    /// plain-language meaning (FocusWordDefinition.meaning). "explain" is the Global
    /// "Hints" verb (docs/audit/AUDIT.md:169).
    /// </summary>
    public const string MeaningOptionLabel = "Show the meaning";

    /// <summary>Hint control label once the budget is spent. Verbatim from BTN-HINT.</summary>
    public const string ExhaustedButtonLabel = "No Hints Left";

    /// <summary>Default hint control label while a hint is still available.</summary>
    public const string AvailableButtonLabel = "Hint";

    /// <summary>Retry control on the exhausted panel. Matches ChallengeModeUI's own "Retry".</summary>
    public const string RetryLabel = "Retry";

    /// <summary>Dismiss control on a panel that has nothing to confirm.</summary>
    public const string CloseLabel = "Close";

    /// <summary>
    /// Shown when the level meters hints (tier 5). The unit is points of the 0-100
    /// metric.score; the noun is taken from LevelResultsCopy.ScoreLabel.
    /// </summary>
    public static string CostLine(int scorePoints) => "Costs " + scorePoints + " score.";

    /// <summary>
    /// Shown on tiers 1-4, where ChallengeTierPolicy.ForTier leaves the budget disabled and
    /// hints are unlimited and free. Stating that is honest; suppressing the modal there
    /// would make Levels 1-4 behave differently from Level 5 for no reason the player can see.
    /// </summary>
    public const string FreeLine = "No cost on this level.";

    /// <summary>Remaining-budget disclosure, shown only when the budget is metered.</summary>
    public static string RemainingLine(int remaining) => "Hints left: " + remaining;

    /// <summary>
    /// Exhausted panel body (AC-4). Every clause maps to shipped behaviour:
    /// ChallengeSession.Retry -> ResetToCheckpoint restores the checkpoint with full clues.
    ///
    /// ⚠️ AC-4 also asks the exhausted state to offer REVIEW. That half is deliberately NOT
    /// built. Review has no in-encounter destination, and routing the player out to a
    /// practice surface would rebuild the pre-combat practice gate D-004 (LOCKED) deleted,
    /// in the other direction. Naming a destination is an owner decision the Decision Log
    /// does not cover. Held and escalated rather than invented.
    /// </summary>
    public const string ExhaustedBody =
        "You have used this level's hint. Retry the checkpoint to try again.";

    /// <summary>
    /// The revealed meaning, after the player confirms. Keeps the word beside its meaning
    /// the way FocusWordPreviewController.cs:72-73 and MemoryCardUI already render the pair.
    /// </summary>
    public static string MeaningReveal(string displayLabel, string meaning) =>
        string.IsNullOrEmpty(displayLabel) ? meaning : displayLabel + " — " + meaning;

    /// <summary>Shown when the unit has no focus word to explain, so nothing was charged.</summary>
    public const string NoHintAvailableBody = "No hint is available for this step.";

    /// <summary>Prefix for the persistent in-encounter hint line. Kept from the shipped
    /// ChallengeModeUI status register, which already read "Hint: ...".</summary>
    public const string HintStatusPrefix = "Hint: ";

    /// <summary>"Hint: IBA — different". The in-encounter record of a purchased hint.</summary>
    public static string HintStatusLine(string displayLabel, string meaning) =>
        HintStatusPrefix + MeaningReveal(displayLabel, meaning);

    /// <summary>"Costs 10 score." from the policy's 0.10 fraction.</summary>
    public static int ScorePointsFromFraction(float costFraction) =>
        Mathf.RoundToInt(Mathf.Clamp01(costFraction) * 100f);
}
