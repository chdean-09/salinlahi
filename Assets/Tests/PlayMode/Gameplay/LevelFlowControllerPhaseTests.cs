using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.PlayMode.Gameplay
{
    /// <summary>
    /// PlayMode coverage for the LevelFlowController coroutine host driving the
    /// LF-CONTRACT-v2 machine (SALIN-178). Exhaustive transition legality lives in
    /// the EditMode LevelFlowMachineTests; this fixture covers the representative
    /// host behaviors: event routing, the atomic-save gate, defeat cleanup, and
    /// stub-phase auto-advance.
    /// </summary>
    [TestFixture]
    public sealed class LevelFlowControllerPhaseTests
    {
        private const string MissingWaveManagerError =
            "[Salinlahi] LevelFlowController: WaveManager reference missing.";

        private readonly List<Object> _objectsToDestroy = new();

        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            ClearSingletonInstance<GameManager>();
            LevelTutorialProgress.ResetLevel1TutorialForTests();
            Time.timeScale = 1f;

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();

            GameObject runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            while (runtimePanel != null)
            {
                Object.DestroyImmediate(runtimePanel);
                runtimePanel = GameObject.Find("[Runtime] ActiveCluePanel");
            }
        }

        [UnityTest]
        public IEnumerator LegacyFlow_ReachesDefenseAndWaitsForDefenseCompletion()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(out _, out _);

            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "With no defense-completion report the flow must hold in Defense.");
            Assert.AreEqual(0, controller.CommitCalls);
        }

        [UnityTest]
        public IEnumerator DefenseComplete_CommitsOnceAndShowsVictory()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out _);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(1, controller.CommitCalls);
            Assert.IsTrue(victoryPanel.activeSelf, "Accepted save must open Results (victory).");
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator DuplicateDefenseComplete_DoesNotDoubleCommit()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(out _, out _);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(5);

            Assert.AreEqual(1, controller.CommitCalls,
                "Duplicate defense-completion events must be inert.");
        }

        [UnityTest]
        public IEnumerator RogueLevelCompleteEvent_IsIgnoredByTheRunningFlow()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out _);

            yield return WaitFrames(10);
            EventBus.RaiseLevelComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(0, controller.CommitCalls,
                "A rogue OnLevelComplete must not commit while the machine holds Defense.");
            Assert.IsFalse(victoryPanel.activeSelf);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator SaveNotAccepted_ShowsFailurePanelAndWithholdsVictory()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out GameObject failureOverlay);
            controller.NextResult = CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.InvalidStructure, "blocked");

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.IsFalse(victoryPanel.activeSelf, "Results must be withheld without an accepted save.");
            Assert.IsTrue(failureOverlay.activeSelf);
            Assert.AreEqual(LevelPhase.AtomicSave, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator SaveRetryAccepted_ThenVictoryShows()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out GameObject failureOverlay);
            controller.NextResult = CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "journal-pending");
            controller.RetryResult = CampaignOutcomeCommitResult.Committed(null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.IsTrue(failureOverlay.activeSelf, "Setup: failure panel must be up before retry.");

            ClickRetryButton(failureOverlay);
            yield return WaitFrames(10);

            Assert.IsTrue(victoryPanel.activeSelf, "An accepted retry must release Results.");
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator GameOver_DuringStoryDialogue_ShowsDefeatAndNeverCommits()
        {
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));

            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureIntroDialogue, out GameObject victoryPanel, out _,
                out GameObject defeatPanel, dialogue);

            yield return WaitFrames(5);
            Assert.AreEqual(LevelPhase.Story, MachineOf(controller).Phase,
                "Setup: the flow must be waiting inside the Story phase.");

            EventBus.RaiseGameOver();
            yield return WaitFrames(2);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(5);

            Assert.IsTrue(defeatPanel.activeSelf);
            Assert.IsFalse(victoryPanel.activeSelf);
            Assert.AreEqual(0, controller.CommitCalls);
            Assert.AreEqual(LevelPhase.Defeated, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator GameOver_DuringDefense_ShowsDefeat_AndLateDefenseCompleteIsIgnored()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out _, out GameObject defeatPanel);

            yield return WaitFrames(10);
            EventBus.RaiseGameOver();
            yield return WaitFrames(2);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(5);

            Assert.IsTrue(defeatPanel.activeSelf);
            Assert.IsFalse(victoryPanel.activeSelf);
            Assert.AreEqual(0, controller.CommitCalls,
                "A defense completion after defeat must be inert.");
            Assert.AreEqual(LevelPhase.Defeated, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator FullPlanConfig_AutoCompletesStubPhasesToDefense()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapFlow(
                config =>
                {
                    config.focusWords.Add(new FocusWordDefinition());
                    config.learningRequirements.Add(new ContentRequirement());
                    config.practiceRequirements.Add(new ContentRequirement());
                },
                out _, out _, out _, dialogueController: null);

            yield return WaitFrames(20);

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Stub executors (FocusWords, SymbolLearning, RequiredPractice) must auto-complete.");
        }

        [UnityTest]
        public IEnumerator Pause_DuringDefense_SetsMachinePausedAndResumeClears()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(out _, out _);

            yield return WaitFrames(10);
            EventBus.RaiseGamePaused();
            Assert.IsTrue(MachineOf(controller).IsPaused);

            EventBus.RaiseGameResumed();
            Assert.IsFalse(MachineOf(controller).IsPaused);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator WaveManagerCompletion_RoutesThroughDefenseCompleteToVictory()
        {
            // WaveManager logs config-resolution errors from both Start() and
            // StartLevel() in a bare test scene; the exact count is not the subject
            // under test (the routing is), so suppress log failures for this test.
            LogAssert.ignoreFailingMessages = true;
            WaveManager waveManager = CreateComponent<WaveManager>("WaveManager");
            TestPhaseFlowController controller = BootstrapFlow(
                _ => { }, out GameObject victoryPanel, out _, out _,
                dialogueController: null, waveManager: waveManager);

            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase);

            InvokePrivate(waveManager, "CompleteRun");
            yield return WaitFrames(10);

            Assert.AreEqual(1, controller.CommitCalls,
                "WaveManager completion must route through DefenseComplete into the atomic save.");
            Assert.IsTrue(victoryPanel.activeSelf);
        }

        // ---------------------------------------------------------------------
        // Bootstrap helpers
        // ---------------------------------------------------------------------

        private TestPhaseFlowController BootstrapLegacyFlow(
            out GameObject victoryPanel, out GameObject failureOverlay)
        {
            return BootstrapFlow(_ => { }, out victoryPanel, out failureOverlay, out _, null);
        }

        private TestPhaseFlowController BootstrapLegacyFlow(
            out GameObject victoryPanel, out GameObject failureOverlay, out GameObject defeatPanel)
        {
            return BootstrapFlow(_ => { }, out victoryPanel, out failureOverlay, out defeatPanel, null);
        }

        private TestPhaseFlowController BootstrapFlow(
            System.Action<LevelConfigSO> configure,
            out GameObject victoryPanel,
            out GameObject failureOverlay,
            out GameObject defeatPanel,
            DialogueController dialogueController,
            WaveManager waveManager = null)
        {
            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);

            LevelConfigSO config = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(config);
            configure(config);

            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            victoryPanel = CreatePanel("VictoryPanel");
            SetPrivateField(victory, "_panel", victoryPanel);

            DefeatScreenUI defeat = CreateComponent<DefeatScreenUI>("DefeatScreen");
            defeatPanel = CreatePanel("DefeatPanel");
            SetPrivateField(defeat, "_panel", defeatPanel);

            CampaignOutcomeSaveFailurePanel failurePanel = CreateFailurePanel(out failureOverlay);

            TestPhaseFlowController controller =
                CreateComponent<TestPhaseFlowController>("LevelFlowController");
            SetPrivateField(controller, "_victoryScreen", victory);
            SetPrivateField(controller, "_defeatScreen", defeat);
            SetPrivateField(controller, "_saveFailurePanel", failurePanel);
            if (dialogueController != null)
                SetPrivateField(controller, "_dialogueController", dialogueController);

            InvokePrivate(controller, "BootstrapRuntimeFlow",
                new object[] { config, waveManager, null, null });
            return controller;
        }

        private void ConfigureIntroDialogue(LevelConfigSO config)
        {
            DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            dialogue.lines = new[] { new DialogueLine { speakerName = "Test", text = "Line" } };
            _objectsToDestroy.Add(dialogue);
            config.introDialogue = dialogue;
        }

        private CampaignOutcomeSaveFailurePanel CreateFailurePanel(out GameObject overlay)
        {
            GameObject owner = new GameObject("FailurePanelOwner");
            _objectsToDestroy.Add(owner);
            overlay = new GameObject("Overlay");
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

        private static void ClickRetryButton(GameObject failureOverlay)
        {
            UnityEngine.UI.Button retry = failureOverlay.transform.Find("Retry")
                .GetComponent<UnityEngine.UI.Button>();
            retry.onClick.Invoke();
        }

        private static LevelFlowMachine MachineOf(LevelFlowController controller)
        {
            return GetPrivateField<LevelFlowMachine>(controller, "_machine")
                ?? throw new AssertionException("The flow has no running machine.");
        }

        private static IEnumerator WaitFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
                yield return null;
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.SetActive(false);
            _objectsToDestroy.Add(panel);
            return panel;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new GameObject(name);
            T component = gameObject.AddComponent<T>();
            _objectsToDestroy.Add(gameObject);
            return component;
        }

        // ---------------------------------------------------------------------
        // Reflection helpers
        // ---------------------------------------------------------------------

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = FindField(target.GetType(), fieldName);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return (T)field.GetValue(target);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                    return field;
                type = type.BaseType;
            }

            return null;
        }

        private static void InvokePrivate(object target, string methodName, object[] args = null)
        {
            System.Type type = target.GetType();
            MethodInfo method = null;
            while (type != null && method == null)
            {
                method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, args ?? new object[0]);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance", BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }

        /// <summary>
        /// Deterministic commit seams, mirroring the pattern used by
        /// LevelFlowControllerOutcomeTests.
        /// </summary>
        private sealed class TestPhaseFlowController : LevelFlowController
        {
            public CampaignOutcomeCommitResult NextResult = CampaignOutcomeCommitResult.Committed(null);
            public CampaignOutcomeCommitResult RetryResult = CampaignOutcomeCommitResult.Committed(null);
            public int CommitCalls { get; private set; }

            protected override CampaignOutcomeCommitResult CommitCompletion()
            {
                CommitCalls++;
                return NextResult;
            }

            protected override CampaignOutcomeCommitResult RetryCompletion()
            {
                return RetryResult;
            }
        }
    }
}
