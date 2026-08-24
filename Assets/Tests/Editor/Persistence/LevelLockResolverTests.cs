using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// SALIN-137 level locks and prerequisites over committed save snapshots.
    /// AC1: every level resolves to exactly one of locked / unlocked / completed.
    /// AC2: a locked level names the single immediately preceding requirement.
    /// AC3: completing an era-ending level leaves the next era's first level unlocked.
    /// The resolver restates the authored rule in
    /// <c>CampaignOutcomeCoordinator.ApplyLevelProgression</c>; it never adds one.
    /// </summary>
    public sealed class LevelLockResolverTests
    {
        // ---------------------------------------------------------------
        // AC1 — three distinct states
        // ---------------------------------------------------------------

        [Test]
        public void Resolve_FirstConfiguredLevelOnFreshSave_IsUnlocked()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[0]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Unlocked));
            Assert.That(status.HasRequirement, Is.False, "The first level has no prerequisite.");
        }

        [Test]
        public void Resolve_CompletedLevel_IsCompletedNotMerelyUnlocked()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            CompleteLevels(pair.Document, 1);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[0]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Completed),
                "Completed must win over unlocked — ApplyLevelProgression sets both flags.");
            Assert.That(status.HasRequirement, Is.False);
        }

        [Test]
        public void Resolve_FreshSave_ProducesAllThreeStatesAcrossTheCampaign()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            CompleteLevels(pair.Document, 1);

            // Level 1 completed, level 2 unlocked by the completion, level 3 still locked.
            Assert.That(LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[0]).State,
                Is.EqualTo(LevelLockState.Completed));
            Assert.That(LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[1]).State,
                Is.EqualTo(LevelLockState.Unlocked));
            Assert.That(LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[2]).State,
                Is.EqualTo(LevelLockState.Locked));
        }

        // ---------------------------------------------------------------
        // AC2 — the single immediately preceding requirement
        // ---------------------------------------------------------------

        [Test]
        public void Resolve_LevelWithIncompletePredecessor_IsLockedBehindThatPredecessor()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[2]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(status.RequiredLevelId, Is.EqualTo(levelIds[1]),
                "AC2 asks for the immediately preceding requirement only, never the whole chain.");
            Assert.That(status.RequiredLevelOrder, Is.EqualTo(2),
                "RequiredLevelOrder is the 1-based campaign position of the predecessor.");
        }

        [Test]
        public void Resolve_LockWithinAnEra_IsNotReportedAsCrossingAnEra()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            // levelIds[2] and levelIds[1] both sit in the first era.
            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[2]);

            Assert.That(status.RequirementCrossesEra, Is.False);
        }

        [Test]
        public void Resolve_FirstLevelOfNextEra_ReportsAnEraCrossingRequirement()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            int eraSize = ContentIdentity.RevisedLevelsPerEra;

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[eraSize]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(status.RequiredLevelId, Is.EqualTo(levelIds[eraSize - 1]),
                "The next era opens behind the previous era's final level.");
            Assert.That(status.RequirementCrossesEra, Is.True);
            Assert.That(status.RequiredEraId,
                Is.EqualTo(ContentIdentity.GetEraIdForLevel(levelIds[eraSize - 1])));
        }

        // ---------------------------------------------------------------
        // AC3 — an era-ending completion opens the next era
        // ---------------------------------------------------------------

        [Test]
        public void Resolve_AfterCompletingTheEraEndingLevel_NextErasFirstLevelIsUnlocked()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            int eraSize = ContentIdentity.RevisedLevelsPerEra;
            // Completes the whole first era; CompleteLevels also unlocks the next index,
            // mirroring ApplyLevelProgression's "complete i unlocks i + 1" across the edge.
            CompleteLevels(pair.Document, eraSize);

            LevelLockStatus eraEnd = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[eraSize - 1]);
            LevelLockStatus nextEraFirst = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[eraSize]);

            Assert.That(eraEnd.State, Is.EqualTo(LevelLockState.Completed));
            Assert.That(nextEraFirst.State, Is.EqualTo(LevelLockState.Unlocked));
            Assert.That(nextEraFirst.HasRequirement, Is.False,
                "An unlocked level has nothing left to explain.");
        }

        [Test]
        public void Resolve_SecondLevelOfTheNextEra_IsStillLockedAfterTheEraEdgeOpens()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            int eraSize = ContentIdentity.RevisedLevelsPerEra;
            CompleteLevels(pair.Document, eraSize);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[eraSize + 1]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(status.RequiredLevelId, Is.EqualTo(levelIds[eraSize]));
            Assert.That(status.RequirementCrossesEra, Is.False,
                "Only the era's first level crosses an era boundary.");
        }

        // ---------------------------------------------------------------
        // Defensive degradation — Level Select renders this per button
        // ---------------------------------------------------------------

        [Test]
        public void Resolve_UnknownLevelId_IsUnknown()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, "level.not.real");

            Assert.That(status.State, Is.EqualTo(LevelLockState.Unknown));
            Assert.That(status.HasRequirement, Is.False);
        }

        [Test]
        public void Resolve_NullOrEmptyConfiguredLevelIds_IsUnknown()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();

            Assert.That(LevelLockResolver.Resolve(pair.Document, null, "level.ugat.01").State,
                Is.EqualTo(LevelLockState.Unknown));
            Assert.That(LevelLockResolver.Resolve(pair.Document, new List<string>(), "level.ugat.01").State,
                Is.EqualTo(LevelLockState.Unknown));
        }

        [Test]
        public void Resolve_NullOrEmptyLevelId_IsUnknown()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            Assert.That(LevelLockResolver.Resolve(pair.Document, levelIds, null).State,
                Is.EqualTo(LevelLockState.Unknown));
            Assert.That(LevelLockResolver.Resolve(pair.Document, levelIds, string.Empty).State,
                Is.EqualTo(LevelLockState.Unknown));
        }

        [Test]
        public void Resolve_NullDocument_DegradesToLockedWithTheConfiguredPredecessor()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            LevelLockStatus status = LevelLockResolver.Resolve(null, levelIds, levelIds[1]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked),
                "A missing record must never advertise an unplayable level as reachable.");
            Assert.That(status.RequiredLevelId, Is.EqualTo(levelIds[0]));
        }

        [Test]
        public void Resolve_MissingProgressRecord_IsLockedRatherThanThrowing()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            pair.Document.progress.levelProgress.RemoveAt(1);

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[1]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(status.RequiredLevelId, Is.EqualTo(levelIds[0]));
        }

        [Test]
        public void Resolve_FirstLevelLockedByInconsistentData_HasNothingToExplain()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            // Inconsistent: CampaignProgressFactory always unlocks index 0.
            pair.Document.progress.levelProgress[0].unlocked = false;

            LevelLockStatus status = LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[0]);

            Assert.That(status.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(status.HasRequirement, Is.False,
                "The first level has no predecessor to blame.");
        }

        [Test]
        public void Resolve_DoesNotMutateTheDocument()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            int recordCount = pair.Document.progress.levelProgress.Count;

            for (int i = 0; i < levelIds.Count; i++)
                LevelLockResolver.Resolve(pair.Document, levelIds, levelIds[i]);

            Assert.That(pair.Document.progress.levelProgress.Count, Is.EqualTo(recordCount));
            for (int i = 0; i < pair.Document.progress.levelProgress.Count; i++)
            {
                LevelProgressRecord record = pair.Document.progress.levelProgress[i];
                Assert.That(record.completed, Is.False, $"record {i} completed");
                Assert.That(record.unlocked, Is.EqualTo(i == 0), $"record {i} unlocked");
            }
        }

        // ---------------------------------------------------------------
        // Repository adapter
        // ---------------------------------------------------------------

        [Test]
        public void Repository_ResolveLevelLock_UsesTheCommittedDocument()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = new CampaignSaveService(storage, new DictionaryLegacySource());
            Assert.That(service.Initialize(pair.Campaign).Status,
                Is.EqualTo(CampaignSaveInitializationStatus.Ready), "precondition");
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            Assert.That(service.TryUpdate(document =>
            {
                document.progress.levelProgress[0].completed = true;
                document.progress.levelProgress[0].bestStars = 3;
                document.progress.levelProgress[1].unlocked = true;
            }), Is.True, "precondition");
            CampaignProgressRepository repository = new CampaignProgressRepository(service, pair.Campaign);

            Assert.That(repository.ResolveLevelLock(levelIds[0]).State,
                Is.EqualTo(LevelLockState.Completed));
            Assert.That(repository.ResolveLevelLock(levelIds[1]).State,
                Is.EqualTo(LevelLockState.Unlocked));

            LevelLockStatus locked = repository.ResolveLevelLock(levelIds[2]);
            Assert.That(locked.State, Is.EqualTo(LevelLockState.Locked));
            Assert.That(locked.RequiredLevelId, Is.EqualTo(levelIds[1]));
        }

        // ---------------------------------------------------------------
        // Parity with the AUTHORED rule, not a copy of it
        // ---------------------------------------------------------------

        /// <summary>
        /// SALIN-137 AC3 at the 5 -> 6 era edge, driven by the REAL
        /// <see cref="CampaignOutcomeCoordinator"/> rather than by this fixture's
        /// <c>CompleteLevels</c> helper. Every other era-edge test above asserts the
        /// resolver against a test-local restatement of the unlock rule, so all of them
        /// would still pass if <c>ApplyLevelProgression</c> and
        /// <see cref="LevelLockResolver"/> drifted apart together. This one cannot: the
        /// save state it reads is whatever the shipped commit path actually wrote.
        /// </summary>
        [Test]
        public void Resolve_AfterTheRealCoordinatorFinishesEraOne_MatchesTheAuthoredUnlockRule()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = new CampaignSaveService(storage, new DictionaryLegacySource());
            Assert.That(service.Initialize(pair.Campaign).Status,
                Is.EqualTo(CampaignSaveInitializationStatus.Ready), "precondition");
            CampaignOutcomeCoordinator coordinator = new CampaignOutcomeCoordinator(
                service,
                new CampaignOutcomeJournal(storage, pair.Campaign),
                pair.Campaign);
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            int eraSize = ContentIdentity.RevisedLevelsPerEra;
            Assert.That(ContentIdentity.GetEraIdForLevel(levelIds[eraSize - 1]),
                Is.Not.EqualTo(ContentIdentity.GetEraIdForLevel(levelIds[eraSize])),
                "precondition: index eraSize-1 and eraSize must straddle an era boundary");

            for (int i = 0; i < eraSize; i++)
                CommitLevelCompletion(coordinator, service, levelIds[i], i + 1);

            CampaignSaveDocument committed = service.Current;

            Assert.That(LevelLockResolver.Resolve(committed, levelIds, levelIds[eraSize - 1]).State,
                Is.EqualTo(LevelLockState.Completed),
                "The era-ending level the coordinator just committed must read as completed.");

            LevelLockStatus nextEraFirst = LevelLockResolver.Resolve(committed, levelIds, levelIds[eraSize]);
            Assert.That(nextEraFirst.State, Is.EqualTo(LevelLockState.Unlocked),
                "AC3: the authored rule unlocks index i+1 across the era boundary.");
            Assert.That(nextEraFirst.HasRequirement, Is.False);

            LevelLockStatus nextEraSecond = LevelLockResolver.Resolve(committed, levelIds, levelIds[eraSize + 1]);
            Assert.That(nextEraSecond.State, Is.EqualTo(LevelLockState.Locked),
                "Only one level past the edge opens; the rest of era two stays locked.");
            Assert.That(nextEraSecond.RequiredLevelId, Is.EqualTo(levelIds[eraSize]));
            Assert.That(nextEraSecond.RequirementCrossesEra, Is.False);
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        /// <summary>
        /// Completes one level through the shipped commit path, so the resulting document
        /// is written by <c>CampaignOutcomeCoordinator.ApplyLevelProgression</c> itself.
        /// </summary>
        private static void CommitLevelCompletion(
            CampaignOutcomeCoordinator coordinator,
            CampaignSaveService service,
            string levelId,
            int ordinal)
        {
            CampaignProgressOutcome outcome = new CampaignProgressOutcome
            {
                outcomeSchemaVersion = CampaignProgressOutcome.CurrentOutcomeSchemaVersion,
                sessionKind = LearningSessionKind.LevelAttempt,
                evidence = new LearningEvidenceBatch { levelId = levelId },
                outcomeId = "outcome." + ordinal.ToString("D32"),
                journeyGenerationId = service.Current.progress.journeyGenerationId,
                campaignId = service.Current.campaignId,
                contentSchemaVersion = service.Current.contentSchemaVersion,
                levelId = levelId,
                stars = 3,
                unlockedSymbolIds = new List<string>(),
                unlockedMemoryIds = new List<string>(),
                claimedRewardIds = new List<string>(),
                completedAtUtc = "2026-08-17T00:00:00.0000000Z",
            };

            Assert.That(coordinator.TryCommit(outcome).Status,
                Is.EqualTo(CampaignOutcomeCommitStatus.Committed),
                $"precondition: {levelId} must commit through the authored rule");
        }

        private static void CompleteLevels(CampaignSaveDocument document, int count)
        {
            List<LevelProgressRecord> records = document.progress.levelProgress;
            for (int i = 0; i < count && i < records.Count; i++)
            {
                records[i].unlocked = true;
                records[i].completed = true;
                records[i].bestStars = 3;
            }
            if (count < records.Count)
                records[count].unlocked = true;
            document.progress.activeLevelId = records[System.Math.Min(count, records.Count - 1)].levelId;
        }
    }
}
