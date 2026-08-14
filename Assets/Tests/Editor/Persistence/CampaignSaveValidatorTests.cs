using System.Linq;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveValidatorTests
    {
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
