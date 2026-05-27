using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class CutscenePlayerSceneBuilder
{
    private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";

    [MenuItem("Salinlahi/Cutscene/Configure In Gameplay Scene")]
    public static void ConfigureInGameplay()
    {
        if (!File.Exists(GameplayScenePath))
        {
            EditorUtility.DisplayDialog(
                "Cutscene Player Builder",
                $"Missing gameplay scene:\n{GameplayScenePath}",
                "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Cutscene Player Builder",
            "This will open Gameplay.unity and add/update the CutscenePlayer Canvas with all UI wiring.",
            "Configure Gameplay",
            "Cancel");

        if (!proceed)
            return;

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        RepairOpenScene(scene);
    }

    [MenuItem("Salinlahi/Cutscene/Repair Open Scene Wiring")]
    public static void RepairOpenSceneWiring()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        bool proceed = EditorUtility.DisplayDialog(
            "Repair Cutscene Wiring",
            $"This will modify the currently open scene '{scene.name}' by adding/updating "
            + "the CutscenePlayer Canvas and wiring it to LevelFlowController.\n\n"
            + "Existing GameObjects may be recreated. Make sure your scene is saved or version-controlled.",
            "Proceed with Repair",
            "Cancel");

        if (!proceed)
            return;

        RepairOpenScene(scene);
    }

    private static void RepairOpenScene(Scene scene)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Cutscene Player Builder");

        try
        {
            RepairOpenSceneCore(scene);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void RepairOpenSceneCore(Scene scene)
    {
        CutscenePlayer player = EnsureCutscenePlayer();
        LevelFlowController flow = EnsureLevelFlowControllerWired(player);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Cutscene Player Builder",
            "CutscenePlayer Canvas created/updated and wired to LevelFlowController.\n\n"
            + "Next steps:\n"
            + "1. Create a LevelCutsceneMapping asset: Assets → Create → Salinlahi → Level Cutscene Mapping\n"
            + "2. Assign it to LevelFlowController's Cutscene section in the Inspector",
            "OK");
    }

    private static CutscenePlayer EnsureCutscenePlayer()
    {
        CutscenePlayer existing = Object.FindFirstObjectByType<CutscenePlayer>();
        if (existing != null)
        {
            RepairCutscenePlayerWiring(existing);
            return existing;
        }

        GameObject canvasGo = new("CutsceneCanvas");
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Cutscene Canvas");

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = RenderOrder.CutsceneCanvas;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGo.AddComponent<GraphicRaycaster>();

        CanvasGroup cg = canvasGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        cg.interactable = false;

        Image panelImage = CreateFullScreenImage(canvasGo.transform, "PanelImage");
        TMP_Text bodyText = CreateBodyText(canvasGo.transform, "BodyText");
        Button tapCatcher = CreateTapCatcher(canvasGo.transform, "TapCatcher");
        Button skipButton = CreateSkipButton(canvasGo.transform);

        CutscenePlayer player = canvasGo.AddComponent<CutscenePlayer>();
        player.enabled = false;

        SerializedObject serialized = new(player);
        serialized.FindProperty("_canvasGroup").objectReferenceValue = cg;
        serialized.FindProperty("_panelImage").objectReferenceValue = panelImage;
        serialized.FindProperty("_imageRectTransform").objectReferenceValue = panelImage.GetComponent<RectTransform>();
        serialized.FindProperty("_bodyText").objectReferenceValue = bodyText;
        serialized.FindProperty("_tapCatcher").objectReferenceValue = tapCatcher;
        serialized.FindProperty("_skipButton").objectReferenceValue = skipButton;
        serialized.FindProperty("_skipButtonRoot").objectReferenceValue = skipButton.gameObject;
        TMP_FontAsset font = EnsureVT323FontAsset();
        serialized.FindProperty("_bodyFont").objectReferenceValue = font;
        serialized.FindProperty("_bodyFontSize").floatValue = 92f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        player.enabled = true;

        EditorUtility.SetDirty(player);
        return player;
    }

    private static void RepairCutscenePlayerWiring(CutscenePlayer player)
    {
        player.enabled = false;

        SerializedObject serialized = new(player);
        Transform root = player.transform;

        CanvasGroup cg = player.GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = player.gameObject.AddComponent<CanvasGroup>();
            Undo.RecordObject(player.gameObject, "Add CanvasGroup to CutscenePlayer");
        }
        cg.alpha = 0f;
        cg.blocksRaycasts = false;

        Image panelImage = EnsureChildImage(root, "PanelImage");
        TMP_Text bodyText = EnsureChildTMPText(root, "BodyText");
        Button tapCatcher = EnsureChildButton(root, "TapCatcher");
        Button skipButton = SkipButtonExists(root)
            ? root.Find("SkipButton").GetComponent<Button>()
            : CreateSkipButton(root);

        serialized.FindProperty("_canvasGroup").objectReferenceValue = cg;
        serialized.FindProperty("_panelImage").objectReferenceValue = panelImage;
        serialized.FindProperty("_imageRectTransform").objectReferenceValue = panelImage.GetComponent<RectTransform>();
        serialized.FindProperty("_bodyText").objectReferenceValue = bodyText;
        serialized.FindProperty("_tapCatcher").objectReferenceValue = tapCatcher;
        serialized.FindProperty("_skipButton").objectReferenceValue = skipButton;
        serialized.FindProperty("_skipButtonRoot").objectReferenceValue = skipButton.gameObject;
        TMP_FontAsset font = EnsureVT323FontAsset();
        serialized.FindProperty("_bodyFont").objectReferenceValue = font;
        serialized.FindProperty("_bodyFontSize").floatValue = 92f;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        player.enabled = true;

        EditorUtility.SetDirty(player);
    }

    private static LevelFlowController EnsureLevelFlowControllerWired(CutscenePlayer player)
    {
        LevelFlowController flow = Object.FindFirstObjectByType<LevelFlowController>();
        if (flow == null)
        {
            GameObject go = new("[Manager] LevelFlowController");
            Undo.RegisterCreatedObjectUndo(go, "Create LevelFlowController");
            flow = go.AddComponent<LevelFlowController>();
        }

        SerializedObject serialized = new(flow);
        serialized.FindProperty("_cutscenePlayer").objectReferenceValue = player;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flow);

        return flow;
    }

    private static Image CreateFullScreenImage(Transform parent, string name)
    {
        GameObject go = new(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    private static TMP_Text CreateBodyText(Transform parent, string name)
    {
        GameObject go = new(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.08f, 0.06f);
        rect.anchorMax = new Vector2(0.92f, 0.26f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = 92;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateTapCatcher(Transform parent, string name)
    {
        GameObject go = new(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        return button;
    }

    private static Button CreateSkipButton(Transform parent)
    {
        GameObject go = new("SkipButton");
        Undo.RegisterCreatedObjectUndo(go, "Create Skip Button");
        go.transform.SetParent(parent, false);

        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(140f, 52f);
        rect.anchoredPosition = new Vector2(-40f, -32f);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.55f);

        Button button = go.AddComponent<Button>();

        GameObject labelGo = new("Label");
        Undo.RegisterCreatedObjectUndo(labelGo, "Create Skip Label");
        labelGo.transform.SetParent(go.transform, false);

        RectTransform labelRect = labelGo.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = "Skip";
        label.fontSize = 22;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        return button;
    }

    private static Image EnsureChildImage(Transform root, string name)
    {
        Transform existing = root.Find(name);
        if (existing != null)
        {
            Image img = existing.GetComponent<Image>();
            if (img != null) return img;
        }

        return CreateFullScreenImage(root, name);
    }

    private static TMP_Text EnsureChildTMPText(Transform root, string name)
    {
        Transform existing = root.Find(name);
        if (existing != null)
        {
            TMP_Text tmp = existing.GetComponent<TMP_Text>();
            if (tmp != null) return tmp;
        }

        return CreateBodyText(root, name);
    }

    private static Button EnsureChildButton(Transform root, string name)
    {
        Transform existing = root.Find(name);
        if (existing != null)
        {
            Button btn = existing.GetComponent<Button>();
            if (btn != null) return btn;
        }

        return CreateTapCatcher(root, name);
    }

    private static bool SkipButtonExists(Transform root)
    {
        return root.Find("SkipButton") != null;
    }

    private static TMP_FontAsset EnsureVT323FontAsset()
    {
        string[] guids = AssetDatabase.FindAssets("VT323 t:TMP_FontAsset");
        if (guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));

        const string ttfPath = "Assets/Art/UI/Fonts/VT323-Regular.ttf";
        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        if (sourceFont == null)
        {
            Debug.LogWarning("[CutsceneBuilder] VT323-Regular.ttf not found at " + ttfPath
                + ". Font will default to Liberation Sans until VT323 is imported.");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);

        const string destDir = "Assets/Resources/Fonts";
        if (!Directory.Exists(destDir))
            Directory.CreateDirectory(destDir);

        string destPath = destDir + "/VT323 SDF.asset";
        AssetDatabase.CreateAsset(fontAsset, destPath);

        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D tex in fontAsset.atlasTextures)
            {
                if (tex != null && !AssetDatabase.IsSubAsset(tex))
                    AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }

        if (fontAsset.material != null && !AssetDatabase.IsSubAsset(fontAsset.material))
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[CutsceneBuilder] Generated VT323 SDF TMP Font Asset at " + destPath);
        return fontAsset;
    }
}
