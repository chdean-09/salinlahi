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

        // -------------------------------------------------------------------
        // SALIN-220 — objective flags across the v3 to v4 outcome schema step
        // -------------------------------------------------------------------

        /// <summary>
        /// SALIN-220 V7. A v3 journal recorded no objectives, and by the rules in force when it
        /// was earned that completion was complete. Leaving the flags false would gate a replayed
        /// in-flight completion on objectives that did not exist when the player earned it.
        /// </summary>
        [Test]
        public void UpgradeToCurrent_FromVersion3LevelAttempt_SetsAllFiveObjectivesTrue_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            CampaignSaveTestFactory.ClearObjectiveFlags(outcome);
            outcome.outcomeSchemaVersion = 3;

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);

            Assert.That(outcome.outcomeSchemaVersion,
                Is.EqualTo(CampaignProgressOutcome.CurrentOutcomeSchemaVersion));
            Assert.That(outcome.storyViewed, Is.True);
            Assert.That(outcome.symbolsPracticed, Is.True);
            Assert.That(outcome.wordsRestored, Is.True);
            Assert.That(outcome.contextPassed, Is.True);
            Assert.That(outcome.finalSyllableRestored, Is.True);
            Assert.That(
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document).IsValid,
                Is.True);
        }

        /// <summary>
        /// SALIN-220. The upgrade must not set flags on a practice journal: the non-level guard
        /// rejects any flag there, so doing so would convert a recoverable pending outcome into a
        /// permanently rejected one.
        /// </summary>
        [Test]
        public void UpgradeToCurrent_FromVersion3PracticeOutcome_LeavesObjectivesFalseAndStillValidates_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            outcome.outcomeSchemaVersion = 3;

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);

            Assert.That(outcome.outcomeSchemaVersion,
                Is.EqualTo(CampaignProgressOutcome.CurrentOutcomeSchemaVersion));
            Assert.That(LevelObjectiveGate.CarriesAnyFlag(outcome), Is.False);
            CampaignSaveValidationResult result =
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document);
            Assert.That(result.IsValid, Is.True, result.ErrorMessage);
        }

        [TestCase(LevelObjectives.StoryViewed)]
        [TestCase(LevelObjectives.SymbolsPracticed)]
        [TestCase(LevelObjectives.WordsRestored)]
        [TestCase(LevelObjectives.ContextPassed)]
        [TestCase(LevelObjectives.FinalSyllableRestored)]
        public void Validate_NonLevelOutcomeCarryingAnObjectiveFlag_IsRejected_SALIN220(string objectiveId)
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = PracticeOutcome(pair);
            switch (objectiveId)
            {
                case LevelObjectives.StoryViewed: outcome.storyViewed = true; break;
                case LevelObjectives.SymbolsPracticed: outcome.symbolsPracticed = true; break;
                case LevelObjectives.WordsRestored: outcome.wordsRestored = true; break;
                case LevelObjectives.ContextPassed: outcome.contextPassed = true; break;
                case LevelObjectives.FinalSyllableRestored: outcome.finalSyllableRestored = true; break;
                default: Assert.Fail($"Unknown objective id {objectiveId}."); break;
            }

            CampaignSaveValidationResult result =
                CampaignOutcomeValidator.Validate(outcome, pair.Campaign, pair.Document);

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
            CampaignSaveTestFactory.ClearObjectiveFlags(outcome);
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
