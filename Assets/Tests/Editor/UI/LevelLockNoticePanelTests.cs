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

            _panel.PresentPrerequisite(3, crossesEra: false, requiredEraName: null);

            // If EnsureSurface built the fallback it would REPLACE these two references
            // with objects it created and parent a "[Runtime] LevelLockNotice" root to
            // some canvas. Asserting on the identities is what makes this test able to
            // fail — HasRequiredReferences alone only restates what SetUp injected.
            Assert.AreSame(_overlay, GetPrivateField("_overlayRoot"),
                "Present must reuse the AUTHORED root, not swap in a runtime-built one.");
            Assert.AreSame(_body, GetPrivateField("_bodyText"),
                "Present must reuse the AUTHORED body text.");
            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite(3, false, null), _body.text,
                "The message must land on the AUTHORED Text component, not a runtime one.");
            Assert.IsNull(GameObject.Find("[Runtime] LevelLockNotice"),
                "No runtime fallback surface may exist when the references are authored.");
        }

        [Test]
        public void PresentPrerequisite_SameEra_ShowsTheImmediatelyPrecedingLevel()
        {
            _panel.PresentPrerequisite(4, crossesEra: false, requiredEraName: null);

            Assert.IsTrue(_panel.IsShowing, "AC2: the explanation is visible on Level Select.");
            StringAssert.Contains("4", _panel.VisibleMessage);
            Assert.AreEqual(
                LevelLockNoticeCopy.Prerequisite(4, false, null),
                _panel.VisibleMessage,
                "All copy must come from the single LevelLockNoticeCopy source.");
        }

        [Test]
        public void PresentPrerequisite_EraCrossing_NamesTheEraThatMustBeFinished()
        {
            _panel.PresentPrerequisite(5, crossesEra: true, requiredEraName: "Ugat");

            Assert.IsTrue(_panel.IsShowing);
            StringAssert.Contains("Ugat", _panel.VisibleMessage);
            StringAssert.Contains("5", _panel.VisibleMessage);
        }

        [Test]
        public void PresentPrerequisite_EraCrossingWithoutAnEraName_FallsBackToTheLevelNumberForm()
        {
            _panel.PresentPrerequisite(5, crossesEra: true, requiredEraName: null);

            Assert.IsTrue(_panel.IsShowing);
            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite(5, false, null), _panel.VisibleMessage,
                "A missing era name degrades to the plain form the legacy path always uses.");
        }

        [Test]
        public void PresentPrerequisite_NothingToExplain_ClearsAnExplanationAlreadyOnScreen()
        {
            // Start from a VISIBLE notice. Asserting "still hidden" from the hidden start
            // state would pass against a Present that does nothing at all.
            _panel.PresentPrerequisite(4, crossesEra: false, requiredEraName: null);
            Assert.IsTrue(_panel.IsShowing, "precondition: there is something on screen to clear");

            _panel.PresentPrerequisite(0, crossesEra: false, requiredEraName: null);

            Assert.IsFalse(_panel.IsShowing,
                "A reachable level, or a blocked save, must not blame a prerequisite — and "
                + "must take down any explanation still showing.");
        }

        [Test]
        public void Hide_AfterPresenting_ClearsTheNotice()
        {
            _panel.PresentPrerequisite(2, crossesEra: false, requiredEraName: null);
            Assert.IsTrue(_panel.IsShowing, "precondition");

            _panel.Hide();

            Assert.IsFalse(_panel.IsShowing);
        }

        [Test]
        public void Copy_BelowFirstLevel_IsEmptySoCallersStaySilent()
        {
            Assert.AreEqual(string.Empty, LevelLockNoticeCopy.Prerequisite(0, false, null));
            Assert.AreEqual(string.Empty, LevelLockNoticeCopy.Prerequisite(-1, true, "Ugat"));
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
            string message = LevelLockNoticeCopy.MissingObjective(objectiveId, 4);

            Assert.IsNotEmpty(message, objectiveId);
            StringAssert.Contains("Level 4", message);
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
                Assert.IsTrue(seen.Add(LevelLockNoticeCopy.MissingObjective(id, 7)),
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
            string message = LevelLockNoticeCopy.MissingObjective("objective.notAuthoredYet", 3);

            Assert.AreEqual(LevelLockNoticeCopy.Prerequisite(3, false, null), message);
            StringAssert.DoesNotContain("objective.", message);
        }

        [Test]
        public void MissingObjectiveCopy_BelowFirstLevel_IsEmptySoCallersStaySilent_SALIN220()
        {
            Assert.AreEqual(string.Empty, LevelLockNoticeCopy.MissingObjective(LevelObjectives.StoryViewed, 0));
        }

        [Test]
        public void PresentMissingObjective_ShowsTheObjectiveSentence_SALIN220()
        {
            _panel.PresentMissingObjective(LevelObjectives.ContextPassed, 5);

            Assert.IsTrue(_panel.IsShowing);
            Assert.AreEqual(
                LevelLockNoticeCopy.MissingObjective(LevelObjectives.ContextPassed, 5),
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
