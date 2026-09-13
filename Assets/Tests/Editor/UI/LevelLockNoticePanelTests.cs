using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-137 AC2 — the locked-level explanation, exercised over an AUTHORED surface.
    ///
    /// These tests inject the serialized references directly, so nothing here depends on
    /// the MonoBehaviour lifecycle and everything runs in Edit Mode. The panel's
    /// no-Inspector-wiring fallback, which builds its own surface in <c>Awake</c>, is
    /// lifecycle-dependent and is covered by the Play Mode suite instead.
    /// </summary>
    public sealed class LevelLockNoticePanelTests
    {
        private GameObject _root;
        private GameObject _overlay;
        private Text _body;
        private LevelLockNoticePanel _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("LevelLockNoticePanel_Test");
            _overlay = new GameObject("Overlay");
            _overlay.transform.SetParent(_root.transform);
            _body = new GameObject("Body").AddComponent<Text>();
            _body.transform.SetParent(_overlay.transform);
            _panel = _root.AddComponent<LevelLockNoticePanel>();
            SetPrivateField("_overlayRoot", _overlay);
            SetPrivateField("_bodyText", _body);
            _overlay.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void AuthoredReferences_AreUsedAndTheRuntimeFallbackNeverBuilds()
        {
            Assert.IsTrue(_panel.HasRequiredReferences);
            Assert.IsFalse(_panel.IsShowing, "The notice starts hidden.");

            _panel.PresentPrerequisite("Ugat Level 3", crossesEra: false, requiredEraName: null);

            // If EnsureSurface built the fallback it would REPLACE these two references
            // with objects it created and parent a "[Runtime] LevelLockNotice" root to
            // some canvas. Asserting on the identities is what makes this test able to
            // fail — HasRequiredReferences alone only restates what SetUp injected.
            Assert.AreSame(_overlay, GetPrivateField("_overlayRoot"),
                "Present must reuse the AUTHORED root, not swap in a runtime-built one.");
            Assert.AreSame(_body, GetPrivateField("_bodyText"),
                "Present must reuse the AUTHORED body text.");
            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite("Ugat Level 3", false, null), _body.text,
                "The message must land on the AUTHORED Text component, not a runtime one.");
            Assert.IsNull(GameObject.Find("[Runtime] LevelLockNotice"),
                "No runtime fallback surface may exist when the references are authored.");
        }

        /// <summary>
        /// SALIN-258 rewrote this test's assertion. It previously read
        /// <c>StringAssert.Contains("4", VisibleMessage)</c> against level 4 — a digit that is
        /// Ugat-local AND global, so it passed identically before and after era-relative
        /// numbering and proved nothing. It now runs on Ugnayan Level 2 (global 7), where the
        /// two numbers differ, and asserts the global one is ABSENT.
        /// </summary>
        [Test]
        public void PresentPrerequisite_SameEra_ShowsTheEraLocalNumberNotTheGlobalOne_SALIN258()
        {
            _panel.PresentPrerequisite("Ugnayan Level 2", crossesEra: false, requiredEraName: "Ugnayan");

            Assert.IsTrue(_panel.IsShowing, "AC2: the explanation is visible on Level Select.");
            StringAssert.Contains("Ugnayan Level 2", _panel.VisibleMessage);
            StringAssert.DoesNotContain("7", _panel.VisibleMessage,
                "Global level 7 is Ugnayan Level 2; the global id must never be shown.");
            Assert.AreEqual(
                LevelLockNoticeCopy.Prerequisite("Ugnayan Level 2", false, "Ugnayan"),
                _panel.VisibleMessage,
                "All copy must come from the single LevelLockNoticeCopy source.");
        }

        /// <summary>
        /// SALIN-258 AC-2, the ticket's one verbatim-specified sentence. Previously asserted
        /// <c>Contains("5")</c>, which is Ugat-local and global alike and therefore unfalsifiable;
        /// it now pins the whole required sentence.
        /// </summary>
        [Test]
        public void PresentPrerequisite_EraCrossing_ReadsFinishUgatLevel5_SALIN258()
        {
            _panel.PresentPrerequisite("Ugat Level 5", crossesEra: true, requiredEraName: "Ugat");

            Assert.IsTrue(_panel.IsShowing);
            Assert.AreEqual(
                "Locked. Finish Ugat Level 5 to open this era.",
                _panel.VisibleMessage,
                "AC-2, frozen I56 verbatim: the lock notice for Era 2 Level 1 says "
                + "'Finish Ugat Level 5'.");
        }

        /// <summary>
        /// The era-crossing case for a LATER era boundary, where the era-local number and the
        /// global number diverge. Global 11 is Pamana Level 1, unlocked by finishing global 10 =
        /// Ugnayan Level 5. Neither 10 nor 11 may appear.
        /// </summary>
        [Test]
        public void PresentPrerequisite_EraCrossingIntoAThirdEra_NamesNoGlobalNumber_SALIN258()
        {
            _panel.PresentPrerequisite("Ugnayan Level 5", crossesEra: true, requiredEraName: "Ugnayan");

            Assert.AreEqual("Locked. Finish Ugnayan Level 5 to open this era.", _panel.VisibleMessage);
            StringAssert.DoesNotContain("10", _panel.VisibleMessage);
            StringAssert.DoesNotContain("11", _panel.VisibleMessage);
        }

        [Test]
        public void PresentPrerequisite_EraCrossingWithoutAnEraName_FallsBackToTheLevelNumberForm()
        {
            // The legacy progress path: no era resolved, so CampaignLevelLabel yields the plain
            // "Level 5" form and the era-crossing sentence must not be used.
            _panel.PresentPrerequisite("Level 5", crossesEra: true, requiredEraName: null);

            Assert.IsTrue(_panel.IsShowing);
            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite("Level 5", false, null), _panel.VisibleMessage,
                "A missing era name degrades to the plain form the legacy path always uses.");
        }

        [Test]
        public void PresentPrerequisite_NothingToExplain_ClearsAnExplanationAlreadyOnScreen()
        {
            // Start from a VISIBLE notice. Asserting "still hidden" from the hidden start
            // state would pass against a Present that does nothing at all.
            _panel.PresentPrerequisite("Ugat Level 4", crossesEra: false, requiredEraName: "Ugat");
            Assert.IsTrue(_panel.IsShowing, "precondition: there is something on screen to clear");

            // SALIN-258: the "nothing to explain" signal is now an EMPTY LABEL rather than a
            // level number below 1. CampaignLevelLabel.Format produces exactly that for order < 1,
            // so the guard survived the signature change.
            _panel.PresentPrerequisite(string.Empty, crossesEra: false, requiredEraName: null);

            Assert.IsFalse(_panel.IsShowing,
                "A reachable level, or a blocked save, must not blame a prerequisite — and "
                + "must take down any explanation still showing.");
        }

        [Test]
        public void Hide_AfterPresenting_ClearsTheNotice()
        {
            _panel.PresentPrerequisite("Ugat Level 2", crossesEra: false, requiredEraName: "Ugat");
            Assert.IsTrue(_panel.IsShowing, "precondition");

            _panel.Hide();

            Assert.IsFalse(_panel.IsShowing);
        }

        /// <summary>
        /// SALIN-258: the "nothing to name" signal moved from an int below 1 to an empty label.
        /// The third case ties the two together — it feeds the copy builder the ACTUAL output of
        /// CampaignLevelLabel for an invalid order, so the guard cannot silently stop lining up
        /// with what the production caller passes.
        /// </summary>
        [Test]
        public void Copy_WithNoLevelToName_IsEmptySoCallersStaySilent()
        {
            Assert.AreEqual(string.Empty, LevelLockNoticeCopy.Prerequisite(string.Empty, false, null));
            Assert.AreEqual(string.Empty, LevelLockNoticeCopy.Prerequisite(null, true, "Ugat"));
            Assert.AreEqual(
                string.Empty,
                LevelLockNoticeCopy.Prerequisite(CampaignLevelLabel.Format("Ugat", 0, 0), false, null),
                "An unresolvable level must still take the notice off screen end to end.");
        }

        // ------------------------------------------------------------------
        // SALIN-220 AC6 - the missing-objective copy
        // ------------------------------------------------------------------

        [TestCase(LevelObjectives.StoryViewed)]
        [TestCase(LevelObjectives.SymbolsPracticed)]
        [TestCase(LevelObjectives.WordsRestored)]
        [TestCase(LevelObjectives.ContextPassed)]
        [TestCase(LevelObjectives.FinalSyllableRestored)]
        public void MissingObjectiveCopy_NamesTheOwingLevel_SALIN220(string objectiveId)
        {
            // SALIN-258: run on Ugnayan Level 2 (= global 7) rather than Ugat Level 4, so the
            // assertion distinguishes era-local from global instead of passing on a digit that
            // happens to be both.
            string message = LevelLockNoticeCopy.MissingObjective(objectiveId, "Ugnayan Level 2");

            Assert.IsNotEmpty(message, objectiveId);
            StringAssert.Contains("Ugnayan Level 2", message);
            StringAssert.DoesNotContain("7", message);
            StringAssert.StartsWith("Locked.", message);
        }

        /// <summary>
        /// Every objective must read differently, or the copy tells the player nothing the
        /// generic prerequisite wording did not already say. A switch that fell through to a
        /// shared default would still pass the per-case test above.
        /// </summary>
        [Test]
        public void MissingObjectiveCopy_IsDistinctPerObjective_SALIN220()
        {
            string[] ids =
            {
                LevelObjectives.StoryViewed,
                LevelObjectives.SymbolsPracticed,
                LevelObjectives.WordsRestored,
                LevelObjectives.ContextPassed,
                LevelObjectives.FinalSyllableRestored,
            };

            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string id in ids)
                Assert.IsTrue(seen.Add(LevelLockNoticeCopy.MissingObjective(id, "Ugnayan Level 2")),
                    $"{id} repeats another objective's sentence.");

            Assert.AreEqual(ids.Length, seen.Count);
        }

        /// <summary>
        /// A sixth objective added without copy must degrade to the prerequisite wording,
        /// never to an empty panel or a raw identifier on screen.
        /// </summary>
        [Test]
        public void MissingObjectiveCopy_UnknownIdentifier_FallsBackToPrerequisite_SALIN220()
        {
            string message = LevelLockNoticeCopy.MissingObjective("objective.notAuthoredYet", "Ugat Level 3");

            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite("Ugat Level 3", false, null), message);
            StringAssert.DoesNotContain("objective.", message);
        }

        [Test]
        public void MissingObjectiveCopy_WithNoLevelToName_IsEmptySoCallersStaySilent_SALIN220()
        {
            Assert.AreEqual(
                string.Empty,
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.StoryViewed, string.Empty));
        }

        [Test]
        public void PresentMissingObjective_ShowsTheObjectiveSentence_SALIN220()
        {
            _panel.PresentMissingObjective(LevelObjectives.ContextPassed, "Ugat Level 5");

            Assert.IsTrue(_panel.IsShowing);
            Assert.AreEqual(
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.ContextPassed, "Ugat Level 5"),
                _panel.VisibleMessage);
            StringAssert.Contains("challenge", _panel.VisibleMessage);
        }

        private void SetPrivateField(string fieldName, object value) =>
            PrivateField(fieldName).SetValue(_panel, value);

        private object GetPrivateField(string fieldName) =>
            PrivateField(fieldName).GetValue(_panel);

        private static FieldInfo PrivateField(string fieldName)
        {
            FieldInfo info = typeof(LevelLockNoticePanel).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(info, fieldName);
            return info;
        }
    }
}
