using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Data
{
    public class CampaignIdentityManifestTests
    {
        [Test]
        public void CreateRevisedV1_UsesFrozenSalin166Values()
        {
            CampaignIdentityManifest manifest = CampaignIdentityManifest.CreateRevisedV1();

            Assert.AreEqual(1, manifest.identityManifestVersion);
            Assert.AreEqual("campaign.revised-v1", manifest.campaignId);
            Assert.AreEqual(1, manifest.contentSchemaVersion);
            Assert.AreEqual(1, manifest.saveSchemaVersion);
            CollectionAssert.AreEqual(new[] { 0, 1 }, manifest.supportedSourceContentSchemas);
            CollectionAssert.AreEqual(new[] { 0, 1 }, manifest.supportedSourceSaveSchemas);
            Assert.AreEqual("legacy-v0-to-revised-v1", manifest.migrationId);
            Assert.AreEqual(1, manifest.minimumReadableSaveSchema);
            Assert.AreEqual(1, manifest.maximumReadableSaveSchema);
            Assert.AreEqual("level.ugat.01", manifest.startingLevelId);
            Assert.AreEqual(ContentIdentity.ApprovedWorkbookSha256, manifest.sourceWorkbookSha256);
            Assert.IsTrue(manifest.IsRevisedV1);
        }

        [TestCase("campaign.revised-v1", true)]
        [TestCase("level.ugat.01", true)]
        [TestCase("symbol.dara", true)]
        [TestCase("Level.Ugat.01", false)]
        [TestCase("level..01", false)]
        [TestCase(" level.ugat.01", false)]
        [TestCase("", false)]
        public void IsCanonical_AcceptsOnlyLowercaseAsciiDottedIds(string value, bool expected)
        {
            Assert.AreEqual(expected, ContentIdentity.IsCanonical(value));
        }
    }
}
