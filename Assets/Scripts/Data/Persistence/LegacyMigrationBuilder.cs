using System;

public static class LegacyMigrationBuilder
{
    public static CampaignSaveDocument CreateFreshJourney(
        LegacyProgressArchive archive,
        string validatedArchiveSha256,
        CampaignConfigSO campaign,
        DateTime utcNow)
    {
        CampaignSaveDocument document = CampaignProgressFactory.CreateClean(campaign, utcNow);
        document.migration = new CampaignMigrationReceipt
        {
            migrationId = "legacy-v0-to-revised-v1",
            sourceSaveSchemaVersion = 0,
            state = CampaignMigrationState.Completed,
            legacyArchiveSha256 = validatedArchiveSha256,
            completedAtUtc = utcNow.ToUniversalTime().ToString("O"),
            noticeAcknowledged = false,
        };
        return document;
    }
}
