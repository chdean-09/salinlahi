using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveMigrationTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "salin227-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [Test]
        public void TryUpgradeToCurrent_PreservesProgressAndAddsCurrentSchemaFields()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument v1 = CampaignSaveSerializer.DeepClone(pair.Document);
            v1.saveSchemaVersion = 1;
            v1.progress.journeyGenerationId = null;
            v1.progress.appliedOutcomeReceipts = null;
            v1.progress.levelProgress[0].completed = true;
            v1.progress.levelProgress[0].bestStars = 2;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeToCurrent(
                v1, pair.Campaign, "journey.00000000000000000000000000000001");

            Assert.That(result.Success, Is.True);
            Assert.That(result.Document.saveSchemaVersion, Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));
            Assert.That(result.Document.progress.levelProgress[0].bestStars, Is.EqualTo(2));
            Assert.That(result.Document.progress.journeyGenerationId,
                Is.EqualTo("journey.00000000000000000000000000000001"));
            Assert.That(result.Document.progress.appliedOutcomeReceipts, Is.Empty);
        }

        [Test]
        public void TryUpgradeToCurrent_WhenSourceIsHigherSchema_BlocksWithoutMutation()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 99;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeToCurrent(
                source, pair.Campaign, "journey.00000000000000000000000000000001");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.UnsupportedSchema));
            Assert.That(source.saveSchemaVersion, Is.EqualTo(99));
        }

        [Test]
        public void TryUpgradeToCurrent_Version2Source_UpgradesRatherThanRejecting()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 2;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeToCurrent(
                source, pair.Campaign, source.progress.journeyGenerationId);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Document.saveSchemaVersion, Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));
        }

        [Test]
        public void TryUpgradeToCurrent_Version1Source_UpgradesThroughTheWholeChain()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = 1;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeToCurrent(
                source, pair.Campaign, source.progress.journeyGenerationId);

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.Document.saveSchemaVersion, Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));
            Assert.That(result.Document.progress.appliedOutcomeReceipts, Is.Empty);
        }

        [Test]
        public void TryUpgradeToCurrent_NewerThanCurrent_IsRejected()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument source = CampaignSaveSerializer.DeepClone(pair.Document);
            source.saveSchemaVersion = CampaignSaveDocument.CurrentSaveSchemaVersion + 1;

            CampaignSaveMigrationResult result = CampaignSaveMigrator.TryUpgradeToCurrent(
                source, pair.Campaign, source.progress.journeyGenerationId);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.UnsupportedSchema));
        }

        // ------------------------------------------------------------------------------------
        // SALIN-227. Everything above builds its "old" document IN MEMORY, which is why two
        // reachability barriers survived in this area for three tickets: an in-memory document is
        // handed straight to the migrator and never has its checksum re-derived. The tests below
        // write real files through CampaignSaveFileStorage and read them back through the real
        // load path instead.
        // ------------------------------------------------------------------------------------

        /// <summary>
        /// V-3. The load-bearing fact behind this ticket's whole design: a save written by a build
        /// whose field set differed from this one's cannot pass the integrity check, because
        /// TryDeserialize re-derives the hash by re-serializing with the CURRENT field set. This is
        /// why no v3 -> v4 migration arm could ever run against a real file, and why pre-bump saves
        /// are reset rather than migrated. Delete the endlessModeUnlocked injection below and this
        /// test goes green for the wrong reason -- the file would no longer be a v3 file.
        /// </summary>
        [Test]
        public void PreBumpSaveOnDisk_FailsIntegrityBecauseTheFieldSetMoved_SALIN227()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveFileStorage storage = new CampaignSaveFileStorage(_root);
            storage.WriteAllTextFlushed(CampaignSaveFileRole.Primary, BuildLegacyV3Json(pair.Document));

            CampaignSaveParseResult parsed = CampaignSaveSerializer.TryDeserialize(
                storage.ReadAllText(CampaignSaveFileRole.Primary));

            Assert.That(parsed.Success, Is.False,
                "A v3 file must not deserialize under the v4 field set.");
            Assert.That(parsed.FailureCode, Is.EqualTo(CampaignSaveFailureCode.ChecksumMismatch),
                "Expected the integrity check to be what rejects it -- any other code means the "
                + "fixture is malformed rather than superseded.");
        }

        /// <summary>
        /// V-6, the ticket's acceptance sentence end to end: a save written before the bump opens
        /// with a safe-reset notice and Level 1 is playable. Also pins the Step 2 honesty fix --
        /// the quarantined file is filed as superseded, not as corruption.
        /// </summary>
        [Test]
        public void PreBumpSaveOnDisk_SafeResetsWithNoticeAndLevelOnePlayable_SALIN227()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveFileStorage storage = new CampaignSaveFileStorage(_root);
            storage.WriteAllTextFlushed(CampaignSaveFileRole.Primary, BuildLegacyV3Json(pair.Document));

            CampaignSaveService service = new CampaignSaveService(
                storage, new NoLegacyData(), new FixedMetadata());
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.SafeReset),
                "A superseded save must reset, not block the boot.");
            Assert.That(service.Current.recovery.reasonCode, Is.EqualTo("safe-reset"));
            Assert.That(service.Current.recovery.noticeAcknowledged, Is.False,
                "The notice must still be pending so the player actually sees it.");
            Assert.That(service.Current.saveSchemaVersion, Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));

            LevelProgressRecord first = service.Current.progress.levelProgress[0];
            Assert.That(first.levelId, Is.EqualTo("level.ugat.01"));
            Assert.That(first.unlocked, Is.True, "AC-4: Level 1 must be playable after the reset.");
            Assert.That(first.completed, Is.False);
            Assert.That(service.Current.progress.activeLevelId, Is.EqualTo("level.ugat.01"));

            string[] quarantined = Directory.GetFiles(_root, "*.quarantine.*");
            Assert.That(quarantined.Length, Is.EqualTo(1));
            Assert.That(Path.GetFileName(quarantined[0]), Does.Contain("superseded-schema"),
                "A superseded save filed as corrupt-primary misinforms whoever reads the "
                + "quarantine later; that misdiagnosis is the defect this ticket fixes.");
            Assert.That(Path.GetFileName(quarantined[0]), Does.Not.Contain("corrupt-primary"));
        }

        /// <summary>
        /// V-4. Negative control for Barrier B, and a defect that predates this ticket: the load
        /// path routed ONLY `saveSchemaVersion == 1` to the migrator, so the v2 -> v3 arm that has
        /// shipped since SALIN-171 could never run against a file. Revert the range test in
        /// CampaignSaveService.Inspect to `== 1` and this test fails -- the v2 file falls through to
        /// Validate and is rejected as InvalidStructure, safe-resetting instead of migrating.
        /// </summary>
        [Test]
        public void Version2SaveOnDisk_ReachesTheMigratorInsteadOfBeingRejected_SALIN227()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignSaveDocument v2 = CampaignSaveSerializer.DeepClone(pair.Document);
            v2.saveSchemaVersion = 2;
            CampaignSaveFileStorage storage = new CampaignSaveFileStorage(_root);
            storage.WriteAllTextFlushed(
                CampaignSaveFileRole.Primary, CampaignSaveSerializer.Serialize(v2));

            CampaignSaveService service = new CampaignSaveService(
                storage, new NoLegacyData(), new FixedMetadata());
            CampaignSaveInitializationResult result = service.Initialize(pair.Campaign);

            Assert.That(result.Status, Is.EqualTo(CampaignSaveInitializationStatus.Migrated),
                "A v2 save read from disk must reach the migrator.");
            Assert.That(service.Current.saveSchemaVersion, Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));
            Assert.That(CampaignSaveSerializer.TryDeserialize(
                    storage.ReadAllText(CampaignSaveFileRole.Primary)).Document.saveSchemaVersion,
                Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion), "The upgrade must be published back to the file, not just held in memory.");
        }

        /// <summary>
        /// V-5. SupersededSchema must stay OUT of CampaignSaveRecoveryResolver.IsBlocking. Adding it
        /// there is the intuitive edit and it is wrong: it yields a Blocked decision, the boot
        /// refuses rather than resetting, and AC-4 fails. Add SupersededSchema to IsBlocking and
        /// this test goes red.
        /// </summary>
        [Test]
        public void SupersededSchema_IsNotBlocking_SoTheBootReachesSafeReset_SALIN227()
        {
            CandidateInspection superseded = new CandidateInspection
            {
                Role = CampaignSaveFileRole.Primary,
                Exists = true,
                FailureCode = CampaignSaveFailureCode.SupersededSchema,
            };

            RecoveryDecision decision = CampaignSaveRecoveryResolver.Resolve(
                superseded,
                CandidateInspection.Missing(CampaignSaveFileRole.Temporary),
                CandidateInspection.Missing(CampaignSaveFileRole.Backup));

            Assert.That(decision.Kind, Is.Not.EqualTo(RecoveryDecisionKind.Blocked),
                "A superseded save must never block the boot.");
            Assert.That(decision.Kind, Is.EqualTo(RecoveryDecisionKind.CorruptRevisedData),
                "It must still take the quarantine-then-safe-reset route.");
        }

        /// <summary>
        /// V-7. The manifest-tampering guard still bites. Bumping the SAVE DOCUMENT schema must not
        /// tempt anyone into "lining up" CampaignIdentityManifest.saveSchemaVersion, which is an
        /// unrelated field read only by IsRevisedV1. Doing so fails every gate with InvalidCampaign.
        /// </summary>
        [Test]
        public void ManifestSaveSchemaLiteral_IsUnrelatedToTheDocumentSchema_SALIN227()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            Assert.That(pair.Campaign.manifest.IsRevisedV1, Is.True,
                "Precondition: the untouched manifest is revised v1.");
            Assert.That(pair.Campaign.manifest.saveSchemaVersion, Is.EqualTo(1),
                "The manifest literal stays at 1 however far the document schema advances.");
            Assert.That(CampaignSaveDocument.CurrentSaveSchemaVersion,
                Is.Not.EqualTo(pair.Campaign.manifest.saveSchemaVersion),
                "These are different fields. Pinning a literal here only breaks on every bump; "
                + "what matters is that they are not the same number.");

            pair.Campaign.manifest.saveSchemaVersion = CampaignSaveDocument.CurrentSaveSchemaVersion;

            Assert.That(pair.Campaign.manifest.IsRevisedV1, Is.False,
                "Matching the manifest literal to the document schema flips IsRevisedV1 false.");
            CampaignSaveValidationResult result =
                CampaignSaveValidator.Validate(pair.Document, pair.Campaign);
            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.InvalidCampaign),
                "...which cascades to InvalidCampaign and boots back to RevisedBlocked.");
        }

        /// <summary>
        /// Reconstructs, byte for byte, the file a v3 build would have written: the same field
        /// order, `endlessModeUnlocked` present, and an integrity hash computed over THAT field
        /// set. Built by text surgery on purpose -- keeping a frozen CampaignSaveDocumentV3 class
        /// alive just to produce a fixture is the permanent versioned-shadow cost this ticket
        /// declined to take on.
        /// </summary>
        private static string BuildLegacyV3Json(CampaignSaveDocument document)
        {
            CampaignSaveDocument clone = CampaignSaveSerializer.DeepClone(document);
            clone.saveSchemaVersion = 3;
            clone.integritySha256 = string.Empty;
            string unsigned = JsonUtility.ToJson(clone, false);

            Assert.That(unsigned, Does.Not.Contain("endlessModeUnlocked"),
                "The field must already be gone from the class, or this fixture is not a v3 file.");
            Assert.That(unsigned, Does.EndWith("}}"),
                "`progress` is the document's last field, so its object closes just before the "
                + "document's own brace -- that is where the dropped field used to sit.");
            unsigned = unsigned.Substring(0, unsigned.Length - 2)
                + ",\"endlessModeUnlocked\":false}}";

            string hash = CampaignSaveSerializer.ComputeSha256(unsigned);
            return unsigned.Replace(
                "\"integritySha256\":\"\"", "\"integritySha256\":\"" + hash + "\"");
        }

        private sealed class NoLegacyData : ILegacyProgressSource
        {
            public bool HasKey(string key) => false;
            public int GetInt(string key, int defaultValue) => defaultValue;
            public float GetFloat(string key, float defaultValue) => defaultValue;
            public string GetString(string key, string defaultValue) => defaultValue;
        }

        private sealed class FixedMetadata : ITransactionMetadataProvider
        {
            public DateTime UtcNow => new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc);
            public string CreateTransactionId() => Guid.NewGuid().ToString("N");
        }
    }
}
