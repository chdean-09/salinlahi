using System.IO;
using System.Text;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class Salin141PauseMenuSceneBuilder
{
    private const string RestartButtonName = "RestartButton";
    private const string ConfirmationPanelName = "LevelExitConfirmationPanel";
    private const string ConfirmButtonName = "ConfirmButton";
    private const string CancelButtonName = "CancelButton";
    private const string ConfirmationMessageName = "ConfirmationMessage";
    private const string LegacyConfirmationMessageName = "Title";

    private const float ResumeButtonY = 170f;
    private const float RestartButtonY = 0f;
    private const float LeaveButtonY = -170f;
    private const float ConfirmationConfirmButtonY = 80f;
    private const float ConfirmationCancelButtonY = -110f;
    private const float ConfirmationMessageY = 300f;
    private const float ConfirmationMessageWidth = 700f;
    private const float ConfirmationMessageHeight = 120f;
    private const float ConfirmationMessageFontSize = 56f;

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
        DebugLogger.Log("SALIN-141 pause menu scene authoring complete.");
    }

    private static void AuthorPauseMenuScene(string scenePath)
    {
        if (!File.Exists(scenePath))
        {
            DebugLogger.LogError($"SALIN-141: Scene not found: {scenePath}");
            return;
        }

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        PauseMenuUI pauseMenu = FindInScene<PauseMenuUI>(scene);
        if (pauseMenu == null)
        {
            DebugLogger.LogError($"SALIN-141: PauseMenuUI not found in {scenePath}");
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
            DebugLogger.LogError($"SALIN-141: Existing pause menu references are incomplete in {scenePath}");
            return;
        }

        Transform pausePanelParent = panel.transform.parent;
        restartButton ??= FindDirectChild(pausePanelParent, RestartButtonName)?.GetComponent<Button>();
        if (restartButton == null)
        {
            restartButton = Object.Instantiate(leaveButton, leaveButton.transform.parent);
            restartButton.name = RestartButtonName;
            Undo.RegisterCreatedObjectUndo(restartButton.gameObject, "Create SALIN-141 Restart Button");
        }

        SetButtonLabel(restartButton, "Restart Level");
        SetButtonLabel(leaveButton, "Leave Level");
        ArrangePauseButtons(resumeButton, restartButton, leaveButton);

        confirmationPanel ??= FindDirectChild(pausePanelParent, ConfirmationPanelName)?.gameObject;
        if (confirmationPanel == null)
        {
            confirmationPanel = Object.Instantiate(panel, pausePanelParent);
            confirmationPanel.name = ConfirmationPanelName;
            Undo.RegisterCreatedObjectUndo(confirmationPanel, "Create SALIN-141 Confirmation Panel");
        }

        confirmationPanel.SetActive(false);
        Transform clonedRestartButton = FindDirectChild(confirmationPanel.transform, RestartButtonName);
        if (clonedRestartButton != null)
            Object.DestroyImmediate(clonedRestartButton.gameObject);

        Button confirmButton = GetReference<Button>(serializedPauseMenu, "_confirmationConfirmButton")
            ?? FindDirectChild(confirmationPanel.transform, ConfirmButtonName)?.GetComponent<Button>()
            ?? FindDirectChild(confirmationPanel.transform, "ResumeButton")?.GetComponent<Button>();
        Button cancelButton = GetReference<Button>(serializedPauseMenu, "_confirmationCancelButton")
            ?? FindDirectChild(confirmationPanel.transform, CancelButtonName)?.GetComponent<Button>()
            ?? FindDirectChild(confirmationPanel.transform, "QuitButton")?.GetComponent<Button>();
        if (confirmButton == null || cancelButton == null)
        {
            DebugLogger.LogError($"SALIN-141: Confirmation panel needs Confirm and Cancel buttons in {scenePath}");
            return;
        }

        confirmButton.name = ConfirmButtonName;
        cancelButton.name = CancelButtonName;
        SetButtonLabel(confirmButton, "Confirm");
        SetButtonLabel(cancelButton, "Cancel");

        TMP_Text confirmationMessage = ResolveConfirmationMessage(confirmationPanel, scenePath);
        if (confirmationMessage == null)
            return;

        confirmationMessage.text = "Are you sure?";

        ArrangeConfirmationControls(confirmButton, cancelButton, confirmationMessage);

        SetReference(serializedPauseMenu, "_restartButton", restartButton);
        SetReference(serializedPauseMenu, "_confirmationPanel", confirmationPanel);
        SetReference(serializedPauseMenu, "_confirmationConfirmButton", confirmButton);
        SetReference(serializedPauseMenu, "_confirmationCancelButton", cancelButton);
        serializedPauseMenu.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(pauseMenu);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        DebugLogger.Log($"SALIN-141: Authored pause menu controls in {scenePath}");
    }

    private static void ArrangePauseButtons(Button resumeButton, Button restartButton, Button leaveButton)
    {
        SetButtonY(resumeButton, ResumeButtonY);
        SetButtonY(restartButton, RestartButtonY);
        SetButtonY(leaveButton, LeaveButtonY);
    }

    private static void ArrangeConfirmationControls(Button confirmButton, Button cancelButton, TMP_Text message)
    {
        SetButtonY(confirmButton, ConfirmationConfirmButtonY);
        SetButtonY(cancelButton, ConfirmationCancelButtonY);

        if (message == null)
            return;

        RectTransform rect = message.transform as RectTransform;
        if (rect == null)
            return;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, ConfirmationMessageY);
        rect.sizeDelta = new Vector2(ConfirmationMessageWidth, ConfirmationMessageHeight);

        message.fontSize = ConfirmationMessageFontSize;
        message.enableAutoSizing = false;
        message.alignment = TextAlignmentOptions.Center;
    }

    private static TMP_Text ResolveConfirmationMessage(GameObject confirmationPanel, string scenePath)
    {
        TMP_Text confirmationMessage = FindUniqueNamedNonButtonText(
            confirmationPanel,
            ConfirmationMessageName,
            scenePath);
        if (confirmationMessage != null)
            return confirmationMessage;

        TMP_Text legacyTitle = FindUniqueNamedNonButtonText(
            confirmationPanel,
            LegacyConfirmationMessageName,
            scenePath);
        if (legacyTitle != null)
        {
            legacyTitle.name = ConfirmationMessageName;
            return legacyTitle;
        }

        List<TMP_Text> candidates = GetNonButtonTexts(confirmationPanel);
        if (candidates.Count > 1)
        {
            DebugLogger.LogError(
                $"SALIN-141: Confirmation panel text layout is ambiguous in {scenePath}. "
                + $"Expected '{ConfirmationMessageName}' or legacy '{LegacyConfirmationMessageName}', found: {DescribeTextCandidates(candidates)}");
            return null;
        }

        string candidateName = candidates.Count == 1 ? candidates[0].name : "<none>";
        DebugLogger.LogError(
            $"SALIN-141: Confirmation panel message text not found in {scenePath}. "
            + $"Expected '{ConfirmationMessageName}' or legacy '{LegacyConfirmationMessageName}', found: {candidateName}");
        return null;
    }

    private static TMP_Text FindUniqueNamedNonButtonText(GameObject root, string textName, string scenePath)
    {
        if (root == null)
            return null;

        List<TMP_Text> matches = new();
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null
                || text.name != textName
                || text.GetComponentInParent<Button>() != null)
            {
                continue;
            }

            matches.Add(text);
        }

        if (matches.Count <= 1)
            return matches.Count == 1 ? matches[0] : null;

        DebugLogger.LogError(
            $"SALIN-141: Found multiple non-button TMP texts named '{textName}' in {scenePath}. "
            + $"Refusing to pick one arbitrarily.");
        return null;
    }

    private static List<TMP_Text> GetNonButtonTexts(GameObject root)
    {
        List<TMP_Text> candidates = new();
        if (root == null)
            return candidates;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null || text.GetComponentInParent<Button>() != null)
                continue;

            candidates.Add(text);
        }

        return candidates;
    }

    private static string DescribeTextCandidates(List<TMP_Text> candidates)
    {
        StringBuilder builder = new();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (i > 0)
                builder.Append(", ");

            builder.Append('\'');
            builder.Append(candidates[i] != null ? candidates[i].name : "<null>");
            builder.Append('\'');
        }

        return builder.ToString();
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
