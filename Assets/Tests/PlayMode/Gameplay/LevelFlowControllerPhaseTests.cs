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

            ChallengeRuntimeState.Clear();
            TutorialRuntimeState.Clear();
            foreach (Level1TutorialGuideUI guide in Object.FindObjectsByType<Level1TutorialGuideUI>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (guide != null)
                    Object.DestroyImmediate(guide.gameObject);
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
        public IEnumerator GameOver_DuringBlockedSave_IsIgnoredAndRetryStillCompletes()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out GameObject victoryPanel, out GameObject failureOverlay, out GameObject defeatPanel);
            controller.NextResult = CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "journal-pending");
            controller.RetryResult = CampaignOutcomeCommitResult.Committed(null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.AtomicSave, MachineOf(controller).Phase,
                "Setup: the flow must be holding the atomic-save retry gate.");

            EventBus.RaiseGameOver();
            yield return WaitFrames(5);

            Assert.IsFalse(defeatPanel.activeSelf,
                "A game over raised at the save gate must not open the defeat screen.");
            Assert.AreEqual(LevelPhase.AtomicSave, MachineOf(controller).Phase);

            ClickRetryButton(failureOverlay);
            yield return WaitFrames(10);

            Assert.IsTrue(victoryPanel.activeSelf,
                "The level must still complete after the ignored defeat.");
            Assert.IsFalse(defeatPanel.activeSelf);
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator GameOver_DuringOutro_IsIgnoredAndVictoryStillShows()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));

            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureOutroDialogue, out GameObject victoryPanel, out _,
                out GameObject defeatPanel, dialogue);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.Results, MachineOf(controller).Phase,
                "Setup: the flow must be waiting on the outro inside Results.");

            EventBus.RaiseGameOver();
            yield return WaitFrames(5);

            Assert.IsFalse(defeatPanel.activeSelf,
                "A straggler kill during the outro must not open the defeat screen on a saved level.");
            Assert.IsFalse(victoryPanel.activeSelf,
                "The ignored defeat must not release the outro wait early either.");
            Assert.AreEqual(LevelPhase.Results, MachineOf(controller).Phase);

            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(5);

            Assert.IsTrue(victoryPanel.activeSelf);
            Assert.IsFalse(defeatPanel.activeSelf,
                "Victory and defeat panels must never be up together.");
            Assert.AreEqual(1, controller.CommitCalls);
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator StubLearningPhases_AutoCompleteToDefense()
        {
            // FocusWords gained its surface with SALIN-138 and now holds for the
            // preview; SymbolLearning and RequiredPractice remain auto-completing
            // stubs until their campaign gates land.
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapFlow(
                config =>
                {
                    config.learningRequirements.Add(new ContentRequirement());
                    config.practiceRequirements.Add(new ContentRequirement());
                },
                out _, out _, out _, dialogueController: null);

            yield return WaitFrames(20);

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Stub executors (SymbolLearning, RequiredPractice) must auto-complete.");
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

        [UnityTest]
        public IEnumerator NoRunningMachine_WaveManagerCompletion_KeepsTheLegacyLevelCompleteRaise()
        {
            // Same bare-scene config-resolution noise as the routed case above; the
            // branch taken, not the log count, is the subject under test.
            LogAssert.ignoreFailingMessages = true;
            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);

            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            GameObject victoryPanel = CreatePanel("VictoryPanel");
            SetPrivateField(victory, "_panel", victoryPanel);

            TestPhaseFlowController controller =
                CreateComponent<TestPhaseFlowController>("LevelFlowController");
            SetPrivateField(controller, "_victoryScreen", victory);

            WaveManager waveManager = CreateComponent<WaveManager>("WaveManager");
            yield return WaitFrames(5);
            Assert.IsFalse(LevelFlowController.RoutesDefenseCompletion,
                "Setup: a controller with no running flow must not claim defense routing.");

            InvokePrivate(waveManager, "CompleteRun");
            yield return WaitFrames(10);

            Assert.AreEqual(1, controller.CommitCalls,
                "With no machine the legacy OnLevelComplete path still owns completion.");
            Assert.IsTrue(victoryPanel.activeSelf);
        }

        [UnityTest]
        public IEnumerator ContextChallenge_RunsAfterDefense_AndCompletionCommits()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureContextChallenge, out GameObject victoryPanel, out _, out _,
                dialogueController: null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "The context challenge must run as phase 6, after Defense.");
            Assert.AreEqual(0, controller.CommitCalls,
                "No campaign progress may commit while the challenge is open.");

            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            Assert.IsNotNull(challenge, "The flow must provide a ChallengeFlowController for phase 6.");
            challenge.SubmitPlacement("w-1");
            yield return WaitFrames(10);

            Assert.AreEqual(1, controller.CommitCalls);
            Assert.IsTrue(victoryPanel.activeSelf);
            Assert.AreEqual(LevelPhase.Completed, MachineOf(controller).Phase);
        }

        [UnityTest]
        public IEnumerator ContextChallenge_ExitDoesNotCommitProgress()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureContextChallenge, out GameObject victoryPanel, out _, out _,
                dialogueController: null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "Setup: the challenge phase must be open.");

            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            challenge.Exit();
            yield return WaitFrames(10);

            Assert.AreEqual(0, controller.CommitCalls,
                "Exiting the challenge must never commit partial campaign progress.");
            Assert.IsFalse(victoryPanel.activeSelf);
            Assert.AreEqual(LevelPhase.Exited, MachineOf(controller).Phase);
        }

        // SALIN-135 AC3/AC4. TutorialRuntimeState is static, so it outlives the scene. A defeat
        // landing mid-beat skips the beat's own unwind, and the retried attempt would inherit a
        // combat override or an input lock -- combat or drawing dead on arrival, with no way for
        // the player to tell why. Only a tutorial that actually replays would self-heal, and a
        // resumed or completed sequence does not replay.
        [UnityTest]
        public IEnumerator GameOver_WithTutorialStateOpen_ClearsTheTutorialRuntimeStatics()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapLegacyFlow(
                out _, out _, out GameObject defeatPanel);

            yield return WaitFrames(10);

            // Stand in for a defeat arriving in the middle of a teaching beat.
            TutorialRuntimeState.Begin(1);
            TutorialRuntimeState.SetCombatOverrideActive(true);
            TutorialRuntimeState.SetDrawingInputLocked(true);
            Assert.IsTrue(TutorialRuntimeState.IsCombatOverrideActive,
                "Setup: the beat must actually hold the override for this test to bite.");

            EventBus.RaiseGameOver();
            yield return WaitFrames(5);

            Assert.AreEqual(LevelPhase.Defeated, MachineOf(controller).Phase);
            Assert.IsTrue(defeatPanel.activeSelf);
            Assert.AreEqual(0, controller.CommitCalls,
                "A defeat must never commit campaign progress.");
            Assert.IsFalse(TutorialRuntimeState.IsActive,
                "Terminal cleanup must close the tutorial statics.");
            Assert.IsFalse(TutorialRuntimeState.IsCombatOverrideActive,
                "A retried attempt must start with combat live.");
            Assert.IsFalse(TutorialRuntimeState.IsDrawingInputLocked,
                "A retried attempt must start with drawing unlocked.");
        }

        [UnityTest]
        public IEnumerator Exit_WithTutorialStateOpen_ClearsTheTutorialRuntimeStatics()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureContextChallenge, out _, out _, out _, dialogueController: null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.ContextChallenge, MachineOf(controller).Phase,
                "Setup: the challenge phase must be open.");

            TutorialRuntimeState.Begin(1);
            TutorialRuntimeState.SetDrawingInputLocked(true);

            ChallengeFlowController challenge =
                GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController");
            challenge.Exit();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.Exited, MachineOf(controller).Phase);
            Assert.AreEqual(0, controller.CommitCalls,
                "Exiting must never commit partial campaign progress.");
            Assert.IsFalse(TutorialRuntimeState.IsDrawingInputLocked,
                "Exiting mid-beat must not strand the input lock for the next attempt.");
            Assert.IsFalse(TutorialRuntimeState.IsActive);
        }

        [UnityTest]
        public IEnumerator FocusWordPreview_RendersConfigCopyWhileDrawingStaysDisabled()
        {
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureFocusWordLevel, out _, out _, out _, dialogue);

            yield return WaitFrames(5);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase,
                "The flow must hold in FocusWords while the preview is up.");
            Assert.IsFalse(GameManager.Instance.AcceptsDrawingInput,
                "Both words and decompositions must be readable BEFORE drawing begins.");

            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview, "The flow must provide the focus-word preview surface.");
            Assert.IsTrue(preview.IsPresenting);
            StringAssert.Contains("LUNA", preview.RenderedText);
            StringAssert.Contains("test-moon", preview.RenderedText);
            StringAssert.Contains("TALA", preview.RenderedText);
            StringAssert.Contains("test-star", preview.RenderedText);
            StringAssert.Contains("lu", preview.RenderedText);
            StringAssert.Contains("ta", preview.RenderedText);
        }

        [UnityTest]
        public IEnumerator FocusWordPreview_ContinueEnablesDrawingExactlyOnceAtDefense()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureFocusWordLevel, out _, out _, out _, dialogue);

            yield return WaitFrames(5);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase,
                "Setup: the preview must be open before Continue.");

            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview);

            int enableTransitions = 0;
            bool previous = GameManager.Instance.AcceptsDrawingInput;
            Assert.IsFalse(previous, "Setup: drawing must be disabled while the preview is up.");

            preview.Continue();
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                bool current = GameManager.Instance.AcceptsDrawingInput;
                if (current && !previous)
                    enableTransitions++;
                previous = current;
            }

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Continue must carry the flow into Defense.");
            Assert.AreEqual(1, enableTransitions,
                "Drawing input must be enabled exactly once, when the defense sequence begins.");
            Assert.IsTrue(GameManager.Instance.AcceptsDrawingInput);
        }

        private void ConfigureFocusWordLevel(LevelConfigSO config)
        {
            ConfigureIntroDialogue(config);

            BaybayinCharacterSO lu = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            lu.characterID = "LU";
            lu.syllable = "lu";
            lu.stableId = "symbol.test-lu";
            _objectsToDestroy.Add(lu);
            BaybayinCharacterSO ta = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
            ta.characterID = "TA2";
            ta.syllable = "ta";
            ta.stableId = "symbol.test-ta";
            _objectsToDestroy.Add(ta);

            config.focusWords.Add(new FocusWordDefinition
            {
                stableId = "level.test.focus.01",
                latinSpelling = "LUNA",
                displayLabel = "LUNA",
                meaning = "test-moon",
                decomposition = new System.Collections.Generic.List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = lu, spokenValueId = "value.test-lu" },
                },
            });
            config.focusWords.Add(new FocusWordDefinition
            {
                stableId = "level.test.focus.02",
                latinSpelling = "TALA",
                displayLabel = "TALA",
                meaning = "test-star",
                decomposition = new System.Collections.Generic.List<SymbolValueReference>
                {
                    new SymbolValueReference { symbol = ta, spokenValueId = "value.test-ta" },
                },
            });
        }

        [UnityTest]
        public IEnumerator FocusWordPreview_ReleasesDrawingBeforeThePreWaveBeats()
        {
            TestPhaseFlowController controller = BootstrapPreWaveBeatFlow();

            yield return WaitFrames(5);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase,
                "Setup: the preview must be open before Continue.");
            Assert.IsFalse(GameManager.Instance.AcceptsDrawingInput,
                "Setup: drawing must be suppressed while the preview is up.");

            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview, "Setup: the flow must provide the preview surface.");
            preview.Continue();
            yield return WaitFrames(15);

            Assert.AreEqual(LevelPhase.Defense, MachineOf(controller).Phase,
                "Setup: Continue must carry the flow into Defense.");
            Assert.IsFalse(PreWaveBeatOf(controller).IsFinished,
                "Setup: the pre-wave beat must still be holding the Defense executor.");
            Assert.IsTrue(GameManager.Instance.AcceptsDrawingInput,
                "Suppression must be released ahead of the pre-wave beats — a beat's "
                + "StartGame() remedy cannot clear it, so a late release hard-locks the level.");
        }

        [UnityTest]
        public IEnumerator AbortedPreWaveBeat_ReleasesDrawingSuppression()
        {
            TestPhaseFlowController controller = BootstrapPreWaveBeatFlow();

            yield return WaitFrames(5);
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);
            Assert.IsFalse(GameManager.Instance.AcceptsDrawingInput,
                "Setup: drawing must be suppressed while the preview is up.");

            FocusWordPreviewController preview =
                Object.FindFirstObjectByType<FocusWordPreviewController>();
            Assert.IsNotNull(preview, "Setup: the flow must provide the preview surface.");
            preview.Continue();
            yield return WaitFrames(15);
            Assert.IsFalse(PreWaveBeatOf(controller).IsFinished,
                "Setup: the pre-wave beat must be open before the exit.");

            PreWaveBeatOf(controller).Exit();
            yield return WaitFrames(15);

            Assert.IsFalse(MachineOf(controller).IsTerminal,
                "Setup: an aborted flow leaves the machine non-terminal, so terminal "
                + "cleanup cannot be the thing that releases suppression.");
            Assert.IsTrue(GameManager.Instance.AcceptsDrawingInput,
                "An aborted flow must not strand drawing suppression on the persistent "
                + "GameManager — it survives scene loads and kills drawing everywhere.");
        }

        [UnityTest]
        public IEnumerator TeardownMidPreview_ReleasesDrawingSuppression()
        {
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));
            TestPhaseFlowController controller = BootstrapFlow(
                ConfigureFocusWordLevel, out _, out _, out _, dialogue);

            yield return WaitFrames(5);
            // The intro dialogue parks GameManager in Paused, and the real controller
            // lifts that pause itself before it raises the event (DialogueController:
            // ExitDialoguePause then RaiseDialogueComplete). Faking only the event
            // strands the fixture in Paused, where AcceptsDrawingInput is false whatever
            // the suppression flag holds — every assertion below would then pass on the
            // pause alone. The other release tests reach Defense, whose StartGame()
            // restores Playing for them; this one is torn down before Defense opens.
            GameManager.Instance.ExitDialoguePause();
            EventBus.RaiseDialogueComplete();
            yield return WaitFrames(10);
            Assert.AreEqual(GameState.Playing, GameManager.Instance.CurrentState,
                "Setup: only a Playing GameManager lets AcceptsDrawingInput report "
                + "the suppression flag rather than the pause.");
            Assert.AreEqual(LevelPhase.FocusWords, MachineOf(controller).Phase,
                "Setup: the preview must be open.");
            Assert.IsFalse(GameManager.Instance.AcceptsDrawingInput,
                "Setup: drawing must be suppressed while the preview is up.");

            Object.DestroyImmediate(controller.gameObject);
            yield return WaitFrames(2);

            Assert.IsTrue(GameManager.Instance.AcceptsDrawingInput,
                "A scene unload mid-preview must release suppression: coroutines never "
                + "run their finally blocks when the host is destroyed.");
        }

        private TestPhaseFlowController BootstrapPreWaveBeatFlow()
        {
            DialogueController dialogue = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogue, "_overlayPanel", CreatePanel("DialogueOverlay"));
            return BootstrapFlow(
                config =>
                {
                    ConfigureFocusWordLevel(config);
                    ConfigureContextChallenge(config);
                    // The prototype path runs the sequence as a pre-wave beat inside
                    // the Defense executor instead of planning it as phase 6.
                    config.challengePrototypeEnabled = true;
                },
                out _, out _, out _, dialogue);
        }

        private static ChallengeFlowController PreWaveBeatOf(LevelFlowController controller)
        {
            return GetPrivateField<ChallengeFlowController>(controller, "_challengeFlowController")
                ?? throw new AssertionException("The flow has no pre-wave beat controller.");
        }

        [UnityTest]
        public IEnumerator AcceptedSave_PopulatesResultsAndRewardGrant()
        {
            LogAssert.Expect(LogType.Error, MissingWaveManagerError);
            BaybayinCharacterSO introduced = null;
            TestPhaseFlowController controller = BootstrapFlow(
                config =>
                {
                    config.stableId = "level.test.01";
                    config.rewardIds.Add("memory.test");
                    config.rewardIds.Add("title.test");
                    introduced = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
                    introduced.stableId = "symbol.test";
                    introduced.firstIntroductionLevelId = "level.test.01";
                    _objectsToDestroy.Add(introduced);
                    config.cumulativeSymbolPool.Add(new SymbolValueReference
                    {
                        symbol = introduced,
                        spokenValueId = "value.test",
                    });
                },
                out GameObject victoryPanel, out _, out _, dialogueController: null);

            yield return WaitFrames(10);
            EventBus.RaiseDefenseComplete();
            yield return WaitFrames(10);

            Assert.AreEqual(1, controller.CommitCalls);
            Assert.IsTrue(victoryPanel.activeSelf);

            Assert.IsNotNull(controller.LastResults,
                "AtomicSave must compute the level results before committing.");
            Assert.GreaterOrEqual(controller.LastResults.Stars, 1);
            Assert.IsNotNull(controller.LastRewardGrant,
                "AtomicSave must resolve the reward grant before committing.");
            CollectionAssert.AreEqual(new[] { "symbol.test" }, controller.LastRewardGrant.UnlockedSymbolIds);
            CollectionAssert.AreEqual(new[] { "memory.test" }, controller.LastRewardGrant.UnlockedMemoryIds);

            GameObject summary = GameObject.Find("[Runtime] ResultsSummary");
            Assert.IsNotNull(summary, "Results must present the learning outcome summary.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(
                summary.GetComponent<TMPro.TextMeshProUGUI>().text));
        }

        private void ConfigureContextChallenge(LevelConfigSO config)
        {
            ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            _objectsToDestroy.Add(sequence);
            sequence.sequenceId = "phase6-test";
            sequence.units = new[]
            {
                new ChallengeUnitDefinition
                {
                    unitId = "place-1",
                    mode = ChallengeMode.WordPlacement,
                    tokens = new[]
                    {
                        new ChallengeTokenDefinition { tokenId = "t1", displayText = "t1", occurrenceId = "w-1" },
                        new ChallengeTokenDefinition { tokenId = "t2", displayText = "t2", occurrenceId = "w-2" },
                    },
                    slots = new[]
                    {
                        new ChallengeSlotDefinition { slotId = "s1", expectedOccurrenceId = "w-1" },
                    },
                    candidateOccurrenceIds = new[] { "w-1", "w-2" },
                    maxErrors = 3,
                    heartPenalty = 1,
                },
            };
            config.challengeSequence = sequence;
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

        private void ConfigureOutroDialogue(LevelConfigSO config)
        {
            DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            dialogue.lines = new[] { new DialogueLine { speakerName = "Test", text = "Line" } };
            _objectsToDestroy.Add(dialogue);
            config.outroDialogue = dialogue;
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
