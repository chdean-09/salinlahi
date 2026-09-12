using System.Linq;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveValidatorTests
    {
        [Test]
        public void CreateClean_InitializesCurrentSchemaJourneyGenerationAndReceipts()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();

            Assert.That(pair.Document.saveSchemaVersion, Is.EqualTo(3));
            Assert.That(pair.Document.progress.journeyGenerationId,
                Does.Match("^journey\\.[0-9a-f]{32}$"));
            Assert.That(pair.Document.progress.appliedOutcomeReceipts, Is.Empty);
        }

        /// <summary>
        /// SALIN-220 V6, the negative control behind the schema decision. Adding the five flags is
        /// additive under JsonUtility and no structural check inspects them, so a document that
        /// carries them still validates at saveSchemaVersion 3 and this ticket owes no save-schema
        /// bump. That belongs to SALIN-227. Break the claim by adding an invariant over the flags
        /// in CampaignSaveValidator and this test fails.
        /// </summary>
        [Test]
        public void Validate_DocumentCarryingObjectiveFlagsAtSaveSchema3_IsStillValid_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            LevelProgressRecord first = pair.Document.progress.levelProgress[0];
            first.unlocked = true;
            first.completed = true;
            first.bestStars = 3;
            first.storyViewed = true;
            first.symbolsPracticed = true;
            first.wordsRestored = true;
            first.contextPassed = true;
            first.finalSyllableRestored = true;

            // The gated shape too: a completed level whose successor is deliberately still
            // locked, which is exactly what the gate produces and must never be rejected.
            LevelProgressRecord second = pair.Document.progress.levelProgress[1];
            second.unlocked = false;
            second.contextPassed = true;

            CampaignSaveValidationResult result =
                CampaignSaveValidator.Validate(pair.Document, pair.Campaign);

            Assert.That(pair.Document.saveSchemaVersion, Is.EqualTo(3),
                "SALIN-220 deliberately does not move the save schema.");
            Assert.That(result.IsValid, Is.True, result.ErrorMessage);
        }

        [Test]
        public void Validate_WhenOutcomeReceiptsContainDuplicateId_ReturnsInvalidStructure()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            pair.Document.progress.appliedOutcomeReceipts.Add(
                new AppliedOutcomeReceipt("outcome.01", "level.ugat.01", "2026-08-17T00:00:00.0000000Z"));
            pair.Document.progress.appliedOutcomeReceipts.Add(
                new AppliedOutcomeReceipt("outcome.01", "level.ugat.01", "2026-08-17T00:00:01.0000000Z"));

            CampaignSaveValidationResult result =
                CampaignSaveValidator.Validate(pair.Document, pair.Campaign);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidStructure));
        }

        [Test]
        public void CreateClean_StartsAtUgatOneWithOnlyThatLevelUnlocked()
        {
            using (CampaignTestFixture fixture = CampaignTestFixture.CreateValid())
            {
                CampaignSaveDocument document = CampaignProgressFactory.CreateClean(
                    fixture.Campaign, new System.DateTime(2026, 8, 13, 0, 0, 0, System.DateTimeKind.Utc));

                Assert.That(document.progress.activeLevelId, Is.EqualTo(ContentIdentity.RevisedLevelIds[0]));
                Assert.That(document.progress.levelProgress.Single(x => x.levelId == ContentIdentity.RevisedLevelIds[0]).unlocked, Is.True);
                Assert.That(document.progress.levelProgress.Where(x => x.levelId != ContentIdentity.RevisedLevelIds[0]).All(x => !x.unlocked), Is.True);
                Assert.That(CampaignSaveValidator.Validate(document, fixture.Campaign).IsValid, Is.True);
            }
        }

        [Test]
        public void Validate_WhenSaveSchemaIsHigher_BlocksAsUnsupported()
        {
            using (CampaignTestFixture fixture = CampaignTestFixture.CreateValid())
            {
                CampaignSaveDocument document = CampaignProgressFactory.CreateClean(fixture.Campaign, System.DateTime.UtcNow);
                document.saveSchemaVersion = CampaignSaveDocument.CurrentSaveSchemaVersion + 1;

                CampaignSaveValidationResult result = CampaignSaveValidator.Validate(document, fixture.Campaign);

                Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.UnsupportedSchema));
            }
        }
    }
}
