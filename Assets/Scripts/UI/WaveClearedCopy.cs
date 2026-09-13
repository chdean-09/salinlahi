/// <summary>
/// ============================================================================
/// SALIN-232 — PLACEHOLDER PLAYER-FACING COPY. NOT PRODUCT-APPROVED.
/// ============================================================================
/// Every string the Wave Cleared screen can show lives here, and nowhere else, so
/// product/content can rewrite the wording without touching flow logic. Mirrors
/// <see cref="LevelResultsCopy"/> (SALIN-234), <see cref="CampaignSaveNoticeCopy"/>
/// (SALIN-272) and <see cref="LevelLockNoticeCopy"/> (SALIN-137), which shipped
/// under the same banner.
///
/// LANGUAGE: English. The project splits by role -- UI chrome is English (this class,
/// LevelResultsCopy, CampaignSaveNoticeCopy, LevelLockNoticeCopy,
/// LevelContentMissingPanel), narrative content is Filipino
/// (Assets/ScriptableObjects/Dialogue/*). That split was already in force; it is
/// followed here rather than re-decided.
///
/// NO ACCURACY READOUT — the live SALIN-232 description asks for a "combat accuracy"
/// figure on this screen. DO NOT ADD ONE. Decision D-021 (LOCKED) cut the displayed
/// accuracy/streak statistic, and the Master source of truth's Final Agreed Behavior
/// column (F27) withdrew the earlier "may remain if separately approved" allowance:
/// "no accuracy or streak statistic". Owner ruling R1 (2026-09-13) is already
/// transcribed into merged code at LevelResultsCopy.cs:22-33 and
/// LevelFlowController.cs:745-751. No combat-accuracy metric exists anywhere in the
/// project either — LevelResultsCalculator.cs:23-28 is the complete metric surface.
///
/// The ruling cut the DISPLAY only. Tracing accuracy is still weighted at 0.5 and
/// context accuracy at 0.3 of metric.score (LevelResultsCalculator.cs:43-50), pinned
/// by LevelResultsScoringWeightPinTests. That calculator is deliberately untouched by
/// SALIN-232: a reader who notices accuracy missing from the UI must not "tidy up" the
/// calculator to match and silently re-tune which levels award three stars.
/// WaveClearedScreenTests.NoCopyStringMentionsAccuracyOrTracing pins the absence.
///
/// D-006 ("tracing" -> "drawing" in prose and UI strings) is satisfied by construction:
/// no string here contains "Trac*".
///
/// ACTION REQUIRED: product/content review before release. Two specific concerns, both
/// recorded rather than silently resolved:
///   1. ContinueLabel is reproduced VERBATIM from the ticket description. It names the
///      wrong phase — the button advances into ContextChallenge (phase 6), not
///      MemoryReward (phase 7, LevelPhase.cs:16-17) — and D-003 (LOCKED) retired the
///      post-wave restoration board whose "Restore" vocabulary this label belongs to.
///      Shipped as given because the acceptance criteria are written against it; the
///      wording is product's to settle and changes in one constant.
///   2. BannerLabel is derived, not approved: see its own note below.
/// ============================================================================
/// </summary>
public static class WaveClearedCopy
{
    /// <summary>
    /// Banner heading (AC-1). Derived, not invented: the noun phrase is the ticket
    /// summary's own ("Add the Wave Cleared screen") and matches the existing runtime
    /// vocabulary EventBus.OnWaveCleared (EventBus.cs:24). Title Case matches the
    /// shipped heading/button register — "Replay Level" (LevelResultsCopy.cs:81),
    /// "Start Journey" (MainMenuUI.cs:67).
    /// </summary>
    public const string BannerLabel = "Wave Cleared";

    /// <summary>
    /// Continue-button label (AC-4). VERBATIM from the live ticket description and
    /// docs/audit/BACKLOG.md:298, :301. Not reworded — see the class banner's concern 1
    /// and WaveClearedScreenTests.ContinueLabel_IsTheTicketsLiteralLabel, which pins it.
    /// </summary>
    public const string ContinueLabel = "Restore the Memory";

    /// <summary>
    /// "Hearts 2/3" (AC-2). Deliberately delegates to <see cref="LevelResultsCopy.Hearts"/>
    /// rather than declaring a second hearts string: the register already shipped in
    /// SALIN-234, derived there from HeartSystem.cs:66, and two sources of truth for one
    /// readout is how the two screens drift apart. LevelResultsCopy is read, never edited.
    /// </summary>
    public static string Hearts(int remaining, int max) => LevelResultsCopy.Hearts(remaining, max);
}
