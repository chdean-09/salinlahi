using System;
using System.Collections.Generic;

public sealed class CampaignOutcomeCoordinator
{
    private readonly CampaignSaveService _service;
    private readonly CampaignOutcomeJournal _journal;
    private readonly CampaignConfigSO _campaign;
    private readonly ITransactionMetadataProvider _metadata;

    public CampaignOutcomeCoordinator(
        CampaignSaveService service,
        CampaignOutcomeJournal journal,
        CampaignConfigSO campaign,
        ITransactionMetadataProvider metadata = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _journal = journal ?? throw new ArgumentNullException(nameof(journal));
        _campaign = campaign ?? throw new ArgumentNullException(nameof(campaign));
        _metadata = metadata ?? new SystemTransactionMetadataProvider();
    }

    public CampaignOutcomeCommitResult TryCommit(CampaignProgressOutcome outcome)
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");

        CampaignSaveValidationResult validation = CampaignOutcomeValidator.Validate(
            outcome, _campaign, _service.Current);
        if (!validation.IsValid)
            return CampaignOutcomeCommitResult.Rejected(
                outcome, validation.FailureCode, "outcome-invalid");

        if (HasReceipt(_service.Current, outcome.outcomeId))
        {
            if (!_journal.Clear())
                return CampaignOutcomeCommitResult.PendingRetry(
                    outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
            return CampaignOutcomeCommitResult.AlreadyCommitted(outcome);
        }

        CampaignOutcomeJournalLoadResult unresolved = _journal.TryLoadRecoverable(_service.Current);
        if (unresolved.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, unresolved.FailureCode, unresolved.ReasonCode);
        if (unresolved.Status == CampaignOutcomeCommitStatus.PendingRetry &&
            !SameOutcome(unresolved.Outcome, outcome))
            return CampaignOutcomeCommitResult.Blocked(
                outcome, CampaignSaveFailureCode.InvalidStructure, "different-pending-outcome");

        CampaignOutcomeJournalWriteResult journalResult = _journal.TryPersist(
            outcome, _service.Current);
        if (!journalResult.Success)
            return ResolveJournalFailure(outcome, journalResult.FailureCode, journalResult.ReasonCode);

        return CommitJournaledOutcome(outcome);
    }

