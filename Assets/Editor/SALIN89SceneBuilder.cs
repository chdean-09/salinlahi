#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SALIN89SceneBuilder
    {
        private const string MenuPath = "Salinlahi/SALIN-89: Wire Up Gameplay Scene";

        [MenuItem(MenuPath)]
        public static void WireUpGameplayScene()
        {
            int added = 0;

            // --- CombatResolver ---
            added += EnsureRootComponent<CombatResolver>("CombatResolver");

            // --- HUD child components ---
            added += EnsureComponentOnSearch<HeartDisplay>("Heart");
            added += EnsureComponentOnSearch<WaveDisplay>("Wave");
            added += EnsureComponentOnSearch<ComboDisplay>("Combo", "Streak");
            added += EnsureComponentOnSearch<FocusModeIndicator>("FocusMode", "Focus");
            added += EnsureComponentOnSearch<DrawingFeedback>("Feedback", "DrawingFeedback", "Reject");

            // Auto-wire serialized fields via reflection where possible
            AutoWireHeartDisplay();
            AutoWireWaveDisplay();
            AutoWireComboDisplay();
            AutoWireDrawingFeedback();
            AutoWireFocusModeIndicator();

            EditorSceneManager.MarkSceneDirty(
                EditorSceneManager.GetActiveScene());

            EditorUtility.DisplayDialog("SALIN-89 Scene Setup",
                $"Added/verified {added} component(s).\n\n" +
                "Review each component in the Inspector and assign\n" +
                "any remaining serialized references that could not\n" +
                "be auto-wired.\n\n" +
                "Remember to SAVE THE SCENE.",
                "OK");
        }

        // ─── Component placement helpers ───

        /// Ensures a component exists on a root-level GameObject (creates if missing).
        private static int EnsureRootComponent<T>(string goName) where T : Component
        {
            T existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                Debug.Log($"[SALIN-89] {typeof(T).Name} already exists on '{existing.gameObject.name}'.");
                return 0;
            }

            var go = new GameObject(goName);
            go.AddComponent<T>();
            Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
            Debug.Log($"[SALIN-89] Created '{goName}' with {typeof(T).Name}.");
            return 1;
        }

        /// Finds a GameObject by searching names (partial match, recursive),
        /// then adds the component if not already present.
        private static int EnsureComponentOnSearch<T>(params string[] searchTerms) where T : Component
        {
            T existing = Object.FindFirstObjectByType<T>();
            if (existing != null)
            {
                Debug.Log($"[SALIN-89] {typeof(T).Name} already exists on '{existing.gameObject.name}'.");
                return 0;
            }

            GameObject target = FindGameObjectByTerms(searchTerms);
            if (target == null)
            {
                Debug.LogWarning($"[SALIN-89] Could not find GameObject for {typeof(T).Name} " +
                                 $"(searched: {string.Join(", ", searchTerms)}). " +
                                 "Add it manually.");
                return 0;
            }

            Undo.AddComponent<T>(target);
            Debug.Log($"[SALIN-89] Added {typeof(T).Name} to '{target.name}'.");
            return 1;
        }

        private static GameObject FindGameObjectByTerms(string[] terms)
        {
            // Search all root objects and their children
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var result = SearchRecursive(root.transform, terms);
                if (result != null) return result;
            }
            return null;
        }

        private static GameObject SearchRecursive(Transform parent, string[] terms)
        {
            foreach (string term in terms)
            {
                if (parent.name.IndexOf(term, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return parent.gameObject;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                var result = SearchRecursive(parent.GetChild(i), terms);
                if (result != null) return result;
            }
            return null;
        }

        // ─── Auto-wire helpers ───

        private static void AutoWireHeartDisplay()
        {
            var comp = Object.FindFirstObjectByType<HeartDisplay>();
            if (comp == null) return;

            // Find heart icons — look for Image children with "Heart" in name
            var heartImages = new System.Collections.Generic.List<Image>();
            foreach (Transform child in comp.transform)
            {
                if (child.name.IndexOf("Heart", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Image img = child.GetComponent<Image>();
                    if (img != null) heartImages.Add(img);
                }
            }

            if (heartImages.Count > 0)
            {
                heartImages.Sort((a, b) =>
                    string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                SetField(comp, "_heartIcons", heartImages.ToArray());
                Debug.Log($"[SALIN-89] Auto-wired {heartImages.Count} heart icons.");
            }
        }

        private static void AutoWireWaveDisplay()
        {
            var comp = Object.FindFirstObjectByType<WaveDisplay>();
            if (comp == null) return;

            var tmp = comp.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null)
            {
                SetField(comp, "_waveText", tmp);
                Debug.Log($"[SALIN-89] Auto-wired WaveDisplay._waveText to '{tmp.name}'.");
            }
        }

        private static void AutoWireComboDisplay()
        {
            var comp = Object.FindFirstObjectByType<ComboDisplay>();
            if (comp == null) return;

            var tmp = comp.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null)
            {
                SetField(comp, "_streakText", tmp);
                Debug.Log($"[SALIN-89] Auto-wired ComboDisplay._streakText to '{tmp.name}'.");
            }
        }

        private static void AutoWireDrawingFeedback()
        {
            var comp = Object.FindFirstObjectByType<DrawingFeedback>();
            if (comp == null) return;

            // Look for CanvasGroups among siblings/children
            Transform parent = comp.transform.parent ?? comp.transform;
            foreach (Transform child in parent)
            {
                CanvasGroup cg = child.GetComponent<CanvasGroup>();
                if (cg == null) continue;

                string lower = child.name.ToLowerInvariant();
                if (lower.Contains("reject"))
                {
                    SetField(comp, "_rejectFlash", cg);
                    // Look for X mark child
                    foreach (Transform sub in child)
                    {
                        if (sub.name.IndexOf("X", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                            sub.name.IndexOf("Mark", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            SetField(comp, "_rejectXMark", sub.gameObject);
                            break;
                        }
                    }
                    Debug.Log($"[SALIN-89] Auto-wired DrawingFeedback._rejectFlash to '{child.name}'.");
                }
                else if (lower.Contains("success"))
                {
                    SetField(comp, "_successFlash", cg);
                    Debug.Log($"[SALIN-89] Auto-wired DrawingFeedback._successFlash to '{child.name}'.");
                }
            }
        }

        private static void AutoWireFocusModeIndicator()
        {
            var comp = Object.FindFirstObjectByType<FocusModeIndicator>();
            if (comp == null) return;

            SetField(comp, "_focusModeIndicator", comp.gameObject);

            CanvasGroup cg = comp.GetComponent<CanvasGroup>();
            if (cg == null) cg = comp.gameObject.AddComponent<CanvasGroup>();
            SetField(comp, "_focusModeCanvasGroup", cg);
            Debug.Log("[SALIN-89] Auto-wired FocusModeIndicator.");
        }

        // ─── Reflection utility ───

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(target, value);
                EditorUtility.SetDirty(target as Object);
            }
        }
    }
#endif
