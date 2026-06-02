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

        [SetUp]
        public void SetUp()
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();
            LevelTutorialProgress.ResetLevel1TutorialForTests();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
            DestroyAllProtagonistManagers();
            Time.timeScale = 1f;
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

            EventBus.RaiseGameOver();

            Assert.IsTrue(panel.activeSelf);
        }

        [UnityTest]
        public IEnumerator DialogueCanPlayFromLevelCompleteAndRestoresLevelComplete()
        {
            GameManager gameManager = CreateGameManager();
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
            GameManager gameManager = CreateGameManager();
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
                "[Salinlahi] LevelFlowController: Level 1 FTUE is due, but Level1OnboardingController is not in the scene. Run Salinlahi → Tutorial → 5. Wire Level 1 Scene.");

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

        [UnityTest]
        public IEnumerator NonTutorialLevel_WithProtagonistEnabled_CreatesProtagonistWhenManagerMissing()
        {
            LevelConfigSO levelConfig = CreateLevelConfig();
            levelConfig.levelNumber = 2;
            levelConfig.hasProtagonist = true;
            levelConfig.protagonistWalksIn = false;

            GameManager gameManager = CreateGameManager();
            gameManager.SetLevel(levelConfig);

            WaveManager waveManager = CreateComponent<WaveManager>("WaveManager");
            LevelFlowController controller = CreateComponent<LevelFlowController>("LevelFlowController");
            SetPrivateField(controller, "_levelConfig", levelConfig);
            SetPrivateField(controller, "_waveManager", waveManager);

            Assert.IsNull(ProtagonistManager.Instance, "Test setup expects no pre-existing ProtagonistManager.");
            LogAssert.Expect(LogType.Error, "[Salinlahi] WaveManager.StartLevel: No LevelConfigSO assigned.");

            IEnumerator start = InvokePrivate<IEnumerator>(controller, "Start");
            while (start.MoveNext())
                yield return start.Current;

            ProtagonistManager protagonistManager =
                ProtagonistManager.Instance ?? Object.FindFirstObjectByType<ProtagonistManager>();

            Assert.IsNotNull(protagonistManager,
                "Level flow should ensure a ProtagonistManager exists when protagonist spawning is enabled.");
            Assert.IsNotNull(protagonistManager.ProtagonistTransform,
                "Level flow should spawn protagonist for non-tutorial levels when hasProtagonist is true.");
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
