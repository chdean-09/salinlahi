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
    public const string ConfirmTitle = "I-reset ang Paglalakbay mo?";
    public const string ConfirmBody =
        "Mabubura ang progreso at mga bituin sa mga antas, mga naibalik na salita at simbolo, " +
        "mga na-unlock na alaala at tauhan. " +
        "Mananatili ang mga setting ng tunog at kasaysayan ng mga update sa paglalakbay mo. " +
        "Hindi na ito maibabalik.";
    public const string ConfirmButtonLabel = "I-reset ang Paglalakbay";
    public const string CancelButtonLabel = "Kanselahin";

    public const string SuccessTitle = "Na-reset na ang Paglalakbay";
    public const string SuccessBody = "Na-reset na ang Paglalakbay mo. Magsisimula muli ang pakikipagsapalaran mo.";
    public const string ContinueButtonLabel = "Magpatuloy";

    public const string FailureTitle = "Hindi nakumpleto ang pag-reset";
    public const string FailureBody =
        "Hindi nakumpleto ang pag-reset. Hindi nabago ang progreso mo. " +
        "Tingnan ang storage ng device at subukan muli.";
    public const string RetryButtonLabel = "Subukan Muli";
    public const string CloseButtonLabel = "Isara";

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
