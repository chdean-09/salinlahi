using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;
using Salinlahi.Tests.Editor.Persistence;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class LearningStateSnapshotTests
    {
        [Test]
        public void IntroducedSymbolIds_ReturnsSeededSymbols()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            LearningProgressWriter.Apply(pair.Document.progress,
                InstructionBatch("symbol.ba", "symbol.ma"), pair.Campaign.learningTuning);

            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            Assert.That(snapshot.IntroducedSymbolIds,
                Is.EquivalentTo(new[] { "symbol.ba", "symbol.ma" }));
        }

        [Test]
        public void GetSymbolState_UnknownSymbol_ReturnsNone()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();

            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            Assert.That(snapshot.GetSymbolState("symbol.notreal"), Is.EqualTo(MasteryState.None));
        }

        [Test]
        public void GetSymbolState_SeededSymbol_ReturnsIntroduced()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            LearningProgressWriter.Apply(pair.Document.progress,
                InstructionBatch("symbol.ba"), pair.Campaign.learningTuning);

            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            Assert.That(snapshot.GetSymbolState("symbol.ba"), Is.EqualTo(MasteryState.Introduced));
        }

        [Test]
        public void GetRequiredReviewItems_IncludesLevelConfigRequirements()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            var required = snapshot.GetRequiredReviewItems("level.ugat.01");

            Assert.That(required, Is.Not.Empty);
        }

        [Test]
        public void GetSuggestedPracticeItems_CannotRemoveARequiredItem()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            var requiredAlone = snapshot.GetRequiredReviewItems("level.ugat.01");
            snapshot.GetSuggestedPracticeItems("level.ugat.01", maxCount: 5);
            var requiredAfter = snapshot.GetRequiredReviewItems("level.ugat.01");

            Assert.That(requiredAfter.Select(i => i.ContentId),
                Is.EqualTo(requiredAlone.Select(i => i.ContentId)));
        }

        [Test]
        public void GetSuggestedPracticeItems_RespectsMaxCount()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            var snapshot = new LearningStateSnapshot(pair.Document.progress, pair.Campaign);

            Assert.That(snapshot.GetSuggestedPracticeItems("level.ugat.01", maxCount: 2).Count,
                Is.LessThanOrEqualTo(2));
        }

        private static LearningEvidenceBatch InstructionBatch(params string[] ids)
        {
            return new LearningEvidenceBatch
            {
                levelId = "level.ugat.01",
                sessionKind = LearningSessionKind.LevelAttempt,
                instructedContentIds = ids.ToList(),
                entries = new List<LearningEvidenceEntry>(),
            };
        }
    }
}
