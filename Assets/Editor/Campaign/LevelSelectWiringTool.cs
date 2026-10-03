using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Salinlahi.EditorTools
{
    /// <summary>
    /// SALIN-137 owed Editor wiring, applied deterministically rather than by hand.
    ///
    /// Two assignments the code-only pipeline could not make, both of which target
    /// assets that already exist:
    ///
    /// 1. <c>LevelSelectUI._eras</c> lists only Era_01, though Era_02 and Era_03 are
    ///    authored under Assets/ScriptableObjects/Themes/. SALIN-137 shipped a runtime
    ///    fallback to SaveManager.Campaign.eras precisely because this list was stale;
    ///    wiring it properly makes that fallback inert, which is its intended end state.
    ///
    /// 2. <c>LevelButton._completionBadge</c> is unassigned, so a completed level renders
    ///    identically to an unlocked one. The LevelButton components are authored in
    ///    LevelSelect.unity itself (the LevelButton.prefab asset carries the CompletionCheck
    ///    child but not the component), so each of the five is wired individually against
    ///    its own CompletionCheck child.
    ///
    /// Idempotent: re-running makes no further change and reports "already wired".
    /// Run from the menu, or headless via
    /// -executeMethod Salinlahi.EditorTools.LevelSelectWiringTool.RunFromCommandLine
    /// </summary>
    public static class LevelSelectWiringTool
    {
        private const string LevelSelectScenePath = "Assets/_Scenes/LevelSelect.unity";
        private const string CompletionBadgeChildName = "CompletionCheck";
        private const string CompletionBadgeField = "_completionBadge";
        private const string ErasField = "_eras";

        private static readonly string[] EraAssetPaths =
        {
            "Assets/ScriptableObjects/Themes/Era_01.asset",
            "Assets/ScriptableObjects/Themes/Era_02.asset",
            "Assets/ScriptableObjects/Themes/Era_03.asset",
        };

        [MenuItem("Salinlahi/Campaign/Wire Level Select (SALIN-137)")]
        public static void Run()
        {
            // Both edits live in LevelSelect.unity, so open it once and resolve the
            // hierarchy in memory. The LevelButton components are authored in the scene
            // (some inside prefab instances), so CompletionCheck cannot be located by
            // reading the scene YAML — Unity resolves prefab children only at load.
            Scene scene = EditorSceneManager.OpenScene(LevelSelectScenePath, OpenSceneMode.Single);

            int badges = WireCompletionBadges(scene);
            bool eras = WireEras(scene);

            if (badges > 0 || eras)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
            }

            UnityEngine.Debug.Log(
                $"[Salinlahi] LevelSelectWiringTool: completion badges wired on {badges} LevelButton(s); "
                + $"era list {(eras ? "WIRED to Era_01/02/03" : "already correct")}.");
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
                UnityEngine.Debug.LogError($"[Salinlahi] LevelSelectWiringTool failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Assigns each LevelButton's <c>_completionBadge</c> to its own CompletionCheck
        /// child. Returns how many were newly wired; already-wired buttons are skipped, so
        /// the tool is idempotent. Throws if a button has no CompletionCheck child, rather
        /// than silently leaving AC1 unsatisfied.
        /// </summary>
        private static int WireCompletionBadges(Scene scene)
        {
            Component[] buttons = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Component>(includeInactive: true))
                .Where(c => c != null && c.GetType().Name == "LevelButton")
                .ToArray();

            if (buttons.Length == 0)
                throw new System.InvalidOperationException("No LevelButton components found in LevelSelect.unity.");

            int wired = 0;
            foreach (Component button in buttons)
            {
                var so = new SerializedObject(button);
                SerializedProperty badge = so.FindProperty(CompletionBadgeField);
                if (badge == null)
                    throw new System.InvalidOperationException(
                        $"{CompletionBadgeField} not found on LevelButton — is SALIN-137 merged into this branch?");

                if (badge.objectReferenceValue != null)
                    continue;

                Transform child = button.transform
                    .GetComponentsInChildren<Transform>(includeInactive: true)
                    .FirstOrDefault(t => t.name == CompletionBadgeChildName);
                if (child == null)
                    throw new System.InvalidOperationException(
                        $"'{CompletionBadgeChildName}' child not found under LevelButton '{button.name}'.");

                badge.objectReferenceValue = child.gameObject;
                so.ApplyModifiedPropertiesWithoutUndo();
                wired++;
            }

            return wired;
        }

        private static bool WireEras(Scene scene)
        {
            Component ui = scene.GetRootGameObjects()
                .SelectMany(go => go.GetComponentsInChildren<Component>(includeInactive: true))
                .FirstOrDefault(c => c != null && c.GetType().Name == "LevelSelectUI");
            if (ui == null)
                throw new System.InvalidOperationException("LevelSelectUI not found in LevelSelect.unity.");

            var so = new SerializedObject(ui);
            SerializedProperty eras = so.FindProperty(ErasField);
            if (eras == null || !eras.isArray)
                throw new System.InvalidOperationException($"{ErasField} not found or not an array.");

            Object[] wanted = EraAssetPaths
                .Select(p => AssetDatabase.LoadAssetAtPath<Object>(p))
                .ToArray();
            for (int i = 0; i < wanted.Length; i++)
            {
                if (wanted[i] == null)
                    throw new System.InvalidOperationException($"Era asset missing: {EraAssetPaths[i]}");
            }

            bool alreadyCorrect = eras.arraySize == wanted.Length;
            if (alreadyCorrect)
            {
                for (int i = 0; i < wanted.Length; i++)
                {
                    if (eras.GetArrayElementAtIndex(i).objectReferenceValue != wanted[i])
                    {
                        alreadyCorrect = false;
                        break;
                    }
                }
            }

            if (alreadyCorrect)
                return false;

            eras.arraySize = wanted.Length;
            for (int i = 0; i < wanted.Length; i++)
                eras.GetArrayElementAtIndex(i).objectReferenceValue = wanted[i];

            so.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
