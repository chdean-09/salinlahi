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
                outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion,
                sessionKind = LearningSessionKind.LevelAttempt,
                evidence = new LearningEvidenceBatch { levelId = "level.ugat.01" },
                outcomeId = "outcome.00000000000000000000000000000001",
                journeyGenerationId = current.progress.journeyGenerationId,
                campaignId = current.campaignId,
                contentSchemaVersion = current.contentSchemaVersion,
                levelId = "level.ugat.01",
                stars = 3,

                // SALIN-220: a level attempt that satisfied every objective, which is what every
                // completable level produces today. The existing fixtures assert the successor
                // unlocks, and the unlock is now gated on these -- leaving them false would fail
                // those tests for the wrong reason.
                storyViewed = true,
                symbolsPracticed = true,
                wordsRestored = true,
                contextPassed = true,
                finalSyllableRestored = true,
                unlockedSymbolIds = new List<string>(),
                unlockedMemoryIds = new List<string> { "memory.ugat.ina" },
                claimedRewardIds = new List<string> { "reward.ugat.01" },
                completedAtUtc = "2026-08-17T00:00:00.0000000Z",
            };
        }

        /// <summary>
        /// SALIN-220. Strips the objective flags a level attempt carries, for fixtures that
        /// reshape the outcome into a practice or review session. A non-level outcome carrying
        /// any flag is rejected, the same way one carrying stars or metrics is.
        /// </summary>
        public static CampaignProgressOutcome ClearObjectiveFlags(CampaignProgressOutcome outcome)
        {
            outcome.storyViewed = false;
            outcome.symbolsPracticed = false;
            outcome.wordsRestored = false;
            outcome.contextPassed = false;
            outcome.finalSyllableRestored = false;
            return outcome;
        }
    }
}
