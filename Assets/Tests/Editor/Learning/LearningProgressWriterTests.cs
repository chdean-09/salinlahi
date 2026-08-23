using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class LearningProgressWriterTests
    {
        private LearningTuningSO _tuning;
        private CampaignProgressData _progress;

        [SetUp]
        public void SetUp()
        {
            _tuning = ScriptableObject.CreateInstance<LearningTuningSO>();
            _progress = new CampaignProgressData();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        private static LearningEvidenceBatch Batch(
            string levelId = "level.ugat.01",
            LearningSessionKind kind = LearningSessionKind.LevelAttempt,
            string[] instructed = null)
        {
            return new LearningEvidenceBatch
            {
                levelId = levelId,
                sessionKind = kind,
                instructedContentIds = (instructed ?? new string[0]).ToList(),
                entries = new List<LearningEvidenceEntry>(),
            };
        }

        private static LearningEvidenceEntry Entry(
            string contentId,
            MasteryDimension dimension,
            int attempts = 1,
            int successes = 1,
            int retrievals = 1,
            LearningContentKind kind = LearningContentKind.Symbol)
        {
            return new LearningEvidenceEntry
            {
                contentId = contentId,
                contentKind = kind,
                dimension = dimension,
                attemptCount = attempts,
                successCount = successes,
                retrievalSuccessCount = retrievals,
            };
        }

        private SymbolMasteryRecord Symbol(string id) =>
            _progress.symbolMastery.Single(record => record.symbolId == id);

        private DimensionEvidence Form(string id) =>
            Symbol(id).dimensions.Single(d => d.dimension == MasteryDimension.Form);

        [Test]
        public void Apply_Instruction_SeedsEveryApplicableDimensionAtIntroduced()
        {
            LearningProgressWriter.Apply(_progress,
                Batch(instructed: new[] { "symbol.ba" }), _tuning);

            SymbolMasteryRecord record = Symbol("symbol.ba");
            Assert.That(record.dimensions.Count, Is.EqualTo(3));
            Assert.That(record.dimensions.All(d => d.highWaterState == MasteryState.Introduced), Is.True);
            Assert.That(record.introducedAtLevelId, Is.EqualTo("level.ugat.01"));
        }

        [Test]
        public void Apply_Instruction_RecordsNoAttempt()
        {
            LearningProgressWriter.Apply(_progress,
                Batch(instructed: new[] { "symbol.ba" }), _tuning);

            Assert.That(Symbol("symbol.ba").dimensions.Sum(d => d.immediateAttempts), Is.EqualTo(0));
            Assert.That(Symbol("symbol.ba").dimensions.Sum(d => d.delayedAttempts), Is.EqualTo(0));
        }

        [Test]
        public void Apply_EvidenceForNeverInstructedContent_CreatesNoRecord()
        {
            LearningEvidenceBatch batch = Batch();
            batch.entries.Add(Entry("symbol.ba", MasteryDimension.Form));

            LearningProgressWriter.Apply(_progress, batch, _tuning);

            Assert.That(_progress.symbolMastery, Is.Empty);
        }

        [Test]
        public void Apply_InstructedContent_CountsAsImmediate()
        {
            LearningEvidenceBatch batch = Batch(instructed: new[] { "symbol.ba" });
            batch.entries.Add(Entry("symbol.ba", MasteryDimension.Form));

            LearningProgressWriter.Apply(_progress, batch, _tuning);

            Assert.That(Form("symbol.ba").immediateSuccesses, Is.EqualTo(1));
            Assert.That(Form("symbol.ba").delayedSuccesses, Is.EqualTo(0));
        }

        [Test]
        public void Apply_UninstructedContentWithExistingRecord_CountsAsDelayed()
        {
            LearningProgressWriter.Apply(_progress,
                Batch(instructed: new[] { "symbol.ba" }), _tuning);

            LearningEvidenceBatch later = Batch(levelId: "level.ugat.02");
            later.entries.Add(Entry("symbol.ba", MasteryDimension.Form));
            LearningProgressWriter.Apply(_progress, later, _tuning);

            Assert.That(Form("symbol.ba").delayedSuccesses, Is.EqualTo(1));
            Assert.That(Form("symbol.ba").immediateSuccesses, Is.EqualTo(0));
            Assert.That(Form("symbol.ba").lastEvidenceLevelId, Is.EqualTo("level.ugat.02"));
        }

        [Test]
        public void Apply_DelayedSessionCount_IncrementsOncePerSessionNotPerSuccess()
        {
            LearningProgressWriter.Apply(_progress,
                Batch(instructed: new[] { "symbol.ba" }), _tuning);

            LearningEvidenceBatch later = Batch(levelId: "level.ugat.02");
            later.entries.Add(Entry("symbol.ba", MasteryDimension.Form,
                attempts: 3, successes: 3, retrievals: 3));
            LearningProgressWriter.Apply(_progress, later, _tuning);

            Assert.That(Form("symbol.ba").delayedSuccesses, Is.EqualTo(3));
            Assert.That(Form("symbol.ba").delayedSessionCount, Is.EqualTo(1));
        }

        [Test]
        public void Apply_VisibleAnswerOnly_CannotReachRecalled()
        {
            LearningEvidenceBatch first = Batch(instructed: new[] { "symbol.ba" });
            first.entries.Add(Entry("symbol.ba", MasteryDimension.Form,
                attempts: 2, successes: 2, retrievals: 2));
            LearningProgressWriter.Apply(_progress, first, _tuning);

            LearningEvidenceBatch later = Batch(levelId: "level.ugat.02");
            later.entries.Add(Entry("symbol.ba", MasteryDimension.Form,
                attempts: 1, successes: 1, retrievals: 0));
            LearningProgressWriter.Apply(_progress, later, _tuning);

            Assert.That(Form("symbol.ba").highWaterState, Is.EqualTo(MasteryState.Practiced));
            Assert.That(Form("symbol.ba").delayedAttempts, Is.EqualTo(1));
            Assert.That(Form("symbol.ba").delayedSessionCount, Is.EqualTo(0));
        }

        [Test]
        public void Apply_Failures_CountAttemptsButNotSuccesses()
        {
            LearningEvidenceBatch batch = Batch(instructed: new[] { "symbol.ba" });
            batch.entries.Add(Entry("symbol.ba", MasteryDimension.Form,
                attempts: 3, successes: 0, retrievals: 0));

            LearningProgressWriter.Apply(_progress, batch, _tuning);

            Assert.That(Form("symbol.ba").immediateAttempts, Is.EqualTo(3));
            Assert.That(Form("symbol.ba").immediateSuccesses, Is.EqualTo(0));
        }

        [Test]
        public void Apply_WordInstruction_SeedsFourDimensions()
        {
            LearningEvidenceBatch batch = Batch();
            batch.instructedContentIds.Add("level.ugat.01.focus.01");
            batch.entries.Add(Entry("level.ugat.01.focus.01", MasteryDimension.Meaning,
                kind: LearningContentKind.Word));

            LearningProgressWriter.Apply(_progress, batch, _tuning);

            WordMasteryRecord record = _progress.wordMastery.Single();
            Assert.That(record.dimensions.Count, Is.EqualTo(4));
            Assert.That(record.sourceLevelId, Is.EqualTo("level.ugat.01"));
        }

        [Test]
        public void Apply_InapplicableDimension_IsIgnored()
        {
            LearningEvidenceBatch batch = Batch(instructed: new[] { "symbol.ba" });
            batch.entries.Add(Entry("symbol.ba", MasteryDimension.Meaning));

            LearningProgressWriter.Apply(_progress, batch, _tuning);

            Assert.That(Symbol("symbol.ba").dimensions.Count, Is.EqualTo(3));
        }

        [Test]
        public void Apply_RecordsAreSortedById()
        {
            LearningProgressWriter.Apply(_progress,
                Batch(instructed: new[] { "symbol.ma", "symbol.ba" }), _tuning);

            Assert.That(_progress.symbolMastery.Select(r => r.symbolId),
                Is.EqualTo(new[] { "symbol.ba", "symbol.ma" }));
        }
    }
}
