using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveMigrationTests
    {
        [Test]
        public void TryUpgradeV1_PreservesProgressAndAddsSchemaTwoFields()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument v1 = CampaignSaveSerializer.DeepClone(pair.Document);
            v1.saveSchemaVersion = 1;
            v1.progress.journeyGenerationId = null;
            v1.progress.appliedOutcomeReceipts = null;
            v1.progress.levelProgress[0].completed = true;
            v1.progress.levelProgress[0].bestStars = 2;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeV1(
                v1, pair.Campaign, "journey.00000000000000000000000000000001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Document.saveSchemaVersion, Is.EqualTo(2));
            Assert.That(result.Document.progress.levelProgress[0].bestStars, Is.EqualTo(2));
            Assert.That(result.Document.progress.journeyGenerationId,
                Is.EqualTo("journey.00000000000000000000000000000001"));
            Assert.That(result.Document.progress.appliedOutcomeReceipts, Is.Empty);
        }

        [Test]
        public void TryUpgradeV1_WhenSourceIsHigherSchema_BlocksWithoutMutation()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 99;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeV1(
                source, pair.Campaign, "journey.00000000000000000000000000000001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.UnsupportedSchema));
            Assert.That(source.saveSchemaVersion, Is.EqualTo(99));
        }
    }
}
