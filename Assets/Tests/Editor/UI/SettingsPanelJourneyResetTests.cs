using System.Reflection;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class SettingsPanelJourneyResetTests
    {
        private const string RuntimeButtonName = "JourneyResetButton_Runtime";

        private GameObject _panelObject;
        private SettingsPanel _panel;
        private GameObject _saveManagerObject;
        private GameObject _progressManagerObject;
        private GameObject _sceneLoaderObject;
        private CampaignTestFixture _fixture;

        [SetUp]
        public void SetUp()
        {
            _panelObject = new GameObject("SettingsPanel_Test");
            _panel = _panelObject.AddComponent<SettingsPanel>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_panelObject != null) Object.DestroyImmediate(_panelObject);
            if (_saveManagerObject != null) Object.DestroyImmediate(_saveManagerObject);
            if (_progressManagerObject != null) Object.DestroyImmediate(_progressManagerObject);
            if (_sceneLoaderObject != null) Object.DestroyImmediate(_sceneLoaderObject);
            ClearSingletonInstance<SaveManager>();
            ClearSingletonInstance<ProgressManager>();
            ClearSingletonInstance<SceneLoader>();
            _fixture?.Dispose();
            _fixture = null;
            EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
            if (eventSystem != null) Object.DestroyImmediate(eventSystem.gameObject);
        }

        [Test]
        public void OnEnable_WithoutEnableJourneyReset_DoesNotBuildResetButton()
        {
            InvokeLifecycle(_panel, "OnEnable");

            Assert.That(_panelObject.transform.Find(RuntimeButtonName), Is.Null,
                "The pause-menu instance never calls EnableJourneyReset, so no button may appear.");
        }

        [Test]
        public void OnEnable_WithJourneyResetEnabledButNoSaveManager_DoesNotBuildResetButton()
        {
            // Explicit == engages Unity's fake-null semantics; fails loudly on a polluted run.
            Assert.That(SaveManager.Instance == null, Is.True, "precondition");
            _panel.EnableJourneyReset();
            InvokeLifecycle(_panel, "OnEnable");

            Assert.That(_panelObject.transform.Find(RuntimeButtonName), Is.Null);
        }

        [Test]
        public void OnEnable_WithJourneyResetEnabledButNoProgressManager_DoesNotBuildResetButton()
        {
            CreateReadySaveManager();
            CreateSceneLoader();

            _panel.EnableJourneyReset();
            InvokeLifecycle(_panel, "OnEnable");

            Assert.That(_panelObject.transform.Find(RuntimeButtonName), Is.Null);
        }

        [Test]
        public void OnEnable_WithJourneyResetEnabledButNoSceneLoader_DoesNotBuildResetButton()
        {
            CreateReadySaveManager();
            CreateProgressManager();

            _panel.EnableJourneyReset();
            InvokeLifecycle(_panel, "OnEnable");

            Assert.That(_panelObject.transform.Find(RuntimeButtonName), Is.Null);
        }

        [Test]
        public void OnEnable_WithJourneyResetEnabledAndRevisedReady_BuildsActiveResetButton()
        {
            SaveManager saveManager = CreateReadySaveManager();
            CreateProgressManager();
            CreateSceneLoader();
            Assert.That(SaveManager.Instance, Is.SameAs(saveManager), "precondition");
            Assert.That(saveManager.Mode, Is.EqualTo(SaveManagerMode.RevisedReady), "precondition");

            _panel.EnableJourneyReset();
            InvokeLifecycle(_panel, "OnEnable");

            Transform button = _panelObject.transform.Find(RuntimeButtonName);
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeSelf, Is.True);
        }

        private SaveManager CreateReadySaveManager()
        {
            _fixture = CampaignTestFixture.CreateValid();
            _saveManagerObject = new GameObject("SaveManager_Test");
            SaveManager saveManager = _saveManagerObject.AddComponent<SaveManager>();
            InvokeLifecycle(saveManager, "Awake");
            saveManager.SetCampaignForTests(_fixture.Campaign);
            saveManager.SetServiceForTests(new CampaignSaveService(
                new InMemoryCampaignSaveStorage(),
                new Salinlahi.Tests.Editor.Persistence.DictionaryLegacySource()));
            return saveManager;
        }

        private void CreateProgressManager()
        {
            _progressManagerObject = new GameObject("ProgressManager_Test");
            ProgressManager progressManager = _progressManagerObject.AddComponent<ProgressManager>();
            InvokeLifecycle(progressManager, "Awake");
        }

        private void CreateSceneLoader()
        {
            _sceneLoaderObject = new GameObject("SceneLoader_Test");
            SceneLoader sceneLoader = _sceneLoaderObject.AddComponent<SceneLoader>();
            InvokeLifecycle(sceneLoader, "Awake");
        }

        private static void InvokeLifecycle(MonoBehaviour target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, null);
        }

        private static void ClearSingletonInstance<T>() where T : MonoBehaviour
        {
            typeof(Singleton<T>).GetProperty("Instance")
                .GetSetMethod(true)
                .Invoke(null, new object[] { null });
        }
    }
}
