#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class SALIN84SceneBuilder
{
    private const string MenuPath = "Salinlahi/SALIN-84: Setup Level Select (15 Levels)";
    private const string LevelConfigsFolder = "Assets/ScriptableObjects/Levels";

    private static readonly (string name, int number)[] PlaceholderLevels = new[]
    {
        ("Chapter 2 - Level 1", 6),
        ("Chapter 2 - Level 2", 7),
        ("Chapter 2 - Level 3", 8),
        ("Chapter 2 - Level 4", 9),
        ("Chapter 2 - Level 5", 10),
        ("Chapter 3 - Level 1", 11),
        ("Chapter 3 - Level 2", 12),
        ("Chapter 3 - Level 3", 13),
        ("Chapter 3 - Level 4", 14),
        ("Chapter 3 - Level 5", 15),
    };

    [MenuItem(MenuPath)]
    public static void SetupLevelSelect()
    {
        Undo.SetCurrentGroupName("SALIN-84: Setup Level Select");
        int undoGroup = Undo.GetCurrentGroup();

        List<LevelConfigSO> allConfigs = CreateMissingPlaceholderAssets();

        GameObject canvas = FindByExactName("Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "Canvas not found. Open the LevelSelect scene.", "OK");
            return;
        }

        GameObject gridContainer = FindByExactName("GridContainer");

        GameObject scrollView = CreateScrollView(canvas, gridContainer);
        if (scrollView == null)
        {
            EditorUtility.DisplayDialog("Error", "Failed to create ScrollView structure.", "OK");
            return;
        }

        GameObject content = scrollView.transform.Find("Viewport/Content")?.gameObject;

        List<LevelButton> buttons = CreateLevelButtons(content, gridContainer);

        GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            Undo.RecordObject(grid, "Configure GridLayoutGroup");
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = new Vector2(280, 280);
            grid.spacing = new Vector2(40, 40);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(20, 20, 20, 20);
            Debug.Log("[SALIN-84] GridLayoutGroup configured: 3 columns, 280x280 cells.");
        }

        GameObject backButton = FindByExactName("BackButton");
        WireLevelSelectUI(allConfigs, buttons, backButton);
        SetButtonEditorPreview(allConfigs, buttons);

        if (gridContainer != null)
            Undo.DestroyObjectImmediate(gridContainer);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("SALIN-84 Scene Setup",
            "Setup complete!\n\n" +
            $"Created {allConfigs.Count} LevelConfigs ({PlaceholderLevels.Length} placeholders).\n" +
            $"Created {buttons.Count} LevelButton instances under ScrollView.\n" +
            "GridLayoutGroup set to 3 columns.\n" +
            "LevelSelectUI._levelSlots wired with all 15 slots.\n\n" +
            "REVIEW each component in Inspector, then SAVE THE SCENE.",
            "OK");
    }

    private static List<LevelConfigSO> CreateMissingPlaceholderAssets()
    {
        List<LevelConfigSO> allConfigs = new List<LevelConfigSO>();

        if (!AssetDatabase.IsValidFolder(LevelConfigsFolder))
        {
            Debug.LogError("[SALIN-84] Folder not found: " + LevelConfigsFolder);
            return allConfigs;
        }

        string[] existingGuids = AssetDatabase.FindAssets("t:LevelConfigSO", new[] { LevelConfigsFolder });
        Dictionary<int, LevelConfigSO> existingByNumber = new Dictionary<int, LevelConfigSO>();

        foreach (string guid in existingGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            LevelConfigSO config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            if (config != null)
                existingByNumber[config.levelNumber] = config;
        }

        for (int i = 1; i <= 15; i++)
        {
            if (existingByNumber.TryGetValue(i, out LevelConfigSO existing))
            {
                allConfigs.Add(existing);
                continue;
            }

            string assetName = $"Level{i}_Config";
            string assetPath = $"{LevelConfigsFolder}/{assetName}.asset";

            LevelConfigSO newConfig = ScriptableObject.CreateInstance<LevelConfigSO>();
            newConfig.levelName = GetPlaceholderName(i);
            newConfig.levelNumber = i;
            newConfig.waves = new List<WaveConfigSO>();
            newConfig.allowedCharacters = new List<BaybayinCharacterSO>();
            newConfig.isAvailableInLite = false;

            AssetDatabase.CreateAsset(newConfig, assetPath);
            AssetDatabase.SaveAssets();

            LevelConfigSO loaded = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(assetPath);
            allConfigs.Add(loaded);
            Debug.Log($"[SALIN-84] Created placeholder: {assetName} (level {i})");
        }

        allConfigs.Sort((a, b) => a.levelNumber.CompareTo(b.levelNumber));
        return allConfigs;
    }

    private static string GetPlaceholderName(int levelNumber)
    {
        foreach (var (name, number) in PlaceholderLevels)
            if (number == levelNumber) return name;
        return $"Level {levelNumber}";
    }

    private static GameObject CreateScrollView(GameObject canvas, GameObject gridContainer)
    {
        GameObject scrollView = new GameObject("ScrollView");
        Undo.RegisterCreatedObjectUndo(scrollView, "Create ScrollView");
        scrollView.transform.SetParent(canvas.transform, false);

        RectTransform scrollViewRect = scrollView.AddComponent<RectTransform>();
        scrollViewRect.anchorMin = new Vector2(0, 0);
        scrollViewRect.anchorMax = new Vector2(1, 1);
        scrollViewRect.offsetMin = new Vector2(20, 100);
        scrollViewRect.offsetMax = new Vector2(-20, -120);

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        Image scrollBg = scrollView.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0);

        GameObject viewport = new GameObject("Viewport");
        Undo.RegisterCreatedObjectUndo(viewport, "Create Viewport");
        viewport.transform.SetParent(scrollView.transform, false);

        RectTransform viewportRect = viewport.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content");
        Undo.RegisterCreatedObjectUndo(content, "Create Content");
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        ContentSizeFitter sizeFitter = content.AddComponent<ContentSizeFitter>();
        sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        content.AddComponent<GridLayoutGroup>();

        scrollRect.content = contentRect;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 20f;

        Debug.Log("[SALIN-84] Created ScrollView with ScrollRect (vertical, elastic).");
        return scrollView;
    }

    private static List<LevelButton> CreateLevelButtons(GameObject content,
        GameObject gridContainer)
    {
        List<LevelButton> buttons = new List<LevelButton>();

        GameObject levelButtonPrefab = GetLevelButtonPrefab();
        if (levelButtonPrefab == null)
        {
            EditorUtility.DisplayDialog("Error",
                "LevelButton prefab not found at Assets/Prefabs/UI/LevelButton.prefab", "OK");
            return buttons;
        }

        for (int i = 0; i < 15; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(levelButtonPrefab,
                content.transform);
            Undo.RegisterCreatedObjectUndo(instance, $"Create LevelButton_{i + 1:D2}");
            instance.name = $"LevelButton_{i + 1:D2}";

            // Ensure LevelButton component exists
            LevelButton lb = instance.GetComponent<LevelButton>();
            if (lb == null)
                lb = Undo.AddComponent<LevelButton>(instance);

            // Wire LevelButton internal fields via SerializedObject
            WireLevelButtonInternals(lb, instance);

            buttons.Add(lb);
        }

        if (gridContainer != null)
        {
            foreach (Transform child in gridContainer.transform)
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        Debug.Log($"[SALIN-84] Created {buttons.Count} LevelButton instances.");
        return buttons;
    }

    /// <summary>
    /// Wires the internal serialized fields on a LevelButton instance:
    /// _button, _levelNumberText, _lockIcon, _completionBadge.
    /// Replaces legacy Text with TextMeshProUGUI if needed.
    /// </summary>
    private static void WireLevelButtonInternals(LevelButton lb, GameObject instance)
    {
        var so = new SerializedObject(lb);

        // Wire _button → the Button component on the root
        Button btn = instance.GetComponent<Button>();
        if (btn != null)
            so.FindProperty("_button").objectReferenceValue = btn;

        // Find the Label child and ensure it has TMP_Text (not legacy Text)
        Transform labelTransform = instance.transform.Find("Label");
        if (labelTransform != null)
        {
            // Remove legacy Text if present
            Text legacyText = labelTransform.GetComponent<Text>();
            if (legacyText != null)
            {
                string existingText = legacyText.text;
                int fontSize = legacyText.fontSize;
                Color color = legacyText.color;
                TextAnchor alignment = legacyText.alignment;
                Undo.DestroyObjectImmediate(legacyText);

                // Add TextMeshProUGUI
                TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(labelTransform.gameObject);
                tmp.text = existingText;
                tmp.fontSize = fontSize;
                tmp.color = color;
                tmp.alignment = ConvertAlignment(alignment);
                tmp.enableAutoSizing = false;
                tmp.raycastTarget = false;

                so.FindProperty("_levelNumberText").objectReferenceValue = tmp;
                Debug.Log($"[SALIN-84] Replaced legacy Text with TMP on '{instance.name}/Label'.");
            }
            else
            {
                // Already has TMP or needs one added
                TextMeshProUGUI tmp = labelTransform.GetComponent<TextMeshProUGUI>();
                if (tmp == null)
                    tmp = Undo.AddComponent<TextMeshProUGUI>(labelTransform.gameObject);
                so.FindProperty("_levelNumberText").objectReferenceValue = tmp;
            }
        }

        // Wire _lockIcon → LockOverlay child
        Transform lockTransform = instance.transform.Find("LockOverlay");
        if (lockTransform != null)
            so.FindProperty("_lockIcon").objectReferenceValue = lockTransform.gameObject;

        // Wire _completionBadge → CompletionCheck child
        Transform checkTransform = instance.transform.Find("CompletionCheck");
        if (checkTransform != null)
            so.FindProperty("_completionBadge").objectReferenceValue = checkTransform.gameObject;

        so.ApplyModifiedProperties();
    }

    private static TextAlignmentOptions ConvertAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
            TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
            TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center,
        };
    }

    private static void WireLevelSelectUI(List<LevelConfigSO> configs,
        List<LevelButton> buttons, GameObject backButton)
    {
        LevelSelectUI ui = Object.FindFirstObjectByType<LevelSelectUI>();
        if (ui == null)
        {
            Debug.LogError("[SALIN-84] LevelSelectUI component not found in scene.");
            return;
        }

        SerializedObject so = new SerializedObject(ui);

        SerializedProperty slotsProp = so.FindProperty("_levelSlots");
        if (slotsProp == null)
        {
            Debug.LogError("[SALIN-84] Could not find _levelSlots property on LevelSelectUI.");
            return;
        }

        int count = Mathf.Min(configs.Count, buttons.Count);
        slotsProp.arraySize = count;

        for (int i = 0; i < count; i++)
        {
            SerializedProperty slot = slotsProp.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("config").objectReferenceValue = configs[i];
            slot.FindPropertyRelative("button").objectReferenceValue = buttons[i];
        }

        if (backButton != null)
        {
            Button backBtn = backButton.GetComponent<Button>();
            if (backBtn != null)
                so.FindProperty("_backButton").objectReferenceValue = backBtn;
        }

        so.ApplyModifiedProperties();
        Debug.Log($"[SALIN-84] Wired LevelSelectUI with {count} level slots.");
    }

    private static void SetButtonEditorPreview(List<LevelConfigSO> configs,
        List<LevelButton> buttons)
    {
        int count = Mathf.Min(configs.Count, buttons.Count);

        for (int i = 0; i < count; i++)
        {
            LevelButton lb = buttons[i];
            LevelConfigSO config = configs[i];
            if (lb == null || config == null) continue;

            var so = new SerializedObject(lb);

            // Set level number text
            SerializedProperty textProp = so.FindProperty("_levelNumberText");
            if (textProp != null && textProp.objectReferenceValue != null)
            {
                var tmp = textProp.objectReferenceValue as TMPro.TMP_Text;
                if (tmp != null)
                {
                    Undo.RecordObject(tmp, "Set level number text");
                    tmp.text = config.levelNumber.ToString();
                }
            }

            // Level 1 unlocked by default, rest locked
            bool unlocked = config.levelNumber == 1;

            // Set lock icon visibility
            SerializedProperty lockProp = so.FindProperty("_lockIcon");
            if (lockProp != null && lockProp.objectReferenceValue != null)
            {
                var lockGO = lockProp.objectReferenceValue as GameObject;
                if (lockGO != null)
                {
                    Undo.RecordObject(lockGO, "Set lock icon visibility");
                    lockGO.SetActive(!unlocked);
                }
            }

            // Hide completion badge
            SerializedProperty badgeProp = so.FindProperty("_completionBadge");
            if (badgeProp != null && badgeProp.objectReferenceValue != null)
            {
                var badgeGO = badgeProp.objectReferenceValue as GameObject;
                if (badgeGO != null)
                {
                    Undo.RecordObject(badgeGO, "Hide completion badge");
                    badgeGO.SetActive(false);
                }
            }

            // Set button interactability
            SerializedProperty btnProp = so.FindProperty("_button");
            if (btnProp != null && btnProp.objectReferenceValue != null)
            {
                var btn = btnProp.objectReferenceValue as Button;
                if (btn != null)
                {
                    Undo.RecordObject(btn, "Set button interactable");
                    btn.interactable = unlocked;
                }
            }
        }

        Debug.Log($"[SALIN-84] Set editor preview on {count} buttons (level numbers, lock state).");
    }

    private static GameObject GetLevelButtonPrefab()
    {
        string[] guids = AssetDatabase.FindAssets("LevelButton t:Prefab",
            new[] { "Assets/Prefabs" });

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("LevelButton.prefab"))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        return null;
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
}
#endif