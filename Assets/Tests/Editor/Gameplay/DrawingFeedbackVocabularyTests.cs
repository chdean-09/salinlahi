using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Salinlahi.Tests.Editor.Gameplay
{
    /// <summary>
    /// SALIN-163 AC1. The player is told what to do next in their own language, and is never
    /// shown the recognizer's confidence.
    ///
    /// These are pure string and reflection assertions with no MonoBehaviour lifecycle, so Edit
    /// Mode is correct for them. The AC2/AC3 behaviour lives in DrawingFeedbackHelpOfferTests in
    /// the Play Mode suite, because DrawingFeedback subscribes in OnEnable and Edit Mode never
    /// runs that on a runtime-created GameObject.
    /// </summary>
    [TestFixture]
    public class DrawingFeedbackVocabularyTests
    {
        [Test]
        public void ForRejection_FirstAttempt_AsksForAnotherTry()
        {
            Assert.AreEqual(
                DrawingFeedbackVocabulary.RejectedFirstAttempt,
                DrawingFeedbackVocabulary.ForRejection(1, helpAvailable: false),
                "The first rejection should simply invite another attempt.");
        }

        // Repeating one sentence at a player who has already failed reads as the game not
        // noticing. The wording has to move even before the help threshold is reached.
        [Test]
        public void ForRejection_RepeatAttempt_ChangesTheWording()
        {
            string first = DrawingFeedbackVocabulary.ForRejection(1, helpAvailable: false);
            string second = DrawingFeedbackVocabulary.ForRejection(2, helpAvailable: false);

            Assert.AreNotEqual(first, second,
                "A second failure must not repeat the first message verbatim.");
            Assert.AreEqual(DrawingFeedbackVocabulary.RejectedAgain, second);
        }

        [Test]
        public void ForRejection_WhenHelpIsAvailable_OffersTheHintInstead()
        {
            Assert.AreEqual(
                DrawingFeedbackVocabulary.HelpOffered,
                DrawingFeedbackVocabulary.ForRejection(3, helpAvailable: true),
                "At the help threshold the wording must offer the hint, not more encouragement.");
        }

        // The AC's actual prohibition, asserted against every string the type can produce rather
        // than against the one call site that used to violate it.
        [Test]
        public void EveryPlayerFacingString_ContainsNoRecognizerMetrics()
        {
            FieldInfo[] copy = typeof(DrawingFeedbackVocabulary)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(string))
                .ToArray();

            Assert.IsNotEmpty(copy, "Setup: the vocabulary must expose its strings for review.");

            foreach (FieldInfo field in copy)
            {
                var text = (string)field.GetValue(null);

                Assert.IsFalse(text.Any(char.IsDigit),
                    $"'{field.Name}' shows the player a number: \"{text}\". Recognition copy must "
                    + "not read as a score.");
                Assert.IsFalse(text.Contains('%'),
                    $"'{field.Name}' shows the player a percentage: \"{text}\".");
            }
        }

        // FeedbackToast used to render score * 100 as a percentage. Not printing it would be a
        // fix one careless edit could undo; not being *able* to print it is the durable one, so
        // the guarantee asserted here is the shape of the API rather than the body of a method.
        [Test]
        public void FeedbackToastShow_CannotBeHandedARecognizerScore()
        {
            MethodInfo show = typeof(FeedbackToast).GetMethod(
                "Show", BindingFlags.Public | BindingFlags.Instance);

            Assert.IsNotNull(show, "Setup: FeedbackToast.Show must exist.");

            ParameterInfo[] numeric = show.GetParameters()
                .Where(p => p.ParameterType == typeof(float) || p.ParameterType == typeof(double))
                .ToArray();

            Assert.IsEmpty(numeric,
                "FeedbackToast.Show takes a recognizer score again ("
                + string.Join(", ", numeric.Select(p => $"{p.ParameterType.Name} {p.Name}"))
                + "). The score must not be reachable from the view at all.");
        }
    }
}
