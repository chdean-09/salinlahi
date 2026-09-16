using System.Collections.Generic;

/// <summary>
/// ============================================================================
/// SALIN-234 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Level Results screen can show lives here, and nowhere else, so
/// product/content can rewrite the wording without touching flow logic. Mirrors
/// <see cref="CampaignSaveNoticeCopy"/> (SALIN-272, PR #216) and
/// <see cref="LevelLockNoticeCopy"/> (SALIN-137), which shipped under the same banner.
///
/// WHY THE STRINGS MOVED: they were concatenated inline inside
/// LevelFlowController.BuildResultsSummary (SALIN-202), where no one could review them
/// as copy and where a D-006 violation sat unnoticed. Extracting them makes both the
/// approval gap and the wording visible.
///
/// LANGUAGE: English. The project splits by role -- UI chrome is English (this class,
/// CampaignSaveNoticeCopy, LevelLockNoticeCopy, LevelContentMissingPanel), narrative
/// content is Filipino (Assets/ScriptableObjects/Dialogue/*). That split was already in
/// force; it is followed here rather than re-decided.
///
/// NO ACCURACY READOUT — OWNER RULING R1 (2026-09-13). D-021 cut the displayed
/// accuracy/streak statistic, so the shipped "Tracing N%   Context N%" line
/// (LevelFlowController.cs:741-742 before this ticket) is gone and no accuracy figure is
/// offered here. The ruling cut the DISPLAY only: tracing accuracy is still weighted at
/// 0.5 and context accuracy at 0.3 of metric.score, and both star thresholds still gate
/// on them (LevelResultsCalculator.cs:43-50). That calculator is deliberately untouched
/// by SALIN-234 — see LevelResultsScoringWeightPinTests, which pins those numbers so a
/// later reader who notices accuracy missing from the UI cannot "tidy up" the calculator
/// to match and silently re-tune which levels award three stars.
///
/// D-006 is satisfied by deletion rather than rewording: the only player-facing
/// "Tracing" prose on this screen was that accuracy line.
///
/// ACTION REQUIRED: product/content review of the wording before release. The language
/// split above is settled; the sentences themselves are not sacred.
/// ============================================================================
/// </summary>
public static class LevelResultsCopy
{
    /// <summary>Label before the earned star count. Unchanged from LevelFlowController.cs:734.</summary>
    public const string StarsLabel = "Stars ";

    /// <summary>Denominator shared by the summary line and the star-count readout. Unchanged.</summary>
    public const string StarsTotalSuffix = "/3";

    /// <summary>Label before the 0-100 score. Unchanged from LevelFlowController.cs:736.</summary>
    public const string ScoreLabel = "Score ";

    /// <summary>
    /// Label before the remaining-hearts count. NEW in SALIN-234 (AC-4).
    /// The noun is taken from the existing runtime register — HeartSystem.cs:66 logs
    /// "Hearts remaining: {n}/{max}" — and the {n}/{max} shape mirrors "Stars n/3" on the
    /// same screen.
    /// </summary>
    public const string HeartsLabel = "Hearts ";

    /// <summary>Separator between a count and its maximum, as in "2/3".</summary>
    public const string OutOfSeparator = "/";

    /// <summary>
    /// Label before the hint count. NEW in SALIN-234 (AC-5). The wording is taken verbatim
    /// from the acceptance criterion's worked example ("Hints 1"), not invented.
    /// </summary>
    public const string HintsLabel = "Hints ";

    /// <summary>
    /// Label before the hint score penalty. NEW in SALIN-231 (AC-3).
    ///
    /// ⚠️ THE UNIT IS SCORE, NOT STARS. docs/audit/BACKLOG.md:293 calls this a "star cost";
    /// that is wrong about the engine. LevelResultsCalculator.cs:46-50 derives stars from the
    /// hearts ratio and the two accuracies alone, and the penalty enters metric.score only
    /// (:43-44). Copy saying "costs a star" would lie to the player. The calculator is PINNED
    /// by LevelResultsScoringWeightPinTests, so making the backlog's phrasing true by editing
    /// the engine is a STOP AND ASK, not an edit — the wording was matched to the engine
    /// instead, and BACKLOG.md:293 is flagged for a docs correction.
    ///
    /// "cost" rather than "penalty" matches the modal's pre-use disclosure
    /// (HintModalCopy.CostLine), so the player sees one noun on both screens.
    /// </summary>
    public const string HintPenaltyLabel = "Hint cost ";

    /// <summary>Label before the restored focus words. Unchanged from LevelFlowController.cs:747.</summary>
    public const string RestoredLabel = "Restored: ";

    /// <summary>Separator between restored focus words. Unchanged from LevelFlowController.cs:750.</summary>
    public const string RestoredSeparator = ", ";

    /// <summary>Label before the newly unlocked symbol count. Unchanged from LevelFlowController.cs:756.</summary>
    public const string NewSymbolsLabel = "New symbols: ";

    /// <summary>
    /// Replay-Level button label (AC-8, BTN-REPLAY). NEW in SALIN-234, taken verbatim from
    /// the ticket summary. Title Case matches the shipped button register
    /// ("Start Journey", MainMenuUI.cs:67).
    /// </summary>
    public const string ReplayLevelLabel = "Replay Level";

    /// <summary>Gap between two readouts on the same line. Unchanged from LevelFlowController.cs:736.</summary>
    public const string InlineSeparator = "   ";

    /// <summary>Line break between summary rows. Unchanged from LevelFlowController.cs:739.</summary>
    public const string LineSeparator = "\n";

    /// <summary>"Stars 2/3" — the summary's first readout.</summary>
    public static string Stars(int stars) => StarsLabel + stars + StarsTotalSuffix;

    /// <summary>"Score 61".</summary>
    public static string Score(int score) => ScoreLabel + score;

    /// <summary>
    /// "Hearts 2/3". Takes the count and maximum directly rather than recovering them from
    /// metric.hearts-ratio: rounding a float back into a heart count is the kind of edit
    /// that fails silently, and the flow already reads both values
    /// (LevelFlowController.ComputeCompletionResults).
    /// </summary>
    public static string Hearts(int remaining, int max) =>
        HeartsLabel + remaining + OutOfSeparator + max;

    /// <summary>"Hints 1".</summary>
    public static string Hints(int hintsUsed) => HintsLabel + hintsUsed;

    /// <summary>
    /// "Hint cost -10". Takes points of the 0-100 score, already converted from
    /// metric.emergency-hint-penalty's 0-1 fraction by the caller.
    /// </summary>
    public static string HintPenalty(int scorePoints) => HintPenaltyLabel + "-" + scorePoints;

    /// <summary>"New symbols: 2".</summary>
    public static string NewSymbols(int count) => NewSymbolsLabel + count;

    /// <summary>"Restored: Ama, Ina". Returns an empty string when there is nothing to list.</summary>
    public static string Restored(IReadOnlyList<string> displayLabels)
    {
        if (displayLabels == null || displayLabels.Count == 0)
            return string.Empty;

        var builder = new System.Text.StringBuilder(RestoredLabel);
        for (int i = 0; i < displayLabels.Count; i++)
        {
            if (i > 0)
                builder.Append(RestoredSeparator);
            builder.Append(displayLabels[i]);
        }
        return builder.ToString();
    }
}
