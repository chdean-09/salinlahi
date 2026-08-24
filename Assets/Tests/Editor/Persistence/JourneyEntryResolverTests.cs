using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// SALIN-136 journey-entry routing over committed save snapshots.
    /// AC1: fresh save resolves to a new journey at the first configured level.
    /// AC2: an in-progress journey resolves to the first unlocked, incomplete level.
    /// AC3: a fully completed journey resolves to the review/replay state.
    /// AC4: routing only ever consumes post-migration state.
    /// </summary>
    public sealed class JourneyEntryResolverTests
    {
        [Test]
        public void Resolve_FreshCleanDocument_IsNewJourneyAtFirstConfiguredLevel()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.NewJourney));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[0]));
        }

        [Test]
        public void Resolve_MidJourney_ContinuesAtFirstUnlockedIncompleteLevel()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            CompleteLevels(pair.Document, 2);

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.ContinueLevel));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[2]));
        }

        [Test]
        public void Resolve_ActiveLevelPointsAtReplay_StillContinuesAtNextIncompleteLevel()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            CompleteLevels(pair.Document, 3);
            // The player replayed level 1 last; activeLevelId records that selection.
            pair.Document.progress.activeLevelId = levelIds[0];

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.ContinueLevel));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[3]),
                "Continue routing must prefer campaign order over the last-selected level.");
        }

        [Test]
        public void Resolve_AllLevelsCompleted_IsCompletedJourney()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            CompleteLevels(pair.Document, levelIds.Count);

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.CompletedJourney));
            Assert.That(entry.LevelId, Is.Null);
        }

        [Test]
        public void Resolve_ProgressWithoutNextUnlock_FallsBackToFirstIncompleteLevel()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            // Inconsistent data: level 1 completed but level 2 never unlocked.
            pair.Document.progress.levelProgress[0].completed = true;
            pair.Document.progress.levelProgress[0].bestStars = 2;

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.ContinueLevel));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[1]),
                "Inconsistent unlock data must fall back to the first incomplete level, never an invalid prompt.");
        }

        [Test]
        public void Resolve_NothingCompletedWithExtraUnlocks_IsStillRoutedFromTheStart()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            // Inconsistent data: extra unlock with no completion anywhere.
            pair.Document.progress.levelProgress[1].unlocked = true;

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.NewJourney));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[0]));
        }

        [Test]
        public void Resolve_NullDocument_DefaultsToNewJourneyAtFirstConfiguredLevel()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(null, levelIds);

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.NewJourney));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[0]));
        }

        [Test]
        public void Resolve_NoConfiguredLevels_IsBlocked()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();

            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(pair.Document, new List<string>());

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.Blocked));
        }

        [Test]
        public void Repository_ResolveJourneyEntryPoint_UsesTheCommittedDocument()
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

            JourneyEntryPoint entry = repository.ResolveJourneyEntryPoint();

            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.ContinueLevel));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[1]));
        }

        [Test]
        public void Resolve_AfterHistoricalSchemaMigration_IsNewJourneyAtLevelOne()
        {
            // AC4: BL-E2-S6 migration/recovery completes before revised progress is
            // consumed. The resolver only ever sees the committed post-migration
            // document, which is the clean level-one journey.
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CampaignSaveService service = new CampaignSaveService(storage, legacy);

            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated),
                "Routing input must only exist after migration completed.");
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(pair.Campaign);
            JourneyEntryPoint entry = JourneyEntryResolver.Resolve(service.Current, levelIds);
            Assert.That(entry.Kind, Is.EqualTo(JourneyEntryKind.NewJourney));
            Assert.That(entry.LevelId, Is.EqualTo(levelIds[0]));
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
