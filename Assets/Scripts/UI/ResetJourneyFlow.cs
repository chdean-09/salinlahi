using System;

public enum ResetJourneyOutcome
{
    Succeeded,
    RetryableFailure,
}

/// <summary>
/// Pure decision logic and player-facing copy for the intentional Reset Journey flow
/// (SALIN-142). The persistence work itself is ProgressManager.ClearAllProgress, which
/// wraps the atomic CampaignOutcomeCoordinator.TryResetJourney transaction.
/// </summary>
public static class ResetJourneyFlow
{
    public const string ConfirmTitle = "Reset your journey?";
    public const string ConfirmBody =
        "This will clear: level progress and stars, restored words and symbols, " +
        "unlocked memories, character unlocks, and Endless Mode. " +
        "This will keep: your audio settings and your journey's update history. " +
        "This cannot be undone.";
    public const string ConfirmButtonLabel = "Reset Journey";
    public const string CancelButtonLabel = "Cancel";

    public const string SuccessTitle = "Journey reset";
    public const string SuccessBody = "Your journey has been reset. Your adventure starts fresh.";
    public const string ContinueButtonLabel = "Continue";

    public const string FailureTitle = "Reset could not be completed";
    public const string FailureBody =
        "The reset could not be completed. Your progress was not changed. " +
        "Check device storage and try again.";
    public const string RetryButtonLabel = "Retry";
    public const string CloseButtonLabel = "Close";

    public static bool CanOfferReset(SaveManagerMode mode)
    {
        return mode == SaveManagerMode.RevisedReady;
    }

    public static ResetJourneyOutcome Classify(CampaignOutcomeCommitResult result)
    {
        return result != null && result.IsAccepted
            ? ResetJourneyOutcome.Succeeded
            : ResetJourneyOutcome.RetryableFailure;
    }

    public static ResetJourneyOutcome Execute(
        Func<SaveManagerMode> modeProvider,
        Action clearAllProgress,
        Func<CampaignOutcomeCommitResult> lastResultProvider)
    {
        if (modeProvider == null || clearAllProgress == null || lastResultProvider == null)
            return ResetJourneyOutcome.RetryableFailure;
        // Re-check availability at execution time: a stale accepted LastOutcomeResult
        // from startup must not be misread as a successful reset.
        if (!CanOfferReset(modeProvider()))
            return ResetJourneyOutcome.RetryableFailure;
        clearAllProgress();
        return Classify(lastResultProvider());
    }

    public static ResetJourneyOutcome Execute()
    {
        if (SaveManager.Instance == null || ProgressManager.Instance == null)
            return ResetJourneyOutcome.RetryableFailure;
        return Execute(
            () => SaveManager.Instance.Mode,
            ProgressManager.Instance.ClearAllProgress,
            () => SaveManager.Instance.LastOutcomeResult);
    }
}
