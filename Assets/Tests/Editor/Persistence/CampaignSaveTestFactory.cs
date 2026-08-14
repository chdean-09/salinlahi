using System;
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
    }
}
