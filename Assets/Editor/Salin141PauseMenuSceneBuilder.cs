using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Salin141PauseMenuSceneBuilder
{
    private static readonly string[] GameplayScenePaths =
    {
        "Assets/_Scenes/Gameplay.unity",
        "Assets/_Scenes/Level_01_Tutorial.unity"
    };

    [MenuItem("Salinlahi/SALIN-141/Author Pause Menu Scenes")]
    public static void AuthorPauseMenuScenes()
    {
        foreach (string scenePath in GameplayScenePaths)
            AuthorPauseMenuScene(scenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("SALIN-141 pause menu scene authoring complete.");
    }

    private static void AuthorPauseMenuScene(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            Debug.LogError($"SALIN-141: Scene not found: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        PauseMenuUI pauseMenu = FindInScene<PauseMenuUI>(scene);
        if (pauseMenu == null)
        {
            Debug.LogError($"SALIN-141: PauseMenuUI not found in {scenePath}");
            return;
        }

        SerializedObject serializedPauseMenu = new(pauseMenu);
        GameObject panel = GetReference<GameObject>(serializedPauseMenu, "_panel");
        Button resumeButton = GetReference<Button>(serializedPauseMenu, "_resumeButton");
        Button leaveButton = GetReference<Button>(serializedPauseMenu, "_quitButton");
        Button restartButton = GetReference<Button>(serializedPauseMenu, "_restartButton");
        GameObject confirmationPanel = GetReference<GameObject>(serializedPauseMenu, "_confirmationPanel");

        if (panel == null || resumeButton == null || leaveButton == null)
        {
            Debug.LogError($"SALIN-141: Existing pause menu references are incomplete in {scenePath}");
            return;
        }

        Transform pausePanelParent = panel.transform.parent;
        restartButton ??= FindDirectChild(pausePanelParent, "RestartButton")?.GetComponent<Button>();
        if (restartButton == null)
        {
            restartButton = Object.Instantiate(leaveButton, leaveButton.transform.parent);
            restartButton.name = "RestartButton";
            Undo.RegisterCreatedObjectUndo(restartButton.gameObject, "Create SALIN-141 Restart Button");
        }

        SetButtonLabel(restartButton, "Restart Level");
        SetButtonLabel(leaveButton, "Leave Level");
        ArrangePauseButtons(resumeButton, restartButton, leaveButton);

        confirmationPanel ??= FindDirectChild(pausePanelParent, "LevelExitConfirmationPanel")?.gameObject;
        if (confirmationPanel == null)
        {
            confirmationPanel = Object.Instantiate(panel, pausePanelParent);
            confirmationPanel.name = "LevelExitConfirmationPanel";
            Undo.RegisterCreatedObjectUndo(confirmationPanel, "Create SALIN-141 Confirmation Panel");
        }

        confirmationPanel.SetActive(false);
        Transform clonedRestartButton = FindDirectChild(confirmationPanel.transform, "RestartButton");
        if (clonedRestartButton != null)
            Object.DestroyImmediate(clonedRestartButton.gameObject);

        Button confirmButton = FindDirectChild(confirmationPanel.transform, "ResumeButton")?.GetComponent<Button>();
        Button cancelButton = FindDirectChild(confirmationPanel.transform, "QuitButton")?.GetComponent<Button>();
        if (confirmButton == null || cancelButton == null)
        {
            Debug.LogError($"SALIN-141: Confirmation panel needs Confirm and Cancel buttons in {scenePath}");
            return;
        }

        confirmButton.name = "ConfirmButton";
        cancelButton.name = "CancelButton";
        SetButtonLabel(confirmButton, "Confirm");
        SetButtonLabel(cancelButton, "Cancel");

        TMP_Text confirmationMessage = null;
        foreach (TMP_Text text in confirmationPanel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text == null || text.GetComponentInParent<Button>() != null)
                continue;

            confirmationMessage = text;
            text.name = "ConfirmationMessage";
            text.text = "Are you sure?";
            break;
        }

        ArrangeConfirmationControls(confirmButton, cancelButton, confirmationMessage);

        SetReference(serializedPauseMenu, "_restartButton", restartButton);
        SetReference(serializedPauseMenu, "_confirmationPanel", confirmationPanel);
        SetReference(serializedPauseMenu, "_confirmationConfirmButton", confirmButton);
        SetReference(serializedPauseMenu, "_confirmationCancelButton", cancelButton);
        serializedPauseMenu.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(pauseMenu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"SALIN-141: Authored pause menu controls in {scenePath}");
    }

    private static void ArrangePauseButtons(Button resumeButton, Button restartButton, Button leaveButton)
    {
        SetButtonY(resumeButton, 170f);
        SetButtonY(restartButton, 0f);
        SetButtonY(leaveButton, -170f);
    }

    private static void ArrangeConfirmationControls(Button confirmButton, Button cancelButton, TMP_Text message)
    {
        SetButtonY(confirmButton, 80f);
        SetButtonY(cancelButton, -110f);

        if (message == null)
            return;

        RectTransform rect = message.transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 300f);
        rect.sizeDelta = new Vector2(700f, 120f);

        message.fontSize = 56f;
        message.enableAutoSizing = false;
        message.alignment = TextAlignmentOptions.Center;
    }

    private static void SetButtonY(Button button, float y)
    {
        RectTransform rect = button != null ? button.transform as RectTransform : null;
        if (rect == null)
            return;

        Vector2 position = rect.anchoredPosition;
        rect.anchoredPosition = new Vector2(position.x, y);
    }

    private static void SetButtonLabel(Button button, string label)
    {
        if (button == null)
            return;

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;
    }

    private static T GetReference<T>(SerializedObject serializedObject, string propertyName)
        where T : Object
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property?.objectReferenceValue as T;
    }

    private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }

        return null;
    }
}
