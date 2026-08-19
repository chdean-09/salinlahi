using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class LegacyArchiveServiceTests
    {
        [Test]
        public void Catalog_ContainsExactly46UniqueKeys()
        {
            HashSet<string> keys = new HashSet<string>();
            for (int i = 0; i < LegacyProgressKeyCatalog.All.Count; i++)
                Assert.That(keys.Add(LegacyProgressKeyCatalog.All[i].Key), Is.True);
            Assert.That(keys.Count, Is.EqualTo(46));
        }

        [Test]
        public void LoadOrCreate_WithExistingCompatibleArchive_LoadsWithoutRewriting()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            Func<DateTime> now = () => new DateTime(2026, 8, 19, 0, 0, 0, DateTimeKind.Utc);
            LegacyArchiveLoadResult created = new LegacyArchiveService(storage, legacy, now)
                .LoadOrCreate(pair.Campaign);
            Assert.That(created.Status, Is.EqualTo(LegacyArchiveStatus.Created));
            string archiveJson = storage.ReadAllText(CampaignSaveFileRole.LegacyArchive);

            LegacyArchiveLoadResult loaded = new LegacyArchiveService(storage, legacy, now)
                .LoadOrCreate(pair.Campaign);

            Assert.That(loaded.Status, Is.EqualTo(LegacyArchiveStatus.LoadedExisting));
            Assert.That(loaded.IntegritySha256, Is.EqualTo(created.IntegritySha256));
            Assert.That(storage.ReadAllText(CampaignSaveFileRole.LegacyArchive), Is.EqualTo(archiveJson));
        }

        [Test]
        public void LoadOrCreate_CapturesPresentValuesAndMarksAbsentKeys()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = new DictionaryLegacySource();
            legacy.SetInt("salinlahi.progress.unlocked.3", 1);
            legacy.SetFloat("salinlahi.audio.bgm_volume", 0.25f);

            LegacyArchiveLoadResult result = new LegacyArchiveService(storage, legacy)
                .LoadOrCreate(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(LegacyArchiveStatus.Created));
            LegacyProgressRecord unlocked = FindRecord(result.Archive, "salinlahi.progress.unlocked.3");
            Assert.That(unlocked.wasPresent, Is.True);
            Assert.That(unlocked.intValue, Is.EqualTo(1));
            LegacyProgressRecord bgm = FindRecord(result.Archive, "salinlahi.audio.bgm_volume");
            Assert.That(bgm.wasPresent, Is.True);
            Assert.That(bgm.floatValue, Is.EqualTo(0.25f).Within(0.0001f));
            LegacyProgressRecord absent = FindRecord(result.Archive, "salinlahi.progress.stars.9");
            Assert.That(absent.wasPresent, Is.False);
        }

        [Test]
        public void LoadOrCreate_WhenArchiveTargetsDifferentCampaign_QuarantinesAndRebuilds()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            InMemoryCampaignSaveStorage storage = new InMemoryCampaignSaveStorage();
            DictionaryLegacySource legacy = DictionaryLegacySource.CreateRepresentativeHistoricalSave();
            LegacyArchiveService service = new LegacyArchiveService(storage, legacy);
            LegacyProgressArchive foreign = service.LoadOrCreate(pair.Campaign).Archive;
            foreign.targetCampaignId = "campaign.someone.else";
            // Serialize recomputes the checksum, so the file is well-formed but incompatible.
            storage.Set(CampaignSaveFileRole.LegacyArchive, LegacyArchiveSerializer.Serialize(foreign));

            LegacyArchiveLoadResult result = new LegacyArchiveService(storage, legacy)
                .LoadOrCreate(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(LegacyArchiveStatus.Rebuilt));
            Assert.That(storage.QuarantinedRoles, Does.Contain(CampaignSaveFileRole.LegacyArchive));
            Assert.That(result.Archive.targetCampaignId, Is.EqualTo(pair.Campaign.manifest.campaignId));
        }

        private static LegacyProgressRecord FindRecord(LegacyProgressArchive archive, string key)
        {
            for (int i = 0; i < archive.records.Count; i++)
                if (string.Equals(archive.records[i].key, key, StringComparison.Ordinal))
                    return archive.records[i];
            Assert.Fail("Archive is missing record for key: " + key);
            return null;
        }
    }
}
