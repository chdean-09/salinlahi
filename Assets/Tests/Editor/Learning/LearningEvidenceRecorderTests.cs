using System.Linq;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class LearningEvidenceRecorderTests
    {
        [Test]
        public void Build_FoldsRepeatedAttemptsIntoOneEntryPerContentAndDimension()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.LevelAttempt);
            recorder.RecordAttempt("symbol.ba", LearningContentKind.Symbol,
                MasteryDimension.Form, true, false);
            recorder.RecordAttempt("symbol.ba", LearningContentKind.Symbol,
                MasteryDimension.Form, false, false);

            LearningEvidenceBatch batch = recorder.Build();

            Assert.That(batch.entries.Count, Is.EqualTo(1));
            Assert.That(batch.entries[0].attemptCount, Is.EqualTo(2));
            Assert.That(batch.entries[0].successCount, Is.EqualTo(1));
            Assert.That(batch.entries[0].retrievalSuccessCount, Is.EqualTo(1));
        }

        [Test]
        public void Build_VisibleAnswerSuccess_CountsAsSuccessButNotRetrieval()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.LevelAttempt);
            recorder.RecordAttempt("symbol.ba", LearningContentKind.Symbol,
                MasteryDimension.Form, true, true);

            LearningEvidenceEntry entry = recorder.Build().entries.Single();

            Assert.That(entry.successCount, Is.EqualTo(1));
            Assert.That(entry.retrievalSuccessCount, Is.EqualTo(0));
        }

        [Test]
        public void Build_IncludesInstructedContentIdsWithoutDuplicates()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.LevelAttempt);
            recorder.RecordInstruction("symbol.ba", LearningContentKind.Symbol);
            recorder.RecordInstruction("symbol.ba", LearningContentKind.Symbol);

            Assert.That(recorder.Build().instructedContentIds, Is.EquivalentTo(new[] { "symbol.ba" }));
        }

        [Test]
        public void Build_EntriesAreSortedDeterministically()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.LevelAttempt);
            recorder.RecordAttempt("symbol.ma", LearningContentKind.Symbol,
                MasteryDimension.Sound, true, false);
            recorder.RecordAttempt("symbol.ba", LearningContentKind.Symbol,
                MasteryDimension.Form, true, false);

            Assert.That(recorder.Build().entries.Select(e => e.contentId),
                Is.EqualTo(new[] { "symbol.ba", "symbol.ma" }));
        }

        [Test]
        public void Reset_DiscardsEverything()
        {
            var recorder = new LearningEvidenceRecorder(
                "level.ugat.01", LearningSessionKind.LevelAttempt);
            recorder.RecordInstruction("symbol.ba", LearningContentKind.Symbol);
            recorder.RecordAttempt("symbol.ba", LearningContentKind.Symbol,
                MasteryDimension.Form, true, false);
            recorder.Reset();

            LearningEvidenceBatch batch = recorder.Build();

            Assert.That(batch.instructedContentIds, Is.Empty);
            Assert.That(batch.entries, Is.Empty);
        }
    }
}
