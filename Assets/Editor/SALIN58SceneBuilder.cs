#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SALIN58SceneBuilder
{
    private const string MenuPathMainMenu = "Salinlahi/SALIN-58: Setup UI Overlays (MainMenu)";
    private const string MenuPathGameplay = "Salinlahi/SALIN-58: Setup UI Overlays (Gameplay)";

    private const float PanelBgAlpha = 0.85f;
    private static readonly Color PanelBgColor = new Color(0.1f, 0.1f, 0.15f, PanelBgAlpha);
    private static readonly Color AccentColor = new Color(0.2f, 0.6f, 1f, 1f);
    private static readonly Color StarGoldColor = new Color(1f, 0.85f, 0.1f, 1f);
    private static readonly Color HeartRedColor = new Color(0.9f, 0.2f, 0.2f, 1f);

    #region MainMenu Setup

    [MenuItem(MenuPathMainMenu)]
    public static void SetupMainMenu()
    {
        Undo.SetCurrentGroupName("SALIN-58: Setup MainMenu Overlays");
        int undoGroup = Undo.GetCurrentGroup();

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Equals("MainMenu"))
        {
            if (!EditorUtility.DisplayDialog("Warning",
                $"Active scene is '{scene.name}', not 'MainMenu'. Continue anyway?",
                "Yes", "Cancel"))
                return;
        }

        GameObject canvas = FindByExactName("Canvas");
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error",
                "Canvas not found in the MainMenu scene.", "OK");
            return;
        }

        GameObject settingsPanel = FindOrCreateOverlay(canvas, "SettingsPanel");
        CreateSettingsPanelContent(settingsPanel);

        GameObject creditsPanel = FindOrCreateOverlay(canvas, "CreditsPanel");
        CreateCreditsPanelContent(creditsPanel);

        WireMainMenuUI(settingsPanel, creditsPanel);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("SALIN-58 MainMenu Setup",
            "MainMenu overlays created!\n\n" +
            "Hierarchy under Canvas:\n" +
            "  SettingsPanel (SettingsPanel)\n" +
            "    Background\n" +
            "    Title (Settings)\n" +
            "    MasterSlider / MasterLabel\n" +
            "    BGMSlider / BGMLabel\n" +
            "    SFXSlider / SFXLabel\n" +
            "    CloseButton\n" +
            "  CreditsPanel (CreditsPanel)\n" +
            "    Background\n" +
            "    Title (Credits)\n" +
            "    Content (TMP text)\n" +
            "    CloseButton\n\n" +
            "MainMenuUI._settingsPanel and _creditsPanel wired.\n" +
            "REVIEW Inspectors, then SAVE THE SCENE.",
            "OK");
    }

    private static void CreateSettingsPanelContent(GameObject panel)
    {
        SettingsPanel settingsComp = panel.GetComponent<SettingsPanel>();
        if (settingsComp == null)
            settingsComp = Undo.AddComponent<SettingsPanel>(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = Undo.AddComponent<CanvasGroup>(panel);

        GameObject bg = CreateFullRectChild(panel, "Background", PanelBgColor);
        GameObject title = CreateLabel(panel, "Title", "Settings", 36, FontStyles.Bold, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(-40, -60));

        GameObject masterLabel = CreateLabel(panel, "MasterLabel", "Master Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, 20), new Vector2(-10, -20));
        GameObject masterSlider = CreateSlider(panel, "MasterSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, 20), new Vector2(-20, -20));

        GameObject bgmLabel = CreateLabel(panel, "BGMLabel", "BGM Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, -40), new Vector2(-10, -80));
        GameObject bgmSlider = CreateSlider(panel, "BGMSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, -40), new Vector2(-20, -80));

        GameObject sfxLabel = CreateLabel(panel, "SFXLabel", "SFX Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, -100), new Vector2(-10, -140));
        GameObject sfxSlider = CreateSlider(panel, "SFXSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, -100), new Vector2(-20, -140));

        GameObject closeButton = CreateButton(panel, "CloseButton", "Close",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, -180), new Vector2(160, 40));

        var so = new SerializedObject(settingsComp);
        so.FindProperty("_masterSlider").objectReferenceValue = masterSlider.GetComponent<Slider>();
        so.FindProperty("_bgmSlider").objectReferenceValue = bgmSlider.GetComponent<Slider>();
        so.FindProperty("_sfxSlider").objectReferenceValue = sfxSlider.GetComponent<Slider>();
        so.FindProperty("_closeButton").objectReferenceValue = closeButton.GetComponent<Button>();
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] SettingsPanel created and wired.");
    }

    private static void CreateCreditsPanelContent(GameObject panel)
    {
        CreditsPanel creditsComp = panel.GetComponent<CreditsPanel>();
        if (creditsComp == null)
            creditsComp = Undo.AddComponent<CreditsPanel>(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = Undo.AddComponent<CanvasGroup>(panel);

        GameObject bg = CreateFullRectChild(panel, "Background", PanelBgColor);
        GameObject title = CreateLabel(panel, "Title", "Credits", 36, FontStyles.Bold, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(-40, -60));

        GameObject content = CreateScrollViewContent(panel, "Content",
            "Team Salinlahi\n\n" +
            "Lead Developer — Juan Dela Cruz\n" +
            "Game Designer — Maria Santos\n" +
            "Artist — Andres Reyes\n" +
            "Sound Designer — Lina Gonzales\n\n" +
            "(Replace with actual team names)",
            22, FontStyles.Normal, Color.white,
            new Vector2(0, 0f), new Vector2(1f, 0.85f),
            new Vector2(20, -80), new Vector2(-20, -140));

        GameObject closeButton = CreateButton(panel, "CloseButton", "Close",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, -30), new Vector2(160, 40));

        var so = new SerializedObject(creditsComp);
        so.FindProperty("_closeButton").objectReferenceValue = closeButton.GetComponent<Button>();
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] CreditsPanel created and wired.");
    }

    private static void WireMainMenuUI(GameObject settingsPanel, GameObject creditsPanel)
    {
        MainMenuUI mainMenuUI = Object.FindFirstObjectByType<MainMenuUI>();
        if (mainMenuUI == null)
        {
            Debug.LogWarning("[SALIN-58] MainMenuUI component not found in scene. Wire _settingsPanel and _creditsPanel manually.");
            return;
        }

        var so = new SerializedObject(mainMenuUI);
        so.FindProperty("_settingsPanel").objectReferenceValue = settingsPanel.GetComponent<SettingsPanel>();
        so.FindProperty("_creditsPanel").objectReferenceValue = creditsPanel.GetComponent<CreditsPanel>();
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] MainMenuUI wired to SettingsPanel and CreditsPanel.");
    }

    #endregion

    #region Gameplay Setup

    [MenuItem(MenuPathGameplay)]
    public static void SetupGameplay()
    {
        Undo.SetCurrentGroupName("SALIN-58: Setup Gameplay Overlays");
        int undoGroup = Undo.GetCurrentGroup();

        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.Equals("Gameplay"))
        {
            if (!EditorUtility.DisplayDialog("Warning",
                $"Active scene is '{scene.name}', not 'Gameplay'. Continue anyway?",
                "Yes", "Cancel"))
                return;
        }

        GameObject canvas = FindByExactName("Canvas");
        if (canvas == null)
        {
            canvas = FindByExactName("HUDCanvas");
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Canvas or HUDCanvas not found in the Gameplay scene.", "OK");
                return;
            }
        }

        GameObject victoryPanel = FindOrCreateOverlay(canvas, "VictoryPanel");
        CreateVictoryPanelContent(victoryPanel);

        GameObject defeatPanel = FindOrCreateOverlay(canvas, "DefeatPanel");
        CreateDefeatPanelContent(defeatPanel);

        GameObject pauseSettingsPanel = FindOrCreateOverlay(canvas, "PauseSettingsPanel");
        CreatePauseSettingsPanelContent(pauseSettingsPanel);

        WirePauseMenuUI(pauseSettingsPanel);

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("SALIN-58 Gameplay Setup",
            "Gameplay overlays created!\n\n" +
            "Hierarchy:\n" +
            "  VictoryPanel (VictoryScreenUI)\n" +
            "    Background\n" +
            "    StarCount / StarIcons\n" +
            "    NextLevelButton / LevelSelectButton\n" +
            "  DefeatPanel (DefeatScreenUI)\n" +
            "    Background\n" +
            "    HeartCountText\n" +
            "    RetryButton / LevelSelectButton\n" +
            "  PauseSettingsPanel (SettingsPanel)\n" +
            "    (same layout as MainMenu Settings)\n\n" +
            "PauseMenuUI._settingsPanel wired.\n" +
            "REVIEW Inspectors, then SAVE THE SCENE.",
            "OK");
    }

    private static void CreateVictoryPanelContent(GameObject panel)
    {
        VictoryScreenUI victoryComp = panel.GetComponent<VictoryScreenUI>();
        if (victoryComp == null)
            victoryComp = Undo.AddComponent<VictoryScreenUI>(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = Undo.AddComponent<CanvasGroup>(panel);

        GameObject bg = CreateFullRectChild(panel, "Background", PanelBgColor);

        GameObject titleObj = CreateLabel(panel, "VictoryTitle", "Level Complete!", 48, FontStyles.Bold, StarGoldColor,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(-40, -90));

        GameObject starCount = CreateLabel(panel, "StarCount", "0/3", 28, FontStyles.Normal, Color.white,
            new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), new Vector2(0, -10), new Vector2(-20, -50));

        GameObject starIconsRoot = new GameObject("StarIcons");
        Undo.RegisterCreatedObjectUndo(starIconsRoot, "Create StarIcons");
        starIconsRoot.transform.SetParent(panel.transform, false);
        RectTransform starIconsRect = starIconsRoot.AddComponent<RectTransform>();
        starIconsRect.anchorMin = new Vector2(0.5f, 0.55f);
        starIconsRect.anchorMax = new Vector2(0.5f, 0.55f);
        starIconsRect.pivot = new Vector2(0.5f, 0.5f);
        starIconsRect.sizeDelta = new Vector2(300, 80);

        GameObject[] starIcons = new GameObject[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject star = new GameObject($"Star_{i}");
            Undo.RegisterCreatedObjectUndo(star, $"Create Star_{i}");
            star.transform.SetParent(starIconsRoot.transform, false);
            RectTransform sr = star.AddComponent<RectTransform>();
            sr.anchorMin = new Vector2(0.5f, 0.5f);
            sr.anchorMax = new Vector2(0.5f, 0.5f);
            sr.pivot = new Vector2(0.5f, 0.5f);
            sr.anchoredPosition = new Vector2((i - 1) * 100, 0);
            sr.sizeDelta = new Vector2(70, 70);
            Image starImg = star.AddComponent<Image>();
            starImg.color = StarGoldColor;
            starImg.raycastTarget = false;
            star.SetActive(false);
            starIcons[i] = star;
        }

        GameObject nextLevelButton = CreateButton(panel, "NextLevelButton", "Next Level",
            new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f),
            new Vector2(0, 10), new Vector2(240, 50));

        GameObject levelSelectButton = CreateButton(panel, "LevelSelectButton", "Level Select",
            new Vector2(0.5f, 0.15f), new Vector2(0.5f, 0.15f),
            new Vector2(0, 10), new Vector2(240, 50));

        var so = new SerializedObject(victoryComp);
        so.FindProperty("_starCountText").objectReferenceValue = starCount.GetComponent<TMP_Text>();
        so.FindProperty("_starIcons").arraySize = 3;
        for (int i = 0; i < 3; i++)
            so.FindProperty("_starIcons").GetArrayElementAtIndex(i).objectReferenceValue = starIcons[i];
        so.FindProperty("_nextLevelButton").objectReferenceValue = nextLevelButton.GetComponent<Button>();
        so.FindProperty("_levelSelectButton").objectReferenceValue = levelSelectButton.GetComponent<Button>();
        so.FindProperty("_panel").objectReferenceValue = panel;
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] VictoryPanel created and wired.");
    }

    private static void CreateDefeatPanelContent(GameObject panel)
    {
        DefeatScreenUI defeatComp = panel.GetComponent<DefeatScreenUI>();
        if (defeatComp == null)
            defeatComp = Undo.AddComponent<DefeatScreenUI>(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = Undo.AddComponent<CanvasGroup>(panel);

        GameObject bg = CreateFullRectChild(panel, "Background", PanelBgColor);

        GameObject titleObj = CreateLabel(panel, "DefeatTitle", "Defeated", 48, FontStyles.Bold, HeartRedColor,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -30), new Vector2(-40, -90));

        GameObject heartCount = CreateLabel(panel, "HeartCountText", "0/3", 28, FontStyles.Normal, HeartRedColor,
            new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0, -10), new Vector2(-20, -50));

        GameObject retryButton = CreateButton(panel, "RetryButton", "Retry",
            new Vector2(0.5f, 0.35f), new Vector2(0.5f, 0.35f),
            new Vector2(0, 10), new Vector2(240, 50));

        GameObject levelSelectButton = CreateButton(panel, "LevelSelectButton", "Level Select",
            new Vector2(0.5f, 0.25f), new Vector2(0.5f, 0.25f),
            new Vector2(0, 10), new Vector2(240, 50));

        var so = new SerializedObject(defeatComp);
        so.FindProperty("_heartCountText").objectReferenceValue = heartCount.GetComponent<TMP_Text>();
        so.FindProperty("_retryButton").objectReferenceValue = retryButton.GetComponent<Button>();
        so.FindProperty("_levelSelectButton").objectReferenceValue = levelSelectButton.GetComponent<Button>();
        so.FindProperty("_panel").objectReferenceValue = panel;
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] DefeatPanel created and wired.");
    }

    private static void CreatePauseSettingsPanelContent(GameObject panel)
    {
        SettingsPanel settingsComp = panel.GetComponent<SettingsPanel>();
        if (settingsComp == null)
            settingsComp = Undo.AddComponent<SettingsPanel>(panel);

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg == null)
            cg = Undo.AddComponent<CanvasGroup>(panel);

        GameObject bg = CreateFullRectChild(panel, "Background", PanelBgColor);
        GameObject title = CreateLabel(panel, "Title", "Settings", 36, FontStyles.Bold, Color.white,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -20), new Vector2(-40, -60));

        GameObject masterLabel = CreateLabel(panel, "MasterLabel", "Master Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, 20), new Vector2(-10, -20));
        GameObject masterSlider = CreateSlider(panel, "MasterSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, 20), new Vector2(-20, -20));

        GameObject bgmLabel = CreateLabel(panel, "BGMLabel", "BGM Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, -40), new Vector2(-10, -80));
        GameObject bgmSlider = CreateSlider(panel, "BGMSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, -40), new Vector2(-20, -80));

        GameObject sfxLabel = CreateLabel(panel, "SFXLabel", "SFX Volume", 20, FontStyles.Normal, Color.white,
            new Vector2(0, 0.5f), new Vector2(0.35f, 0.5f), new Vector2(-10, -100), new Vector2(-10, -140));
        GameObject sfxSlider = CreateSlider(panel, "SFXSlider", 0f, 1f, 1f,
            new Vector2(0.35f, 0.5f), new Vector2(1f, 0.5f), new Vector2(20, -100), new Vector2(-20, -140));

        GameObject closeButton = CreateButton(panel, "CloseButton", "Close",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0, -180), new Vector2(160, 40));

        var so = new SerializedObject(settingsComp);
        so.FindProperty("_masterSlider").objectReferenceValue = masterSlider.GetComponent<Slider>();
        so.FindProperty("_bgmSlider").objectReferenceValue = bgmSlider.GetComponent<Slider>();
        so.FindProperty("_sfxSlider").objectReferenceValue = sfxSlider.GetComponent<Slider>();
        so.FindProperty("_closeButton").objectReferenceValue = closeButton.GetComponent<Button>();
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] PauseSettingsPanel created and wired.");
    }

    private static void WirePauseMenuUI(GameObject settingsPanel)
    {
        PauseMenuUI pauseMenuUI = Object.FindFirstObjectByType<PauseMenuUI>();
        if (pauseMenuUI == null)
        {
            Debug.LogWarning("[SALIN-58] PauseMenuUI component not found in scene. Wire _settingsPanel and _settingsButton manually.");
            return;
        }

        SettingsPanel sp = settingsPanel.GetComponent<SettingsPanel>();
        Button settingsBtn = FindButtonInChildren(FindByExactName("PauseMenu") ?? FindByExactName("PauseMenuPanel"), "SettingsButton");

        var so = new SerializedObject(pauseMenuUI);

        if (sp != null)
            so.FindProperty("_settingsPanel").objectReferenceValue = sp;

        if (settingsBtn != null)
            so.FindProperty("_settingsButton").objectReferenceValue = settingsBtn;

        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-58] PauseMenuUI wired to PauseSettingsPanel.");
    }

    #endregion

    #region UI Helpers

    private static GameObject FindOrCreateOverlay(GameObject parent, string name)
    {
        GameObject existing = FindByExactName(name);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Warning",
                $"'{name}' already exists. Delete and recreate?", "Yes", "Cancel"))
                return existing;

            Undo.DestroyObjectImmediate(existing);
        }

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        go.SetActive(false);
        return go;
    }

    private static GameObject CreateFullRectChild(GameObject parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        return go;
    }

    private static GameObject CreateLabel(GameObject parent, string name, string text,
        float fontSize, FontStyles style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }

    private static GameObject CreateScrollViewContent(GameObject parent, string name, string text,
        float fontSize, FontStyles style, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 1f);

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        return go;
    }

    private static GameObject CreateSlider(GameObject parent, string name,
        float minValue, float maxValue, float value,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        rect.pivot = new Vector2(0.5f, 0.5f);

        GameObject bg = new GameObject("Background");
        Undo.RegisterCreatedObjectUndo(bg, "Create Slider Background");
        bg.transform.SetParent(go.transform, false);
        RectTransform bgRect = bg.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        bgRect.pivot = new Vector2(0.5f, 0.5f);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.3f, 0.3f, 0.35f, 1f);

        GameObject fillArea = new GameObject("Fill Area");
        Undo.RegisterCreatedObjectUndo(fillArea, "Create Fill Area");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        Undo.RegisterCreatedObjectUndo(fill, "Create Slider Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fill.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = AccentColor;

        GameObject handleArea = new GameObject("Handle Slide Area");
        Undo.RegisterCreatedObjectUndo(handleArea, "Create Handle Area");
        handleArea.transform.SetParent(go.transform, false);
        RectTransform handleAreaRect = handleArea.AddComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = Vector2.zero;
        handleAreaRect.offsetMax = Vector2.zero;

        GameObject handle = new GameObject("Handle");
        Undo.RegisterCreatedObjectUndo(handle, "Create Slider Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handle.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(20, 20);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        Slider slider = go.AddComponent<Slider>();
        slider.interactable = true;
        slider.transition = Selectable.Transition.ColorTint;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        slider.value = value;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;

        return go;
    }

    private static GameObject CreateButton(GameObject parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent.transform, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image img = go.AddComponent<Image>();
        img.color = AccentColor;

        Button btn = go.AddComponent<Button>();
        btn.transition = Selectable.Transition.ColorTint;

        GameObject label = new GameObject("Label");
        Undo.RegisterCreatedObjectUndo(label, $"Create {name} Label");
        label.transform.SetParent(go.transform, false);
        RectTransform labelRect = label.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        labelRect.pivot = new Vector2(0.5f, 0.5f);

        TMP_Text tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        return go;
    }

    private static Button FindButtonInChildren(GameObject parent, string buttonName)
    {
        if (parent == null) return null;
        Transform found = parent.transform.Find(buttonName);
        if (found != null)
            return found.GetComponent<Button>();

        for (int i = 0; i < parent.transform.childCount; i++)
        {
            Button btn = FindButtonInChildren(parent.transform.GetChild(i).gameObject, buttonName);
            if (btn != null) return btn;
        }
        return null;
    }

    #endregion

    #region Scene Helpers

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

    #endregion
}
#endif