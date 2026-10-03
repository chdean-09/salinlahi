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
    public static CampaignSaveMigrationResult TryUpgradeToCurrent(
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
        if (source.saveSchemaVersion < 1)
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "The save schema is not supported.");
        if (!ContentIdentity.IsCanonical(journeyGenerationId) ||
            !journeyGenerationId.StartsWith("journey.", StringComparison.Ordinal))
            return CampaignSaveMigrationResult.Failed(
                CampaignSaveFailureCode.InvalidStructure, "The journey generation is invalid.");

        CampaignSaveDocument candidate = CampaignSaveSerializer.DeepClone(source);

        if (candidate.saveSchemaVersion == 1)
        {
            candidate.progress.journeyGenerationId = journeyGenerationId;
            candidate.progress.appliedOutcomeReceipts = new List<AppliedOutcomeReceipt>();
            candidate.saveSchemaVersion = 2;
        }

        if (candidate.saveSchemaVersion == 2)
        {
            // DeepClone's Normalize guarantees non-null mastery collections; this step exists to
            // move the version and to give a future v3-specific transform somewhere to live.
            candidate.saveSchemaVersion = 3;
        }

        if (candidate.saveSchemaVersion == 3)
        {
            // SALIN-227. A VERSION ADVANCE ONLY -- deliberately no data transform, and there is no
            // v3 -> v4 migration arm anywhere in this file. Read this before adding one.
            //
            // v4 dropped `endlessModeUnlocked` from CampaignProgressData. There is nothing to
            // transform: JsonUtility.FromJson simply ignores the stored key. More importantly, an
            // arm here could never run against a real v3 FILE. TryDeserialize re-derives the
            // integrity hash from JSON re-serialized with the CURRENT field set, so a stored key
            // this build no longer emits changes the hash input and the file is rejected before it
            // ever reaches this method. Measured on disk, not inferred -- see
            // CampaignSaveMigrationTests.PreBumpSaveOnDisk_*.
            //
            // That rejection is the accepted outcome, and it is what the ticket means by "document
            // a one-time dev reset via safe-reset": CampaignSaveService.Inspect reports such a file
            // as SupersededSchema, quarantines it under "superseded-schema", and boots a clean
            // Level 1 with a safe-reset notice.
            //
            // The step itself is NOT dead. The chain must terminate at CurrentSaveSchemaVersion or
            // the Validate call below rejects every migration, including the reachable v1 and v2
            // routes. Removing it silently disables the whole migrator.
            candidate.saveSchemaVersion = 4;
        }

        if (candidate.saveSchemaVersion == 4)
        {
            // D-025. A VERSION ADVANCE ONLY, for the same reason as the v3 step above.
            //
            // v5 renamed the symbol id "symbol.dara" to "symbol.da". Unlike the v4 field removal,
            // this changes a VALUE, not the field set, so a stored v4 file still re-serializes to
            // the same hash and is NOT rejected by TryDeserialize. It is caught one step later:
            // CampaignSaveValidator line ~118 runs unlockedSymbolIds through ValidateUniqueKnownList
            // against the campaign catalog, where "symbol.dara" is no longer known, and returns
            // InvalidStructure.
            //
            // Bumping the version is what makes that rejection HONEST. InvalidStructure is not in
            // IsBlocking, so an unbumped save would still safe-reset -- but it would be quarantined
            // as "corrupt-primary", which is exactly the mislabelling SALIN-227 existed to fix. With
            // the bump, Inspect sees 4 < 5, classifies SupersededSchema, and quarantines under
            // "superseded-schema" with a safe-reset notice.
            //
            // As with v3: the step is NOT dead. The chain must terminate at CurrentSaveSchemaVersion
            // or the Validate call below rejects every migration, v1 and v2 included.
            candidate.saveSchemaVersion = 5;
        }

        CampaignSaveValidationResult validation = CampaignSaveValidator.Validate(
            candidate, campaign, candidate.migration?.legacyArchiveSha256);
        return validation.IsValid
            ? CampaignSaveMigrationResult.Succeeded(candidate)
            : CampaignSaveMigrationResult.Failed(validation.FailureCode, validation.ErrorMessage);
    }
}
