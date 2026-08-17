using System;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveCommitterTests
    {
        [Test]
        public void TryCommit_WhenPublishedReadBackFails_RestoresBackupAndDoesNotExposeCandidate()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveDocument current = CampaignSaveSerializer.DeepClone(pair.Document);
            storage.Set(CampaignSaveFileRole.Primary, CampaignSaveSerializer.Serialize(current));
            CampaignSaveCommitter committer = new CampaignSaveCommitter(
                storage, pair.Campaign, new FixedMetadata());
            storage.FailAt = StorageFaultPoint.PublishedReadBack;

            CampaignSaveCommitResult result = committer.TryCommit(
                current,
                document => document.progress.activeLevelId = "level.ugat.01",
                new CampaignSaveCommitContext { CanBackupValidatedPrimary = true });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Document, Is.Null);
            Assert.That(storage.Exists(CampaignSaveFileRole.Backup), Is.True);
            Assert.That(CampaignSaveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.Primary)).Document.revision,
                Is.EqualTo(current.revision));
        }

        [Test]
        public void TryPromoteValidatedTemporary_WhenPublishedReadBackFails_RestoresBackup()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveDocument current = CampaignSaveSerializer.DeepClone(pair.Document);
            CampaignSaveDocument candidate = CampaignSaveSerializer.DeepClone(current);
            candidate.revision++;
            candidate.transactionId = "transaction.recovery.01";
            storage.Set(CampaignSaveFileRole.Primary, CampaignSaveSerializer.Serialize(current));
            storage.Set(CampaignSaveFileRole.Temporary, CampaignSaveSerializer.Serialize(candidate));
            CampaignSaveCommitter committer = new CampaignSaveCommitter(
                storage, pair.Campaign, new FixedMetadata());
            storage.FailAt = StorageFaultPoint.PublishedReadBack;

            CampaignSaveCommitResult result = committer.TryPromoteValidatedTemporary(
                candidate, current);

            Assert.That(result.Success, Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.Backup), Is.True);
            Assert.That(CampaignSaveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.Primary)).Document.revision,
                Is.EqualTo(current.revision));
        }

        private sealed class FixedMetadata : ITransactionMetadataProvider
        {
            public DateTime UtcNow => new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
            public string CreateTransactionId() => "transaction.test.02";
        }
    }
}
