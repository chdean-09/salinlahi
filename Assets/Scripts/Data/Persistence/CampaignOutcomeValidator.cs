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
        if (outcome.outcomeSchemaVersion != CampaignProgressOutcome.CurrentOutcomeSchemaVersion)
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
        if (outcome.stars < 1 || outcome.stars > 3)
            return Invalid("The outcome star count is invalid.");

        if (!campaign.TryGetLevel(outcome.levelId, out _) ||
            FindLevel(current, outcome.levelId)?.unlocked != true)
            return Invalid("The outcome level is unknown or locked.");

        if (!ValidateKnownList(outcome.unlockedSymbolIds, value => campaign.TryGetSymbol(value, out _)) ||
            !ValidateCanonicalList(outcome.unlockedMemoryIds) ||
            !ValidateCanonicalList(outcome.claimedRewardIds))
            return Invalid("The outcome collections contain an unknown or duplicate ID.");

        return CampaignSaveValidationResult.Valid();
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
