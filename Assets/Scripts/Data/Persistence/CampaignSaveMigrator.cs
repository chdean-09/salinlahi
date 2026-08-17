using System;
using System.Collections.Generic;

public sealed class CampaignSaveMigrationResult
{
    public bool Success { get; private set; }
    public CampaignSaveDocument Document { get; private set; }
    public CampaignSaveFailureCode FailureCode { get; private set; }
    public string ErrorMessage { get; private set; }

    public static CampaignSaveMigrationResult Succeeded(CampaignSaveDocument document) =>
        new CampaignSaveMigrationResult
        {
            Success = true,
            Document = document,
            FailureCode = CampaignSaveFailureCode.None,
        };

    public static CampaignSaveMigrationResult Failed(
        CampaignSaveFailureCode code,
        string message) => new CampaignSaveMigrationResult
        {
            Success = false,
            FailureCode = code,
            ErrorMessage = message,
        };
}

public static class CampaignSaveMigrator
{
    public static CampaignSaveMigrationResult TryUpgradeV1(
        CampaignSaveDocument source,
        CampaignConfigSO campaign,
        string journeyGenerationId)
    {
        if (source == null)
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.Missing, "The source save is missing.");
        if (campaign == null || campaign.manifest == null || !campaign.manifest.IsRevisedV1)
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.InvalidCampaign, "The assigned campaign is not revised v1.");
        if (source.saveSchemaVersion > CampaignSaveDocument.CurrentSaveSchemaVersion)
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.UnsupportedSchema, "The save was created by a newer version.");
        if (source.saveSchemaVersion != 1 && source.saveSchemaVersion != CampaignSaveDocument.CurrentSaveSchemaVersion)
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "The save schema is not supported.");
        if (!ContentIdentity.IsCanonical(journeyGenerationId) ||
            !journeyGenerationId.StartsWith("journey.", StringComparison.Ordinal))
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "The journey generation is invalid.");

        CampaignSaveDocument candidate = CampaignSaveSerializer.DeepClone(source);
        if (source.saveSchemaVersion == 1)
        {
            candidate.saveSchemaVersion = CampaignSaveDocument.CurrentSaveSchemaVersion;
            candidate.progress.journeyGenerationId = journeyGenerationId;
            candidate.progress.appliedOutcomeReceipts = new List<AppliedOutcomeReceipt>();
        }

        CampaignSaveValidationResult validation = CampaignSaveValidator.Validate(
            candidate, campaign, candidate.migration?.legacyArchiveSha256);
        return validation.IsValid
            ? CampaignSaveMigrationResult.Succeeded(candidate)
            : CampaignSaveMigrationResult.Failed(validation.FailureCode, validation.ErrorMessage);
    }
}
