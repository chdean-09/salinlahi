#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Salinlahi.Editor
{
    public static class SALIN89SceneBuilder
    {
        private const string MenuPath = "Salinlahi/Debug/SALIN-89: Wire Up HUD Components";
        private const string MenuPathBorderPulse = "Salinlahi/Debug/SALIN-89: Add CanvasGroup to BorderPulse";

        [MenuItem(MenuPath)]
        public static void WireUpHUDComponents()
        {
            GameObject hudRoot = GameObject.Find("HUDRoot");
            if (hudRoot == null)
            {
                EditorUtility.DisplayDialog("HUD Root Not Found",
                    "Could not find a GameObject named 'HUDRoot' in the scene. " +
                    "Make sure the Gameplay scene is loaded.", "OK");
                return;
            }

            int added = 0;

            added += AddComponentToChild<HeartDisplay>(hudRoot, "HeartsPanel",
                comp =>
                {
                    var hearts = FindChildrenNamed(hudRoot.transform, "Heart_");
                    if (hearts.Count > 0)
                    {
                        var field = typeof(HeartDisplay).GetField("_heartIcons",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field?.SetValue(comp, hearts.ToArray());
                    }

                    AssignSpriteByName(comp, "_heartFull", "Heart Full");
                    AssignSpriteByName(comp, "_heartEmpty", "Heart Empty");
                });

            added += AddComponentToChild<WaveDisplay>(hudRoot, "WaveText",
                comp => { });

            added += AddComponentToChild<ComboDisplay>(hudRoot, "ComboText",
                comp => { });

            added += AddComponentToChild<FocusModeIndicator>(hudRoot, "FocusModeIndicator",
                comp =>
                {
                    AssignGameObjectField(comp, "_focusModeIndicator",
                        FindChildNamed(hudRoot.transform, "FocusModeIndicator"));
                    var cg = FindChildNamed(hudRoot.transform, "FocusModeIndicator")
                        ?.GetComponent<CanvasGroup>();
                    if (cg != null)
                    {
                        var field = typeof(FocusModeIndicator).GetField("_focusModeCanvasGroup",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        field?.SetValue(comp, cg);
                    }
                });

            added += AddComponentToChild<DrawingFeedback>(hudRoot, "RejectFlash",
                comp =>
                {
                    AssignGameObjectField(comp, "_rejectXMark",
                        FindChildNamed(hudRoot.transform, "RejectXMark"));
                    AssignCanvasGroupInChildren(comp, "_successFlash",
                        FindChildNamed(hudRoot.transform, "SuccessFlash"));
                });

            EditorUtility.DisplayDialog("SALIN-89 HUD Setup Complete",
                $"Added {added} component(s) under HUDRoot.\n\n" +
                "IMPORTANT: Review each component in the Inspector and " +
                "assign any remaining serialized references (sprites, etc.) " +
                "that could not be auto-wired.", "OK");
        }

        [MenuItem(MenuPathBorderPulse)]
        public static void AddBorderPulseCanvasGroup()
        {
            BorderPulse[] allBorderPulses = Object.FindObjectsByType<BorderPulse>(FindObjectsSortMode.None);
            if (allBorderPulses.Length == 0)
            {
                EditorUtility.DisplayDialog("No BorderPulse Found",
                    "No BorderPulse component found in the scene. " +
                    "Add one first, then run this command.", "OK");
                return;
            }

            int added = 0;
            foreach (BorderPulse bp in allBorderPulses)
            {
                CanvasGroup cg = bp.GetComponent<CanvasGroup>();
                if (cg == null)
                {
                    cg = bp.gameObject.AddComponent<CanvasGroup>();
                    added++;
                    DebugLogger.Log($"SALIN-89: Added CanvasGroup to BorderPulse on '{bp.gameObject.name}'.");
                }
                else
                {
                    DebugLogger.Log($"SALIN-89: BorderPulse on '{bp.gameObject.name}' already has CanvasGroup.");
                }
            }

            EditorUtility.DisplayDialog("SALIN-89 BorderPulse Setup Complete",
                $"Added {added} CanvasGroup(s) to BorderPulse GameObject(s).", "OK");
        }

        private static int AddComponentToChild<T>(GameObject parent, string childName, System.Action<T> wire) where T : Component
        {
            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                DebugLogger.Log($"SALIN-89: Could not find child '{childName}' under '{parent.name}'. Skipping {typeof(T).Name}.");
                return 0;
            }

            T existing = child.GetComponent<T>();
            if (existing != null)
            {
                DebugLogger.Log($"SALIN-89: '{childName}' already has {typeof(T).Name}. Skipping.");
                return 0;
            }

            T comp = child.gameObject.AddComponent<T>();
            wire(comp);
            DebugLogger.Log($"SALIN-89: Added {typeof(T).Name} to '{childName}'.");
            return 1;
        }

        private static GameObject FindChildNamed(Transform parent, string name)
        {
            Transform found = parent.Find(name);
            return found?.gameObject;
        }

        private static System.Collections.Generic.List<Image> FindChildrenNamed(Transform parent, string prefix)
        {
            var results = new System.Collections.Generic.List<Image>();
            foreach (Transform child in parent)
            {
                if (child.name.StartsWith(prefix))
                {
                    Image img = child.GetComponent<Image>();
                    if (img != null)
                        results.Add(img);
                }
            }

            results.Sort((a, b) =>
                string.Compare(a.gameObject.name, b.gameObject.name, System.StringComparison.Ordinal));
            return results;
        }

        private static void AssignGameObjectField(Component target, string fieldName, GameObject value)
        {
            if (value == null) return;
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        private static void AssignCanvasGroupInChildren(Component target, string fieldName, GameObject parent)
        {
            if (parent == null) return;
            CanvasGroup cg = parent.GetComponent<CanvasGroup>();
            if (cg == null) return;
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, cg);
        }

        private static void AssignSpriteByName(Component target, string fieldName, string assetSearchName)
        {
            string[] guids = AssetDatabase.FindAssets($"{assetSearchName} t:Sprite");
            if (guids.Length == 0) return;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) return;
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(target, sprite);
        }
    }
}
#endif