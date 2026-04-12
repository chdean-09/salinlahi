#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// One-click builder for SALIN-43 LevelSelect.unity scene.
/// Constructs Canvas + GridLayoutGroup + 5 LevelButton instances + BackButton + Title,
/// creates Assets/Prefabs/UI/LevelButton.prefab if missing, and wires every
/// SerializeField on LevelSelectUI via SerializedObject.
///
/// Running this is destructive: it clears LevelSelect.unity root objects and rebuilds
/// from scratch. Idempotent — safe to run repeatedly.
/// </summary>
public static class LevelSelectSceneBuilder
{
    private const string ScenePath = "Assets/_Scenes/LevelSelect.unity";
    private const string PrefabDir = "Assets/Prefabs/UI";
    private const string PrefabPath = "Assets/Prefabs/UI/LevelButton.prefab";

    private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
    private const int LevelCount = 5;

    [MenuItem("Salinlahi/SALIN-43/Build LevelSelect Scene")]
    public static void Build()
    {
        if (!EditorUtility.DisplayDialog(
                "Build LevelSelect Scene",
                $"This will clear all root objects in {ScenePath} and rebuild the Level Select UI from scratch.\n\nContinue?",
                "Build", "Cancel"))
        {
            return;
        }

        try
        {
            var prefab = CreateOrLoadLevelButtonPrefab();
            Debug.Log($"[LevelSelectSceneBuilder] Prefab ready: {prefab != null}");

            var scene = OpenOrCreateScene();
            Debug.Log($"[LevelSelectSceneBuilder] Scene opened: {scene.name}");

            EnsureSceneInBuildSettings();
            ClearRootObjects(scene);
            Debug.Log("[LevelSelectSceneBuilder] Root objects cleared.");

            CreateMainCamera();
            Debug.Log("[LevelSelectSceneBuilder] Camera created.");

            CreateEventSystem();
            Debug.Log("[LevelSelectSceneBuilder] EventSystem created.");

            var canvas = CreateCanvas();
            Debug.Log("[LevelSelectSceneBuilder] Canvas created.");

            CreateTitle(canvas.transform);
            var backButton = CreateBackButton(canvas.transform);
            Debug.Log("[LevelSelectSceneBuilder] Title + BackButton created.");

            var (buttons, locks, checks) = CreateLevelGrid(canvas.transform, prefab);
            Debug.Log("[LevelSelectSceneBuilder] Grid created.");

            WireLevelSelectController(buttons, locks, checks, backButton);
            Debug.Log("[LevelSelectSceneBuilder] Controller wired.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log($"[LevelSelectSceneBuilder] Built {ScenePath} with {LevelCount} level buttons. Ready for Play Mode.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LevelSelectSceneBuilder] FAILED: {e.Message}\n{e.StackTrace}");
        }
    }

    // ------------------------------------------------------------------
    // Scene setup
    // ------------------------------------------------------------------

    private static Scene OpenOrCreateScene()
    {
        if (System.IO.File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        return scene;
    }

    private static void ClearRootObjects(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateMainCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        go.tag = "MainCamera";
        var cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
    }

    private static void CreateEventSystem()
    {
        // StandaloneInputModule works for both mouse (editor) and touch (mobile) under legacy input.
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static void EnsureSceneInBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == ScenePath))
        {
            return;
        }

