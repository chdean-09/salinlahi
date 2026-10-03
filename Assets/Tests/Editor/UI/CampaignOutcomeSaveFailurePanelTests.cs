using System;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class CampaignOutcomeSaveFailurePanelTests
    {
        private GameObject _root;
        private GameObject _overlay;
        private Button _retry;
        private Button _mainMenu;
        private TMP_Text _title;
        private TMP_Text _body;
        private CampaignOutcomeSaveFailurePanel _panel;

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
            _retry = new GameObject("Retry").AddComponent<Button>();
            _retry.transform.SetParent(_overlay.transform);
            _mainMenu = new GameObject("MainMenu").AddComponent<Button>();
            _mainMenu.transform.SetParent(_overlay.transform);
            _panel = _root.AddComponent<CampaignOutcomeSaveFailurePanel>();
            SetPrivateField("_overlayRoot", _overlay);
            SetPrivateField("_titleText", _title);
            SetPrivateField("_bodyText", _body);
            SetPrivateField("_retryButton", _retry);
            SetPrivateField("_mainMenuButton", _mainMenu);
            _overlay.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                UnityEngine.Object.DestroyImmediate(_root);
            else
            {
                if (_overlay != null) UnityEngine.Object.DestroyImmediate(_overlay);
                if (_title != null) UnityEngine.Object.DestroyImmediate(_title.gameObject);
                if (_body != null) UnityEngine.Object.DestroyImmediate(_body.gameObject);
                if (_retry != null) UnityEngine.Object.DestroyImmediate(_retry.gameObject);
                if (_mainMenu != null) UnityEngine.Object.DestroyImmediate(_mainMenu.gameObject);
            }
        }

        [Test]
        public void Awake_HidesOverlay()
        {
            _overlay.SetActive(true);
            typeof(CampaignOutcomeSaveFailurePanel).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_panel, null);
            Assert.That(_overlay.activeSelf, Is.False);
        }

        [Test]
        public void PresentPending_UsesPendingSafeCopyAndEnablesActions()
        {
            _panel.Present(
                CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () => CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () => { },
                () => { });

            Assert.That(_title.text, Is.EqualTo("Your progress is waiting to be saved"));
            Assert.That(_body.text, Does.Contain("Your completion will remain pending"));
            Assert.That(_overlay.activeSelf, Is.True);
            Assert.That(_retry.interactable, Is.True);
            Assert.That(_mainMenu.interactable, Is.True);
        }

        [Test]
        public void PresentRejected_ExplainsReplayMayBeRequired()
        {
            _panel.Present(
                CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.InvalidStructure, "bad"),
                () => CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.InvalidStructure, "bad"),
                () => { },
                () => { });

            Assert.That(_title.text, Is.EqualTo("This completion could not be preserved"));
            Assert.That(_body.text, Does.Contain("you may need to replay this level"));
            Assert.That(_body.text, Does.Not.Contain("bad"));
        }

        [Test]
        public void Awake_AfterPresent_DoesNotHidePanelOrDuplicateRetry()
        {
            int retryAttempts = 0;
            int accepted = 0;
            _panel.Present(
                CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () =>
                {
                    retryAttempts++;
                    return CampaignOutcomeCommitResult.Committed(null);
                },
                () => accepted++,
                () => { });

            InvokeAwake();

            Assert.That(_overlay.activeSelf, Is.True);
            _retry.onClick.Invoke();

            Assert.That(retryAttempts, Is.EqualTo(1));
            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(_overlay.activeSelf, Is.False);
        }

        [Test]
        public void Retry_WhenAccepted_HidesPanelAndInvokesSuccessOnce()
        {
            int accepted = 0;
            _panel.Present(
                CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () => CampaignOutcomeCommitResult.Committed(null),
                () => accepted++,
                () => { });

            _retry.onClick.Invoke();
            _retry.onClick.Invoke();

            Assert.That(accepted, Is.EqualTo(1));
            Assert.That(_overlay.activeSelf, Is.False);
        }

        [Test]
        public void Retry_WhenItFails_ReenablesButtonsAndUsesSafeBody()
        {
            _panel.Present(
                CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "first"),
                () => CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.InvalidStructure, "raw-error"),
                () => { },
                () => { });

            _retry.onClick.Invoke();

            Assert.That(_retry.interactable, Is.True);
            Assert.That(_mainMenu.interactable, Is.True);
            Assert.That(_title.text, Is.EqualTo("This completion could not be preserved"));
            Assert.That(_body.text, Does.Not.Contain("raw-error"));
        }

        [Test]
        public void MainMenu_InvokesNavigationWithoutClearingPanelState()
        {
            int navigated = 0;
            _panel.Present(
                CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () => CampaignOutcomeCommitResult.PendingRetry(null, CampaignSaveFailureCode.IoFailure, "io"),
                () => { },
                () => navigated++);

            _mainMenu.onClick.Invoke();

            Assert.That(navigated, Is.EqualTo(1));
            Assert.That(_overlay.activeSelf, Is.True);
        }

        [Test]
        public void Present_WhenRequiredReferencesAreMissing_DoesNotThrow()
        {
            GameObject incompleteObject = new GameObject("Incomplete");
            CampaignOutcomeSaveFailurePanel incomplete = incompleteObject.AddComponent<CampaignOutcomeSaveFailurePanel>();

            Assert.DoesNotThrow(() => incomplete.Present(
                CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.InvalidStructure, "bad"),
                null, null, null));
            UnityEngine.Object.DestroyImmediate(incompleteObject);
        }

        private void SetPrivateField(string fieldName, object value)
        {
            System.Reflection.FieldInfo field = typeof(CampaignOutcomeSaveFailurePanel).GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_panel, value);
        }

        private void InvokeAwake()
        {
            typeof(CampaignOutcomeSaveFailurePanel).GetMethod(
                "Awake",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(_panel, null);
        }
    }
}
