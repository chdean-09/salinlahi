using System;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignOutcomeJournalTests
    {
        [Test]
        public void TryPersist_WhenTemporaryWriteFails_DoesNotPublishJournal()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage
            {
                FailAt = StorageFaultPoint.JournalTemporaryWrite,
            };
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalWriteResult result = journal.TryPersist(
                CampaignSaveTestFactory.CreateValidOutcome(pair.Document), pair.Document);

            Assert.That(result.Success, Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
        }

        [Test]
        public void TryLoadRecoverable_WhenOnlyTemporaryIsValid_PromotesIt()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            storage.Set(CampaignSaveFileRole.PendingOutcomeTemporary, Serialize(outcome));
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalLoadResult result = journal.TryLoadRecoverable(pair.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.PendingRetry));
            Assert.That(result.Outcome.outcomeId, Is.EqualTo(outcome.outcomeId));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.True);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcomeTemporary), Is.False);
        }

        [Test]
        public void TryLoadRecoverable_WhenPublishedAndTemporaryAreIdentical_KeepsPublishedAndDeletesTemporary()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            string json = Serialize(outcome);
            storage.Set(CampaignSaveFileRole.PendingOutcome, json);
            storage.Set(CampaignSaveFileRole.PendingOutcomeTemporary, json);
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalLoadResult result = journal.TryLoadRecoverable(pair.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.PendingRetry));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.PendingOutcome), Is.EqualTo(json));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcomeTemporary), Is.False);
        }

        [Test]
        public void TryLoadRecoverable_WhenPublishedAndTemporaryDiffer_BlocksWithoutMutation()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignProgressOutcome first = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            CampaignProgressOutcome second = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            second.outcomeId = "outcome.00000000000000000000000000000002";
            string published = Serialize(first);
            string temporary = Serialize(second);
            storage.Set(CampaignSaveFileRole.PendingOutcome, published);
            storage.Set(CampaignSaveFileRole.PendingOutcomeTemporary, temporary);
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalLoadResult result = journal.TryLoadRecoverable(pair.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Blocked));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.PendingOutcome), Is.EqualTo(published));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.PendingOutcomeTemporary), Is.EqualTo(temporary));
        }

        [Test]
        public void TryLoadRecoverable_WhenTemporaryChecksumIsBad_QuarantinesItAndLoadsPublished()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            storage.Set(CampaignSaveFileRole.PendingOutcome, Serialize(outcome));
            storage.Set(CampaignSaveFileRole.PendingOutcomeTemporary, Serialize(outcome).Replace("reward.ugat.01", "reward.ugat.02"));
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalLoadResult result = journal.TryLoadRecoverable(pair.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.PendingRetry));
            Assert.That(storage.QuarantinedRoles, Does.Contain(CampaignSaveFileRole.PendingOutcomeTemporary));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.True);
        }

        [Test]
        public void TryLoadRecoverable_WhenJournalSchemaIsHigher_BlocksAndLeavesItInPlace()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignOutcomeJournalDocument document = new CampaignOutcomeJournalDocument
            {
                journalSchemaVersion = CampaignOutcomeJournalDocument.CurrentJournalSchemaVersion + 1,
                outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document),
            };
            string json = CampaignOutcomeSerializer.Serialize(document);
            storage.Set(CampaignSaveFileRole.PendingOutcome, json);
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            CampaignOutcomeJournalLoadResult result = journal.TryLoadRecoverable(pair.Document);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Blocked));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.PendingOutcome), Is.EqualTo(json));
        }

        [Test]
        public void Clear_RemovesPublishedAndTemporaryJournalRoles()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            string json = Serialize(CampaignSaveTestFactory.CreateValidOutcome(pair.Document));
            storage.Set(CampaignSaveFileRole.PendingOutcome, json);
            storage.Set(CampaignSaveFileRole.PendingOutcomeTemporary, json);
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(
                storage, pair.Campaign, new FixedMetadata());

            Assert.That(journal.Clear(), Is.True);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcomeTemporary), Is.False);
        }

        private static string Serialize(CampaignProgressOutcome outcome)
        {
            return CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = outcome });
        }

        private sealed class FixedMetadata : ITransactionMetadataProvider
        {
            public DateTime UtcNow => new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
            public string CreateTransactionId() => "transaction.test.01";
        }
    }
}
