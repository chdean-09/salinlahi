using System;
using System.Collections.Generic;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// SALIN-143 end-to-end resume-safety scenarios. "Relaunch" is a fresh
    /// CampaignSaveService over the same storage; "interruption" is a one-shot
    /// StorageFaultPoint.
    /// </summary>
    public sealed class CampaignSaveResumeSafetyTests
    {
        [Test]
        public void Initialize_FreshInstall_CreatesCleanJourneyWithoutArchiveOrNotice()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = CreateService(storage, new DictionaryLegacySource());

            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Ready));
            Assert.That(storage.Exists(CampaignSaveFileRole.LegacyArchive), Is.False);
            Assert.That(service.Current.migration.state, Is.EqualTo(CampaignMigrationState.NotRequired));
            Assert.That(service.Current.progress.activeLevelId, Is.EqualTo(FirstLevelId(pair.Campaign)));
            CampaignProgressRepository repository = new CampaignProgressRepository(service, pair.Campaign);
            Assert.That(repository.GetPendingNotice().kind, Is.EqualTo(CampaignSaveNoticeKind.None));
        }

        [Test]
        public void Initialize_WithHistoricalPlayerPrefs_ArchivesThenStartsCleanJourneyAtLevelOne()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CampaignSaveService service = CreateService(storage, legacy);

            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(storage.Exists(CampaignSaveFileRole.LegacyArchive), Is.True);
            LegacyArchiveParseResult archive = LegacyArchiveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.LegacyArchive));
            Assert.That(archive.Success, Is.True);
            Assert.That(archive.Archive.records.Count, Is.EqualTo(LegacyProgressKeyCatalog.All.Count));
            Assert.That(archive.Archive.targetCampaignId, Is.EqualTo(pair.Campaign.manifest.campaignId));

            // The committed save references the archive checksum: the archive necessarily
            // existed and validated before the revised save was published (AC1 ordering).
            Assert.That(service.Current.migration.state, Is.EqualTo(CampaignMigrationState.Completed));
            Assert.That(service.Current.migration.migrationId, Is.EqualTo("legacy-v0-to-revised-v1"));
            Assert.That(service.Current.migration.legacyArchiveSha256, Is.EqualTo(archive.IntegritySha256));
            Assert.That(service.Current.migration.noticeAcknowledged, Is.False);

            AssertCleanLevelOneState(service.Current, pair.Campaign);

            CampaignProgressRepository repository = new CampaignProgressRepository(service, pair.Campaign);
            CampaignSaveNotice notice = repository.GetPendingNotice();
            Assert.That(notice.kind, Is.EqualTo(CampaignSaveNoticeKind.Migration));
            Assert.That(notice.reasonCode, Is.EqualTo("migration-completed"));
        }

        [Test]
        public void Initialize_WithHistoricalPlayerPrefs_CapturesAudioPreferencesAndLeavesSourceReadable()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CampaignSaveService service = CreateService(storage, legacy);

            service.Initialize(pair.Campaign);

            LegacyProgressArchive archive = LegacyArchiveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.LegacyArchive)).Archive;
            AssertArchivedFloat(archive, "salinlahi.audio.master_volume", 0.8f);
            AssertArchivedFloat(archive, "salinlahi.audio.bgm_volume", 0.55f);
            AssertArchivedFloat(archive, "salinlahi.audio.sfx_volume", 0.35f);

            // ILegacyProgressSource is read-only by contract, so migration cannot delete
            // preferences; AudioManager keeps reading the same keys afterwards (AC3).
            Assert.That(legacy.GetFloat("salinlahi.audio.master_volume", -1f), Is.EqualTo(0.8f));
            Assert.That(legacy.GetFloat("salinlahi.audio.bgm_volume", -1f), Is.EqualTo(0.55f));
            Assert.That(legacy.GetFloat("salinlahi.audio.sfx_volume", -1f), Is.EqualTo(0.35f));
        }

        [Test]
        public void Initialize_WhenArchiveWriteFails_BlocksWithoutWritingThenRetrySucceeds()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage
            {
                FailAt = StorageFaultPoint.ArchiveWrite,
            };
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();

            CampaignSaveInitializationResult blocked = CreateService(storage, legacy).Initialize(pair.Campaign);

            Assert.That(blocked.Status, Is.EqualTo(CampaignSaveInitializationStatus.BlockedIo));
            Assert.That(blocked.ReasonCode, Is.EqualTo("archive-io-failure"));
            Assert.That(storage.Exists(CampaignSaveFileRole.LegacyArchive), Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.Primary), Is.False);
            Assert.That(storage.Exists(CampaignSaveFileRole.Temporary), Is.False);

            // FailAt is one-shot; the relaunch models the player retrying after the I/O issue clears.
            CampaignSaveService relaunched = CreateService(storage, legacy);
            CampaignSaveInitializationResult retried = relaunched.Initialize(pair.Campaign);

            Assert.That(retried.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(storage.Exists(CampaignSaveFileRole.LegacyArchive), Is.True);
            AssertCleanLevelOneState(relaunched.Current, pair.Campaign);
        }

        [Test]
        public void Initialize_WhenInitialCommitInterrupted_RelaunchReusesArchiveByteIdentically()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage
            {
                FailAt = StorageFaultPoint.TemporaryWrite,
            };
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();

            CampaignSaveInitializationResult blocked = CreateService(storage, legacy).Initialize(pair.Campaign);

            Assert.That(blocked.Status, Is.EqualTo(CampaignSaveInitializationStatus.BlockedIo));
            Assert.That(blocked.ReasonCode, Is.EqualTo("initial-save-failed"));
            Assert.That(storage.Exists(CampaignSaveFileRole.LegacyArchive), Is.True,
                "The archive must be written before the fresh journey commit is attempted.");
            Assert.That(storage.Exists(CampaignSaveFileRole.Primary), Is.False);
            string archiveJson = storage.ReadAllText(CampaignSaveFileRole.LegacyArchive);

            CampaignSaveService relaunched = CreateService(storage, legacy);
            CampaignSaveInitializationResult resumed = relaunched.Initialize(pair.Campaign);

            Assert.That(resumed.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.LegacyArchive), Is.EqualTo(archiveJson),
                "Re-running migration must reuse the existing archive, not rewrite it.");
            Assert.That(relaunched.Current.migration.state, Is.EqualTo(CampaignMigrationState.Completed));
            Assert.That(relaunched.Current.migration.legacyArchiveSha256,
                Is.EqualTo(LegacyArchiveSerializer.TryDeserialize(archiveJson).IntegritySha256));
            // Never-merges guard: the re-run yields a clean Level-1 state, not merged progress.
            AssertCleanLevelOneState(relaunched.Current, pair.Campaign);

            CampaignProgressRepository repository = new CampaignProgressRepository(relaunched, pair.Campaign);
            Assert.That(repository.GetPendingNotice().kind, Is.EqualTo(CampaignSaveNoticeKind.Migration));
        }

        [Test]
        public void Initialize_RelaunchAfterSuccessfulMigration_IsReadyAndDoesNotTouchArchive()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CreateService(storage, legacy).Initialize(pair.Campaign);
            string archiveJson = storage.ReadAllText(CampaignSaveFileRole.LegacyArchive);
            string primaryJson = storage.ReadAllText(CampaignSaveFileRole.Primary);

            CampaignSaveService relaunched = CreateService(storage, legacy);
            CampaignSaveInitializationResult result = relaunched.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Ready));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.LegacyArchive), Is.EqualTo(archiveJson));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.Primary), Is.EqualTo(primaryJson),
                "A plain relaunch must not rewrite the primary save.");
            Assert.That(relaunched.Current.migration.state, Is.EqualTo(CampaignMigrationState.Completed));
        }

        [Test]
        public void AcknowledgedMigrationNotice_DoesNotReturnAfterRelaunch()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CampaignSaveService service = CreateService(storage, legacy);
            service.Initialize(pair.Campaign);
            CampaignProgressRepository repository = new CampaignProgressRepository(service, pair.Campaign);
            Assert.That(repository.GetPendingNotice().kind, Is.EqualTo(CampaignSaveNoticeKind.Migration));

            Assert.That(repository.TryAcknowledgePendingNotice(), Is.True);
            Assert.That(repository.GetPendingNotice().kind, Is.EqualTo(CampaignSaveNoticeKind.None));

            CampaignSaveService relaunched = CreateService(storage, legacy);
            relaunched.Initialize(pair.Campaign);
            CampaignProgressRepository relaunchedRepository =
                new CampaignProgressRepository(relaunched, pair.Campaign);

            Assert.That(relaunchedRepository.GetPendingNotice().kind,
                Is.EqualTo(CampaignSaveNoticeKind.None),
                "Acknowledgment must persist across relaunch — the player is prompted exactly once.");
        }

        [Test]
        public void ReplayPendingOnStartup_WithStaleGenerationJournal_DoesNotApplyOutcome()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveService service = CreateService(storage, new DictionaryLegacySource());
            service.Initialize(pair.Campaign);

            // A journal left behind by a previous journey generation (e.g. pre-reset or
            // pre-migration identity) must never be applied to the current journey.
            CampaignProgressOutcome stale = CampaignSaveTestFactory.CreateValidOutcome(service.Current);
            stale.journeyGenerationId = "journey.ffffffffffffffffffffffffffffffff";
            storage.Set(CampaignSaveFileRole.PendingOutcome,
                CampaignOutcomeSerializer.Serialize(new CampaignOutcomeJournalDocument { outcome = stale }));

            CampaignSaveService relaunched = CreateService(storage, new DictionaryLegacySource());
            relaunched.Initialize(pair.Campaign);
            FixedMetadata metadata = new FixedMetadata();
            CampaignOutcomeCoordinator coordinator = new CampaignOutcomeCoordinator(
                relaunched,
                new CampaignOutcomeJournal(storage, pair.Campaign, metadata),
                pair.Campaign,
                metadata);

            CampaignOutcomeCommitResult replay = coordinator.ReplayPendingOnStartup();

            Assert.That(replay.Status, Is.EqualTo(CampaignOutcomeCommitStatus.Rejected));
            Assert.That(relaunched.Current.progress.appliedOutcomeReceipts, Is.Empty);
            Assert.That(relaunched.Current.progress.claimedRewardIds, Is.Empty);
            Assert.That(storage.Exists(CampaignSaveFileRole.PendingOutcome), Is.False,
                "The stale journal must be quarantined so it cannot re-trigger on every launch.");
            Assert.That(storage.QuarantinedRoles, Does.Contain(CampaignSaveFileRole.PendingOutcome));
        }

        [Test]
        public void Initialize_WithCorruptArchiveAndLegacyDataPresent_QuarantinesAndRebuilds()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            storage.Set(CampaignSaveFileRole.LegacyArchive, "{ this is not a valid archive");
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();

            CampaignSaveService service = CreateService(storage, legacy);
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(storage.QuarantinedRoles, Does.Contain(CampaignSaveFileRole.LegacyArchive));
            LegacyArchiveParseResult rebuilt = LegacyArchiveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.LegacyArchive));
            Assert.That(rebuilt.Success, Is.True);
            Assert.That(rebuilt.Archive.records.Count, Is.EqualTo(LegacyProgressKeyCatalog.All.Count));
            Assert.That(service.Current.migration.legacyArchiveSha256, Is.EqualTo(rebuilt.IntegritySha256));
        }

        [Test]
        public void Initialize_WithCorruptArchiveAndNoLegacyData_SafeResetsWithRecoveryNotice()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            storage.Set(CampaignSaveFileRole.LegacyArchive, "{ this is not a valid archive");

            CampaignSaveService service = CreateService(storage, new DictionaryLegacySource());
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.SafeReset));
            Assert.That(service.Current.recovery.reasonCode, Is.EqualTo("safe-reset"));
            AssertCleanLevelOneState(service.Current, pair.Campaign);
            CampaignProgressRepository repository = new CampaignProgressRepository(service, pair.Campaign);
            Assert.That(repository.GetPendingNotice().kind, Is.EqualTo(CampaignSaveNoticeKind.Recovery));
        }

        [Test]
        public void Initialize_WithCorruptRevisedSaveAndValidArchive_QuarantinesAndRemigrates()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            CreateService(storage, legacy).Initialize(pair.Campaign);
            string archiveJson = storage.ReadAllText(CampaignSaveFileRole.LegacyArchive);

            // Corrupt every revised save role that exists; the archive stays valid.
            storage.Set(CampaignSaveFileRole.Primary, "{ corrupt save");
            if (storage.Exists(CampaignSaveFileRole.Backup))
                storage.Set(CampaignSaveFileRole.Backup, "{ corrupt backup");
            if (storage.Exists(CampaignSaveFileRole.Temporary))
                storage.Set(CampaignSaveFileRole.Temporary, "{ corrupt temporary");

            CampaignSaveService relaunched = CreateService(storage, legacy);
            CampaignSaveInitializationResult result = relaunched.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated));
            Assert.That(storage.QuarantinedRoles, Does.Contain(CampaignSaveFileRole.Primary));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.LegacyArchive), Is.EqualTo(archiveJson),
                "Re-migration after corrupt revised data must reuse the existing archive.");
            AssertCleanLevelOneState(relaunched.Current, pair.Campaign);
        }

        [Test]
        public void Initialize_RelaunchAfterSafeReset_IsReadyAndReusesTheSafeResetJourney()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            storage.Set(CampaignSaveFileRole.LegacyArchive, "{ this is not a valid archive");
            CampaignSaveInitializationResult first =
                CreateService(storage, new DictionaryLegacySource()).Initialize(pair.Campaign);

            CampaignSaveService relaunched = CreateService(storage, new DictionaryLegacySource());
            CampaignSaveInitializationResult second = relaunched.Initialize(pair.Campaign);

            Assert.That(first.Status, Is.EqualTo(CampaignSaveInitializationStatus.SafeReset));
            Assert.That(second.Status, Is.EqualTo(CampaignSaveInitializationStatus.Ready),
                "A safe reset must not repeat on the next launch.");
            Assert.That(second.Document.progress.journeyGenerationId,
                Is.EqualTo(first.Document.progress.journeyGenerationId),
                "Relaunch must reuse the safe-reset journey, not create another.");
            Assert.That(relaunched.Current.recovery.reasonCode, Is.EqualTo("safe-reset"),
                "The recovery receipt must persist for the pending notice.");
        }

        [Test]
        public void Initialize_RepeatedLaunchWithHigherSchema_StaysBlockedWithoutMutatingFiles()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 99;
            string original = CampaignSaveSerializer.Serialize(source);
            storage.Set(CampaignSaveFileRole.Primary, original);

            CampaignSaveInitializationResult first =
                CreateService(storage, new DictionaryLegacySource()).Initialize(pair.Campaign);
            CampaignSaveInitializationResult second =
                CreateService(storage, new DictionaryLegacySource()).Initialize(pair.Campaign);

            Assert.That(first.Status, Is.EqualTo(CampaignSaveInitializationStatus.BlockedUnsupportedSchema));
            Assert.That(second.Status, Is.EqualTo(CampaignSaveInitializationStatus.BlockedUnsupportedSchema),
                "Blocked stays deterministically blocked across launches.");
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.Primary), Is.EqualTo(original),
                "A newer-version save must never be modified.");
            Assert.That(storage.QuarantinedRoles, Is.Empty,
                "Blocked is not corruption; nothing may be quarantined.");
        }

        private static void AssertArchivedFloat(LegacyProgressArchive archive, string key, float expected)
        {
            for (int i = 0; i < archive.records.Count; i++)
            {
                if (!string.Equals(archive.records[i].key, key, StringComparison.Ordinal))
                    continue;
                Assert.That(archive.records[i].wasPresent, Is.True, key);
                Assert.That(archive.records[i].floatValue, Is.EqualTo(expected).Within(0.0001f), key);
                return;
            }
            Assert.Fail("Archive is missing record for key: " + key);
        }

        private static CampaignSaveService CreateService(
            InMemoryCampaignSaveStorage storage, ILegacyProgressSource legacySource)
        {
            return new CampaignSaveService(storage, legacySource, new FixedMetadata());
        }

        private static string FirstLevelId(CampaignConfigSO campaign)
        {
            return CampaignSaveValidator.GetConfiguredLevelIds(campaign)[0];
        }

        private static void AssertCleanLevelOneState(CampaignSaveDocument document, CampaignConfigSO campaign)
        {
            List<string> levelIds = CampaignSaveValidator.GetConfiguredLevelIds(campaign);
            Assert.That(document.progress.activeLevelId, Is.EqualTo(levelIds[0]));
            Assert.That(document.progress.levelProgress.Count, Is.EqualTo(levelIds.Count));
            for (int i = 0; i < document.progress.levelProgress.Count; i++)
            {
                LevelProgressRecord record = document.progress.levelProgress[i];
                Assert.That(record.unlocked, Is.EqualTo(i == 0), record.levelId);
                Assert.That(record.completed, Is.False, record.levelId);
                Assert.That(record.bestStars, Is.EqualTo(0), record.levelId);
            }
            Assert.That(document.progress.appliedOutcomeReceipts, Is.Empty);
            Assert.That(document.progress.unlockedSymbolIds, Is.Empty);
            Assert.That(document.progress.unlockedMemoryIds, Is.Empty);
            Assert.That(document.progress.claimedRewardIds, Is.Empty);
            Assert.That(document.progress.journeyGenerationId, Does.StartWith("journey."));
        }

        private sealed class FixedMetadata : ITransactionMetadataProvider
        {
            public DateTime UtcNow => new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);
            public string CreateTransactionId() => Guid.NewGuid().ToString("N");
        }
    }
}
