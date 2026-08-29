using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// SALIN-140. Recorded metrics and score are the one part of AC1 that was never persisted:
    /// LevelResultsCalculator computed them, the Results screen displayed them, and they were then
    /// discarded, so a completed level's score was unrecoverable the moment that screen closed.
    ///
    /// The other seven AC1 domains, and the interrupt/migration/reset machinery behind AC2–AC4, were
    /// already delivered by SALIN-174/171/143/175 and are covered by their own suites. These tests
    /// assert the properties those criteria demand hold *for the new fields* — which is the part that
    /// could regress — rather than re-testing the transaction itself.
    ///
    /// Edit Mode: pure C# over in-memory fakes, no MonoBehaviour lifecycle, matching the sibling
    /// CampaignOutcomeCoordinatorTests.
    /// </summary>
    public sealed class CampaignOutcomeMetricsTests
    {
        // ---------- AC1: one versioned outcome ----------

        [Test]
        public void TryCommit_RecordsMetricsInTheSameTransactionAsCompletion()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            long beforeRevision = service.Current.revision;

            CampaignProgressOutcome outcome = CreateOutcomeWithScore(service, 82.5f);
            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed));

            // One transaction: the metrics land in the same single revision bump as completion,
            // stars and the next-level unlock -- not in a second write that could arrive alone.
            Assert.That(service.Current.revision, Is.EqualTo(beforeRevision + 1),
                "Metrics must not cost an extra commit.");
            Assert.That(level.completed, Is.True);
            Assert.That(level.bestStars, Is.EqualTo(3));
            Assert.That(FindLevel(service.Current, "level.ugat.02").unlocked, Is.True);
            Assert.That(level.bestScore, Is.EqualTo(82.5f).Within(0.001f));
            Assert.That(MetricValue(level.bestMetrics, LevelResultsCalculator.TracingAccuracyMetricId),
                Is.EqualTo(0.9f).Within(0.001f));
            Assert.That(level.bestMetrics.Count, Is.EqualTo(3));
        }

        // Metrics are stored sorted, because the journal's integrity hash covers the serialized
        // document: identical data in a different order would checksum differently.
        [Test]
        public void TryCommit_StoresMetricsSortedByIdRegardlessOfInputOrder()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.metrics = new List<LevelMetricRecord>
            {
                new LevelMetricRecord(LevelResultsCalculator.ScoreMetricId, 50f),
                new LevelMetricRecord(LevelResultsCalculator.HeartsRatioMetricId, 1f),
                new LevelMetricRecord(LevelResultsCalculator.ContextAccuracyMetricId, 0.5f),
            };

            coordinator.TryCommit(outcome);

            List<string> ids = FindLevel(service.Current, "level.ugat.01")
                .bestMetrics.Select(m => m.metricId).ToList();
            Assert.That(ids, Is.EqualTo(ids.OrderBy(id => id, StringComparer.Ordinal).ToList()),
                "Unsorted metrics would checksum identical data differently.");
        }

        // A practice or review session is not a level attempt. Letting one carry level metrics would
        // write a level score the player never earned for that level.
        [Test]
        public void TryCommit_NonLevelOutcomeCarryingMetrics_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.sessionKind = LearningSessionKind.FreePractice;
            outcome.stars = 0;
            outcome.unlockedMemoryIds.Clear();
            outcome.claimedRewardIds.Clear();
            outcome.metrics = new List<LevelMetricRecord>
            {
                new LevelMetricRecord(LevelResultsCalculator.ScoreMetricId, 99f),
            };

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Rejected));
            Assert.That(FindLevel(service.Current, "level.ugat.01").bestScore, Is.EqualTo(0f));
        }

        [Test]
        public void Validate_RejectsANonFiniteMetricValue()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.metrics = new List<LevelMetricRecord>
            {
                new LevelMetricRecord(LevelResultsCalculator.ScoreMetricId, float.NaN),
            };

            CampaignOutcomeCommitResult result = coordinator.TryCommit(outcome);

            // JsonUtility serializes NaN happily, and a NaN score poisons every later comparison,
            // so it has to be stopped at the validation boundary rather than in the save.
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Rejected));
            Assert.That(FindLevel(service.Current, "level.ugat.01").bestScore, Is.EqualTo(0f));
        }

        // ---------- best-run coherence ----------

        [Test]
        public void TryCommit_WeakerLaterAttempt_KeepsTheBetterMetricSetIntact()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            coordinator.TryCommit(CreateOutcomeWithScore(service, 90f, tracing: 0.95f));
            CampaignProgressOutcome weaker = CreateOutcomeWithScore(service, 40f, tracing: 0.2f);
            weaker.outcomeId = "outcome.00000000000000000000000000000002";

            CampaignOutcomeCommitResult result = coordinator.TryCommit(weaker);

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(result.IsAccepted, Is.True, "A weaker replay is still a valid commit.");
            Assert.That(level.bestScore, Is.EqualTo(90f).Within(0.001f));
            Assert.That(MetricValue(level.bestMetrics, LevelResultsCalculator.TracingAccuracyMetricId),
                Is.EqualTo(0.95f).Within(0.001f),
                "The whole set must stay from the better run -- never a mix of two attempts.");
        }

        [Test]
        public void TryCommit_StrongerLaterAttempt_ReplacesTheWholeMetricSet()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            coordinator.TryCommit(CreateOutcomeWithScore(service, 40f, tracing: 0.2f));
            CampaignProgressOutcome stronger = CreateOutcomeWithScore(service, 95f, tracing: 0.99f);
            stronger.outcomeId = "outcome.00000000000000000000000000000003";

            coordinator.TryCommit(stronger);

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(level.bestScore, Is.EqualTo(95f).Within(0.001f));
            Assert.That(MetricValue(level.bestMetrics, LevelResultsCalculator.TracingAccuracyMetricId),
                Is.EqualTo(0.99f).Within(0.001f));
        }

        // A zero-scoring run is still a real completion. Refusing to write it would leave the record
        // looking as though the level had never been played.
        [Test]
        public void TryCommit_FirstCompletionWithAZeroScore_StillRecordsTheMetrics()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            coordinator.TryCommit(CreateOutcomeWithScore(service, 0f, tracing: 0f));

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(level.bestMetrics, Is.Not.Empty);
            Assert.That(level.bestScore, Is.EqualTo(0f));
        }

        // ---------- AC2: interrupted, then resumed ----------

        [Test]
        public void TryCommit_DuplicateOutcome_DoesNotDoubleApplyMetrics()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            CampaignProgressOutcome outcome = CreateOutcomeWithScore(service, 70f);
            coordinator.TryCommit(outcome);
            long afterFirst = service.Current.revision;

            CampaignOutcomeCommitResult second = coordinator.TryCommit(outcome);

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(second.Status, Is.EqualTo(CampaignOutcomeCommitStatus.AlreadyCommitted));
            Assert.That(service.Current.revision, Is.EqualTo(afterFirst));
            Assert.That(level.bestScore, Is.EqualTo(70f).Within(0.001f));
            Assert.That(level.bestMetrics.Count, Is.EqualTo(3),
                "A replayed receipt must not append the same metrics twice.");
        }

        /// <summary>
        /// The upgrade case this schema change actually creates. The journal's integrity hash is
        /// recomputed over the re-serialized parsed document, so adding a field changes the hash of
        /// any journal written by the previous build.
        ///
        /// AC2 requires that this be *safely repeatable*, not fatal. Only `UnsupportedSchema` sets
        /// `IsUnsupported` and hard-blocks; a checksum mismatch leaves the candidate merely invalid,
        /// which is quarantined and treated as missing. The campaign save is untouched, so the player
        /// replays that one level rather than losing progress or gaining a partial unlock.
        /// </summary>
        [Test]
        public void PendingJournalFromAnOlderBuild_IsQuarantinedNotBlocked()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);

            var journalDocument = new CampaignOutcomeJournalDocument
            {
                outcome = CreateOutcomeWithScore(service, 60f),
            };
            string json = CampaignOutcomeSerializer.Serialize(journalDocument);
            // Stand in for "hashed before the metrics field existed": the stored digest no longer
            // matches what this build computes for the same document.
            json = json.Replace("\"integritySha256\":\"", "\"integritySha256\":\"0");
            storage.WriteAllTextFlushed(CampaignSaveFileRole.PendingOutcome, json);
            long beforeRevision = service.Current.revision;

            CampaignOutcomeCommitResult result = coordinator.ReplayPendingOnStartup();

            Assert.That(result.Status, Is.Not.EqualTo(CampaignOutcomeCommitStatus.Blocked),
                "An unreadable pending journal must never block startup.");
            Assert.That(storage.QuarantinedRoles, Contains.Item(CampaignSaveFileRole.PendingOutcome));
            Assert.That(service.Current.revision, Is.EqualTo(beforeRevision),
                "The campaign save must be untouched.");
            Assert.That(FindLevel(service.Current, "level.ugat.01").completed, Is.False,
                "No partial unlock may survive a discarded journal.");
        }

        // ---------- AC3: migrated data is never mixed ----------

        [Test]
        public void UpgradeToCurrent_V2Outcome_BecomesV3WithEmptyMetrics()
        {
            var outcome = new CampaignProgressOutcome { outcomeSchemaVersion = 2, metrics = null };

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);

            Assert.That(outcome.outcomeSchemaVersion, Is.EqualTo(3));
            Assert.That(outcome.metrics, Is.Not.Null.And.Empty,
                "A v2 outcome recorded no metrics: absence of history, not partial migration.");
        }

        // The previous single-step form returned early unless the version was exactly 1, then stamped
        // it straight to "current". With v3 that would skip the v1 -> v2 step entirely.
        [Test]
        public void UpgradeToCurrent_V1Outcome_TraversesBothStepsToV3()
        {
            var outcome = new CampaignProgressOutcome
            {
                outcomeSchemaVersion = 1,
                evidence = null,
                metrics = null,
            };

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);

            Assert.That(outcome.outcomeSchemaVersion, Is.EqualTo(3));
            Assert.That(outcome.evidence, Is.Not.Null, "The v1 -> v2 step must still run.");
            Assert.That(outcome.sessionKind, Is.EqualTo(LearningSessionKind.LevelAttempt));
            Assert.That(outcome.metrics, Is.Not.Null.And.Empty);
        }

        [Test]
        public void UpgradeToCurrent_OutcomeFromANewerBuild_IsLeftAlone()
        {
            var outcome = new CampaignProgressOutcome
            {
                outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion + 1,
            };

            CampaignOutcomeValidator.UpgradeToCurrent(outcome);

            Assert.That(outcome.outcomeSchemaVersion,
                Is.EqualTo(CampaignProgressOutcome.CurrentOutcomeSchemaVersion + 1),
                "Downgrading a newer outcome would silently reinterpret data this build cannot read.");
        }

        // ---------- AC4: reset returns every domain to its initial state ----------

        [Test]
        public void TryResetJourney_ClearsRecordedScoreAndMetrics()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveService service = CreateService(pair, out InMemoryCampaignSaveStorage storage);
            CampaignOutcomeCoordinator coordinator = CreateCoordinator(pair, service, storage);
            coordinator.TryCommit(CreateOutcomeWithScore(service, 88f));
            Assert.That(FindLevel(service.Current, "level.ugat.01").bestScore, Is.GreaterThan(0f),
                "Setup: metrics were recorded before the reset.");

            CampaignOutcomeCommitResult result = coordinator.TryResetJourney();

            LevelProgressRecord level = FindLevel(service.Current, "level.ugat.01");
            Assert.That(result.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Committed));
            Assert.That(level.bestScore, Is.EqualTo(0f));
            Assert.That(level.bestMetrics, Is.Empty);
            Assert.That(level.completed, Is.False);
        }

        // ---------- helpers ----------

        private static CampaignProgressOutcome CreateOutcomeWithScore(
            CampaignSaveService service, float score, float tracing = 0.9f)
        {
            CampaignProgressOutcome outcome =
                CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            outcome.metrics = new List<LevelMetricRecord>
            {
                new LevelMetricRecord(LevelResultsCalculator.ContextAccuracyMetricId, 0.8f),
                new LevelMetricRecord(LevelResultsCalculator.ScoreMetricId, score),
                new LevelMetricRecord(LevelResultsCalculator.TracingAccuracyMetricId, tracing),
            };
            return outcome;
        }

        private static float MetricValue(List<LevelMetricRecord> metrics, string metricId)
        {
            LevelMetricRecord found = metrics?.FirstOrDefault(
                m => string.Equals(m.metricId, metricId, StringComparison.Ordinal));
            Assert.That(found, Is.Not.Null, $"Metric '{metricId}' was not recorded.");
            return found.value;
        }

        private static LevelProgressRecord FindLevel(CampaignSaveDocument document, string levelId)
        {
            return document.progress.levelProgress.First(record => record.levelId == levelId);
        }

        private static CampaignSaveService CreateService(
            CampaignSaveTestPair pair, out InMemoryCampaignSaveStorage storage)
        {
            storage = new InMemoryCampaignSaveStorage();
            var service = new CampaignSaveService(
                storage, new EmptyLegacySource(), new FixedMetadata());
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);
            Assert.That(result.Document, Is.Not.Null, "Setup: the save service must initialize.");
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
