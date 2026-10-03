using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Learning
{
    public sealed class ReviewSchedulerTests
    {
        private LearningTuningSO _tuning;
        private static readonly int[] StandardEras = { 5, 5, 5 };

        [SetUp]
        public void SetUp() => _tuning = ScriptableObject.CreateInstance<LearningTuningSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_tuning);

        private System.Collections.Generic.IReadOnlyList<ScheduledCheckpoint> Schedule(
            int sourceIndex, int[] eraSizes = null)
        {
            return ReviewScheduler.BuildSchedule(sourceIndex, eraSizes ?? StandardEras, _tuning);
        }

        private static int DueIndexOf(
            System.Collections.Generic.IReadOnlyList<ScheduledCheckpoint> schedule,
            ReviewCheckpoint checkpoint)
        {
            return schedule.Single(entry => entry.Checkpoint == checkpoint).DueLevelIndex;
        }

        [Test]
        public void BuildSchedule_MidEraWord_YieldsAllFourCheckpoints()
        {
            var schedule = Schedule(1);

            Assert.That(schedule.Count, Is.EqualTo(4));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.NextLevel), Is.EqualTo(2));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.ThreeLevelsLater), Is.EqualTo(4));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.EraEnding), Is.EqualTo(4));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.LaterEra), Is.EqualTo(5));
        }

        [Test]
        public void BuildSchedule_WordAtEraLastLevel_HasNoEraEnding()
        {
            Assert.That(Schedule(4).Any(e => e.Checkpoint == ReviewCheckpoint.EraEnding), Is.False);
        }

        [Test]
        public void BuildSchedule_FinalEraWord_HasNoLaterEra()
        {
            Assert.That(Schedule(11).Any(e => e.Checkpoint == ReviewCheckpoint.LaterEra), Is.False);
        }

        [Test]
        public void BuildSchedule_WordInFinalThreeLevels_HasNoThreeLevelsLater()
        {
            Assert.That(Schedule(13).Any(e => e.Checkpoint == ReviewCheckpoint.ThreeLevelsLater), Is.False);
        }

        [Test]
        public void BuildSchedule_FinaleWord_YieldsNoCheckpoints()
        {
            Assert.That(Schedule(14), Is.Empty);
        }

        [Test]
        public void BuildSchedule_EraBoundary_NextLevelAndLaterEraCollideButStayDistinct()
        {
            var schedule = Schedule(4);

            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.NextLevel), Is.EqualTo(5));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.LaterEra), Is.EqualTo(5));
            Assert.That(schedule.Count(e => e.Checkpoint == ReviewCheckpoint.NextLevel ||
                e.Checkpoint == ReviewCheckpoint.LaterEra), Is.EqualTo(2));
        }

        [Test]
        public void BuildSchedule_ShortEra_ReadsBoundariesFromEraSizes()
        {
            var schedule = Schedule(1, new[] { 4, 5, 5 });

            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.EraEnding), Is.EqualTo(3));
            Assert.That(DueIndexOf(schedule, ReviewCheckpoint.LaterEra), Is.EqualTo(4));
        }

        [Test]
        public void GetDue_ExcludesSatisfiedCheckpoints()
        {
            var due = ReviewScheduler.GetDue(1, 5, StandardEras,
                new[] { ReviewCheckpoint.NextLevel.ToString() }, _tuning);

            Assert.That(due.Any(e => e.Checkpoint == ReviewCheckpoint.NextLevel), Is.False);
            Assert.That(due.Any(e => e.Checkpoint == ReviewCheckpoint.EraEnding), Is.True);
        }

        [Test]
        public void GetDue_ExcludesCheckpointsNotYetReached()
        {
            var due = ReviewScheduler.GetDue(1, 2, StandardEras, new string[0], _tuning);

            Assert.That(due.Count, Is.EqualTo(1));
            Assert.That(due[0].Checkpoint, Is.EqualTo(ReviewCheckpoint.NextLevel));
        }
    }
}
