using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignOutcomeSerializerTests
    {
        [Test]
        public void SerializeThenDeserialize_RoundTripsChecksum()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            CampaignOutcomeJournalDocument document = new CampaignOutcomeJournalDocument { outcome = outcome };

            string json = CampaignOutcomeSerializer.Serialize(document);
            CampaignOutcomeJournalParseResult result = CampaignOutcomeSerializer.TryDeserialize(json);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Document.integritySha256, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(result.Document.outcome.outcomeId, Is.EqualTo(outcome.outcomeId));
        }

        /// <summary>
        /// SALIN-220. The flags are the journal's record of what a completion earned; if they do
        /// not survive the round trip, a recovered outcome would re-apply as if the objectives
        /// were never met and the gate would withhold an unlock the player already earned.
        /// </summary>
        [Test]
        public void SerializeThenDeserialize_RoundTripsEveryObjectiveFlag_SALIN220()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignProgressOutcome outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document);
            outcome.contextPassed = false;

            CampaignOutcomeJournalParseResult result = CampaignOutcomeSerializer.TryDeserialize(
                CampaignOutcomeSerializer.Serialize(
                    new CampaignOutcomeJournalDocument { outcome = outcome }));

            Assert.That(result.Success, Is.True);
            CampaignProgressOutcome restored = result.Document.outcome;
            Assert.That(restored.outcomeSchemaVersion, Is.EqualTo(4));
            Assert.That(restored.storyViewed, Is.True);
            Assert.That(restored.symbolsPracticed, Is.True);
            Assert.That(restored.wordsRestored, Is.True);
            Assert.That(restored.contextPassed, Is.False, "A false flag must round-trip as false.");
            Assert.That(restored.finalSyllableRestored, Is.True);
            Assert.That(LevelObjectiveGate.CarriesAnyFlag(restored), Is.True);
        }

        [Test]
        public void TryDeserialize_WhenPayloadChanges_ReturnsChecksumMismatch()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            string json = CampaignOutcomeSerializer.Serialize(
                new CampaignOutcomeJournalDocument
                {
                    outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document),
                });

            CampaignOutcomeJournalParseResult result = CampaignOutcomeSerializer.TryDeserialize(
                json.Replace("level.ugat.01", "level.ugat.02"));

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.ChecksumMismatch));
        }

        [Test]
        public void TryDeserialize_WhenJournalSchemaIsHigher_ReturnsUnsupportedSchema()
        {
            using CampaignSaveTestPair pair = CampaignSaveTestPair.CreateValidPair();
            CampaignOutcomeJournalDocument document = new CampaignOutcomeJournalDocument
            {
                journalSchemaVersion = CampaignOutcomeJournalDocument.CurrentJournalSchemaVersion + 1,
                outcome = CampaignSaveTestFactory.CreateValidOutcome(pair.Document),
            };

            CampaignOutcomeJournalParseResult result = CampaignOutcomeSerializer.TryDeserialize(
                CampaignOutcomeSerializer.Serialize(document));

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.UnsupportedSchema));
        }
    }
}
