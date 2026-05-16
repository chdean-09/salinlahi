using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Salinlahi.Tests.Editor.Gameplay
{
    [TestFixture]
    public class LevelFlowControllerTests
    {
        private readonly List<Object> _objectsToDestroy = new();

        [TearDown]
        public void TearDown()
        {
            ClearSingletonInstance<GameManager>();

            for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
            {
                if (_objectsToDestroy[i] != null)
                    Object.DestroyImmediate(_objectsToDestroy[i]);
            }

            _objectsToDestroy.Clear();
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
