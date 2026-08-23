using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class MasteryEvaluatorTests
    {
        private LearningTuningSO _tuning;

        [SetUp]
        public void SetUp() => _tuning = ScriptableObject.CreateInstance<LearningTuningSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        private static DimensionEvidence Evidence(
            int immediateSuccesses = 0, int delayedSuccesses = 0,
            int delayedSessionCount = 0, MasteryState highWater = MasteryState.Introduced)
        {
            return new DimensionEvidence
            {
                dimension = MasteryDimension.Form,
                immediateSuccesses = immediateSuccesses,
                immediateAttempts = immediateSuccesses,
                delayedSuccesses = delayedSuccesses,
                delayedAttempts = delayedSuccesses,
                delayedSessionCount = delayedSessionCount,
                highWaterState = highWater,
            };
        }

        private static DimensionEvidence Dimension(MasteryDimension dimension, MasteryState state)
        {
            return new DimensionEvidence { dimension = dimension, highWaterState = state };
        }

        [Test]
        public void Evaluate_OneImmediateSuccess_StaysIntroduced()
        {
            Assert.That(MasteryEvaluator.Evaluate(Evidence(immediateSuccesses: 1), _tuning),
                Is.EqualTo(MasteryState.Introduced));
        }

        [Test]
        public void Evaluate_TwoImmediateSuccesses_ReachesPracticed()
        {
            Assert.That(MasteryEvaluator.Evaluate(Evidence(immediateSuccesses: 2), _tuning),
                Is.EqualTo(MasteryState.Practiced));
        }

        [Test]
        public void Evaluate_DelayedSuccessAfterPracticed_ReachesRecalled()
        {
            Assert.That(MasteryEvaluator.Evaluate(
                Evidence(immediateSuccesses: 2, delayedSuccesses: 1, delayedSessionCount: 1), _tuning),
                Is.EqualTo(MasteryState.Recalled));
        }

        [Test]
        public void Evaluate_DelayedSuccessWithoutPracticed_StopsAtIntroduced()
        {
            Assert.That(MasteryEvaluator.Evaluate(
                Evidence(delayedSuccesses: 5, delayedSessionCount: 5), _tuning),
                Is.EqualTo(MasteryState.Introduced));
        }

        [Test]
        public void Evaluate_TwoDelayedSuccessesAcrossTwoSessions_ReachesMastered()
        {
            Assert.That(MasteryEvaluator.Evaluate(
                Evidence(immediateSuccesses: 2, delayedSuccesses: 2, delayedSessionCount: 2), _tuning),
                Is.EqualTo(MasteryState.Mastered));
        }

        [Test]
        public void Evaluate_TwoDelayedSuccessesInOneSession_StopsAtRecalled()
        {
            Assert.That(MasteryEvaluator.Evaluate(
                Evidence(immediateSuccesses: 2, delayedSuccesses: 2, delayedSessionCount: 1), _tuning),
                Is.EqualTo(MasteryState.Recalled));
        }

        [Test]
        public void Evaluate_NeverDemotesBelowHighWater()
        {
            _tuning.immediateSuccessesForPracticed = 99;

            Assert.That(MasteryEvaluator.Evaluate(
                Evidence(highWater: MasteryState.Mastered), _tuning),
                Is.EqualTo(MasteryState.Mastered));
        }

        [Test]
        public void Aggregate_IsMinimumAcrossApplicableDimensions()
        {
            var dimensions = new List<DimensionEvidence>
            {
                Dimension(MasteryDimension.Form, MasteryState.Mastered),
                Dimension(MasteryDimension.Sound, MasteryState.Practiced),
                Dimension(MasteryDimension.Assembly, MasteryState.Recalled),
            };

            Assert.That(MasteryEvaluator.Aggregate(dimensions, LearningContentKind.Symbol),
                Is.EqualTo(MasteryState.Practiced));
        }

        [Test]
        public void Aggregate_MissingDimension_ReadsAsNone()
        {
            var dimensions = new List<DimensionEvidence>
            {
                Dimension(MasteryDimension.Form, MasteryState.Mastered),
            };

            Assert.That(MasteryEvaluator.Aggregate(dimensions, LearningContentKind.Symbol),
                Is.EqualTo(MasteryState.None));
        }

        [Test]
        public void Aggregate_SymbolIgnoresMeaningDimension()
        {
            var dimensions = new List<DimensionEvidence>
            {
                Dimension(MasteryDimension.Form, MasteryState.Mastered),
                Dimension(MasteryDimension.Sound, MasteryState.Mastered),
                Dimension(MasteryDimension.Assembly, MasteryState.Mastered),
                Dimension(MasteryDimension.Meaning, MasteryState.None),
            };

            Assert.That(MasteryEvaluator.Aggregate(dimensions, LearningContentKind.Symbol),
                Is.EqualTo(MasteryState.Mastered));
        }

        [Test]
        public void Aggregate_WordCountsMeaningDimension()
        {
            var dimensions = new List<DimensionEvidence>
            {
                Dimension(MasteryDimension.Form, MasteryState.Mastered),
                Dimension(MasteryDimension.Sound, MasteryState.Mastered),
                Dimension(MasteryDimension.Assembly, MasteryState.Mastered),
                Dimension(MasteryDimension.Meaning, MasteryState.Introduced),
            };

            Assert.That(MasteryEvaluator.Aggregate(dimensions, LearningContentKind.Word),
                Is.EqualTo(MasteryState.Introduced));
        }
    }
}
