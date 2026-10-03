using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class ResetJourneyConfirmationPanelTests
    {
        private GameObject _root;
        private GameObject _overlay;
        private TMP_Text _title;
        private TMP_Text _body;
        private Button _confirm;
        private Button _cancel;
        private ResetJourneyConfirmationPanel _panel;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Panel");
            _overlay = new GameObject("Overlay");
            _overlay.transform.SetParent(_root.transform);
            _title = new GameObject("Title").AddComponent<TextMeshProUGUI>();
            _title.transform.SetParent(_overlay.transform);
            _body = new GameObject("Body").AddComponent<TextMeshProUGUI>();
            _body.transform.SetParent(_overlay.transform);
            _confirm = new GameObject("Confirm").AddComponent<Button>();
            _confirm.transform.SetParent(_overlay.transform);
            _cancel = new GameObject("Cancel").AddComponent<Button>();
            _cancel.transform.SetParent(_overlay.transform);
            _panel = _root.AddComponent<ResetJourneyConfirmationPanel>();
            SetPrivateField("_overlayRoot", _overlay);
            SetPrivateField("_titleText", _title);
            SetPrivateField("_bodyText", _body);
            SetPrivateField("_confirmButton", _confirm);
            SetPrivateField("_cancelButton", _cancel);
            _overlay.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Present_ShowsConfirmationCopyWithBothButtons()
        {
            _panel.Present(() => ResetJourneyOutcome.Succeeded, () => { });

            Assert.That(_overlay.activeSelf, Is.True);
            Assert.That(_title.text, Is.EqualTo(ResetJourneyFlow.ConfirmTitle));
            Assert.That(_body.text, Is.EqualTo(ResetJourneyFlow.ConfirmBody));
            Assert.That(_confirm.gameObject.activeSelf, Is.True);
            Assert.That(_cancel.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Cancel_HidesWithoutInvokingExecute()
        {
            // Pins AC2: cancelling must not touch persistence.
            int executes = 0;
            _panel.Present(() => { executes++; return ResetJourneyOutcome.Succeeded; }, () => { });

            _cancel.onClick.Invoke();

            Assert.That(executes, Is.EqualTo(0));
            Assert.That(_overlay.activeSelf, Is.False);
        }

        [Test]
        public void Confirm_WhenSucceeded_ShowsSuccessAndContinueInvokesCallback()
        {
            int continues = 0;
            _panel.Present(() => ResetJourneyOutcome.Succeeded, () => continues++);

            _confirm.onClick.Invoke();

            Assert.That(_title.text, Is.EqualTo(ResetJourneyFlow.SuccessTitle));
            Assert.That(_cancel.gameObject.activeSelf, Is.False,
                "Success state offers Continue only.");

            _confirm.onClick.Invoke();
            Assert.That(continues, Is.EqualTo(1));
        }

        [Test]
        public void Confirm_WhenFailed_ShowsFailureAndRetryReinvokesExecute()
        {
            int executes = 0;
            _panel.Present(
                () => { executes++; return ResetJourneyOutcome.RetryableFailure; },
                () => { });

            _confirm.onClick.Invoke();

            Assert.That(_title.text, Is.EqualTo(ResetJourneyFlow.FailureTitle));
            Assert.That(_body.text, Is.EqualTo(ResetJourneyFlow.FailureBody));
            Assert.That(_cancel.gameObject.activeSelf, Is.True, "Failure state offers Close.");

            _confirm.onClick.Invoke();
            Assert.That(executes, Is.EqualTo(2), "Retry must re-run the reset.");
        }

        [Test]
        public void FailedThenRetrySucceeds_ReachesSuccessState()
        {
            int executes = 0;
            _panel.Present(
                () => ++executes >= 2 ? ResetJourneyOutcome.Succeeded : ResetJourneyOutcome.RetryableFailure,
                () => { });

            _confirm.onClick.Invoke();
            _confirm.onClick.Invoke();

            Assert.That(_title.text, Is.EqualTo(ResetJourneyFlow.SuccessTitle));
        }

        [Test]
        public void Close_AfterFailure_HidesPanel()
        {
            _panel.Present(() => ResetJourneyOutcome.RetryableFailure, () => { });
            _confirm.onClick.Invoke();

            _cancel.onClick.Invoke();

            Assert.That(_overlay.activeSelf, Is.False);
        }

        [Test]
        public void Present_WithNoInjectedReferences_SelfBuildsAndShows()
        {
            GameObject bare = new GameObject("Bare");
            try
            {
                ResetJourneyConfirmationPanel panel = bare.AddComponent<ResetJourneyConfirmationPanel>();
                Assert.DoesNotThrow(() => panel.Present(() => ResetJourneyOutcome.Succeeded, () => { }));
                Assert.That(panel.HasRequiredReferences, Is.True,
                    "Present must runtime-build missing UI references.");
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void Present_WithNullExecute_DoesNotShow()
        {
            _panel.Present(null, () => { });
            Assert.That(_overlay.activeSelf, Is.False);
        }

        private void SetPrivateField(string fieldName, object value)
        {
            FieldInfo field = typeof(ResetJourneyConfirmationPanel).GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(_panel, value);
        }
    }
}
