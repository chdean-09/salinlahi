using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignOutcomeValidatorTests
    {
        [Test]
        public void Validate_WhenGenerationDoesNotMatchCurrent_ReturnsWrongIdentity()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.journeyGenerationId = "journey.00000000000000000000000000000002";

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.WrongIdentity));
        }

        [Test]
        public void Validate_WhenLevelIsLocked_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.levelId = "level.ugat.02";

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        [Test]
        public void Validate_WhenStarsAreOutsideOneToThree_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.stars = 4;

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        [Test]
        public void Validate_WhenCollectionsContainDuplicateIds_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.unlockedMemoryIds = new List<string> { "memory.ugat.ina", "memory.ugat.ina" };

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        [Test]
        public void Validate_WhenSymbolIsNotConfigured_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.unlockedSymbolIds = new List<string> { "symbol.unknown" };

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        [Test]
        public void Validate_WhenMemoryOrRewardIdIsNotCanonical_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.unlockedMemoryIds = new List<string> { "Memory Invalid" };

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                outcome, pair.Campaign, pair.Document);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        private static CampaignProgressOutcome PracticeOutcome(CampaignSaveTestPair pair)
        {
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.sessionKind = LearningSessionKind.FreePractice;
            outcome.stars = 0;
            outcome.unlockedSymbolIds.Clear();
            outcome.unlockedMemoryIds.Clear();
            outcome.claimedRewardIds.Clear();
            outcome.evidence = new LearningEvidenceBatch
            {
                levelId = pair.Document.progress.activeLevelId,
                sessionKind = LearningSessionKind.FreePractice,
            };
            return outcome;
        }

        [Test]
        public void Validate_Version1Outcome_IsAcceptedAfterUpgrade()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.outcomeSchemaVersion = 1;
            outcome.evidence = null;

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);
            CampaignSaveValidationResult result =
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document);

            Assert.That(result.IsValid, Is.True, result.ErrorMessage);
            Assert.That(outcome.sessionKind, Is.EqualTo(LearningSessionKind.LevelAttempt));
            Assert.That(outcome.evidence, Is.Not.Null);
        }

        [Test]
        public void Validate_PracticeWithZeroStarsAndNoUnlocks_IsAccepted()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();

            CampaignSaveValidationResult result = CampaignOutcomeValidator.Validate(
                PracticeOutcome(pair), pair.Campaign, pair.Document);

            Assert.That(result.IsValid, Is.True, result.ErrorMessage);
        }

        [Test]
        public void Validate_PracticeWithStars_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.stars = 3;

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_PracticeWithUnlockedSymbols_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.unlockedSymbolIds.Add(pair.Campaign.symbols[0].stableId);

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_MeaningDimensionOnSymbolEntry_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.evidence.entries.Add(new LearningEvidenceEntry
            {
                contentId = pair.Campaign.symbols[0].stableId,
                contentKind = LearningContentKind.Symbol,
                dimension = MasteryDimension.Meaning,
                attemptCount = 1, successCount = 1, retrievalSuccessCount = 1,
            });

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_DuplicateContentDimensionPair_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            for (int i = 0; i < 2; i++)
                outcome.evidence.entries.Add(new LearningEvidenceEntry
                {
                    contentId = pair.Campaign.symbols[0].stableId,
                    contentKind = LearningContentKind.Symbol,
                    dimension = MasteryDimension.Form,
                    attemptCount = 1, successCount = 1, retrievalSuccessCount = 1,
                });

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_UnknownContentId_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.evidence.entries.Add(new LearningEvidenceEntry
            {
                contentId = "symbol.notreal",
                contentKind = LearningContentKind.Symbol,
                dimension = MasteryDimension.Form,
                attemptCount = 1, successCount = 1, retrievalSuccessCount = 1,
            });

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_CountsExceedingAttempts_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.evidence.entries.Add(new LearningEvidenceEntry
            {
                contentId = pair.Campaign.symbols[0].stableId,
                contentKind = LearningContentKind.Symbol,
                dimension = MasteryDimension.Form,
                attemptCount = 1, successCount = 2, retrievalSuccessCount = 2,
            });

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }

        [Test]
        public void Validate_EvidenceForLockedSymbol_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            // Not in unlockedSymbolIds and not instructed in this batch.
            outcome.evidence.entries.Add(new LearningEvidenceEntry
            {
                contentId = pair.Campaign.symbols[0].stableId,
                contentKind = LearningContentKind.Symbol,
                dimension = MasteryDimension.Form,
                attemptCount = 1, successCount = 1, retrievalSuccessCount = 1,
            });

            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.False);
        }
    }
}
