using System.Collections.Generic;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Learning
{
    /// <summary>
    /// SALIN-202: table-driven coverage of the documented Level 1 metric and star
    /// formulas (docs/design/scoring-and-stars.md).
    /// </summary>
    [TestFixture]
    public sealed class LevelResultsCalculatorTests
    {
        private static LearningEvidenceBatch Batch(params LearningEvidenceEntry[] entries)
        {
            var batch = new LearningEvidenceBatch { levelId = "level.ugat.01" };
            batch.entries.AddRange(entries);
            return batch;
        }

        private static LearningEvidenceEntry Entry(
            MasteryDimension dimension, int attempts, int successes,
            string contentId = "symbol.na", LearningContentKind kind = LearningContentKind.Symbol)
        {
            return new LearningEvidenceEntry
            {
                contentId = contentId,
                contentKind = kind,
                dimension = dimension,
                attemptCount = attempts,
                successCount = successes,
            };
        }

        [Test]
        public void PerfectRun_WithNoRecordedAttempts_ScoresFullMarks()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(), heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(1f, results.Metrics[LevelResultsCalculator.TracingAccuracyMetricId], 0.0001f);
            Assert.AreEqual(1f, results.Metrics[LevelResultsCalculator.ContextAccuracyMetricId], 0.0001f);
            Assert.AreEqual(1f, results.Metrics[LevelResultsCalculator.HeartsRatioMetricId], 0.0001f);
            Assert.AreEqual(100f, results.Metrics[LevelResultsCalculator.ScoreMetricId], 0.0001f);
            Assert.AreEqual(3, results.Stars);
        }

        [Test]
        public void TracingAccuracy_ComesFromFormEntries()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(
                    Entry(MasteryDimension.Form, attempts: 3, successes: 1),
                    Entry(MasteryDimension.Form, attempts: 1, successes: 1, contentId: "symbol.ei")),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(0.5f, results.Metrics[LevelResultsCalculator.TracingAccuracyMetricId], 0.0001f);
        }

        [Test]
        public void ContextAccuracy_CombinesAssemblyAndMeaning()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(
                    Entry(MasteryDimension.Assembly, attempts: 2, successes: 1,
                        contentId: "level.ugat.01.focus.01", kind: LearningContentKind.Word),
                    Entry(MasteryDimension.Meaning, attempts: 2, successes: 1,
                        contentId: "level.ugat.01.focus.02", kind: LearningContentKind.Word)),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(0.5f, results.Metrics[LevelResultsCalculator.ContextAccuracyMetricId], 0.0001f);
        }

        [Test]
        public void Score_AppliesTheEmergencyHintPenalty()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(), heartsRemaining: 3, maxHearts: 3, hintsUsed: 1, emergencyHintPenalty: 0.10f);

            Assert.AreEqual(90f, results.Metrics[LevelResultsCalculator.ScoreMetricId], 0.0001f);
            Assert.AreEqual(1f, results.Metrics[LevelResultsCalculator.HintsUsedMetricId], 0.0001f);
            Assert.AreEqual(0.10f, results.Metrics[LevelResultsCalculator.EmergencyHintPenaltyMetricId], 0.0001f);
        }

        [Test]
        public void Stars_TwoWhenHeartsDipBelowFull()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(), heartsRemaining: 2, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(2, results.Stars,
                "Half-or-better hearts with strong accuracy earns two stars; three needs full hearts.");
        }

        [Test]
        public void Stars_OneWhenContextAccuracyIsWeak()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Meaning, attempts: 2, successes: 1,
                    contentId: "level.ugat.01.focus.01", kind: LearningContentKind.Word)),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(1, results.Stars,
                "Context accuracy below 0.6 holds the outcome at one star even on full hearts.");
        }

        [Test]
        public void Stars_ThreeRequiresStrongTracing()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Form, attempts: 4, successes: 3)),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(2, results.Stars,
                "Tracing accuracy below 0.8 caps the outcome at two stars.");
        }

        [Test]
        public void ZeroMaxHearts_IsSafeAndScoresOneStar()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(), heartsRemaining: 0, maxHearts: 0, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(0f, results.Metrics[LevelResultsCalculator.HeartsRatioMetricId], 0.0001f);
            Assert.AreEqual(1, results.Stars);
        }

        [Test]
        public void Metrics_CarryEveryStableIdentifier()
        {
            LevelResults results = LevelResultsCalculator.Compute(
                Batch(), heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            CollectionAssert.IsSubsetOf(
                new[]
                {
                    LevelResultsCalculator.TracingAccuracyMetricId,
                    LevelResultsCalculator.ContextAccuracyMetricId,
                    LevelResultsCalculator.HeartsRatioMetricId,
                    LevelResultsCalculator.HintsUsedMetricId,
                    LevelResultsCalculator.EmergencyHintPenaltyMetricId,
                    LevelResultsCalculator.ScoreMetricId,
                },
                new List<string>(results.Metrics.Keys));
        }
    }
}
