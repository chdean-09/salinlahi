using System;
using System.Collections.Generic;

public static class CampaignProgressFactory
{
    public static CampaignSaveDocument CreateClean(CampaignConfigSO campaign, DateTime utcNow)
    {
        if (campaign == null || campaign.manifest == null)
            throw new ArgumentNullException(nameof(campaign));

        List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(campaign);
        CampaignSaveDocument document = new CampaignSaveDocument
        {
            fileFormat = "salinlahi-campaign-save",
            campaignId = campaign.manifest.campaignId,
            contentSchemaVersion = campaign.manifest.contentSchemaVersion,
            saveSchemaVersion = CampaignSaveDocument.CurrentSaveSchemaVersion,
            transactionId = "transaction.clean." + Guid.NewGuid().ToString("N"),
            revision = 1,
            transactionState = CampaignSaveTransactionState.Committed,
            createdAtUtc = utcNow.ToUniversalTime().ToString("O"),
            updatedAtUtc = utcNow.ToUniversalTime().ToString("O"),
            migration = new CampaignMigrationReceipt
            {
                state = CampaignMigrationState.NotRequired,
            },
            progress = new CampaignProgressData
            {
                journeyGenerationId = "journey." + Guid.NewGuid().ToString("N"),
                appliedOutcomeReceipts = new List<AppliedOutcomeReceipt>(),
                activeLevelId = campaign.manifest.startingLevelId,
            },
        };

        if (string.IsNullOrEmpty(document.progress.activeLevelId) && levelIds.Count > 0)
            document.progress.activeLevelId = levelIds[0];

        for (int i = 0; i < levelIds.Count; i++)
        {
            document.progress.levelProgress.Add(new LevelProgressRecord
            {
                levelId = levelIds[i],
                unlocked = i == 0,
                completed = false,
                bestStars = 0,
            });
        }

        return document;
    }
}
