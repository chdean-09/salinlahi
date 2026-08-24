using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-135 AC2. A rejected draw -- recognized-but-wrong as well as unreadable -- must
    /// produce exactly one HUD correction cue. That the cue is non-destructive holds by
    /// construction: ShowRejectFeedback raises nothing, so there is no heart, no evidence
    /// success and no clue advance for a rejection to spend. It is deliberately not asserted
    /// here, because nothing this fixture builds can raise OnBaseHit and a probe that cannot
    /// fire would only ever read zero.
    ///
    /// These live in Play Mode on purpose. DrawingFeedback registers its EventBus handlers in
    /// OnEnable and carries no [ExecuteAlways], so the Edit Mode editor does not run the
    /// lifecycle on a runtime-created GameObject: the component would exist, the subscription
    /// would never happen, and every assertion below would read zero for the wrong reason.
    /// The sibling Edit Mode fixture (CombatFeedbackCueTests) keeps the AC1 resolver tests,
    /// which are pure logic and need no lifecycle.
    /// </summary>
    [TestFixture]
    public class DrawingFeedbackRejectCueTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
        }

        // AC2. A recognized-but-wrong draw raised OnDrawingMissed, which no HUD listened to, so
        // the player saw the enemy simply not die and had nothing telling them to try again.
        [UnityTest]
        public IEnumerator RejectedDraw_Missed_RaisesTheHudCorrectionCue()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            EventBus.RaiseDrawingMissed();

            Assert.AreEqual(1, feedback.RejectCueCount,
                "A recognized-but-wrong draw must show the same correction cue as an unreadable one.");
        }

        [UnityTest]
        public IEnumerator RejectedDraw_Failed_StillRaisesTheHudCorrectionCue()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            EventBus.RaiseDrawingFailed();

            Assert.AreEqual(1, feedback.RejectCueCount,
                "The below-threshold cue that already existed must survive the new subscription.");
        }

        // The two rejection events are mutually exclusive per draw (RecognitionManager raises
        // one or the other, and a boss WrongGlyph consumes the draw before the miss branch),
        // so one rejection must be exactly one cue -- not two now that a single handler is
        // attached to both events -- and OnDisable has to release the new subscription as
        // well as the old one, or a torn-down HUD keeps flashing and then throws from a dead
        // component the next time a draw is rejected.
        [UnityTest]
        public IEnumerator RejectedDraw_CueFiresOncePerRejectionAndStopsWhenDisabled()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            EventBus.RaiseDrawingMissed();
            Assert.AreEqual(1, feedback.RejectCueCount,
                "One rejection is one cue: the shared handler must not be attached twice.");

            EventBus.RaiseDrawingFailed();
            Assert.AreEqual(2, feedback.RejectCueCount,
                "A second, separate rejection is a second cue -- the two events must not "
                + "cancel or swallow each other.");

            feedback.gameObject.SetActive(false);
            yield return null;

            EventBus.RaiseDrawingMissed();
            EventBus.RaiseDrawingFailed();
            Assert.AreEqual(2, feedback.RejectCueCount,
                "A disabled HUD must be unsubscribed from both rejection events.");
        }

        private DrawingFeedback CreateDrawingFeedback()
        {
            var go = new GameObject("DrawingFeedback_Test");

            // Deactivate first so Awake and OnEnable run exactly once, on activation, rather
            // than racing AddComponent. The serialized flash references stay unwired on
            // purpose: the cue must be observable without scene state this fixture cannot see.
            go.SetActive(false);
            DrawingFeedback feedback = go.AddComponent<DrawingFeedback>();
            go.SetActive(true);

            _objectsToDestroy.Add(go);
            Assert.AreEqual(0, feedback.RejectCueCount, "Setup: the cue counter starts clean.");
            return feedback;
        }
    }
}
