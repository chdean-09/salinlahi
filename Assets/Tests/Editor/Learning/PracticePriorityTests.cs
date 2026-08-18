using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class PracticePriorityTests
    {
        private LearningTuningSO _tuning;

        [SetUp]
        public void SetUp() => _tuning = ScriptableObject.CreateInstance<LearningTuningSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        [Test]
        public void Compute_LowerAccuracy_ScoresHigher()
        {
            float weak = PracticePriority.Compute(0.2f, MasteryState.Practiced, 0, _tuning);
            float strong = PracticePriority.Compute(0.9f, MasteryState.Practiced, 0, _tuning);

            Assert.That(weak, Is.GreaterThan(strong));
        }

        [Test]
        public void Compute_LowerState_ScoresHigher()
        {
            float low = PracticePriority.Compute(1f, MasteryState.Introduced, 0, _tuning);
            float high = PracticePriority.Compute(1f, MasteryState.Recalled, 0, _tuning);

            Assert.That(low, Is.GreaterThan(high));
        }

        [Test]
        public void Compute_MoreOverdueCheckpoints_ScoresHigher()
        {
            float overdue = PracticePriority.Compute(1f, MasteryState.Practiced, 3, _tuning);
            float current = PracticePriority.Compute(1f, MasteryState.Practiced, 0, _tuning);

            Assert.That(overdue, Is.GreaterThan(current));
        }

        [Test]
        public void Compute_MasteredWithPerfectAccuracyAndNoOverdue_IsZero()
        {
            Assert.That(PracticePriority.Compute(1f, MasteryState.Mastered, 0, _tuning),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Compute_ZeroAttempts_TreatsAccuracyAsUnknownNotZero()
        {
            Assert.That(PracticePriority.AccuracyOrDefault(0, 0), Is.EqualTo(1f).Within(0.0001f));
        }
    }
}
