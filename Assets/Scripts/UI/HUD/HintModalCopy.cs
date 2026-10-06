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
    /// <summary>Short modal title, retaining the question form.</summary>
    public const string Title = "Gumamit ng gabay?";

    /// <summary>Compact confirmation control label.</summary>
    public const string ConfirmLabel = "Ipakita";

    /// <summary>Cancel control. Cancel is a no-op by AC-2.</summary>
    public const string CancelLabel = "Kansela";

    /// <summary>
    /// The authored Filipino synonyms, without the answer itself.
    /// </summary>
    public const string HintOptionLabel = "Ipakita ang kasingkahulugan";

    /// <summary>Hint control label once the budget is spent. Verbatim from BTN-HINT.</summary>
    public const string ExhaustedButtonLabel = "Wala nang Pahiwatig";

    /// <summary>Default hint control label while a hint is still available.</summary>
    public const string AvailableButtonLabel = "Gabay";

    /// <summary>Legacy retry label retained for callers; the exhausted panel offers Close only.</summary>
    public const string RetryLabel = "Ulitin";

    /// <summary>Dismiss control on a panel that has nothing to confirm.</summary>
    public const string CloseLabel = "Isara";

    /// <summary>
    /// Shown when the level meters hints (tier 5). The unit is points of the 0-100
    /// metric.score; the noun is taken from LevelResultsCopy.ScoreLabel.
    /// </summary>
    public static string CostLine(int scorePoints) => "May halagang " + scorePoints + " puntos.";

    /// <summary>
    /// Shown on tiers 1-4, where ChallengeTierPolicy.ForTier leaves the budget disabled and
    /// hints are unlimited and free. Stating that is honest; suppressing the modal there
    /// would make Levels 1-4 behave differently from Level 5 for no reason the player can see.
    /// </summary>
    public const string FreeLine = "Walang bayad sa antas na ito.";

    /// <summary>Remaining-budget disclosure, shown only when the budget is metered.</summary>
    public static string RemainingLine(int remaining) => "Natitirang pahiwatig: " + remaining;

    /// <summary>
    /// Exhausted panel body. Checkpoint resets preserve the spent level-attempt hint budget.
    ///
    /// ⚠️ AC-4 also asks the exhausted state to offer REVIEW. That half is deliberately NOT
    /// built. Review has no in-encounter destination, and routing the player out to a
    /// practice surface would rebuild the pre-combat practice gate D-004 (LOCKED) deleted,
    /// in the other direction. Naming a destination is an owner decision the Decision Log
    /// does not cover. Held and escalated rather than invented.
    /// </summary>
    public const string ExhaustedBody =
        "Nagamit mo na ang pahiwatig para sa antas na ito.";

    /// <summary>Shown when the unit has no focus word to explain, so nothing was charged.</summary>
    public const string NoHintAvailableBody = "Walang pahiwatig para sa hakbang na ito.";

    /// <summary>Prefix for the persistent in-encounter hint line. Kept from the shipped
    /// ChallengeModeUI status register, which already read "Hint: ...".</summary>
    public const string HintStatusPrefix = "Pahiwatig: ";

    /// <summary>The same synonyms persist after the modal closes.</summary>
    public static string HintStatusLine(string hintText) => HintStatusPrefix + hintText;

    /// <summary>"Costs 10 score." from the policy's 0.10 fraction.</summary>
    public static int ScorePointsFromFraction(float costFraction) =>
        Mathf.RoundToInt(Mathf.Clamp01(costFraction) * 100f);
}
