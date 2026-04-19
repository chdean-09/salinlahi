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
        RemoveAllOfType<HeartDisplay>();
        RemoveAllOfType<WaveDisplay>();
        RemoveAllOfType<ComboDisplay>();
        RemoveAllOfType<FocusModeIndicator>();
        RemoveAllOfType<DrawingFeedback>();

        int added = 0;

        added += EnsureRootComponent<CombatResolver>("CombatResolver");
        added += EnsureComponentByName<HeartDisplay>("HeartsPanel");
        added += EnsureComponentByName<WaveDisplay>("WaveText");
        added += EnsureComponentByName<ComboDisplay>("HUDRoot");
        added += EnsureComponentByName<FocusModeIndicator>("HUDRoot");
        added += EnsureComponentByName<DrawingFeedback>("HUDRoot");

        WireHeartDisplay();
        WireWaveDisplay();
        WireComboDisplay();
        WireDrawingFeedback();
        WireFocusModeIndicator();

        EnsureActive("ComboText");
        EnsureActive("FocusModeIndicator");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("SALIN-89 Scene Setup",
            $"Processed {added} component(s).\n\n" +
            "Check the Console log for details.\n" +
            "Review each component in Inspector, then SAVE THE SCENE.",
            "OK");
    }

    private static int EnsureRootComponent<T>(string goName) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null)
        {
            Debug.Log($"[SALIN-89] {typeof(T).Name} already exists. Skipping.");
            return 0;
        }
        var go = new GameObject(goName);
        go.AddComponent<T>();
        Undo.RegisterCreatedObjectUndo(go, $"Create {goName}");
        Debug.Log($"[SALIN-89] Created '{goName}' with {typeof(T).Name}.");
        return 1;
    }

    private static int EnsureComponentByName<T>(string exactName) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null)
        {
            Debug.Log($"[SALIN-89] {typeof(T).Name} already exists. Skipping.");
            return 0;
        }

        GameObject go = FindByExactName(exactName);
        if (go == null)
        {
            Debug.LogWarning($"[SALIN-89] GameObject '{exactName}' not found. Add {typeof(T).Name} manually.");
            return 0;
        }

        // Don't add duplicates
        if (go.GetComponent<T>() != null)
        {
            Debug.Log($"[SALIN-89] {typeof(T).Name} already on '{go.name}'. Skipping.");
            return 0;
        }

        Undo.AddComponent<T>(go);
        Debug.Log($"[SALIN-89] Added {typeof(T).Name} to '{go.name}'.");
        return 1;
    }

    private static void EnsureActive(string goName)
    {
        GameObject go = FindByExactName(goName);
        if (go != null && !go.activeSelf)
        {
            go.SetActive(true);
            Debug.Log($"[SALIN-89] Activated '{goName}'.");
            Undo.RecordObject(go, $"Activate {goName}");
        }
    }

    private static GameObject FindByExactName(string name)
    {
        var scene = EditorSceneManager.GetActiveScene();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            var result = FindRecursive(root.transform, name);
            if (result != null) return result;
        }
        return null;
    }

    private static GameObject FindRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    private static void WireHeartDisplay()
    {
        var comp = Object.FindFirstObjectByType<HeartDisplay>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        var iconsProp = so.FindProperty("_heartIcons");
        var hearts = new System.Collections.Generic.List<Image>();
        foreach (Transform child in comp.transform)
        {
            if (child.name.StartsWith("Heart_"))
            {
                Image img = child.GetComponent<Image>();
                if (img != null) hearts.Add(img);
            }
        }
        if (hearts.Count > 0)
        {
            hearts.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
            iconsProp.arraySize = hearts.Count;
            for (int i = 0; i < hearts.Count; i++)
                iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
            Debug.Log($"[SALIN-89] Wired {hearts.Count} heart icons.");
        }
        so.ApplyModifiedProperties();
    }

    private static void WireWaveDisplay()
    {
        var comp = Object.FindFirstObjectByType<WaveDisplay>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        var tmp = comp.GetComponent<TMPro.TMP_Text>();
        if (tmp != null)
        {
            so.FindProperty("_waveText").objectReferenceValue = tmp;
            Debug.Log("[SALIN-89] Wired WaveDisplay._waveText.");
        }
        so.ApplyModifiedProperties();
    }

    private static void WireComboDisplay()
    {
        var comp = Object.FindFirstObjectByType<ComboDisplay>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        var comboTextGO = FindByExactName("ComboText");
        if (comboTextGO != null)
        {
            var tmp = comboTextGO.GetComponent<TMPro.TMP_Text>();
            if (tmp != null)
            {
                so.FindProperty("_streakText").objectReferenceValue = tmp;
                Debug.Log("[SALIN-89] Wired ComboDisplay._streakText.");
            }
        }
        so.ApplyModifiedProperties();
    }

    private static void WireDrawingFeedback()
    {
        var comp = Object.FindFirstObjectByType<DrawingFeedback>();
        if (comp == null) return;

        var so = new SerializedObject(comp);

        GameObject rejectFlashGO = FindByExactName("RejectFlash");
        if (rejectFlashGO != null)
        {
            CanvasGroup cg = rejectFlashGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = rejectFlashGO.AddComponent<CanvasGroup>();
            so.FindProperty("_rejectFlash").objectReferenceValue = cg;
        }

        GameObject rejectXMarkGO = FindByExactName("RejectXMark");
        if (rejectXMarkGO != null)
            so.FindProperty("_rejectXMark").objectReferenceValue = rejectXMarkGO;

        GameObject successFlashGO = FindByExactName("SuccessFlash");
        if (successFlashGO != null)
        {
            CanvasGroup cg = successFlashGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = successFlashGO.AddComponent<CanvasGroup>();
            so.FindProperty("_successFlash").objectReferenceValue = cg;
        }

        so.ApplyModifiedProperties();
        Debug.Log("[SALIN-89] Wired DrawingFeedback.");
    }

    private static void WireFocusModeIndicator()
    {
        var comp = Object.FindFirstObjectByType<FocusModeIndicator>();
        if (comp == null) return;

        var so = new SerializedObject(comp);
        GameObject focusGO = FindByExactName("FocusModeIndicator");

        if (focusGO != null)
        {
            so.FindProperty("_focusModeIndicator").objectReferenceValue = focusGO;
            CanvasGroup cg = focusGO.GetComponent<CanvasGroup>();
            if (cg == null) cg = focusGO.AddComponent<CanvasGroup>();
            so.FindProperty("_focusModeCanvasGroup").objectReferenceValue = cg;
        }

        so.ApplyModifiedProperties();
        Debug.Log("[SALIN-89] Wired FocusModeIndicator.");
    }

    private static void RemoveAllOfType<T>() where T : Component
    {
        T[] existing = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        foreach (T c in existing)
        {
            Debug.Log($"[SALIN-89] Removing old {typeof(T).Name} from '{c.gameObject.name}'.");
            Undo.DestroyObjectImmediate(c);
        }
    }
}
#endif