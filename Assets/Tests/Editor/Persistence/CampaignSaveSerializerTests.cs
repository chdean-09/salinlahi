using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class CampaignSaveSerializerTests
    {
        [Test]
        public void SerializeThenDeserialize_RoundTripsAndValidatesIntegrity()
        {
            CampaignSaveDocument source = CreateValidDocument();

            string json = CampaignSaveSerializer.Serialize(source);
            CampaignSaveParseResult result = CampaignSaveSerializer.TryDeserialize(json);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Document.progress.activeLevelId, Is.EqualTo("level.ugat.01"));
            Assert.That(result.Document.integritySha256, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void TryDeserialize_WhenPayloadChanges_ReturnsChecksumMismatch()
        {
            string json = CampaignSaveSerializer.Serialize(CreateValidDocument());
            string tampered = json.Replace("level.ugat.01", "level.ugat.02");

            CampaignSaveParseResult result = CampaignSaveSerializer.TryDeserialize(tampered);

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.ChecksumMismatch));
        }

        [Test]
        public void TryDeserialize_WhenJsonIsMalformed_ReturnsMalformedJson()
        {
            CampaignSaveParseResult result = CampaignSaveSerializer.TryDeserialize("{");

            Assert.That(result.FailureCode, Is.EqualTo(CampaignSaveFailureCode.MalformedJson));
        }

        [Test]
        public void Serialize_NormalizesExistingChecksumBeforeComputingIt()
        {
            CampaignSaveDocument source = CreateValidDocument();
            source.integritySha256 = "NOT-A-CHECKSUM";

            string json = CampaignSaveSerializer.Serialize(source);
            CampaignSaveParseResult result = CampaignSaveSerializer.TryDeserialize(json);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Document.integritySha256, Does.Match("^[0-9a-f]{64}$"));
        }

        private static CampaignSaveDocument CreateValidDocument()
        {
            return new CampaignSaveDocument
            {
                campaignId = ContentIdentity.RevisedCampaignId,
                contentSchemaVersion = 1,
                transactionId = "transaction.test.01",
                revision = 1,
                transactionState = CampaignSaveTransactionState.Committed,
                createdAtUtc = "2026-08-13T00:00:00.0000000Z",
                updatedAtUtc = "2026-08-13T00:00:00.0000000Z",
                progress = new CampaignProgressData
                {
                    activeLevelId = "level.ugat.01",
                    levelProgress = new List<LevelProgressRecord>
                    {
                        new LevelProgressRecord { levelId = "level.ugat.01", unlocked = true },
                    },
                },
            };
        }
    }
}
