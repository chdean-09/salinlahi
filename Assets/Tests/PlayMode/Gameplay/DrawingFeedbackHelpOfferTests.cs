using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// SALIN-163 AC2 and AC3. Repeated failures make an optional hint available at the
    /// configured threshold, and succeeding afterwards leaves no penalty-state residue.
    ///
    /// Play Mode for the same reason as the sibling DrawingFeedbackRejectCueTests: the
    /// component registers its EventBus handlers in OnEnable and carries no [ExecuteAlways], so
    /// in Edit Mode a runtime-created GameObject would never subscribe and every counter below
    /// would read zero for the wrong reason. The pure wording rules are asserted in Edit Mode
    /// instead, by DrawingFeedbackVocabularyTests.
    ///
    /// The threshold is DrawingFeedback's own serialized default of 3 rejections. These tests
    /// deliberately do not reach in and override it -- the shipped default is the thing worth
    /// pinning, since it is what an unedited scene will actually use.
    /// </summary>
    [TestFixture]
    public class DrawingFeedbackHelpOfferTests
    {
        private const int DefaultHelpThreshold = 3;

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

        // AC2, lower bound. Offering a trace after a single slip would undercut the player
        // before they have had a fair chance at the stroke.
        [UnityTest]
        public IEnumerator RejectionsBelowTheThreshold_DoNotOfferHelp()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            for (int i = 0; i < DefaultHelpThreshold - 1; i++)
                EventBus.RaiseDrawingFailed();

            Assert.AreEqual(DefaultHelpThreshold - 1, feedback.ConsecutiveRejectCount);
            Assert.IsFalse(feedback.HelpAvailable,
                $"Help must not be offered before {DefaultHelpThreshold} consecutive rejections.");
            Assert.AreEqual(DrawingFeedbackVocabulary.RejectedAgain, feedback.LastMessage,
                "Below the threshold the player should still be getting encouragement.");
        }

        [UnityTest]
        public IEnumerator RejectionsReachingTheThreshold_MakeTheHintAvailable()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            for (int i = 0; i < DefaultHelpThreshold; i++)
                EventBus.RaiseDrawingFailed();

            Assert.IsTrue(feedback.HelpAvailable,
                $"{DefaultHelpThreshold} consecutive rejections must make the hint available.");
            Assert.AreEqual(DrawingFeedbackVocabulary.HelpOffered, feedback.LastMessage,
                "At the threshold the player must actually be told the hint exists.");
        }

        // Once offered, the hint stays offered. A player mid-struggle watching the offer appear
        // and vanish attempt by attempt is worse off than one who was never offered it.
        [UnityTest]
        public IEnumerator HelpOnceOffered_SurvivesFurtherRejections()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            for (int i = 0; i < DefaultHelpThreshold + 2; i++)
                EventBus.RaiseDrawingFailed();

            Assert.IsTrue(feedback.HelpAvailable, "The offer must not be withdrawn.");
            Assert.AreEqual(DrawingFeedbackVocabulary.HelpOffered, feedback.LastMessage);
        }

        // AC3, the whole point of the criterion: nothing about having needed help follows the
        // player past the character they finally got right.
        [UnityTest]
        public IEnumerator SuccessAfterHelp_LeavesNoPenaltyStateBehind()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            for (int i = 0; i < DefaultHelpThreshold; i++)
                EventBus.RaiseDrawingFailed();
            Assert.IsTrue(feedback.HelpAvailable, "Setup: the player reached the help threshold.");

            EventBus.RaiseEnemyDefeated(null);

            Assert.AreEqual(0, feedback.ConsecutiveRejectCount,
                "Acceptance must clear the rejection run.");
            Assert.IsFalse(feedback.HelpAvailable,
                "Acceptance must withdraw the help offer.");
            Assert.AreEqual(DrawingFeedbackVocabulary.Accepted, feedback.LastMessage,
                "The player should be told they got it, not left on the hint offer.");
        }

        // The residue that would be easiest to ship by accident: state cleared on the surface,
        // but the next rejection resuming mid-run as though the player were still failing.
        [UnityTest]
        public IEnumerator RejectionAfterASuccess_StartsTheRunOver()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            for (int i = 0; i < DefaultHelpThreshold; i++)
                EventBus.RaiseDrawingFailed();
            EventBus.RaiseEnemyDefeated(null);

            EventBus.RaiseDrawingFailed();

            Assert.AreEqual(1, feedback.ConsecutiveRejectCount,
                "The next rejection is the first of a new run, not the fourth of the old one.");
            Assert.IsFalse(feedback.HelpAvailable,
                "One slip after a success must not immediately re-offer the hint.");
            Assert.AreEqual(DrawingFeedbackVocabulary.RejectedFirstAttempt, feedback.LastMessage);
        }

        // AC1 at the point the player actually reads it, rather than on the constants alone.
        [UnityTest]
        public IEnumerator TheMessageShownOnRejection_IsLanguageNotAMetric()
        {
            DrawingFeedback feedback = CreateDrawingFeedback();
            yield return null;

            EventBus.RaiseDrawingFailed();

            Assert.IsNotEmpty(feedback.LastMessage, "A rejection must say something.");
            Assert.IsFalse(feedback.LastMessage.Any(char.IsDigit),
                $"The rejection message shows a number: \"{feedback.LastMessage}\".");
            Assert.IsFalse(feedback.LastMessage.Contains('%'),
                $"The rejection message shows a percentage: \"{feedback.LastMessage}\".");
        }

        private DrawingFeedback CreateDrawingFeedback()
        {
            var go = new GameObject("DrawingFeedback_HelpOffer_Test");

            // Deactivate first so Awake and OnEnable run exactly once, on activation, rather
            // than racing AddComponent -- same reason as the sibling reject-cue fixture. The
            // serialized flash and prompt references stay unwired on purpose: the offer must be
            // observable without scene state this fixture cannot see.
            go.SetActive(false);
            DrawingFeedback feedback = go.AddComponent<DrawingFeedback>();
            go.SetActive(true);

            _objectsToDestroy.Add(go);
            Assert.AreEqual(0, feedback.ConsecutiveRejectCount, "Setup: the run starts clean.");
            Assert.IsFalse(feedback.HelpAvailable, "Setup: no help is offered up front.");
            return feedback;
        }
    }
}
