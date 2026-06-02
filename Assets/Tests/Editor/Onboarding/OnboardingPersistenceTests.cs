using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Onboarding
{
    [TestFixture]
    public class OnboardingPersistenceTests
    {
        [SetUp]
        public void SetUp()
        {
            OnboardingPersistence.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            OnboardingPersistence.Clear();
        }

        [Test]
        public void GetLastCompletedBeatIndex_WhenUnset_ReturnsNoBeatCompletedSentinel()
        {
            Assert.AreEqual(OnboardingPersistence.NoBeatCompleted, OnboardingPersistence.GetLastCompletedBeatIndex());
        }

        [Test]
        public void GetResumeStartIndex_WhenUnset_ReturnsZero()
        {
            Assert.AreEqual(0, OnboardingPersistence.GetResumeStartIndex());
        }

        [Test]
        public void SetLastCompletedBeatIndex_RoundTrips()
        {
            OnboardingPersistence.SetLastCompletedBeatIndex(3);
            Assert.AreEqual(3, OnboardingPersistence.GetLastCompletedBeatIndex());
        }

        [Test]
        public void GetResumeStartIndex_AfterBeatCompletedReturnsNextBeat()
        {
            OnboardingPersistence.SetLastCompletedBeatIndex(2);
            Assert.AreEqual(3, OnboardingPersistence.GetResumeStartIndex());
        }

        [Test]
        public void SetLastCompletedBeatIndex_NegativeValuesClampToSentinel()
        {
            OnboardingPersistence.SetLastCompletedBeatIndex(-99);
            Assert.AreEqual(OnboardingPersistence.NoBeatCompleted, OnboardingPersistence.GetLastCompletedBeatIndex());
        }

        [Test]
        public void Clear_RemovesPersistedIndex()
        {
            OnboardingPersistence.SetLastCompletedBeatIndex(4);
            OnboardingPersistence.Clear();
            Assert.AreEqual(OnboardingPersistence.NoBeatCompleted, OnboardingPersistence.GetLastCompletedBeatIndex());
            Assert.AreEqual(0, OnboardingPersistence.GetResumeStartIndex());
        }
    }
}
