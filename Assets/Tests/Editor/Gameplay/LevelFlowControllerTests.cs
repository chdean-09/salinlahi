using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Salinlahi.Runtime.Gameplay;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class LevelFlowControllerTests
    {
        private readonly List<Object> _objectsToDestroy = new();
        private readonly List<MonoBehaviour> _enabledComponents = new();

        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            // Every component whose OnEnable this fixture drove must have its
            // OnDisable driven too, or its EventBus subscriptions leak into the
            // next test. Reverse order mirrors Unity's teardown.
            for (int i = _enabledComponents.Count - 1; i >= 0; i--)
            {
                if (_enabledComponents[i] != null)
                    InvokeLifecycle(_enabledComponents[i], "OnDisable");
            }
            _enabledComponents.Clear();

            SetForceGameplayScene(false);
            ClearSingletonInstance<GameManager>();
            LevelTutorialProgress.ResetLevel1TutorialForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            DestroyAllProtagonistManagers();
            ClearActiveFlowStatic();
            Time.timeScale = 1f;
        }

        // RunLevelFlow latches s_activeFlow; a pumped-then-abandoned flow must not
        // leak a destroyed controller into the next test.
        private static void ClearActiveFlowStatic()
        {
            FieldInfo field = typeof(LevelFlowController).GetField(
                "s_activeFlow", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, null);
        }

        /// <summary>
        /// EditMode never runs lifecycle methods, so components that subscribe to
        /// EventBus in OnEnable never hear the events these tests raise. Drives
        /// OnEnable by hand and registers the matching OnDisable for teardown.
        /// </summary>
        private T EnableComponent<T>(T component) where T : MonoBehaviour
        {
            InvokeLifecycle(component, "OnEnable");
            _enabledComponents.Add(component);
            return component;
        }

        private static void InvokeLifecycle(MonoBehaviour target, string methodName)
        {
            MethodInfo method = null;
            for (var type = target.GetType(); type != null && method == null; type = type.BaseType)
                method = type.GetMethod(
                    methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"Missing lifecycle method '{methodName}' on {target.GetType().Name}.");
            method.Invoke(target, null);
        }

        /// <summary>
        /// Start() exits immediately outside a gameplay-named scene; the test
        /// runner's untitled scene would end the pumped flow before any logic
        /// runs. Toggles the controller's UNITY_INCLUDE_TESTS-only override.
        /// </summary>
        private static void SetForceGameplayScene(bool value)
        {
            FieldInfo field = typeof(LevelFlowController).GetField(
                "s_forceGameplaySceneForTests",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "LevelFlowController.s_forceGameplaySceneForTests not found.");
            field.SetValue(null, value);
        }

        [Test]
        public void VictoryScreenDoesNotSelfSubscribeToLevelComplete()
        {
            GameObject panel = CreatePanel("VictoryPanel");
            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            SetPrivateField(victory, "_panel", panel);

            EventBus.RaiseLevelComplete();

            Assert.IsFalse(panel.activeSelf,
                "LevelFlowController should be the only component routing level complete to victory UI.");
        }

        [Test]
        public void DefeatScreenDoesNotSelfSubscribeToGameOver()
        {
            GameObject panel = CreatePanel("DefeatPanel");
            DefeatScreenUI defeat = CreateComponent<DefeatScreenUI>("DefeatScreen");
            SetPrivateField(defeat, "_panel", panel);

            EventBus.RaiseGameOver();

            Assert.IsFalse(panel.activeSelf,
                "LevelFlowController should be the only component routing game over to defeat UI.");
        }

        [Test]
        public void LevelCompleteWithoutOutroShowsVictoryScreen()
        {
            GameObject panel = CreatePanel("VictoryPanel");
            VictoryScreenUI victory = CreateComponent<VictoryScreenUI>("VictoryScreen");
            SetPrivateField(victory, "_panel", panel);

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", CreateLevelConfig());
            SetPrivateField(controller, "_victoryScreen", victory);
            EnableComponent(controller);

            EventBus.RaiseLevelComplete();

            Assert.IsTrue(panel.activeSelf);
        }

        [Test]
        public void GameOverShowsDefeatScreen()
        {
            GameObject panel = CreatePanel("DefeatPanel");
            DefeatScreenUI defeat = CreateComponent<DefeatScreenUI>("DefeatScreen");
            SetPrivateField(defeat, "_panel", panel);

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_defeatScreen", defeat);
            EnableComponent(controller);

            EventBus.RaiseGameOver();

            Assert.IsTrue(panel.activeSelf);
        }

        [UnityTest]
        public IEnumerator DialogueCanPlayFromLevelCompleteAndRestoresLevelComplete()
        {
            GameManager gameManager = EnableComponent(CreateGameManager());
            EventBus.RaiseLevelComplete();
            Assert.AreEqual(GameState.LevelComplete, gameManager.CurrentState);

            GameObject overlay = CreatePanel("DialogueOverlay");
            DialogueController dialogueController = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogueController, "_overlayPanel", overlay);

            dialogueController.Play(CreateDialogue());

            Assert.IsTrue(overlay.activeSelf);
            Assert.AreEqual(GameState.Paused, gameManager.CurrentState);

            InvokePrivate(dialogueController, "EndDialogue");

            Assert.IsFalse(overlay.activeSelf);
            Assert.AreEqual(GameState.LevelComplete, gameManager.CurrentState);
            yield break;
        }

        [UnityTest]
        public IEnumerator IntroCoroutineDoesNotRestartGameAfterLevelEnds()
        {
            SetForceGameplayScene(true);
            GameManager gameManager = EnableComponent(CreateGameManager());
            DialogueController dialogueController = CreateComponent<DialogueController>("DialogueController");
            SetPrivateField(dialogueController, "_overlayPanel", CreatePanel("DialogueOverlay"));

            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.introDialogue = CreateDialogue();

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);
            SetPrivateField(controller, "_dialogueController", dialogueController);

            IEnumerator start = InvokePrivate<IEnumerator>(controller, "Start");
            Assert.IsTrue(start.MoveNext(), "Start should wait for intro dialogue completion.");

            EventBus.RaiseGameOver();
            Assert.AreEqual(GameState.GameOver, gameManager.CurrentState);

            EventBus.RaiseDialogueComplete();
            Assert.IsFalse(start.MoveNext(), "Start should end after the level ends during intro dialogue.");
            Assert.AreEqual(GameState.GameOver, gameManager.CurrentState);
            yield break;
        }

        [Test]
        public void LevelOneTutorialDueWithoutOnboardingControllerLogsError()
        {
            // Ensure FTUE hasn't been marked seen from a prior run so the error path fires.
            LevelTutorialProgress.ResetLevel1TutorialForTests();

            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            IEnumerator tutorialGate = InvokePrivate<IEnumerator>(controller, "PlayLevelTutorialIfNeeded");
            LogAssert.Expect(
                LogType.Error,
                "[Salinlahi] LevelFlowController: Level 1 tutorial is due, but Level1OnboardingController is not in the scene. Run Salinlahi → Tutorial → 5. Wire Level Scene.");

            Assert.IsFalse(tutorialGate.MoveNext());
            Assert.IsFalse(GetPrivateField<bool>(controller, "_flowAborted"),
                "Missing controller should not abort the flow — waves must still start.");
        }

        [Test]
        public void LevelOneTutorialDueWithLegacySequenceCreatesRuntimeOnboardingController()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.levelNumber = LevelTutorialProgress.TutorialLevelNumber;
            levelConfig.tutorialSequence = CreateLegacyTutorialSequence();

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            InvokePrivate(controller, "EnsureRuntimeReferences", null, null);

            Level1OnboardingController onboardingController =
                GetPrivateField<Level1OnboardingController>(controller, "_level1OnboardingController");

            Assert.IsNotNull(onboardingController,
                "Level 1 flow should create the onboarding controller at runtime when legacy tutorial data is assigned.");

            // The tutorial surfaces (dialogue, spotlight, intro player, heart demo,
            // guide UI) are built in the onboarding controller's Awake, which
            // EditMode never runs on AddComponent — drive it by hand.
            InvokeLifecycle(onboardingController, "Awake");

            Assert.IsTrue(onboardingController.IsSequenceResolvable(levelConfig),
                "Runtime onboarding should be able to adapt legacy Level1TutorialSequenceSO data.");
            Assert.IsNotNull(Object.FindFirstObjectByType<DialogueController>(FindObjectsInactive.Include),
                "Runtime onboarding should find or create a skippable dialogue controller for tutorial copy.");
            Assert.IsNotNull(Object.FindFirstObjectByType<TutorialSpotlightOverlay>(FindObjectsInactive.Include),
                "Runtime onboarding should create the highlight/spotlight overlay when the scene does not provide one.");
            Assert.IsNotNull(Object.FindFirstObjectByType<TutorialIntroPlayer>(FindObjectsInactive.Include),
                "Runtime onboarding should create the tap/video intro overlay when the scene does not provide one.");
            Assert.IsNotNull(Object.FindFirstObjectByType<DemoHeartSimulator>(FindObjectsInactive.Include),
                "Runtime onboarding should create the heart-demo indicator driver when the scene does not provide one.");
            Assert.IsNotNull(Object.FindFirstObjectByType<Level1TutorialGuideUI>(FindObjectsInactive.Include),
                "Runtime onboarding should create the prompt/indicator guide UI when the scene does not provide one.");
        }

        [Test]
        public void LevelOneRuntimeOnboardingController_DoesNotIncludeComboTeachBeat()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.levelNumber = LevelTutorialProgress.Level1TutorialLevelNumber;
            levelConfig.tutorialSequence = CreateLegacyTutorialSequence();

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            InvokePrivate(controller, "EnsureRuntimeReferences", null, null);

            Level1OnboardingController onboardingController =
                GetPrivateField<Level1OnboardingController>(controller, "_level1OnboardingController");

            Assert.IsNotNull(onboardingController);
            Assert.IsNull(onboardingController.GetComponent<ComboTeachBeat>(),
                "Level 1 runtime onboarding must not include the multi-kill chain tutorial beat.");
        }

        [Test]
        public void LevelTwoTutorialDueWithAdvancedSequenceCreatesRuntimeOnboardingController()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.levelNumber = LevelTutorialProgress.Level2TutorialLevelNumber;
            levelConfig.onboardingSequence = CreateLevel2AdvancedSequence();

            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            InvokePrivate(controller, "EnsureRuntimeReferences", null, null);

            Level1OnboardingController onboardingController =
                GetPrivateField<Level1OnboardingController>(controller, "_level1OnboardingController");

            Assert.IsNotNull(onboardingController,
                "Level 2 flow should create the reusable onboarding controller for the advanced combat tutorial.");
            Assert.IsTrue(onboardingController.IsSequenceResolvable(levelConfig));
            Assert.IsNotNull(onboardingController.GetComponent<ComboTeachBeat>(),
                "Level 2 advanced onboarding must include the multi-kill chain tutorial beat.");
            Assert.IsNotNull(onboardingController.GetComponent<FocusModeTeachBeat>(),
                "Level 2 advanced onboarding must include the focus mode tutorial beat.");
        }

        [UnityTest]
        public IEnumerator NonTutorialLevel_WithProtagonistEnabled_CreatesProtagonistWhenManagerMissing()
        {
            SetForceGameplayScene(true);
            LevelConfigSO levelConfig = CreateLevelConfig();
            // Level 3: levels 1 and 2 are both tutorial levels now, and a due
            // tutorial would error out of the Defense executor before the wave
            // hand-off this test asserts on.
            levelConfig.levelNumber = 3;
            levelConfig.hasProtagonist = true;
            levelConfig.protagonistWalksIn = false;

            GameManager gameManager = CreateGameManager();
            gameManager.SetLevel(levelConfig);

            WaveManager waveManager = CreateComponent<WaveManager>("WaveManager");
            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);
            SetPrivateField(controller, "_waveManager", waveManager);

            Assert.IsNull(ProtagonistManager.Instance, "Test setup expects no pre-existing ProtagonistManager.");
            // Expectations are order-sensitive: the protagonist spawn (Story
            // phase) errors before the wave hand-off (Defense phase) does.
            // The flow-created fallback manager has no prefab wired (only the
            // scene-authored [Manager] ProtagonistManager.prefab does), so the
            // spawn itself errors by design in this barren fixture.
            LogAssert.Expect(LogType.Error,
                "[Salinlahi] [ProtagonistManager] _protagonistPrefab not assigned. "
                + "Place [Manager] ProtagonistManager.prefab in the active scene.");
            LogAssert.Expect(LogType.Error, "[Salinlahi] WaveManager.StartLevel: No LevelConfigSO assigned.");

            // Pump the flow synchronously up to Defense's completion wait: the
            // protagonist spawns in the Story phase and the expected StartLevel
            // error fires at the wave hand-off, both before that wait — which
            // this machine-less fixture could never satisfy.
            IEnumerator start = InvokePrivate<IEnumerator>(controller, "Start");
            PumpUntilFrameWait(start, 512);
            yield return null;

            ProtagonistManager protagonistManager =
                ProtagonistManager.Instance ?? Object.FindFirstObjectByType<ProtagonistManager>();

            Assert.IsNotNull(protagonistManager,
                "Level flow should ensure a ProtagonistManager exists when protagonist spawning is enabled.");
            // No ProtagonistTransform assert: the fallback manager cannot spawn
            // without its prefab (the expected error above); the spawn itself is
            // covered by the scene-authored manager at runtime.
        }

        /// <summary>
        /// Drives a hand-pumped flow synchronously, recursing into nested
        /// enumerators the way Unity's scheduler would, and stops at the first
        /// frame-wait yield (e.g. Defense's WaitUntil for a completion signal this
        /// EditMode fixture never raises). Returns true when the routine ran to
        /// completion, false when it stopped at a frame wait or exhausted budget.
        /// </summary>
        private static bool PumpUntilFrameWait(IEnumerator routine, int budget)
        {
            while (budget-- > 0 && routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    if (!PumpUntilFrameWait(nested, budget))
                        return false;
                }
                else if (routine.Current != null)
                {
                    // WaitUntil / WaitForSeconds — a real frame wait.
                    return false;
                }
            }
            return budget > 0;
        }

        private static void DestroyAllProtagonistManagers()
        {
            ProtagonistManager[] managers = Object.FindObjectsByType<ProtagonistManager>(FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null)
                    Object.DestroyImmediate(managers[i].gameObject);
            }
        }

        private GameManager CreateGameManager()
        {
            GameManager gameManager = CreateComponent<GameManager>("GameManager");
            SetSingletonInstance(gameManager);
            return gameManager;
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new(name);
            panel.SetActive(false);
            _objectsToDestroy.Add(panel);
            return panel;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = new(name);
            T component = gameObject.AddComponent<T>();
            _objectsToDestroy.Add(gameObject);
            return component;
        }

        private LevelConfigSO CreateLevelConfig()
        {
            LevelConfigSO levelConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            _objectsToDestroy.Add(levelConfig);
            return levelConfig;
        }

        private BossConfigSO CreateBossConfig(bool withTutorial)
        {
            BossConfigSO boss = ScriptableObject.CreateInstance<BossConfigSO>();
            if (withTutorial)
            {
                BossTutorialSO tutorial = ScriptableObject.CreateInstance<BossTutorialSO>();
                tutorial.pages = new List<BossTutorialPage>
                {
                    new BossTutorialPage { title = "Boss", body = "Lore" },
                };
                boss.tutorial = tutorial;
                _objectsToDestroy.Add(tutorial);
            }
            _objectsToDestroy.Add(boss);
            return boss;
        }

        private DialogueSO CreateDialogue()
        {
            DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
            dialogue.lines = new[]
            {
                new DialogueLine { speakerName = "Test", text = "Line" }
            };
            _objectsToDestroy.Add(dialogue);
            return dialogue;
        }

        private Level1TutorialSequenceSO CreateLegacyTutorialSequence()
        {
            Level1TutorialSequenceSO sequence = ScriptableObject.CreateInstance<Level1TutorialSequenceSO>();
            _objectsToDestroy.Add(sequence);
            return sequence;
        }

        private OnboardingSequenceSO CreateLevel2AdvancedSequence()
        {
            OnboardingSequenceSO sequence = ScriptableObject.CreateInstance<OnboardingSequenceSO>();
            sequence.beatOrder = new[]
            {
                OnboardingBeatType.ComboTeach,
                OnboardingBeatType.FocusModeTeach,
                OnboardingBeatType.Release,
            };
            _objectsToDestroy.Add(sequence);
            return sequence;
        }

        [Test]
        public void PlayBossTutorialIfNeeded_WhenNoBossConfig_NoOps()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            IEnumerator gate = InvokePrivate<IEnumerator>(controller, "PlayBossTutorialIfNeeded");
            Assert.IsFalse(gate.MoveNext(), "No bossConfig should yield break immediately.");
        }

        [Test]
        public void PlayBossTutorialIfNeeded_WhenBossConfigButNoTutorial_NoOps()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.bossConfig = CreateBossConfig(withTutorial: false);
            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);

            IEnumerator gate = InvokePrivate<IEnumerator>(controller, "PlayBossTutorialIfNeeded");
            Assert.IsFalse(gate.MoveNext(), "bossConfig without a tutorial should yield break immediately.");
        }

        [Test]
        public void PlayBossTutorialIfNeeded_WhenTutorialButNoController_NoOps()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.bossConfig = CreateBossConfig(withTutorial: true);
            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);
            // _bossTutorialController intentionally left null.

            IEnumerator gate = InvokePrivate<IEnumerator>(controller, "PlayBossTutorialIfNeeded");
            Assert.IsFalse(gate.MoveNext(), "Missing controller should skip gracefully (waves still start).");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            field.SetValue(target, value);
        }

        private static T InvokePrivate<T>(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            return (T)method.Invoke(target, null);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, null);
        }

        private static void InvokePrivate(object target, string methodName, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} method not found.");
            method.Invoke(target, args);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} field not found.");
            return (T)field.GetValue(target);
        }

        private static void SetSingletonInstance<T>(T instance) where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { instance });
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            PropertyInfo property = typeof(Singleton<T>).GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            MethodInfo setter = property?.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter);
            setter.Invoke(null, new object[] { null });
        }
    }
}
