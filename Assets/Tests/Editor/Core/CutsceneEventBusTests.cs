using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Core
{
    [TestFixture]
    public class CutsceneEventBusTests
    {
        private bool _startedFired;
        private bool _completeFired;

        [SetUp]
        public void SetUp()
        {
            _startedFired = false;
            _completeFired = false;

            EventBus.OnCutsceneStarted += HandleStarted;
            EventBus.OnCutsceneComplete += HandleComplete;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.OnCutsceneStarted -= HandleStarted;
            EventBus.OnCutsceneComplete -= HandleComplete;
        }

        private void HandleStarted() => _startedFired = true;
        private void HandleComplete() => _completeFired = true;

        [Test]
        public void RaiseCutsceneStarted_FiresEvent()
        {
            EventBus.RaiseCutsceneStarted();
            Assert.IsTrue(_startedFired);
            Assert.IsFalse(_completeFired);
        }

        [Test]
        public void RaiseCutsceneComplete_FiresEvent()
        {
            EventBus.RaiseCutsceneComplete();
            Assert.IsTrue(_completeFired);
            Assert.IsFalse(_startedFired);
        }

        [Test]
        public void FullCutsceneLifecycle_BothEventsFire()
        {
            EventBus.RaiseCutsceneStarted();
            EventBus.RaiseCutsceneComplete();

            Assert.IsTrue(_startedFired);
            Assert.IsTrue(_completeFired);
        }

        [Test]
        public void NoSubscribers_EventsDoNotThrow()
        {
            EventBus.OnCutsceneStarted -= HandleStarted;
            EventBus.OnCutsceneComplete -= HandleComplete;

            Assert.DoesNotThrow(() => EventBus.RaiseCutsceneStarted());
            Assert.DoesNotThrow(() => EventBus.RaiseCutsceneComplete());

            EventBus.OnCutsceneStarted += HandleStarted;
            EventBus.OnCutsceneComplete += HandleComplete;
        }
    }
}
