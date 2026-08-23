using System.Reflection;
using NUnit.Framework;
using Salinlahi.Tests.Editor.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.Persistence
{
    public sealed class ResetJourneyIntegrationTests
    {
        private const string MasterVolumeKey = "salinlahi.audio.master_volume";

        private CampaignTestFixture _fixture;
        private InMemoryCampaignSaveStorage _storage;
        private CampaignSaveService _service;
        private GameObject _saveManagerObject;
        private GameObject _progressManagerObject;
        private SaveManager _saveManager;
        private bool _hadMasterVolume;
        private float _previousMasterVolume;

        [SetUp]
        public void SetUp()
        {
            _hadMasterVolume = PlayerPrefs.HasKey(MasterVolumeKey);
            _previousMasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 0f);

            _fixture = CampaignTestFixture.CreateValid();
            _storage = new InMemoryCampaignSaveStorage();
            _service = new CampaignSaveService(_storage, new DictionaryLegacySource());
            _saveManagerObject = new GameObject("SaveManager_Test");
            _saveManager = _saveManagerObject.AddComponent<SaveManager>();
            InvokeLifecycle(_saveManager, "Awake");
            _saveManager.SetCampaignForTests(_fixture.Campaign);
            _saveManager.SetServiceForTests(_service);
            _progressManagerObject = new GameObject("ProgressManager_Test");
            ProgressManager progressManager = _progressManagerObject.AddComponent<ProgressManager>();
            InvokeLifecycle(progressManager, "Awake");
        }

        [TearDown]
        public void TearDown()
        {
            if (_progressManagerObject != null) Object.DestroyImmediate(_progressManagerObject);
            if (_saveManagerObject != null) Object.DestroyImmediate(_saveManagerObject);
            ClearSingletonInstance<ProgressManager>();
            ClearSingletonInstance<SaveManager>();
            _fixture?.Dispose();
            if (_hadMasterVolume)
                PlayerPrefs.SetFloat(MasterVolumeKey, _previousMasterVolume);
            else
                PlayerPrefs.DeleteKey(MasterVolumeKey);
            PlayerPrefs.Save();
        }

        [Test]
        public void Execute_ResetsProgressAtomicallyAndPreservesReceiptsAndSettings()
        {
            Assert.That(SaveManager.Instance, Is.SameAs(_saveManager), "precondition");
            Assert.That(_saveManager.Mode, Is.EqualTo(SaveManagerMode.RevisedReady), "precondition");
            PlayerPrefs.SetFloat(MasterVolumeKey, 0.42f);
            // Seed reset-detectable progress with a mutation that passes validation.
            Assert.That(_service.TryUpdate(document =>
            {
                document.progress.levelProgress[0].completed = true;
                document.progress.levelProgress[0].bestStars = 3;
            }), Is.True, "precondition");
            string generationBefore = _service.Current.progress.journeyGenerationId;
            CampaignMigrationState migrationStateBefore = _service.Current.migration.state;

            ResetJourneyOutcome outcome = ResetJourneyFlow.Execute();

            Assert.That(outcome, Is.EqualTo(ResetJourneyOutcome.Succeeded));
            // AC3: everything returns together to the documented new-journey state.
            Assert.That(_service.Current.progress.journeyGenerationId,
                Is.Not.EqualTo(generationBefore));
            Assert.That(_service.Current.progress.levelProgress[0].completed, Is.False);
            Assert.That(_service.Current.progress.levelProgress[0].bestStars, Is.EqualTo(0));
            Assert.That(_service.Current.progress.endlessModeUnlocked, Is.False);
            Assert.That(_service.Current.progress.unlockedSymbolIds, Is.Empty);
            Assert.That(_service.Current.progress.unlockedMemoryIds, Is.Empty);
            Assert.That(_service.Current.progress.appliedOutcomeReceipts, Is.Empty);
            for (int i = 0; i < _service.Current.progress.levelProgress.Count; i++)
                Assert.That(_service.Current.progress.levelProgress[i].unlocked, Is.EqualTo(i == 0));
            // AC3: schema and migration metadata remain valid.
            Assert.That(_service.Current.saveSchemaVersion,
                Is.EqualTo(CampaignSaveDocument.CurrentSaveSchemaVersion));
            Assert.That(_service.Current.migration.state, Is.EqualTo(migrationStateBefore));
            // AC3: approved settings remain.
            Assert.That(PlayerPrefs.GetFloat(MasterVolumeKey, -1f),
                Is.EqualTo(0.42f).Within(0.0001f));
        }

        [Test]
        public void PresentAndCancel_LeavesPersistedSaveUnchanged()
        {
            // AC2 end-to-end: the dialog's cancel path performs no persistence call.
            string primaryBefore = _storage.ReadAllText(CampaignSaveFileRole.Primary);
            long revisionBefore = _service.Current.revision;
            GameObject panelObject = new GameObject("ResetPanel_Test");
            try
            {
                ResetJourneyConfirmationPanel panel =
                    panelObject.AddComponent<ResetJourneyConfirmationPanel>();
                panel.Present(ResetJourneyFlow.Execute, () => { });
                Button cancel = panelObject.transform
                    .Find("Overlay/Card/CancelButton").GetComponent<Button>();

                cancel.onClick.Invoke();

                Assert.That(_service.Current.revision, Is.EqualTo(revisionBefore));
                Assert.That(_storage.ReadAllText(CampaignSaveFileRole.Primary),
                    Is.EqualTo(primaryBefore));
            }
            finally
            {
                Object.DestroyImmediate(panelObject);
            }
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