        scenes.Add(new EditorBuildSettingsScene(ScenePath, enabled: true));
        EditorBuildSettings.scenes = scenes.ToArray();
        Debug.Log($"[LevelSelectSceneBuilder] Added {ScenePath} to Build Settings.");
    }

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private static void CreateTitle(Transform canvasRoot)
    {
        var go = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(canvasRoot, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -120f);
        rect.sizeDelta = new Vector2(800f, 120f);

        var text = go.GetComponent<Text>();
        text.text = "Select Level";
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = 72;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static Button CreateBackButton(Transform canvasRoot)
    {
        var go = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasRoot, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(60f, -60f);
        rect.sizeDelta = new Vector2(200f, 100f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.35f, 0.35f, 0.4f, 1f);

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        CreateLabel(go.transform, "Back", fontSize: 48);

        return button;
    }

    private static (Button[] buttons, GameObject[] locks, GameObject[] checks) CreateLevelGrid(
        Transform canvasRoot, GameObject prefab)
    {
        var gridGo = new GameObject("GridContainer", typeof(RectTransform), typeof(GridLayoutGroup));
        gridGo.transform.SetParent(canvasRoot, false);

        var gridRect = gridGo.GetComponent<RectTransform>();
        gridRect.anchorMin = new Vector2(0.5f, 0.5f);
        gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.sizeDelta = new Vector2(640f, 1000f);

        var grid = gridGo.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(280f, 280f);
        grid.spacing = new Vector2(40f, 40f);
        grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        var buttons = new Button[LevelCount];
        var locks = new GameObject[LevelCount];
        var checks = new GameObject[LevelCount];

        for (int i = 0; i < LevelCount; i++)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gridGo.transform);
            instance.name = $"Level{i + 1}Button";

            // Update label text — supports both legacy Text and TextMeshPro on the Label child.
            var labelTransform = instance.transform.Find("Label");
            if (labelTransform != null)
            {
                var legacyText = labelTransform.GetComponent<Text>();
                if (legacyText != null)
                {
                    legacyText.text = (i + 1).ToString();
                }
                else
                {
                    // TextMeshPro fallback — avoids hard dependency on TMP assembly in Editor script.
                    var tmp = labelTransform.GetComponent("TextMeshProUGUI");
                    if (tmp != null)
                    {
                        var textProp = tmp.GetType().GetProperty("text");
                        textProp?.SetValue(tmp, (i + 1).ToString());
                    }
                }
            }

            buttons[i] = instance.GetComponent<Button>();
            locks[i] = instance.transform.Find("LockOverlay")?.gameObject;
            checks[i] = instance.transform.Find("CompletionCheck")?.gameObject;
        }

        return (buttons, locks, checks);
    }

    private static void SetProperty(SerializedObject so, string propertyName, Object value)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"[LevelSelectSceneBuilder] SerializedProperty '{propertyName}' not found on {so.targetObject.GetType().Name}. Skipping.");
            return;
        }
        prop.objectReferenceValue = value;
    }

    private static void WireLevelSelectController(
        Button[] buttons, GameObject[] locks, GameObject[] checks, Button backButton)
    {
        var go = new GameObject("[UI] LevelSelectController");
        var controller = go.AddComponent<LevelSelectUI>();

        var so = new SerializedObject(controller);
        for (int i = 0; i < LevelCount; i++)
        {
            int n = i + 1;
            SetProperty(so, $"_level{n}Button", buttons[i]);
            SetProperty(so, $"_level{n}LockOverlay", locks[i]);
            SetProperty(so, $"_level{n}CompletionCheck", checks[i]);
        }
        SetProperty(so, "_backButton", backButton);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------
    // Prefab
    // ------------------------------------------------------------------

    private static readonly string[] RequiredPrefabChildren = { "Label", "LockOverlay", "CompletionCheck" };

    private static GameObject CreateOrLoadLevelButtonPrefab()
    {
        EnsurePrefabFolder();

        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null)
        {
            if (HasRequiredChildren(existing))
            {
                return existing;
            }

            Debug.LogWarning($"[LevelSelectSceneBuilder] Existing prefab at {PrefabPath} is missing required children — regenerating.");
            AssetDatabase.DeleteAsset(PrefabPath);
        }

        var root = new GameObject("LevelButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));

        var rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(280f, 280f);

        var bg = root.GetComponent<Image>();
        bg.color = new Color(0.25f, 0.5f, 0.85f, 1f);

        var button = root.GetComponent<Button>();
        button.targetGraphic = bg;
        var colors = button.colors;
        colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        button.colors = colors;

        CreateLabel(root.transform, "0", fontSize: 140);
        CreateLockOverlay(root.transform);
        CreateCompletionCheck(root.transform);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static bool HasRequiredChildren(GameObject prefab)
    {
        foreach (var childName in RequiredPrefabChildren)
        {
            if (prefab.transform.Find(childName) == null)
            {
                return false;
            }
        }
        return true;
    }

    private static void EnsurePrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        if (!AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
        }
    }

    private static void CreateLabel(Transform parent, string content, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var text = go.GetComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.alignment = TextAnchor.MiddleCenter;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private static void CreateLockOverlay(Transform parent)
    {
        var go = new GameObject("LockOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.7f);
        image.raycastTarget = true; // Must block clicks on locked buttons

        go.SetActive(false);
    }

    private static void CreateCompletionCheck(Transform parent)
    {
        var go = new GameObject("CompletionCheck", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-10f, -10f);
        rect.sizeDelta = new Vector2(80f, 80f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.2f, 0.9f, 0.3f, 1f); // green placeholder
        image.raycastTarget = false;

        go.SetActive(false);
    }
}
#endif
