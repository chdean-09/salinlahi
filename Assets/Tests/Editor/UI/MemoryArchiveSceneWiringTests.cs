using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-240 — the ONE genuine scene dependency this ticket has.
    ///
    /// The Memory Archive button is not authored into MainMenu.unity; it is cloned from
    /// SettingsButton at runtime by MainMenuUI.EnsureMemoryArchiveEntryPoint, which finds the
    /// template with transform.Find(MainMenuUI.ArchiveButtonTemplateName). That lookup FAILS
    /// SILENTLY. If SettingsButton is renamed, reparented away from the MainMenuUI transform,
    /// or loses its Button component, the clone is never created, the Memory Archive becomes
    /// completely unreachable from the menu — and without this fixture, not one test in the
    /// project notices. That is the same class of defect as Gameplay.unity's
    /// _starCountText: {fileID: 0}, which rendered no stars while 961 EditMode and 172
    /// PlayMode tests passed over it.
    ///
    /// THE GUARD IS ON HIERARCHY SHAPE, NOT ON A SERIALIZED FIELD, AND THAT IS DELIBERATE.
    /// The sibling fixtures (CampaignSaveNoticeSceneWiringTests, VictoryScreenSceneWiringTests)
    /// read scene-authored [SerializeField] references through SerializedObject because that
    /// is what those tickets' defects lived in. This ticket authors no serialized reference at
    /// all, so a SerializedObject read here would assert nothing and report a false green.
    /// What is actually load-bearing here is the object name and the Button component, so
    /// those are what is asserted — the same reasoning CampaignSaveNoticeSceneWiringTests
    /// gives for locating the Settings header by hierarchy shape rather than by fileID.
    ///
    /// This fixture OPENS the scene and never saves it.
    /// </summary>
    [TestFixture]
    public sealed class MemoryArchiveSceneWiringTests
    {
        private const string MainMenuScenePath = "Assets/_Scenes/MainMenu.unity";

        [Test]
        public void MainMenuScene_ContainsAMainMenuUI()
        {
            Assert.IsNotNull(
                OpenSceneAndFindMainMenu(),
                MainMenuScenePath + " should contain a MainMenuUI. Without one, Start() never "
                + "runs and the Memory Archive button is never created.");
        }

        [Test]
        public void SettingsButtonTemplate_StillExistsUnderTheMainMenuUIAndCarriesAButton()
        {
            MainMenuUI mainMenu = OpenSceneAndFindMainMenu();
            Assert.IsNotNull(mainMenu, MainMenuScenePath + " should contain a MainMenuUI.");

            Transform template = mainMenu.transform.Find(MainMenuUI.ArchiveButtonTemplateName);
            Assert.IsNotNull(
                template,
                "'" + MainMenuUI.ArchiveButtonTemplateName + "' is no longer a direct child of "
                + "the MainMenuUI transform in " + MainMenuScenePath + ". "
                + "EnsureMemoryArchiveEntryPoint clones it to create the Memory Archive button "
                + "and uses transform.Find, which does not search grandchildren. The clone is "
                + "skipped, the button never appears, and the archive is unreachable. If the "
                + "button legitimately moved or was renamed, retarget "
                + "MainMenuUI.ArchiveButtonTemplateName rather than deleting this guard.");

            Assert.IsNotNull(
                template.GetComponent<Button>(),
                "'" + MainMenuUI.ArchiveButtonTemplateName + "' no longer carries a Button, so "
                + "the template lookup returns null and the Memory Archive button is never "
                + "built.");
        }

        private static MainMenuUI OpenSceneAndFindMainMenu()
        {
            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            // FindObjectsInactive.Include is load-bearing: GameObject.Find cannot see inactive
            // objects, and menu roots are routinely inactive as serialized.
            return Object.FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);
        }
    }
}
