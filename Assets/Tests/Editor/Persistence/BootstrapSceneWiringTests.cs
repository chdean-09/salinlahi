using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Salinlahi.Tests.Editor.Persistence
{
    /// <summary>
    /// SALIN-219 (audit T07) — the revised campaign must be wired into <c>SaveManager</c> as
    /// serialized data so the shipped build boots in <see cref="SaveManagerMode.RevisedReady"/>
    /// instead of degrading to <see cref="SaveManagerMode.Legacy"/>.
    ///
    /// This guards a regression that is otherwise silent in every automated channel:
    /// <c>SaveManager.Initialize</c> falls back to Legacy when <c>_campaign</c> is null
    /// <em>without</em> raising an error, the campaign validator never sees a scene, and nothing
    /// else in the suite reads scene or prefab wiring. A revert to <c>{fileID: 0}</c> would
    /// compile, validate and test green while the save, mastery, reward and Reset Journey stacks
    /// stayed dead at runtime — indistinguishable from success without manual Play Mode inspection.
    /// </summary>
    [TestFixture]
    public sealed class BootstrapSceneWiringTests
    {
        private const string BootstrapScenePath = "Assets/_Scenes/Bootstrap.unity";

        private const string SaveManagerPrefabPath =
            "Assets/Prefabs/Managers/[Manager] SaveManager.prefab";

        private const string CampaignAssetPath =
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

        [Test]
        public void BootstrapScene_SaveManager_ReferencesTheRevisedCampaign()
        {
            CampaignConfigSO expected = LoadRevisedCampaign();
            SaveManager saveManager = OpenBootstrapAndFindSaveManager();

            Assert.IsNotNull(
                saveManager.Campaign,
                "SaveManager._campaign is unassigned in the bootstrap scene, so Initialize() " +
                "silently selects Legacy mode and the revised save path never runs.");
            Assert.AreSame(
                expected,
                saveManager.Campaign,
                "SaveManager should reference the campaign authored at " + CampaignAssetPath + ".");
        }

        [Test]
        public void BootstrapScene_WiredCampaign_CarriesTheRevisedIdentity()
        {
            SaveManager saveManager = OpenBootstrapAndFindSaveManager();

            Assert.IsNotNull(saveManager.Campaign, "SaveManager._campaign must be assigned.");
            Assert.AreEqual(
                ContentIdentity.RevisedCampaignId,
                saveManager.Campaign.manifest.campaignId,
                "The wired campaign must be the revised-v1 campaign, not another config.");
            Assert.IsTrue(
                saveManager.Campaign.manifest.IsRevisedV1,
                "A manifest that fails IsRevisedV1 raises MANIFEST_UNSUPPORTED in the validator, " +
                "which is an Error under every profile and re-blocks boot.");
        }

        [Test]
        public void SaveManagerPrefab_ReferencesTheRevisedCampaign()
        {
            CampaignConfigSO expected = LoadRevisedCampaign();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SaveManagerPrefabPath);
            Assert.IsNotNull(prefab, "Expected a SaveManager prefab at " + SaveManagerPrefabPath + ".");

            SaveManager saveManager = prefab.GetComponent<SaveManager>();
            Assert.IsNotNull(saveManager, "The SaveManager prefab should carry a SaveManager component.");
            Assert.AreSame(
                expected,
                saveManager.Campaign,
                "No scene instantiates this prefab today, but it must stay consistent with the " +
                "bootstrap wiring so a future user of it is not born in Legacy mode.");
        }

        private static SaveManager OpenBootstrapAndFindSaveManager()
        {
            EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

            SaveManager saveManager =
                Object.FindFirstObjectByType<SaveManager>(FindObjectsInactive.Include);
            Assert.IsNotNull(saveManager, BootstrapScenePath + " should contain a SaveManager.");
            return saveManager;
        }

        private static CampaignConfigSO LoadRevisedCampaign()
        {
            var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignAssetPath);
            Assert.IsNotNull(campaign, "Expected the revised campaign at " + CampaignAssetPath + ".");
            return campaign;
        }
    }
}
