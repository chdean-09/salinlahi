using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class TracingDojoControllerTests
    {
        [Test]
        public void BuildSelectableList_ExcludesSymbolsNotYetIntroduced()
        {
            using CampaignTestFixture fixture = CampaignTestFixture.CreateValid();
            var selectable = CharacterListPopulator.BuildSelectableList(
                fixture.Campaign, new HashSet<string> { "symbol.ba" });

            Assert.That(selectable.Select(c => c.stableId), Is.EquivalentTo(new[] { "symbol.ba" }));
        }

        [Test]
        public void ResolveEvidence_MatchingTrace_ProducesFormOnly()
        {
            LearningEvidenceEntry entry = TracingDojoEvidence.Resolve(
                "symbol.ba", "symbol.ba", true);

            Assert.That(entry.dimension, Is.EqualTo(MasteryDimension.Form));
            Assert.That(entry.successCount, Is.EqualTo(1));
            Assert.That(entry.retrievalSuccessCount, Is.EqualTo(1));
        }

        [Test]
        public void ResolveEvidence_FailedTrace_RecordsAttemptWithoutSuccess()
        {
            LearningEvidenceEntry entry = TracingDojoEvidence.Resolve(
                "symbol.ba", "symbol.ma", false);

            Assert.That(entry.attemptCount, Is.EqualTo(1));
            Assert.That(entry.successCount, Is.EqualTo(0));
        }

        [Test]
        public void ResolveEvidence_UsesCanonicalStableIdNotLegacyCharacterId()
        {
            LearningEvidenceEntry entry = TracingDojoEvidence.Resolve(
                "symbol.dara", "symbol.dara", true);

            Assert.That(entry.contentId, Is.EqualTo("symbol.dara"));
        }

        [Test]
        public void BuildBatch_UsesFreePracticeKindAndNoInstructedContent()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.FreePractice);

            LearningEvidenceBatch batch = recorder.Build();

            Assert.That(batch.sessionKind, Is.EqualTo(LearningSessionKind.FreePractice));
            Assert.That(batch.instructedContentIds, Is.Empty);
        }
    }
}
