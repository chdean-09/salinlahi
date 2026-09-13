using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.Tests.Editor.UI
{
    /// <summary>
    /// SALIN-234 — the Level Results screen must actually be reachable and renderable in both
    /// scenes that host it.
    ///
    /// This is the SALIN-272 failure class (see CampaignSaveNoticeSceneWiringTests) applied to
    /// the victory screen. Before this ticket the two scenes were wired in mirror image and
    /// nothing in 961 EditMode or 172 PlayMode tests could tell either state from success:
    ///
    ///   Gameplay.unity            _victoryScreen wired (:4459), star fields EMPTY (:6347-6348)
    ///   Level_01_Tutorial.unity   star fields wired (:4787-4788), _victoryScreen NULL (:5392)
    ///
    /// Neither scene is edited by SALIN-234. The missing display is built at runtime
    /// (VictoryScreenUI.EnsureRuntimeControls) and the missing reference is resolved by the
    /// FindFirstObjectByType fallback at LevelFlowController.cs:807, so the conflict surface on
    /// Assets/_Scenes/*.unity — the project's two highest-collision serialized assets, with
    /// .gitattributes:11 declaring a merge driver that is not configured — stays at zero.
    /// These tests pin the facts both mechanisms depend on.
    ///
    /// The serialized fields are private, so they are read through <see cref="SerializedObject"/>
    /// rather than by widening the runtime API for a test.
    /// </summary>
    [TestFixture]
    public sealed class VictoryScreenSceneWiringTests
    {
        private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
        private const string TutorialScenePath = "Assets/_Scenes/Level_01_Tutorial.unity";

        [Test]
        public void Gameplay_VictoryScreen_IsPresentAndItsPanelIsAssigned()
        {
            VictoryScreenUI screen = OpenSceneAndFindScreen(GameplayScenePath);
            var panel = Reference(new SerializedObject(screen), "_panel", GameplayScenePath) as GameObject;

            Assert.IsNotNull(
                panel,
                "_panel is unassigned in " + GameplayScenePath + ". Show(), ShowResultsSummary() " +
                "and the runtime control construction all early-out on a null panel, so the " +
                "whole Results screen silently does nothing.");
        }

        [TestCase(GameplayScenePath)]
        [TestCase(TutorialScenePath)]
        public void NavigationButtons_AreAssignedAndAreNotTheSameObject(string scenePath)
        {
            SerializedObject screen = new SerializedObject(OpenSceneAndFindScreen(scenePath));

            Object next = Reference(screen, "_nextLevelButton", scenePath);
            Object levelSelect = Reference(screen, "_levelSelectButton", scenePath);

            Assert.IsNotNull(next, "_nextLevelButton is unassigned in " + scenePath + ".");
            Assert.IsNotNull(levelSelect, "_levelSelectButton is unassigned in " + scenePath + ".");
            Assert.AreNotSame(
                next,
                levelSelect,
                "_nextLevelButton and _levelSelectButton point at one object in " + scenePath +
                ". Show() hides the next-level button on the last level, which would take the " +
                "Level Select button with it. This is the SALIN-272 aliasing defect.");
        }

        [TestCase(GameplayScenePath)]
        [TestCase(TutorialScenePath)]
        public void VictoryPanel_IsUnderACanvasThatCanRaycast(string scenePath)
        {
            VictoryScreenUI screen = OpenSceneAndFindScreen(scenePath);
            var panel = Reference(new SerializedObject(screen), "_panel", scenePath) as GameObject;
            Assert.IsNotNull(panel, "_panel is unassigned in " + scenePath + ".");

            Assert.IsNotNull(
                panel.GetComponent<RectTransform>(),
                "The victory panel in " + scenePath + " has a plain Transform, so uGUI can lay " +
                "out neither it nor anything the runtime construction parents under it.");

            Canvas canvas = panel.GetComponentInParent<Canvas>(true);
            Assert.IsNotNull(
                canvas,
                "The victory panel in " + scenePath + " has no Canvas ancestor. Nothing outside " +
                "a Canvas renders through uGUI.");
            Assert.IsNotNull(
                canvas.GetComponent<GraphicRaycaster>(),
                "The Canvas above the victory panel in " + scenePath + " has no GraphicRaycaster, " +
                "so neither Replay Level nor Next Level can receive a click however it is wired.");
        }

        [Test]
        public void Gameplay_LevelFlowControllerVictoryScreenReference_Resolves()
        {
            OpenSceneAndFindScreen(GameplayScenePath);
            var flow = Object.FindFirstObjectByType<LevelFlowController>(FindObjectsInactive.Include);
            Assert.IsNotNull(flow, GameplayScenePath + " should contain a LevelFlowController.");

            Object wired = Reference(new SerializedObject(flow), "_victoryScreen", GameplayScenePath);
            Assert.IsNotNull(
                wired,
                "LevelFlowController._victoryScreen is unassigned in " + GameplayScenePath +
                ". This is the scene the game actually plays; losing the reference drops the " +
                "Results screen to the FindFirstObjectByType fallback at " +
                "LevelFlowController.cs:807.");
        }

        /// <summary>
        /// Level_01_Tutorial.unity leaves <c>LevelFlowController._victoryScreen</c> null
        /// (Level_01_Tutorial.unity:5392) and survives only on the FindFirstObjectByType
        /// fallback. SALIN-234 does not edit the scene to fix that — it pins the fallback's
        /// precondition instead: a VictoryScreenUI that the INACTIVE-inclusive search can find.
        /// If that component is ever removed or the panel is re-rooted out of the scene, the
        /// tutorial level reaches no Results screen at all, and this is the test that says so.
        /// </summary>
        [Test]
        public void Tutorial_VictoryScreen_IsResolvableThroughTheRuntimeFallback()
        {
            EditorSceneManager.OpenScene(TutorialScenePath, OpenSceneMode.Single);

            var flow = Object.FindFirstObjectByType<LevelFlowController>(FindObjectsInactive.Include);
            Assert.IsNotNull(flow, TutorialScenePath + " should contain a LevelFlowController.");

            Object wired = Reference(new SerializedObject(flow), "_victoryScreen", TutorialScenePath);
            if (wired != null)
                return;

            var found = Object.FindFirstObjectByType<VictoryScreenUI>(FindObjectsInactive.Include);
            Assert.IsNotNull(
                found,
                "LevelFlowController._victoryScreen is null in " + TutorialScenePath +
                " AND no VictoryScreenUI exists for EnsureRuntimeReferences to find " +
                "(LevelFlowController.cs:807). The level would finish with no Results screen. " +
                "FindObjectsInactive.Include is load-bearing here: the panel is inactive as " +
                "serialized.");
        }

        /// <summary>
        /// Pins WHY the runtime construction exists. Gameplay.unity authors neither
        /// <c>_starCountText</c> nor <c>_starIcons</c>, so if this ever starts failing the scene
        /// has been edited to author them — at which point
        /// <see cref="VictoryScreenUI.EnsureRuntimeControls"/> correctly steps aside and the
        /// note in that method (and in the SALIN-234 PR) is stale and should be revisited.
        /// </summary>
        [Test]
        public void Gameplay_StarDisplayIsUnauthored_WhichIsWhyItIsBuiltAtRuntime()
        {
            SerializedObject screen = new SerializedObject(OpenSceneAndFindScreen(GameplayScenePath));

            SerializedProperty starIcons = screen.FindProperty("_starIcons");
            Assert.IsNotNull(starIcons, "VictoryScreenUI no longer serializes '_starIcons'.");

            bool authored = Reference(screen, "_starCountText", GameplayScenePath) != null
                || starIcons.arraySize > 0;

            Assert.IsFalse(
                authored,
                "Gameplay.unity now authors the star display. That is not a regression — it is " +
                "a change of approach. Reconcile it with VictoryScreenUI.EnsureRuntimeControls " +
                "and with the SALIN-234 PR's stated reason for touching no scene, rather than " +
                "deleting this test.");
        }

        private static VictoryScreenUI OpenSceneAndFindScreen(string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // Include is load-bearing: the victory panel is inactive as serialized.
            var screen = Object.FindFirstObjectByType<VictoryScreenUI>(FindObjectsInactive.Include);
            Assert.IsNotNull(screen, scenePath + " should contain a VictoryScreenUI.");
            return screen;
        }

        private static Object Reference(SerializedObject target, string fieldName, string scenePath)
        {
            SerializedProperty property = target.FindProperty(fieldName);
            Assert.IsNotNull(
                property,
                target.targetObject.GetType().Name + " no longer serializes '" + fieldName +
                "'. If the field was renamed, add [FormerlySerializedAs] so " + scenePath +
                " keeps its wiring, then update this guard.");
            return property.objectReferenceValue;
        }
    }
}
