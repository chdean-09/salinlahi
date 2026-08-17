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
    }
}
