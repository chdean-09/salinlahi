using System;
using System.Collections.Generic;

public sealed class CampaignSaveValidationResult
{
    public bool IsValid { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ErrorMessage { get; private set; }

    public static CampaignSaveValidationResult Valid()
    {
        return new CampaignSaveValidationResult
        {
            IsValid = true,
            FailureCode = CampaignSaveFailureCode.None,
        };
    }

    public static CampaignSaveValidationResult Invalid(CampaignSaveFailureCode code, string message)
    {
        return new CampaignSaveValidationResult
        {
            IsValid = false,
            FailureCode = code,
            ErrorMessage = message,
        };
    }
}

public static class CampaignSaveValidator
{
    public static CampaignSaveValidationResult Validate(
        CampaignSaveDocument document,
        CampaignConfigSO campaign,
        string validatedLegacyArchiveSha256 = null)
    {
        if (document == null || document.progress == null || document.migration == null || document.recovery == null)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "Required save sections are missing.");
        if (campaign == null || campaign.manifest == null || !campaign.manifest.IsRevisedV1)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidCampaign, "The assigned campaign is not revised v1.");
        if (!string.Equals(document.fileFormat, "salinlahi-campaign-save", StringComparison.Ordinal))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "Unexpected save file format.");
        if (!string.Equals(document.campaignId, campaign.manifest.campaignId, StringComparison.Ordinal))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.WrongIdentity, "The save belongs to another campaign.");
        if (document.contentSchemaVersion != campaign.manifest.contentSchemaVersion)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.WrongIdentity, "The save content schema does not match the campaign.");
        if (document.saveSchemaVersion > CampaignSaveDocument.CurrentSaveSchemaVersion)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.UnsupportedSchema, "The save was created by a newer version.");
        if (document.saveSchemaVersion != CampaignSaveDocument.CurrentSaveSchemaVersion)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "The save schema is not supported.");
        if (!string.Equals(document.transactionState, CampaignSaveTransactionState.Committed, StringComparison.Ordinal) ||
            string.IsNullOrEmpty(document.transactionId) || document.revision < 1)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.IncompleteTransaction, "The save transaction is not committed.");

        List<string> levelIds = GetConfiguredLevelIds(campaign);
        if (levelIds.Count == 0)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidCampaign, "The campaign has no levels.");

        if (document.progress.levelProgress == null ||
            document.progress.unlockedSymbolIds == null ||
            document.progress.discoveredEnemyIds == null ||
            document.progress.discoveredBossIds == null ||
            document.progress.unlockedMemoryIds == null ||
            document.progress.claimedRewardIds == null ||
            document.progress.tutorialProgress == null)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "Progress collections are missing.");

        if (document.progress.levelProgress.Count != levelIds.Count)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "The save does not contain one record per level.");

        HashSet<string> configuredLevels = new HashSet<string>(levelIds, StringComparer.Ordinal);
        if (!ValidateOutcomeReceipts(document.progress, configuredLevels))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "Outcome receipts or journey generation are invalid.");

        HashSet<string> seenLevels = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < document.progress.levelProgress.Count; i++)
        {
            LevelProgressRecord record = document.progress.levelProgress[i];
            if (record == null || !configuredLevels.Contains(record.levelId) || !seenLevels.Add(record.levelId))
                return CampaignSaveValidationResult.Invalid(
                    CampaignSaveFailureCode.InvalidStructure, "Level progress contains an unknown or duplicate ID.");
            if (record.bestStars < 0 || record.bestStars > 3 || (!record.completed && record.bestStars != 0) ||
                (record.completed && (!record.unlocked || record.bestStars < 1)))
                return CampaignSaveValidationResult.Invalid(
                    CampaignSaveFailureCode.InvalidStructure, "Level progress contains an invalid star state.");
        }

        for (int i = 1; i < levelIds.Count; i++)
        {
            LevelProgressRecord previous = FindLevel(document.progress.levelProgress, levelIds[i - 1]);
            LevelProgressRecord current = FindLevel(document.progress.levelProgress, levelIds[i]);
            if (current.unlocked && !previous.completed)
                return CampaignSaveValidationResult.Invalid(
                    CampaignSaveFailureCode.InvalidStructure, "A later level is unlocked before its predecessor.");
        }

        LevelProgressRecord active = FindLevel(document.progress.levelProgress, document.progress.activeLevelId);
        if (active == null || !active.unlocked)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "The active level is not known and unlocked.");
        if (document.progress.endlessModeUnlocked && !FindLevel(document.progress.levelProgress, levelIds[levelIds.Count - 1]).completed)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "Endless mode is unlocked before the final level.");

        if (!ValidateUniqueKnownList(document.progress.unlockedSymbolIds, GetKnownSymbolIds(campaign)) ||
            !ValidateUniqueKnownList(document.progress.discoveredEnemyIds, GetKnownEnemyIds(campaign)) ||
            !ValidateUniqueKnownList(document.progress.discoveredBossIds, GetKnownBossIds(campaign)) ||
            !ValidateCanonicalList(document.progress.unlockedMemoryIds) ||
            !ValidateCanonicalList(document.progress.claimedRewardIds))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "A collection contains an unknown or duplicate ID.");

        HashSet<string> tutorialLevels = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < document.progress.tutorialProgress.Count; i++)
        {
            TutorialProgressRecord tutorial = document.progress.tutorialProgress[i];
            if (tutorial == null || !configuredLevels.Contains(tutorial.levelId) ||
                !tutorialLevels.Add(tutorial.levelId) || tutorial.lastCompletedBeatIndex < -1)
                return CampaignSaveValidationResult.Invalid(
                    CampaignSaveFailureCode.InvalidStructure, "Tutorial progress is invalid.");
        }

        if (document.migration.state != CampaignMigrationState.NotRequired &&
            document.migration.state != CampaignMigrationState.Completed)
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "The migration receipt is invalid.");
        if (document.migration.state == CampaignMigrationState.Completed &&
            (!string.Equals(document.migration.migrationId, "legacy-v0-to-revised-v1", StringComparison.Ordinal) ||
             document.migration.sourceSaveSchemaVersion != 0 ||
             string.IsNullOrEmpty(validatedLegacyArchiveSha256) ||
             !string.Equals(document.migration.legacyArchiveSha256, validatedLegacyArchiveSha256, StringComparison.OrdinalIgnoreCase)))
            return CampaignSaveValidationResult.Invalid(
                CampaignSaveFailureCode.InvalidStructure, "The migration receipt is not tied to the archive.");

        return CampaignSaveValidationResult.Valid();
    }

    public static List<string> GetConfiguredLevelIds(CampaignConfigSO campaign)
    {
        var result = new List<string>();
        if (campaign == null || campaign.eras == null)
            return result;
        for (int i = 0; i < campaign.eras.Count; i++)
        {
            EraConfigSO era = campaign.eras[i];
            if (era?.levels == null)
                continue;
            for (int j = 0; j < era.levels.Count; j++)
            {
                LevelConfigSO level = era.levels[j];
                if (level != null && !string.IsNullOrEmpty(level.stableId))
                    result.Add(level.stableId);
            }
        }
        return result;
    }

    private static LevelProgressRecord FindLevel(List<LevelProgressRecord> records, string id)
    {
        for (int i = 0; i < records.Count; i++)
            if (records[i] != null && string.Equals(records[i].levelId, id, StringComparison.Ordinal))
                return records[i];
        return null;
    }

    private static bool ValidateUniqueKnownList(List<string> values, HashSet<string> known)
    {
        if (values == null)
            return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < values.Count; i++)
            if (!seen.Add(values[i]) || (known.Count > 0 && !known.Contains(values[i])))
                return false;
        return known.Count > 0 || values.Count == 0;
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

    private static bool ValidateOutcomeReceipts(
        CampaignProgressData progress,
        HashSet<string> configuredLevels)
    {
        if (!ContentIdentity.IsCanonical(progress.journeyGenerationId) ||
            progress.appliedOutcomeReceipts == null)
            return false;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < progress.appliedOutcomeReceipts.Count; i++)
        {
            AppliedOutcomeReceipt receipt = progress.appliedOutcomeReceipts[i];
            if (receipt == null || !ContentIdentity.IsCanonical(receipt.outcomeId) ||
                !seen.Add(receipt.outcomeId) || !configuredLevels.Contains(receipt.levelId) ||
                !DateTime.TryParse(receipt.appliedAtUtc, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed) ||
                parsed.Kind != DateTimeKind.Utc)
                return false;
        }
        return true;
    }

    private static HashSet<string> GetKnownSymbolIds(CampaignConfigSO campaign)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (campaign?.symbols == null) return result;
        for (int i = 0; i < campaign.symbols.Count; i++)
            if (campaign.symbols[i] != null) result.Add(campaign.symbols[i].stableId);
        return result;
    }

    private static HashSet<string> GetKnownEnemyIds(CampaignConfigSO campaign)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        List<string> levelIds = GetConfiguredLevelIds(campaign);
        for (int i = 0; i < levelIds.Count; i++)
            if (campaign.TryGetLevel(levelIds[i], out LevelConfigSO level) && level.allowedEnemyTypes != null)
                for (int j = 0; j < level.allowedEnemyTypes.Count; j++)
                    if (level.allowedEnemyTypes[j] != null) result.Add(level.allowedEnemyTypes[j].enemyID);
        return result;
    }

    private static HashSet<string> GetKnownBossIds(CampaignConfigSO campaign)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        List<string> levelIds = GetConfiguredLevelIds(campaign);
        for (int i = 0; i < levelIds.Count; i++)
            if (campaign.TryGetLevel(levelIds[i], out LevelConfigSO level) && level.bossConfig != null)
                result.Add(level.bossConfig.bossID);
        return result;
    }
}