    public CampaignOutcomeCommitResult RetryPending()
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");
        CampaignOutcomeJournalLoadResult loaded = _journal.TryLoadRecoverable(_service.Current);
        if (loaded.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(null, loaded.FailureCode, loaded.ReasonCode);
        if (loaded.Status != CampaignOutcomeCommitStatus.PendingRetry || loaded.Outcome == null)
            return CampaignOutcomeCommitResult.Rejected(null, loaded.FailureCode, loaded.ReasonCode);
        CampaignOutcomeValidator.UpgradeToCurrent(loaded.Outcome);
        if (HasReceipt(_service.Current, loaded.Outcome.outcomeId))
        {
            if (!_journal.Clear())
                return CampaignOutcomeCommitResult.PendingRetry(
                    loaded.Outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
            return CampaignOutcomeCommitResult.AlreadyCommitted(loaded.Outcome);
        }
        return CommitJournaledOutcome(loaded.Outcome);
    }

    public CampaignOutcomeCommitResult ReplayPendingOnStartup()
    {
        return RetryPending();
    }

    public CampaignOutcomeCommitResult TryResetJourney()
    {
        if (_service.Current == null)
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "save-service-not-ready");

        CampaignSaveDocument clean = CampaignProgressFactory.CreateClean(_campaign, _metadata.UtcNow);
        CampaignMigrationReceipt migration = _service.Current.migration;
        CampaignRecoveryReceipt recovery = _service.Current.recovery;
        CampaignSaveCommitResult committed = _service.TryCommit(document =>
        {
            document.progress = clean.progress;
            document.migration = migration;
            document.recovery = recovery;
        });
        if (!committed.Success)
            return CampaignOutcomeCommitResult.Blocked(
                null, committed.FailureCode, "reset-save-failed");
        if (!string.Equals(
                committed.Document.progress.journeyGenerationId,
                clean.progress.journeyGenerationId,
                StringComparison.Ordinal))
            return CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "reset-generation-verification-failed");
        if (!_journal.Clear())
            return CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
        return CampaignOutcomeCommitResult.Committed(null);
    }

    private CampaignOutcomeCommitResult CommitJournaledOutcome(CampaignProgressOutcome outcome)
    {
        CampaignSaveCommitResult committed = _service.TryCommit(document => ApplyOutcome(document, outcome));
        if (!committed.Success)
            return ResolveJournalFailure(outcome, committed.FailureCode, "campaign-save-failed");
        if (!VerifyPublishedOutcome(committed.Document, outcome))
            return CampaignOutcomeCommitResult.PendingRetry(
                outcome, CampaignSaveFailureCode.InvalidStructure, "published-outcome-verification-failed");
        if (!_journal.Clear())
            return CampaignOutcomeCommitResult.PendingRetry(
                outcome, CampaignSaveFailureCode.IoFailure, "journal-clear-failed");
        return CampaignOutcomeCommitResult.Committed(outcome);
    }

    private CampaignOutcomeCommitResult ResolveJournalFailure(
        CampaignProgressOutcome outcome,
        CampaignSaveFailureCode fallbackCode,
        string fallbackReason)
    {
        CampaignOutcomeJournalLoadResult recovered = _journal.TryLoadRecoverable(_service.Current);
        if (recovered.Status == CampaignOutcomeCommitStatus.PendingRetry)
            return CampaignOutcomeCommitResult.PendingRetry(
                recovered.Outcome, fallbackCode == CampaignSaveFailureCode.None
                    ? CampaignSaveFailureCode.IoFailure : fallbackCode,
                fallbackReason);
        if (recovered.Status == CampaignOutcomeCommitStatus.Blocked)
            return CampaignOutcomeCommitResult.Blocked(
                outcome, recovered.FailureCode, recovered.ReasonCode);
        return CampaignOutcomeCommitResult.Rejected(
            outcome, fallbackCode == CampaignSaveFailureCode.None
                ? CampaignSaveFailureCode.IoFailure : fallbackCode, fallbackReason);
    }

    private static bool HasReceipt(CampaignSaveDocument document, string outcomeId)
    {
        if (document?.progress?.appliedOutcomeReceipts == null)
            return false;
        for (int i = 0; i < document.progress.appliedOutcomeReceipts.Count; i++)
        {
            AppliedOutcomeReceipt receipt = document.progress.appliedOutcomeReceipts[i];
            if (receipt != null && string.Equals(receipt.outcomeId, outcomeId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static LevelProgressRecord FindLevel(CampaignSaveDocument document, string levelId)
    {
        if (document?.progress?.levelProgress == null)
            return null;
        for (int i = 0; i < document.progress.levelProgress.Count; i++)
        {
            LevelProgressRecord record = document.progress.levelProgress[i];
            if (record != null && string.Equals(record.levelId, levelId, StringComparison.Ordinal))
                return record;
        }
        return null;
    }

    private static void UnionSorted(List<string> target, List<string> additions)
    {
        if (additions == null)
            return;
        for (int i = 0; i < additions.Count; i++)
            if (!target.Contains(additions[i]))
                target.Add(additions[i]);
        target.Sort(StringComparer.Ordinal);
    }

    private void ApplyOutcome(CampaignSaveDocument document, CampaignProgressOutcome outcome)
    {
        if (outcome.sessionKind == LearningSessionKind.LevelAttempt)
            ApplyLevelProgression(document, outcome);

        LearningProgressWriter.Apply(
            document.progress, outcome.evidence, _campaign.learningTuning);

        document.progress.appliedOutcomeReceipts.Add(new AppliedOutcomeReceipt(
            outcome.outcomeId,
            outcome.levelId,
            _metadata.UtcNow.ToUniversalTime().ToString("O"),
            outcome.sessionKind));
        PruneReceipts(document.progress, outcome.outcomeId);
        document.progress.appliedOutcomeReceipts.Sort((left, right) =>
            string.CompareOrdinal(left?.outcomeId, right?.outcomeId));
    }

    /// <summary>
    /// LevelAttempt receipts are the durable idempotency record and are always kept. Practice and
    /// review receipts are bounded, because the journal only ever holds one pending outcome so the
    /// deduplication window is one outcome deep. The receipt just written is never a candidate -
    /// evicting it would fail VerifyPublishedOutcome's HasReceipt check and wedge the journal.
    /// </summary>
    private static void PruneReceipts(CampaignProgressData progress, string protectedOutcomeId)
    {
        const int MaxNonLevelReceipts = 32;

        var candidates = new List<AppliedOutcomeReceipt>();
        for (int i = 0; i < progress.appliedOutcomeReceipts.Count; i++)
        {
            AppliedOutcomeReceipt receipt = progress.appliedOutcomeReceipts[i];
            if (receipt == null ||
                receipt.sessionKind == LearningSessionKind.LevelAttempt ||
                string.Equals(receipt.outcomeId, protectedOutcomeId, StringComparison.Ordinal))
                continue;
            candidates.Add(receipt);
        }

        // Candidates are taken in existing list order, which after the previous commit's sort is
        // ordinal by outcomeId. Do not sort by appliedAtUtc: the test metadata provider returns a
        // constant, so every timestamp is identical and List<T>.Sort is unstable.
        int excess = candidates.Count + 1 - MaxNonLevelReceipts;
        for (int i = 0; i < excess && i < candidates.Count; i++)
            progress.appliedOutcomeReceipts.Remove(candidates[i]);
    }

    private void ApplyLevelProgression(CampaignSaveDocument document, CampaignProgressOutcome outcome)
    {
        LevelProgressRecord level = FindLevel(document, outcome.levelId);
        level.completed = true;
        level.unlocked = true;
        level.bestStars = Math.Max(level.bestStars, outcome.stars);

        List<string> levels = CampaignSaveValidator.GetConfiguredLevelIds(_campaign);
        int index = levels.IndexOf(outcome.levelId);
        if (index >= 0 && index + 1 < levels.Count)
            FindLevel(document, levels[index + 1]).unlocked = true;
        else if (index == levels.Count - 1)
            document.progress.endlessModeUnlocked = true;

        UnionSorted(document.progress.unlockedSymbolIds, outcome.unlockedSymbolIds);
        UnionSorted(document.progress.unlockedMemoryIds, outcome.unlockedMemoryIds);
        UnionSorted(document.progress.claimedRewardIds, outcome.claimedRewardIds);
        ApplyBestMetrics(level, outcome);
    }

    /// <summary>
    /// SALIN-140. Records the attempt's metrics inside the same commit as completion, stars and
    /// unlocks, so there is one transaction rather than a second write that could land alone.
    /// </summary>
    /// <remarks>
    /// Score and metric set move together and only on improvement, so the stored set always describes
    /// a single real attempt. A first completion always writes, including a zero score -- otherwise a
    /// legitimately scoreless run would leave the record looking as though nothing was ever played.
    /// </remarks>
    private static void ApplyBestMetrics(LevelProgressRecord level, CampaignProgressOutcome outcome)
    {
        if (level.bestMetrics == null)
            level.bestMetrics = new List<LevelMetricRecord>();

        float score = ScoreOf(outcome.metrics);
        bool firstCompletion = level.bestMetrics.Count == 0;
        if (!firstCompletion && score <= level.bestScore)
            return;

        level.bestScore = score;
        level.bestMetrics.Clear();
        if (outcome.metrics == null)
            return;

        for (int i = 0; i < outcome.metrics.Count; i++)
        {
            LevelMetricRecord metric = outcome.metrics[i];
            if (metric != null && !string.IsNullOrEmpty(metric.metricId))
                level.bestMetrics.Add(new LevelMetricRecord(metric.metricId, metric.value));
        }

        level.bestMetrics.Sort((left, right) =>
            string.CompareOrdinal(left?.metricId, right?.metricId));
    }

    private static float ScoreOf(List<LevelMetricRecord> metrics)
    {
        if (metrics == null)
            return 0f;

        for (int i = 0; i < metrics.Count; i++)
            if (metrics[i] != null &&
                string.Equals(metrics[i].metricId, LevelResultsCalculator.ScoreMetricId, StringComparison.Ordinal))
                return metrics[i].value;

        return 0f;
    }

    private bool VerifyPublishedOutcome(CampaignSaveDocument document, CampaignProgressOutcome outcome)
    {
        if (document == null || !HasReceipt(document, outcome.outcomeId))
            return false;

        if (!VerifyEvidenceApplied(document, outcome.evidence))
            return false;

        if (outcome.sessionKind != LearningSessionKind.LevelAttempt)
            return true;

        LevelProgressRecord level = FindLevel(document, outcome.levelId);
        if (level == null || !level.completed || level.bestStars < outcome.stars)
            return false;
        if (!VerifyMetricsApplied(level, outcome))
            return false;
        List<string> levels = CampaignSaveValidator.GetConfiguredLevelIds(_campaign);
        int index = levels.IndexOf(outcome.levelId);
        if (index >= 0 && index + 1 < levels.Count && !FindLevel(document, levels[index + 1]).unlocked)
            return false;
        if (index == levels.Count - 1 && !document.progress.endlessModeUnlocked)
            return false;
        return ContainsAll(document.progress.unlockedSymbolIds, outcome.unlockedSymbolIds) &&
            ContainsAll(document.progress.unlockedMemoryIds, outcome.unlockedMemoryIds) &&
            ContainsAll(document.progress.claimedRewardIds, outcome.claimedRewardIds);
    }

    /// <summary>
    /// SALIN-140. Without this the commit would report success while the metrics half of the
    /// transaction had silently not landed.
    /// </summary>
    /// <remarks>
    /// The check is the invariant, not equality: after a commit the level either holds this attempt's
    /// metrics or a better attempt's, and both mean the recorded score is at least this attempt's.
    /// Asserting equality would wrongly fail a replay of a weaker attempt against a stronger record.
    /// </remarks>
    private static bool VerifyMetricsApplied(
        LevelProgressRecord level, CampaignProgressOutcome outcome)
    {
        const float Epsilon = 0.0001f;

        int recorded = outcome.metrics?.Count ?? 0;
        if (recorded == 0)
            return true;

        if (level.bestMetrics == null || level.bestMetrics.Count == 0)
            return false;

        return level.bestScore >= ScoreOf(outcome.metrics) - Epsilon;
    }

    private static bool VerifyEvidenceApplied(
        CampaignSaveDocument document, LearningEvidenceBatch batch)
    {
        if (batch?.instructedContentIds == null)
            return true;

        for (int i = 0; i < batch.instructedContentIds.Count; i++)
            if (!HasMasteryRecord(document.progress, batch.instructedContentIds[i]))
                return false;

        return true;
    }

    private static bool HasMasteryRecord(CampaignProgressData progress, string contentId)
    {
        for (int i = 0; i < progress.symbolMastery.Count; i++)
            if (string.Equals(progress.symbolMastery[i]?.symbolId, contentId, StringComparison.Ordinal))
                return true;
        for (int i = 0; i < progress.wordMastery.Count; i++)
            if (string.Equals(progress.wordMastery[i]?.wordId, contentId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static bool ContainsAll(List<string> values, List<string> expected)
    {
        if (expected == null)
            return true;
        for (int i = 0; i < expected.Count; i++)
            if (!values.Contains(expected[i]))
                return false;
        return true;
    }

    private static bool SameOutcome(CampaignProgressOutcome left, CampaignProgressOutcome right)
    {
        return string.Equals(
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = left }),
            CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = right }),
            StringComparison.Ordinal);
    }
}
