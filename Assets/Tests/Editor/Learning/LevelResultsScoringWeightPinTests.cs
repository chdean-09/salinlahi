using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Learning
{
    /// <summary>
    /// SALIN-234 — OWNER RULING R1 (2026-09-13). A regression pin, required by the ruling.
    ///
    /// D-021 cut the accuracy DISPLAY, not the accuracy SCORING. The Level Results screen no
    /// longer shows any accuracy figure (see LevelResultsCopy), but tracing accuracy is still
    /// weighted at 0.5 and context accuracy at 0.3 of <c>metric.score</c>, and both star
    /// thresholds still gate on them (LevelResultsCalculator.cs:43-50).
    ///
    /// THIS FIXTURE EXISTS FOR ONE READER: whoever later notices that accuracy is absent from
    /// the Results UI and "tidies up" the calculator to match. Doing that would silently
    /// re-tune which levels award which stars — accuracy is 0.8 of the score weight and gates
    /// BOTH the two- and three-star thresholds. No level may change its star rating because of
    /// a UI ticket. Campaign-wide threshold balancing is SALIN-183's
    /// (docs/design/scoring-and-stars.md:5).
    ///
    /// LevelResultsCalculatorTests already covers the formulas as behaviour. These tests are
    /// deliberately redundant with it: they state the numbers and the reason in one place so a
    /// deletion has to argue with the ruling rather than with a coincidence.
    /// </summary>
    [TestFixture]
    public sealed class LevelResultsScoringWeightPinTests
    {
        private static LearningEvidenceBatch Batch(params LearningEvidenceEntry[] entries)
        {
            var batch = new LearningEvidenceBatch { levelId = "level.ugat.01" };
            batch.entries.AddRange(entries);
            return batch;
        }

        private static LearningEvidenceEntry Entry(
            MasteryDimension dimension, int attempts, int successes, string contentId = "symbol.na")
        {
            return new LearningEvidenceEntry
            {
                contentId = contentId,
                contentKind = LearningContentKind.Symbol,
                dimension = dimension,
                attemptCount = attempts,
                successCount = successes,
            };
        }

        [Test]
        public void ScoreStillWeightsAccuracyAtEightyPercent()
        {
            // Zero hearts isolates the accuracy half of the weighting: with heartsRatio 0 the
            // score is exactly 0.5 * tracing + 0.3 * context, times 100.
            LevelResults halfTracing = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Form, attempts: 2, successes: 1)),
                heartsRemaining: 0, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(
                55f,
                halfTracing.Metrics[LevelResultsCalculator.ScoreMetricId],
                0.0001f,
                "0.5 * 0.5 tracing + 0.3 * 1.0 context = 0.55. If this moved, the score weights " +
                "changed. SALIN-234 removed the accuracy READOUT only; the weights are pinned " +
                "by owner ruling R1 and are SALIN-183's to change.");

            LevelResults halfContext = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Assembly, attempts: 2, successes: 1,
                    contentId: "level.ugat.01.focus.01")),
                heartsRemaining: 0, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(
                65f,
                halfContext.Metrics[LevelResultsCalculator.ScoreMetricId],
                0.0001f,
                "0.5 * 1.0 tracing + 0.3 * 0.5 context = 0.65. Context accuracy is still worth " +
                "0.3 of the score.");
        }

        [Test]
        public void TwoStarThresholdStillGatesOnContextAccuracy()
        {
            LevelResults justBelow = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Meaning, attempts: 10, successes: 5,
                    contentId: "level.ugat.01.focus.01")),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(
                1,
                justBelow.Stars,
                "Context accuracy 0.5 is below the 0.6 two-star gate, so full hearts still only " +
                "earn one star. Deleting accuracy from the calculator would hand this run two " +
                "or three stars instead.");
        }

        [Test]
        public void ThreeStarThresholdStillGatesOnBothAccuracies()
        {
            LevelResults weakTracing = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Form, attempts: 10, successes: 7)),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(
                2,
                weakTracing.Stars,
                "Tracing accuracy 0.7 is below the 0.8 three-star gate. Full hearts alone must " +
                "never award three stars.");

            LevelResults weakContext = LevelResultsCalculator.Compute(
                Batch(Entry(MasteryDimension.Assembly, attempts: 10, successes: 7,
                    contentId: "level.ugat.01.focus.01")),
                heartsRemaining: 3, maxHearts: 3, hintsUsed: 0, emergencyHintPenalty: 0f);

            Assert.AreEqual(
                2,
                weakContext.Stars,
                "Context accuracy 0.7 is below the 0.8 three-star gate.");
        }
    }
}
