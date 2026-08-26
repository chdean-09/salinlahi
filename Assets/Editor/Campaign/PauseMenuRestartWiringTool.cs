using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Salinlahi.EditorTools
{
    /// <summary>
    /// SALIN-141 owed Editor wiring: the pause menu has no Restart button, so AC3
    /// ("Restart confirmed → the level reloads") is code-complete and PlayMode-tested
    /// but unreachable in game.
    ///
    /// This authors nothing novel — it clones the existing ResumeButton inside PausePanel,
    /// renames it, relabels it "Restart", inserts it between Resume and Quit, and assigns
    /// <c>PauseMenuUI._restartButton</c>. Cloning keeps the new button visually identical
    /// to its siblings (same RectTransform, Image, Button colours and TMP label style)
    /// rather than inventing a look.
    ///
    /// The confirmation overlay is deliberately NOT authored: SALIN-141 builds it at runtime
    /// when its serialized slots are empty, so it already works.
    ///
    /// Styling is placeholder-by-inheritance and open to design review — the label copy and
    /// the button's position in the row are the two things a designer may want changed.
    ///
    /// Idempotent: re-running reports "already wired" and writes nothing.
    /// Menu: Salinlahi → Campaign → Wire Pause Restart (SALIN-141)
    /// Headless: -executeMethod Salinlahi.EditorTools.PauseMenuRestartWiringTool.RunFromCommandLine
    /// </summary>
    public static class PauseMenuRestartWiringTool
    {
        private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
        private const string ResumeButtonName = "ResumeButton";
        private const string QuitButtonName = "QuitButton";
        private const string RestartButtonName = "RestartButton";
        private const string RestartLabel = "Restart";
        private const string RestartButtonField = "_restartButton";

        [MenuItem("Salinlahi/Campaign/Wire Pause Restart (SALIN-141)")]
        public static void Run()
        {
            Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

            Component pauseMenu = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Component>(includeInactive: true))
                .FirstOrDefault(c => c != null && c.GetType().Name == "PauseMenuUI");
            if (pauseMenu == null)
                throw new System.InvalidOperationException($"PauseMenuUI not found in {GameplayScenePath}.");

            var so = new SerializedObject(pauseMenu);
            SerializedProperty restart = so.FindProperty(RestartButtonField);
            if (restart == null)
                throw new System.InvalidOperationException(
                    $"{RestartButtonField} not found on PauseMenuUI — is SALIN-141 merged into this branch?");

            Button resume = FindButton(pauseMenu, ResumeButtonName);
            Button quit = FindButton(pauseMenu, QuitButtonName);

            if (restart.objectReferenceValue != null)
            {
                // Already wired — still normalise the column so a re-run repairs spacing.
                NormaliseButtonColumn(resume, (Button)restart.objectReferenceValue, quit);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                UnityEngine.Debug.Log("[Salinlahi] PauseMenuRestartWiringTool: already wired; column normalised.");
                return;
            }

            // Clone Resume so the new button inherits its exact size, colours and label style.
            GameObject clone = Object.Instantiate(resume.gameObject, resume.transform.parent);
            clone.name = RestartButtonName;

            // PausePanel is a plain full-screen stretch with no layout group, so sibling
            // order does NOT position anything — the clone would sit exactly on top of
            // Resume. Positions are normalised explicitly below.
            clone.transform.SetSiblingIndex(quit.transform.GetSiblingIndex());

            // Relabel, covering both TMP and legacy Text so this survives either authoring style.
            TMP_Text tmp = clone.GetComponentInChildren<TMP_Text>(includeInactive: true);
            if (tmp != null)
                tmp.text = RestartLabel;
            Text legacy = clone.GetComponentInChildren<Text>(includeInactive: true);
            if (legacy != null)
                legacy.text = RestartLabel;
            if (tmp == null && legacy == null)
                throw new System.InvalidOperationException("Cloned button has no text component to relabel.");

            // The clone inherits Resume's persistent onClick listeners; clear them so it cannot
            // resume the game. PauseMenuUI binds Restart itself at runtime.
            Button cloneButton = clone.GetComponent<Button>();
            if (cloneButton == null)
                throw new System.InvalidOperationException("Cloned object has no Button component.");
            for (int i = cloneButton.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(cloneButton.onClick, i);

            restart.objectReferenceValue = cloneButton;
            so.ApplyModifiedPropertiesWithoutUndo();

            NormaliseButtonColumn(resume, cloneButton, quit);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log(
                $"[Salinlahi] PauseMenuRestartWiringTool: created '{RestartButtonName}' from "
                + $"'{ResumeButtonName}' at sibling index {clone.transform.GetSiblingIndex()} and wired "
                + $"{RestartButtonField}. Persistent onClick listeners cleared.");
        }

        /// <summary>Headless entry point; exits non-zero on failure so a batch run fails loudly.</summary>
        public static void RunFromCommandLine()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[Salinlahi] PauseMenuRestartWiringTool failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Places Resume / Restart / Quit as an evenly spaced vertical column.
        /// The authored layout was Resume y=20 and Quit y=-150 — a 170-unit rhythm for
        /// 150-tall buttons, centred on -65. Adding a third button expands the column
        /// symmetrically about that same centre (105 / -65 / -235) so the group grows
        /// equally up and down instead of drifting into the PausedTitle above.
        /// Applied every run, so re-running also repairs a column edited by hand.
        /// </summary>
        private static void NormaliseButtonColumn(Button resume, Button restart, Button quit)
        {
            const float Spacing = 170f;
            const float Centre = -65f;

            SetAnchoredY(resume, Centre + Spacing);
            SetAnchoredY(restart, Centre);
            SetAnchoredY(quit, Centre - Spacing);
        }

        private static void SetAnchoredY(Button button, float y)
        {
            var rect = button.GetComponent<RectTransform>();
            if (rect == null)
                throw new System.InvalidOperationException($"'{button.name}' has no RectTransform.");

            var so = new SerializedObject(rect);
            SerializedProperty pos = so.FindProperty("m_AnchoredPosition");
            pos.vector2Value = new Vector2(pos.vector2Value.x, y);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button FindButton(Component pauseMenu, string name)
        {
            Button found = pauseMenu.GetComponentsInChildren<Button>(includeInactive: true)
                .FirstOrDefault(b => b.gameObject.name == name);
            if (found == null)
            {
                // PauseMenuUI may sit above the panel; widen the search to the whole canvas.
                Transform root = pauseMenu.transform.root;
                found = root.GetComponentsInChildren<Button>(includeInactive: true)
                    .FirstOrDefault(b => b.gameObject.name == name);
            }

            if (found == null)
                throw new System.InvalidOperationException($"'{name}' not found under the pause menu.");
            return found;
        }
    }
}
