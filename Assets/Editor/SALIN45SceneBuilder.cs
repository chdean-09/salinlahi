#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class SALIN45SceneBuilder
{
    private const string MenuPath = "Salinlahi/SALIN-45: Setup Dialogue Overlay in Gameplay";

    [MenuItem(MenuPath)]
    public static void SetupDialogueOverlay()
    {
        Undo.SetCurrentGroupName("SALIN-45: Setup Dialogue Overlay");
        int undoGroup = Undo.GetCurrentGroup();

        GameObject hudCanvas = FindByExactName("HUDCanvas");
        if (hudCanvas == null)
        {
            EditorUtility.DisplayDialog("Error",
                "HUDCanvas not found. Open the Gameplay scene first.", "OK");
            return;
        }

        GameObject hudRoot = FindByExactName("HUDRoot");
        if (hudRoot == null)
        {
            EditorUtility.DisplayDialog("Error",
                "HUDRoot not found under HUDCanvas.", "OK");
            return;
        }

        GameObject existingOverlay = FindByExactName("DialogueOverlay");
        if (existingOverlay != null)
        {
            if (!EditorUtility.DisplayDialog("Warning",
                "DialogueOverlay already exists. Delete and recreate?", "Yes", "Cancel"))
                return;

            Undo.DestroyObjectImmediate(existingOverlay);
        }

        GameObject overlay = CreateDialogueOverlay(hudCanvas);

        GameObject testDialogueAsset = CreateTestDialogueAsset();

        WireDialogueController(overlay, testDialogueAsset);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Undo.CollapseUndoOperations(undoGroup);

        EditorUtility.DisplayDialog("SALIN-45 Setup",
            "Dialogue overlay created!\n\n" +
            "Hierarchy under HUDCanvas:\n" +
            "  DialogueOverlay (DialogueController)\n" +
            "    TapCatcher (Button, full-screen)\n" +
            "    DialoguePanel (Image, semi-transparent)\n" +
            "      PortraitImage\n" +
            "      SpeakerName (TMP)\n" +
            "      BodyText (TMP)\n\n" +
            "TestDialogue asset created in Assets/ScriptableObjects/Dialogue/\n\n" +
            "REVIEW the DialogueController Inspector, then SAVE THE SCENE.",
            "OK");
    }

    private static GameObject CreateDialogueOverlay(GameObject canvas)
    {
        GameObject overlay = new GameObject("DialogueOverlay");
        Undo.RegisterCreatedObjectUndo(overlay, "Create DialogueOverlay");
        overlay.transform.SetParent(canvas.transform, false);
        overlay.SetActive(false);

        overlay.AddComponent<DialogueController>();

        GameObject tapCatcher = CreateTapCatcher(overlay);
        GameObject dialoguePanel = CreateDialoguePanel(overlay);

        DialogueController ctrl = overlay.GetComponent<DialogueController>();
        SerializedObject so = new SerializedObject(ctrl);

        so.FindProperty("_overlayPanel").objectReferenceValue = overlay;
        so.FindProperty("_speakerText").objectReferenceValue =
            dialoguePanel.transform.Find("SpeakerName")?.GetComponent<TMP_Text>();
        so.FindProperty("_portraitImage").objectReferenceValue =
            dialoguePanel.transform.Find("PortraitImage")?.GetComponent<Image>();
        so.FindProperty("_bodyText").objectReferenceValue =
            dialoguePanel.transform.Find("BodyText")?.GetComponent<TMP_Text>();
        so.FindProperty("_tapCatcher").objectReferenceValue = tapCatcher.GetComponent<Button>();
        so.FindProperty("_charsPerSecond").floatValue = 30f;
        so.ApplyModifiedProperties();

        Debug.Log("[SALIN-45] Created DialogueOverlay with TapCatcher and DialoguePanel.");
        return overlay;
    }

    private static GameObject CreateTapCatcher(GameObject parent)
    {
        GameObject tapCatcher = new GameObject("TapCatcher");
        Undo.RegisterCreatedObjectUndo(tapCatcher, "Create TapCatcher");
        tapCatcher.transform.SetParent(parent.transform, false);

        RectTransform rect = tapCatcher.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image img = tapCatcher.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0f);
        img.raycastTarget = true;

        Button btn = tapCatcher.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;

        Canvas canvasComp = parent.GetComponentInParent<Canvas>();
        if (canvasComp != null)
            tapCatcher.AddComponent<Canvas>().overrideSorting = true;

        Debug.Log("[SALIN-45] Created TapCatcher (full-screen, alpha=0).");
        return tapCatcher;
    }

    private static GameObject CreateDialoguePanel(GameObject parent)
    {
        GameObject panel = new GameObject("DialoguePanel");
        Undo.RegisterCreatedObjectUndo(panel, "Create DialoguePanel");
        panel.transform.SetParent(parent.transform, false);

        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 0);
        panelRect.anchorMax = new Vector2(1, 0.3f);
        panelRect.offsetMin = new Vector2(0, 0);
        panelRect.offsetMax = new Vector2(0, 0);
        panelRect.pivot = new Vector2(0.5f, 0);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.8f);
        bg.raycastTarget = false;

        GameObject portrait = CreatePortrait(panel);
        GameObject speakerName = CreateSpeakerName(panel);
        GameObject bodyText = CreateBodyText(panel);

        Debug.Log("[SALIN-45] Created DialoguePanel (anchored to bottom 30%).");
        return panel;
    }

    private static GameObject CreatePortrait(GameObject parent)
    {
        GameObject portrait = new GameObject("PortraitImage");
        Undo.RegisterCreatedObjectUndo(portrait, "Create PortraitImage");
        portrait.transform.SetParent(parent.transform, false);

        RectTransform rect = portrait.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0.5f);
        rect.anchorMax = new Vector2(0, 0.5f);
        rect.pivot = new Vector2(0, 0.5f);
        rect.anchoredPosition = new Vector2(20, 0);
        rect.sizeDelta = new Vector2(80, 80);

        Image img = portrait.AddComponent<Image>();
        img.raycastTarget = false;

        portrait.SetActive(false);

        Debug.Log("[SALIN-45] Created PortraitImage (hidden by default).");
        return portrait;
    }

    private static GameObject CreateSpeakerName(GameObject parent)
    {
        GameObject speakerName = new GameObject("SpeakerName");
        Undo.RegisterCreatedObjectUndo(speakerName, "Create SpeakerName");
        speakerName.transform.SetParent(parent.transform, false);

        RectTransform rect = speakerName.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 1);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0, 1);
        rect.offsetMin = new Vector2(120, 0);
        rect.offsetMax = new Vector2(-20, -10);
        rect.sizeDelta = new Vector2(0, 30);

        TMP_Text tmp = speakerName.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 24;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.BottomLeft;
        tmp.raycastTarget = false;

        Debug.Log("[SALIN-45] Created SpeakerName (TMP).");
        return speakerName;
    }

    private static GameObject CreateBodyText(GameObject parent)
    {
        GameObject bodyText = new GameObject("BodyText");
        Undo.RegisterCreatedObjectUndo(bodyText, "Create BodyText");
        bodyText.transform.SetParent(parent.transform, false);

        RectTransform rect = bodyText.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.pivot = new Vector2(0, 0.5f);
        rect.offsetMin = new Vector2(120, 10);
        rect.offsetMax = new Vector2(-20, -40);

        TMP_Text tmp = bodyText.AddComponent<TextMeshProUGUI>();
        tmp.text = "";
        tmp.fontSize = 20;
        tmp.fontStyle = FontStyles.Normal;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        Debug.Log("[SALIN-45] Created BodyText (TMP).");
        return bodyText;
    }

    private static GameObject CreateTestDialogueAsset()
    {
        const string folderPath = "Assets/ScriptableObjects/Dialogue";
        if (!AssetDatabase.IsValidFolder(folderPath))
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Dialogue");

        string assetPath = $"{folderPath}/TestDialogue.asset";
        DialogueSO existing = AssetDatabase.LoadAssetAtPath<DialogueSO>(assetPath);
        if (existing != null)
        {
            Debug.Log("[SALIN-45] TestDialogue.asset already exists. Skipping creation.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        DialogueSO dialogue = ScriptableObject.CreateInstance<DialogueSO>();
        dialogue.lines = new DialogueLine[]
        {
            new DialogueLine
            {
                speakerName = "Kuya",
                portrait = null,
                text = "Ang ganda ng umaga."
            },
            new DialogueLine
            {
                speakerName = "Elder",
                portrait = null,
                text = "Maligayang pagdating, anak."
            },
            new DialogueLine
            {
                speakerName = "Kuya",
                portrait = null,
                text = "Salamat po."
            }
        };

        AssetDatabase.CreateAsset(dialogue, assetPath);
        AssetDatabase.SaveAssets();

        Debug.Log("[SALIN-45] Created TestDialogue.asset with 3 lines.");
        return null;
    }

    private static void WireDialogueController(GameObject overlay, GameObject testAsset)
    {
        // Wiring is already done in CreateDialogueOverlay
        Debug.Log("[SALIN-45] DialogueController Inspector fields wired.");
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