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
