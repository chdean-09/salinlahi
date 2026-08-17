using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Gameplay
{
    public sealed class LevelFlowControllerOutcomeTests
    {
        private GameObject _controllerObject;
        private GameObject _victoryObject;
        private GameObject _failureObject;

        [TearDown]
        public void TearDown()
        {
            if (_controllerObject != null) Object.DestroyImmediate(_controllerObject);
            if (_victoryObject != null) Object.DestroyImmediate(_victoryObject);
            if (_failureObject != null) Object.DestroyImmediate(_failureObject);
        }

        [Test]
        public void AcceptedCompletion_ShowsVictoryAfterOutro()
        {
            VictoryScreenUI victory = CreateVictory(out _victoryObject);
            TestLevelFlowController controller = CreateController(out _controllerObject);
            controller.NextResult = CampaignOutcomeCommitResult.Committed(null);
            SetPrivateField(controller, "_victoryScreen", victory);

            EventBus.RaiseLevelComplete();

            Assert.That(controller.CommitCalls, Is.EqualTo(1));
            Assert.That(GetPrivateField<GameObject>(victory, "_panel").activeSelf, Is.True);
        }

        [Test]
        public void PendingCompletion_KeepsVictoryHiddenAndShowsFailurePanel()
        {
            VictoryScreenUI victory = CreateVictory(out _victoryObject);
            CampaignOutcomeSaveFailurePanel panel = CreateFailurePanel(out _failureObject);
            TestLevelFlowController controller = CreateController(out _controllerObject);
            controller.NextResult = CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "journal-pending");
            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_saveFailurePanel", panel);

            EventBus.RaiseLevelComplete();

            Assert.That(GetPrivateField<GameObject>(victory, "_panel").activeSelf, Is.False);
            Assert.That(GetPrivateField<GameObject>(panel, "_overlayRoot").activeSelf, Is.True);
        }

        [TestCase(CampaignOutcomeCommitStatus.Rejected)]
        [TestCase(CampaignOutcomeCommitStatus.Blocked)]
        public void NonAcceptedCompletion_DoesNotShowVictory(CampaignOutcomeCommitStatus status)
        {
            VictoryScreenUI victory = CreateVictory(out _victoryObject);
            CampaignOutcomeSaveFailurePanel panel = CreateFailurePanel(out _failureObject);
            TestLevelFlowController controller = CreateController(out _controllerObject);
            controller.NextResult = status == CampaignOutcomeCommitStatus.Rejected
                ? CampaignOutcomeCommitResult.Rejected(null, CampaignSaveFailureCode.InvalidStructure, "rejected")
                : CampaignOutcomeCommitResult.Blocked(null, CampaignSaveFailureCode.InvalidStructure, "blocked");
            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_saveFailurePanel", panel);

            EventBus.RaiseLevelComplete();

            Assert.That(GetPrivateField<GameObject>(victory, "_panel").activeSelf, Is.False);
            Assert.That(GetPrivateField<GameObject>(panel, "_overlayRoot").activeSelf, Is.True);
        }

        [Test]
        public void DuplicateLevelComplete_DoesNotCommitTwice()
        {
            TestLevelFlowController controller = CreateController(out _controllerObject);
            controller.NextResult = CampaignOutcomeCommitResult.Committed(null);

            EventBus.RaiseLevelComplete();
            EventBus.RaiseLevelComplete();

            Assert.That(controller.CommitCalls, Is.EqualTo(1));
        }

        private TestLevelFlowController CreateController(out GameObject owner)
        {
            owner = new GameObject("LevelFlowController");
            return owner.AddComponent<TestLevelFlowController>();
        }

        private VictoryScreenUI CreateVictory(out GameObject owner)
        {
            owner = new GameObject("Victory");
            GameObject panel = new GameObject("VictoryPanel");
            panel.transform.SetParent(owner.transform);
            panel.SetActive(false);
            VictoryScreenUI victory = owner.AddComponent<VictoryScreenUI>();
            SetPrivateField(victory, "_panel", panel);
            return victory;
        }

        private CampaignOutcomeSaveFailurePanel CreateFailurePanel(out GameObject owner)
        {
            owner = new GameObject("Failure");
            GameObject overlay = new GameObject("Overlay");
            overlay.transform.SetParent(owner.transform);
            GameObject titleObject = new GameObject("Title");
            titleObject.transform.SetParent(overlay.transform);
            GameObject bodyObject = new GameObject("Body");
            bodyObject.transform.SetParent(overlay.transform);
            GameObject retryObject = new GameObject("Retry");
            retryObject.transform.SetParent(overlay.transform);
            GameObject menuObject = new GameObject("Menu");
            menuObject.transform.SetParent(overlay.transform);
            CampaignOutcomeSaveFailurePanel panel = owner.AddComponent<CampaignOutcomeSaveFailurePanel>();
            SetPrivateField(panel, "_overlayRoot", overlay);
            SetPrivateField(panel, "_titleText", titleObject.AddComponent<TMPro.TextMeshProUGUI>());
            SetPrivateField(panel, "_bodyText", bodyObject.AddComponent<TMPro.TextMeshProUGUI>());
            SetPrivateField(panel, "_retryButton", retryObject.AddComponent<UnityEngine.UI.Button>());
            SetPrivateField(panel, "_mainMenuButton", menuObject.AddComponent<UnityEngine.UI.Button>());
            return panel;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                field = typeof(LevelFlowController).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(VictoryScreenUI).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? typeof(CampaignOutcomeSaveFailurePanel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(target);
        }

        private sealed class TestLevelFlowController : LevelFlowController
        {
            public CampaignOutcomeCommitResult NextResult;
            public int CommitCalls { get; private set; }

            protected override CampaignOutcomeCommitResult CommitCompletion()
            {
                CommitCalls++;
                return NextResult;
            }
        }
    }
}
