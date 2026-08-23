using System;
using System.Collections.Generic;

public static class CampaignOutcomeValidator
{
    public static CampaignSaveValidationResult Validate(
        CampaignProgressOutcome outcome,
        CampaignConfigSO campaign,
        CampaignSaveDocument current)
    {
        if (outcome == null || current == null || current.progress == null)
            return Invalid("The outcome or current save is missing.");
        if (campaign == null || campaign.manifest == null || !campaign.manifest.IsRevisedV1)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidCampaign, "The assigned campaign is not revised v1.");
        if (outcome.outcomeSchemaVersion < CampaignProgressOutcome.MinimumOutcomeSchemaVersion ||
            outcome.outcomeSchemaVersion > CampaignProgressOutcome.CurrentOutcomeSchemaVersion)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.UnsupportedSchema, "The outcome schema is not supported.");
        if (!ContentIdentity.IsCanonical(outcome.outcomeId) ||
            !ContentIdentity.IsCanonical(outcome.journeyGenerationId) ||
            !ContentIdentity.IsCanonical(outcome.campaignId) ||
            !ContentIdentity.IsCanonical(outcome.levelId) ||
            !DateTime.TryParse(outcome.completedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out DateTime completedAt) ||
            completedAt.Kind != DateTimeKind.Utc)
            return Invalid("The outcome contains a non-canonical identity or timestamp.");
        if (!string.Equals(outcome.campaignId, campaign.manifest.campaignId, StringComparison.Ordinal) ||
            !string.Equals(outcome.campaignId, current.campaignId, StringComparison.Ordinal) ||
            outcome.contentSchemaVersion != campaign.manifest.contentSchemaVersion ||
            outcome.contentSchemaVersion != current.contentSchemaVersion)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.WrongIdentity, "The outcome belongs to another campaign or content schema.");
        if (!string.Equals(outcome.journeyGenerationId, current.progress.journeyGenerationId, StringComparison.Ordinal))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.WrongIdentity, "The outcome belongs to another journey generation.");
        if (outcome.sessionKind == LearningSessionKind.LevelAttempt)
        {
            if (outcome.stars < 1 || outcome.stars > 3)
                return Invalid("The outcome star count is invalid.");
        }
        else if (outcome.stars != 0 ||
                 outcome.unlockedSymbolIds.Count > 0 ||
                 outcome.unlockedMemoryIds.Count > 0 ||
                 outcome.claimedRewardIds.Count > 0)
        {
            return Invalid("A non-level outcome may not change progression.");
        }

        if (!campaign.TryGetLevel(outcome.levelId, out _) ||
            FindLevel(current, outcome.levelId)?.unlocked != true)
            return Invalid("The outcome level is unknown or locked.");

        if (!ValidateKnownList(outcome.unlockedSymbolIds, value => campaign.TryGetSymbol(value, out _)) ||
            !ValidateCanonicalList(outcome.unlockedMemoryIds) ||
            !ValidateCanonicalList(outcome.claimedRewardIds))
            return Invalid("The outcome collections contain an unknown or duplicate ID.");

        if (!ValidateEvidence(outcome.evidence, campaign, current))
            return Invalid("The outcome evidence is invalid.");

        return CampaignSaveValidationResult.Valid();
    }

    /// <summary>
    /// Stamps a version 1 outcome loaded from a journal written by an older build. Without this the
    /// version check would silently discard an in-flight level completion on upgrade.
    /// </summary>
    public static void UpgradeToCurrent(CampaignProgressOutcome outcome)
    {
        if (outcome == null || outcome.outcomeSchemaVersion != 1)
            return;

        outcome.sessionKind = LearningSessionKind.LevelAttempt;
        if (outcome.evidence == null)
            outcome.evidence = new LearningEvidenceBatch();
        outcome.outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion;
    }

    private static bool ValidateEvidence(
        LearningEvidenceBatch batch, CampaignConfigSO campaign, CampaignSaveDocument current)
    {
        if (batch == null || batch.entries == null || batch.instructedContentIds == null)
            return false;

        for (int i = 0; i < batch.instructedContentIds.Count; i++)
            if (!ContentIdentity.IsCanonical(batch.instructedContentIds[i]))
                return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < batch.entries.Count; i++)
        {
            LearningEvidenceEntry entry = batch.entries[i];
            if (entry == null ||
                !ContentIdentity.IsCanonical(entry.contentId) ||
                !MasteryDimensions.IsApplicable(entry.contentKind, entry.dimension) ||
                !seen.Add(entry.contentId + "|" + entry.dimension) ||
                entry.attemptCount < 0 ||
                entry.successCount < 0 ||
                entry.retrievalSuccessCount < 0 ||
                entry.successCount > entry.attemptCount ||
                entry.retrievalSuccessCount > entry.successCount ||
                !IsKnownContent(campaign, entry.contentId, entry.contentKind))
                return false;

            // A symbol may carry evidence only when it is already unlocked or is being introduced
            // by this same batch. Spec 6.3.
            if (entry.contentKind == LearningContentKind.Symbol &&
                !current.progress.unlockedSymbolIds.Contains(entry.contentId) &&
                !batch.instructedContentIds.Contains(entry.contentId))
                return false;
        }

        return true;
    }

    private static bool IsKnownContent(
        CampaignConfigSO campaign, string contentId, LearningContentKind kind)
    {
        if (kind == LearningContentKind.Symbol)
            return campaign.TryGetSymbol(contentId, out _);

        List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(campaign);
        for (int i = 0; i < levelIds.Count; i++)
        {
            if (!campaign.TryGetLevel(levelIds[i], out LevelConfigSO level) || level.focusWords == null)
                continue;
            for (int j = 0; j < level.focusWords.Count; j++)
                if (string.Equals(level.focusWords[j]?.stableId, contentId, StringComparison.Ordinal))
                    return true;
        }

        return false;
    }

    private static CampaignSaveValidationResult Invalid(string message)
    {
        return CampaignSaveValidationResult.Invalid(CampaignSaveFailureCode.InvalidStructure, message);
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

    private static bool ValidateKnownList(List<string> values, Func<string, bool> known)
    {
        if (values == null)
            return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
            if (!ContentIdentity.IsCanonical(values[i]) || !seen.Add(values[i]) || !known(values[i]))
                return false;
        return true;
    }

    private static bool ValidateCanonicalList(List<string> values)
    {
        if (values == null)
            return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
            if (!ContentIdentity.IsCanonical(values[i]) || !seen.Add(values[i]))
                return false;
        return true;
    }
}
