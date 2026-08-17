using System;
using System.Collections.Generic;
using Salinlahi.Tests.Editor.Data;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveTestPair : IDisposable
    {
        public CampaignTestFixture Fixture { get; }
        public CampaignConfigSO Campaign => Fixture.Campaign;
        public CampaignSaveDocument Document { get; }

        private CampaignSaveTestPair(CampaignTestFixture fixture)
        {
            Fixture = fixture;
            Document = CampaignProgressFactory.CreateClean(fixture.Campaign, DateTime.UtcNow);
        }

        public static CampaignSaveTestPair CreateValidPair()
        {
            return new CampaignSaveTestPair(CampaignTestFixture.CreateValid());
        }

        public void Dispose() => Fixture.Dispose();
    }

    public static class CampaignSaveTestFactory
    {
        public static CampaignSaveDocument CreateValidDocument()
        {
            using (CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair())
                return CampaignSaveSerializer.DeepClone(pair.Document);
        }

        public static CampaignProgressOutcome CreateValidOutcome(CampaignSaveDocument current)
        {
            return new CampaignProgressOutcome
            {
                outcomeSchemaVersion = 1,
                outcomeId = "outcome.00000000000000000000000000000001",
                journeyGenerationId = current.progress.journeyGenerationId,
                campaignId = current.campaignId,
                contentSchemaVersion = current.contentSchemaVersion,
                levelId = "level.ugat.01",
                stars = 3,
                unlockedSymbolIds = new List<string>(),
                unlockedMemoryIds = new List<string> { "memory.ugat.ina" },
                claimedRewardIds = new List<string> { "reward.ugat.01" },
                completedAtUtc = "2026-08-17T00:00:00.0000000Z",
            };
        }
    }
}
