using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void TryCommit_PracticeOutcome_LeavesLevelProgressByteIdentical()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            string levelsBefore = ExtractLevelProgressJson(JsonUtility.ToJson(service.Current.progress));

            CampaignOutcomeCommitResult result = coordinator.TryCommit(CreatePracticeOutcome(service, 1));

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed), result.ReasonCode);
            Assert.That(ExtractLevelProgressJson(JsonUtility.ToJson(service.Current.progress)),
                Is.EqualTo(levelsBefore));
        }

        [Test]
        public void TryCommit_PracticeOutcome_AppliesEvidence()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            coordinator.TryCommit(CreatePracticeOutcome(service, 1));

            Assert.That(service.Current.progress.symbolMastery, Is.Not.Empty);
        }

        [Test]
        public void TryCommit_LevelOutcome_AppliesProgressionAndEvidence()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CreateLevelOutcomeWithEvidence(service);

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed), result.ReasonCode);
            Assert.That(FindLevel(service.Current, outcome.levelId).completed, Is.True);
            Assert.That(service.Current.progress.symbolMastery, Is.Not.Empty);
        }

        [Test]
        public void TryCommit_ManyPracticeOutcomes_KeepsNewestAndAllLevelReceipts()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            coordinator.TryCommit(CreateLevelOutcomeWithEvidence(service));

            CampaignProgressOutcome last = null;
            for (int i = 0; i < 40; i++)
            {
                last = CreatePracticeOutcome(service, i);
                CampaignOutcomeCommitResult result = coordinator.TryCommit(last);
                Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed),
                    $"commit {i} failed: {result.ReasonCode}");
            }

            List<AppliedOutcomeReceipt> receipts = service.Current.progress.appliedOutcomeReceipts;
            Assert.That(receipts.Count(r => r.sessionKind == LearningSessionKind.LevelAttempt),
                Is.EqualTo(1));
            Assert.That(receipts.Count(r => r.sessionKind != LearningSessionKind.LevelAttempt),
                Is.EqualTo(32));
            Assert.That(receipts.Any(r => r.outcomeId == last.outcomeId), Is.True,
                "Pruning must never evict the receipt just written.");
        }

        // -------------------------------------------------------------------
        // SALIN-220 — per-objective flags and the unlock gate
        // -------------------------------------------------------------------

        [Test]
        public void TryCommit_WhenEveryObjectiveIsSatisfied_UnlocksSuccessorAndPersistsAllFiveFlags_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            CampaignOutcomeCommitResult result = coordinator.TryCommit(
                CampaignSaveTestFactory.CreateValidOutcome(service.Current));

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed));
            Assert.That(level.storyViewed, Is.True);
            Assert.That(level.symbolsPracticed, Is.True);
            Assert.That(level.wordsRestored, Is.True);
            Assert.That(level.contextPassed, Is.True);
            Assert.That(level.finalSyllableRestored, Is.True);
            Assert.That(LevelObjectiveGate.AllSatisfied(level), Is.True);
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.True);
        }

        /// <summary>
        /// SALIN-220 V4, the negative control. Removing either half of the gate must fail this:
        /// drop the guard in ApplyLevelProgression and the successor unlocks anyway; drop the
        /// matching guard in VerifyPublishedOutcome and the status becomes PendingRetry, which
        /// wedges the journal and raises the blocking save panel on a finished level.
        /// </summary>
        [TestCase(LevelObjectives.StoryViewed)]
        [TestCase(LevelObjectives.SymbolsPracticed)]
        [TestCase(LevelObjectives.WordsRestored)]
        [TestCase(LevelObjectives.ContextPassed)]
        [TestCase(LevelObjectives.FinalSyllableRestored)]
        public void TryCommit_WithOneObjectiveMissing_KeepsSuccessorLockedAndStillCommits_SALIN220(
            string missingObjectiveId)
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            ClearObjective(outcome, missingObjectiveId);

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed),
                "A gated completion is still a completion. PendingRetry here would wedge the " +
                "journal and raise the save-failure panel on a level the player finished.");
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.False,
                $"{missingObjectiveId} was not satisfied, so the successor must stay locked.");
            Assert.That(level.completed, Is.True, "The level itself still completes (D1).");
            Assert.That(level.unlocked, Is.True);
            Assert.That(level.bestStars, Is.EqualTo(3));
            Assert.That(LevelObjectiveGate.FirstUnsatisfied(level), Is.EqualTo(missingObjectiveId));
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False,
                "The journal must clear: the outcome was committed, not left pending.");
        }

        [Test]
        public void TryCommit_ReplayCarryingFewerObjectives_NeverClearsAnAlreadyEarnedFlag_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            Assert.That(coordinator.TryCommit(
                CampaignSaveTestFactory.CreateValidOutcome(service.Current)).IsAccepted, Is.True);

            CampaignProgressOutcome weaker = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            weaker.outcomeId = "outcome.00000000000000000000000000000002";
            weaker.storyViewed = false;
            weaker.contextPassed = false;

            Assert.That(coordinator.TryCommit(weaker).Status,
                Is.EqualTo(CampaignOutcomeCommitStatus.Committed));

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(LevelObjectiveGate.AllSatisfied(level), Is.True,
                "Flags merge with OR, never assignment — a weaker replay must not un-earn an objective.");
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.True);
        }

        [Test]
        public void TryCommit_WhenAReplaySatisfiesTheLastMissingObjective_UnlocksTheSuccessor_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            CampaignProgressOutcome first = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            first.contextPassed = false;
            Assert.That(coordinator.TryCommit(first).IsAccepted, Is.True);
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.False,
                "precondition: the gate withheld the unlock");

            CampaignProgressOutcome second = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            second.outcomeId = "outcome.00000000000000000000000000000002";

            Assert.That(coordinator.TryCommit(second).Status,
                Is.EqualTo(CampaignOutcomeCommitStatus.Committed));
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.True);
        }

        /// <summary>
        /// SALIN-220 V5. A save written before the flags existed carries all five false with its
        /// successors already unlocked. The gate only ever withholds a NEW unlock, so nothing may
        /// be re-locked and no migration is owed by this ticket.
        /// </summary>
        [Test]
        public void TryCommit_OnAPreObjectiveSaveWithUnlockedSuccessors_NeverRelocksAnything_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            // Level 1 completed by an older build: no flags recorded, successor already open.
            LevelProgressRecord first = FindLevel(service.Current, "level.ugat.01");
            first.completed = true;
            first.unlocked = true;
            first.bestStars = 3;
            FindLevel(service.Current, "level.ugat.02").unlocked = true;
            service.Current.progress.activeLevelId = "level.ugat.02";
            Assert.That(LevelObjectiveGate.AllSatisfied(first), Is.False,
                "precondition: a pre-SALIN-220 record carries every flag false");

            CampaignProgressOutcome outcome = CreateOutcomeFor(service, "level.ugat.02", 2);
            outcome.wordsRestored = false;

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed));
            Assert.That(FindLevel(service.Current, "level.ugat.01").completed, Is.True);
            Assert.That(FindLevel(service.Current, "level.ugat.01").unlocked, Is.True);
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.True,
                "An unlock granted before the gate existed must survive it.");
            Assert.That(FindLevel(service.Current, "level.ugat.02").completed, Is.True);
            Assert.That(FindLevel(service.Current, "level.ugat.03").unlocked, Is.False,
                "The new unlock is the only thing the gate withholds.");
        }

        private static CampaignProgressOutcome CreateOutcomeFor(
            CampaignSaveService service, string levelId, int ordinal)
        {
            CampaignProgressOutcome outcome =
                CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.outcomeId = "outcome." + ordinal.ToString("D32");
            outcome.levelId = levelId;
            outcome.evidence = new LearningEvidenceBatch { levelId = levelId };
            outcome.unlockedMemoryIds.Clear();
            outcome.claimedRewardIds.Clear();
            return outcome;
        }

        private static void ClearObjective(CampaignProgressOutcome outcome, string objectiveId)
        {
            switch (objectiveId)
            {
                case LevelObjectives.StoryViewed: outcome.storyViewed = false; break;
                case LevelObjectives.SymbolsPracticed: outcome.symbolsPracticed = false; break;
                case LevelObjectives.WordsRestored: outcome.wordsRestored = false; break;
                case LevelObjectives.ContextPassed: outcome.contextPassed = false; break;
                case LevelObjectives.FinalSyllableRestored: outcome.finalSyllableRestored = false; break;
                default: Assert.Fail($"Unknown objective id {objectiveId}."); break;
            }
        }

        private static CampaignProgressOutcome CreatePracticeOutcome(
            CampaignSaveService service, int index)
        {
            CampaignProgressOutcome outcome =
                CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.outcomeId = "outcome.practice." + index.ToString("000");
            outcome.sessionKind = LearningSessionKind.FreePractice;
            outcome.stars = 0;
            CampaignSaveTestFactory.ClearObjectiveFlags(outcome);
            outcome.unlockedSymbolIds.Clear();
            outcome.unlockedMemoryIds.Clear();
            outcome.claimedRewardIds.Clear();
            outcome.evidence = CreateEvidence(LearningSessionKind.FreePractice);
            return outcome;
        }

        private static CampaignProgressOutcome CreateLevelOutcomeWithEvidence(
            CampaignSaveService service)
        {
            CampaignProgressOutcome outcome =
                CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.evidence = CreateEvidence(LearningSessionKind.LevelAttempt);
            return outcome;
        }

        // The symbol is instructed in the batch, which satisfies both the locked-symbol rule in
        // CampaignOutcomeValidator and the self-introduction guard in LearningProgressWriter.
        private static LearningEvidenceBatch CreateEvidence(LearningSessionKind kind)
        {
            return new LearningEvidenceBatch
            {
                levelId = "level.ugat.01",
                sessionKind = kind,
                instructedContentIds = new List<string> { "symbol.ba" },
                entries = new List<LearningEvidenceEntry>
                {
                    new LearningEvidenceEntry
                    {
                        contentId = "symbol.ba",
                        contentKind = LearningContentKind.Symbol,
                        dimension = MasteryDimension.Form,
                        attemptCount = 1,
                        successCount = 1,
                        retrievalSuccessCount = 1,
                    },
                },
            };
        }

        private static LevelProgressRecord FindLevel(CampaignSaveDocument document, string levelId)
        {
            return document.progress.levelProgress.First(record => record.levelId == levelId);
        }

        private static string ExtractLevelProgressJson(string progressJson)
        {
            const string Key = "\"levelProgress\":[";
            int start = progressJson.IndexOf(Key, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "levelProgress missing from progress JSON.");
            int end = progressJson.IndexOf(']', start);
            Assert.That(end, Is.GreaterThanOrEqualTo(0), "levelProgress array is unterminated.");
            return progressJson.Substring(start, end - start + 1);
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
