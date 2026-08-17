using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignOutcomeCoordinatorTests
    {
        [Test]
        public void TryCommit_AppliesOneOutcomeAcrossProgressDomainsAndClearsJournal()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            long beforeRevision = service.Current.revision;
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.unlockedSymbolIds.Add("symbol.ba");

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            LevelProgressRecord level = service.Current.progress.levelProgress[0];
            LevelProgressRecord next = service.Current.progress.levelProgress[1];
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed));
            Assert.That(service.Current.revision, Is.EqualTo(beforeRevision + 1));
            Assert.That(level.completed, Is.True);
            Assert.That(level.bestStars, Is.EqualTo(3));
            Assert.That(next.unlocked, Is.True);
            Assert.That(service.Current.progress.unlockedSymbolIds, Contains.Item("symbol.ba"));
            Assert.That(service.Current.progress.unlockedMemoryIds, Contains.Item("memory.ugat.ina"));
            Assert.That(service.Current.progress.claimedRewardIds, Contains.Item("reward.ugat.01"));
            Assert.That(service.Current.progress.appliedOutcomeReceipts.Count, Is.EqualTo(1));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
        }

        [Test]
        public void TryCommit_WhenOutcomeIdWasAlreadyApplied_DoesNotIncrementRevision()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            Assert.That(coordinator.TryCommit(outcome).IsAccepted, Is.True);
            long revision = service.Current.revision;

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.AlreadyCommitted));
            Assert.That(service.Current.revision, Is.EqualTo(revision));
        }

        [Test]
        public void TryCommit_WhenSavePublicationFails_RetainsPendingJournal()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            storage.FailAt = StorageFaultPoint.PromoteTemporary;

            CampaignOutcomeCommitResult result = coordinator.TryCommit(
                CampaignSaveTestFactory.CreateValidOutcome(service.Current));

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.PendingRetry));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.True);
        }

        [Test]
        public void TryCommit_WhenOutcomeValidationFails_DoesNotWriteJournal()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.stars = 0;

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Rejected));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
        }

        [Test]
        public void RetryPending_LoadsJournalInsteadOfAcceptingRuntimePayload()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(storage, pair.Campaign, new FixedMetadata());
            Assert.That(journal.TryPersist(outcome, service.Current).Success, Is.True);
            service.Current.progress.appliedOutcomeReceipts = new List<AppliedOutcomeReceipt>();

            CampaignOutcomeCommitResult result = coordinator.RetryPending();

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(service.Current.progress.appliedOutcomeReceipts.Count, Is.EqualTo(1));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
        }

        [Test]
        public void TryResetJourney_ChangesGenerationClearsProgressAndPendingJournal()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage;
            CampaignSaveService service = CreateService(pair, out storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            Assert.That(coordinator.TryCommit(outcome).IsAccepted, Is.True);
            string previousGeneration = service.Current.progress.journeyGenerationId;
            CampaignOutcomeJournal journal = new CampaignOutcomeJournal(storage, pair.Campaign, new FixedMetadata());
            Assert.That(journal.TryPersist(
                CampaignSaveTestFactory.CreateValidOutcome(service.Current), service.Current).Success, Is.True);

            CampaignOutcomeCommitResult result = coordinator.TryResetJourney();

            Assert.That(result.IsAccepted, Is.True);
            Assert.That(service.Current.progress.journeyGenerationId, Is.Not.EqualTo(previousGeneration));
            Assert.That(service.Current.progress.levelProgress[0].unlocked, Is.True);
            Assert.That(service.Current.progress.levelProgress[0].completed, Is.False);
            Assert.That(service.Current.progress.levelProgress[1].unlocked, Is.False);
            Assert.That(service.Current.progress.appliedOutcomeReceipts, Is.Empty);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcomeTemporary), Is.False);
        }

        private static CampaignSaveService CreateService(
            CampaignSaveTestPair pair,
            out InMemoryCampaignSaveStorage storage)
        {
            storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = new CampaignSaveService(
                storage, new EmptyLegacySource(), new FixedMetadata());
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);
            Assert.That(result.Document, Is.Not.Null);
            return service;
        }

        private static CampaignOutcomeCoordinator CreateCoordinator(
            CampaignSaveTestPair pair,
            CampaignSaveService service,
            InMemoryCampaignSaveStorage storage)
        {
            return new CampaignOutcomeCoordinator(
                service,
                new CampaignOutcomeJournal(storage, pair.Campaign, new FixedMetadata()),
                pair.Campaign,
                new FixedMetadata());
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
