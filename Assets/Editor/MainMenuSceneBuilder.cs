#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One-click builder for MainMenu.unity scene.
/// Constructs Canvas + Title + buttons (Play, Level Select, Endless Mode, Tracing Dojo, Settings)
/// and wires every button's onClick to the corresponding MainMenuUI method via SerializedObject.
///
/// Running this is destructive: it clears MainMenu.unity root objects and rebuilds from scratch.
/// Idempotent — safe to run repeatedly.
/// </summary>
public static class MainMenuSceneBuilder
{
    private const string ScenePath = "Assets/_Scenes/MainMenu.unity";
    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    [MenuItem("Salinlahi/Build MainMenu Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Build MainMenu Scene",
                $"This will clear all root objects in {ScenePath} and rebuild the Main Menu UI from scratch.\n\nContinue?",
                "Build", "Cancel"))
        {
            return;
        }

        try
        {
            var scene = OpenOrCreateScene();
            Debug.Log("[MainMenuSceneBuilder] Scene opened.");

            EnsureSceneInBuildSettings();
            ClearRootObjects(scene);
            Debug.Log("[MainMenuSceneBuilder] Root objects cleared.");

            CreateMainCamera();
            Debug.Log("[MainMenuSceneBuilder] Camera created.");

            CreateEventSystem();
            Debug.Log("[MainMenuSceneBuilder] EventSystem created.");

            var canvas = CreateCanvas();
            Debug.Log("[MainMenuSceneBuilder] Canvas created.");

            var controller = AddMainMenuController(canvas.gameObject);
            Debug.Log("[MainMenuSceneBuilder] MainMenuUI controller added.");

            CreateTitle(canvas.transform);

            var playBtn        = CreateButton(canvas.transform, "PlayButton",        "Play",         new Vector2(0f, 200f),  new Vector2(600f, 120f));
            var levelSelectBtn = CreateButton(canvas.transform, "LevelSelectButton", "Level Select", new Vector2(0f, 50f),   new Vector2(600f, 120f));
            var endlessBtn     = CreateButton(canvas.transform, "EndlessModeButton", "Endless Mode", new Vector2(0f, -100f), new Vector2(600f, 120f));
            var tracingBtn     = CreateButton(canvas.transform, "TracingDojoButton", "Tracing Dojo", new Vector2(0f, -250f), new Vector2(600f, 120f));
            var settingsBtn    = CreateButton(canvas.transform, "SettingsButton",    "Settings",     new Vector2(0f, -400f), new Vector2(600f, 120f));

            Debug.Log("[MainMenuSceneBuilder] Buttons created.");

            WireController(controller, playBtn, levelSelectBtn, endlessBtn);
            WireButtonMethod(playBtn,        controller, "OnPlayButtonPressed");
            WireButtonMethod(levelSelectBtn, controller, "OnLevelSelectPressed");
            WireButtonMethod(endlessBtn,     controller, "OnEndlessModePressed");
            WireButtonMethod(tracingBtn,     controller, "OnTracingDojoPressed");
            WireButtonMethod(settingsBtn,    controller, "OnSettingsPressed");

            Debug.Log("[MainMenuSceneBuilder] Buttons wired.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[MainMenuSceneBuilder] Built {ScenePath}. Ready for Play Mode.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MainMenuSceneBuilder] FAILED: {e.Message}\n{e.StackTrace}");
        }
    }

    // ------------------------------------------------------------------
    // Scene setup
    // ------------------------------------------------------------------

    private static UnityEngine.SceneManagement.Scene OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    private static void EnsureSceneInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath)) return;

        scenes.Add(new EditorBuildSettingsScene(ScenePath, enabled: true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[MainMenuSceneBuilder] Added {ScenePath} to Build Settings.");
    }

    private static void ClearRootObjects(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            Object.DestroyImmediate(root);
    }

    private static void CreateMainCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static MainMenuUI AddMainMenuController(GameObject canvasGo)
    {
        return canvasGo.AddComponent<MainMenuUI>();
    }

    private static void CreateTitle(Transform canvasRoot)
    {
        var go = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(canvasRoot, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -160f);
        rect.sizeDelta = new Vector2(800f, 160f);

        var text = go.GetComponent<Text>();
        text.text = "Salinlahi";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 96;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static Button CreateButton(Transform canvasRoot, string name, string label, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasRoot, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.25f, 0.5f, 0.85f, 1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        button.colors = colors;

        // Label
        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var labelText = labelGo.GetComponent<Text>();
        labelText.text = label;
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.fontSize = 52;
        labelText.color = Color.white;
        labelText.raycastTarget = false;

        return button;
    }

    // ------------------------------------------------------------------
    // Wiring
    // ------------------------------------------------------------------

    /// <summary>
    /// Wires SerializeFields on MainMenuUI (endlessModeButton reference).
    /// </summary>
    private static void WireController(MainMenuUI controller, Button playBtn, Button levelSelectBtn, Button endlessBtn)
    {
        var so = new SerializedObject(controller);

        var endlessProp = so.FindProperty("_endlessModeButton");
        if (endlessProp != null)
            endlessProp.objectReferenceValue = endlessBtn;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// Adds a persistent onClick listener pointing to the named method on MainMenuUI.
    /// Uses UnityEditor.Events.UnityEventTools to persist the call in the scene file.
    /// </summary>
    private static void WireButtonMethod(Button button, MainMenuUI target, string methodName)
    {
        if (button == null) return;

        var so = new SerializedObject(button);
        // Clear existing persistent calls first so re-runs don't duplicate
        button.onClick.RemoveAllListeners();

        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            button.onClick,
            System.Delegate.CreateDelegate(
                typeof(UnityEngine.Events.UnityAction),
                target,
                typeof(MainMenuUI).GetMethod(methodName)) as UnityEngine.Events.UnityAction
        );

        EditorUtility.SetDirty(button);
    }
}
#endif
