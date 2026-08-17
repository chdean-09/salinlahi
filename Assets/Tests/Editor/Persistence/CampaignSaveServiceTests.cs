using System;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveServiceTests
    {
        [Test]
        public void Initialize_WhenPrimaryIsV1PublishesMigratedSchemaTwoSave()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveDocument v1 = CampaignSaveSerializer.DeepClone(pair.Document);
            v1.saveSchemaVersion = 1;
            v1.progress.journeyGenerationId = null;
            v1.progress.appliedOutcomeReceipts = null;
            storage.Set(CampaignSaveFileRole.Primary, CampaignSaveSerializer.Serialize(v1));

            CampaignSaveService service = CreateService(storage);
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(service.Current.saveSchemaVersion, Is.EqualTo(2));
            Assert.That(service.Current.progress.journeyGenerationId, Does.StartWith("journey."));
            Assert.That(storage.Exists(CampaignSaveFileRole.Primary), Is.True);
            Assert.That(CampaignSaveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.Primary)).Document.saveSchemaVersion,
                Is.EqualTo(2));
        }

        [Test]
        public void Initialize_WhenHigherSchemaExists_BlocksWithoutOverwritingIt()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 99;
            string original = CampaignSaveSerializer.Serialize(source);
            storage.Set(CampaignSaveFileRole.Primary, original);

            CampaignSaveService service = CreateService(storage);
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.BlockedUnsupportedSchema));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.Primary), Is.EqualTo(original));
        }

        [Test]
        public void TryCommit_WhenMutationFailsValidation_LeavesCurrentRevisionUnchanged()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = CreateService(storage);
            CampaignSaveInitializationResult initialized = service.Initialize(pair.Campaign);
            long beforeRevision = initialized.Document.revision;

            CampaignSaveCommitResult result = service.TryCommit(document =>
                document.progress.activeLevelId = "level.unknown.99");

            Assert.That(result.Success, Is.False);
            Assert.That(service.Current.revision, Is.EqualTo(beforeRevision));
        }

        private static CampaignSaveService CreateService(InMemoryCampaignSaveStorage storage)
        {
            return new CampaignSaveService(storage, new EmptyLegacySource(), new FixedMetadata());
        }

        private sealed class EmptyLegacySource : ILegacyProgressSource
        {
            public bool HasKey(string key) => false;
            public int GetInt(string key, int defaultValue) => defaultValue;
            public float GetFloat(string key, float defaultValue) => defaultValue;
            public string GetString(string key, string defaultValue) => defaultValue;
        }

        private sealed class FixedMetadata : ITransactionMetadataProvider
        {
            public DateTime UtcNow => new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
            public string CreateTransactionId() => Guid.NewGuid().ToString("N");
        }
    }
}
