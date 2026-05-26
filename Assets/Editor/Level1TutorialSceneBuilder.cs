using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class Level1TutorialSceneBuilder
{
    private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
    private const string Level1ConfigPath = "Assets/ScriptableObjects/Levels/Level1_Config.asset";
    private const string TutorialSequencePath = "Assets/ScriptableObjects/Tutorial/Level1TutorialSequence.asset";

    [MenuItem("Salinlahi/Tutorial/Configure Level 1 Tutorial In Gameplay")]
    public static void ConfigureLevel1TutorialInGameplay()
    {
        if (!File.Exists(GameplayScenePath))
        {
            EditorUtility.DisplayDialog(
                "Level 1 Tutorial Gameplay Builder",
                $"Missing gameplay scene:\n{GameplayScenePath}",
                "OK");
            return;
        }

        bool proceed = EditorUtility.DisplayDialog(
            "Level 1 Tutorial Gameplay Builder",
            "This will open Gameplay.unity and add/update Level 1 tutorial wiring in the normal gameplay scene.",
            "Configure Gameplay",
            "Cancel");

        if (!proceed)
            return;

        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        RepairOpenScene(scene, false);
    }

    [MenuItem("Salinlahi/Tutorial/Repair Open Gameplay Tutorial Wiring")]
    public static void RepairOpenGameplayTutorialWiring()
    {
        Scene scene = EditorSceneManager.GetActiveScene();

        bool proceed = EditorUtility.DisplayDialog(
            "Repair Gameplay Tutorial Wiring",
            $"This will modify the currently open scene '{scene.name}' by adding/updating "
            + "tutorial controllers, UI, markers, and wiring.\n\n"
            + "Existing GameObjects may be recreated. Make sure your scene is saved or version-controlled.",
            "Proceed with Repair",
            "Cancel");

        if (!proceed)
            return;

        RepairOpenScene(scene, false);
    }

    private static void RepairOpenScene(Scene scene, bool createdScene)
    {
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Level1 Tutorial Scene Builder");

        try
        {
            RepairOpenSceneCore(scene, createdScene);
        }
        finally
        {
            Undo.CollapseUndoOperations(undoGroup);
        }
    }

    private static void RepairOpenSceneCore(Scene scene, bool createdScene)
    {
        EnsureDirectPlayManagers();

        Level1TutorialMarkerBuilder.ResolveSpawnPositions(out Vector3 leftSpawn, out Vector3 centerSpawn, out Vector3 rightSpawn);
        WaveSpawner spawner = EnsureWaveSpawner(leftSpawn, centerSpawn, rightSpawn);
        WaveManager waveManager = EnsureWaveManager(spawner);
        LevelFlowController flow = EnsureLevelFlowController(waveManager);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();

        Level1InteractiveTutorialController tutorial = EnsureTutorialController();
        Level1TutorialGuideUI guide = EnsureGuideUI(canvas);
        Level1TutorialMarkerBuilder.ResolveTutorialPositions(
            out Vector3 protagonistPosition,
            out Vector3 protagonistStartPosition,
            out Vector3 protagonistEndPosition,
            out Vector3 enemyStopPosition);
        Transform protagonist = Level1TutorialMarkerBuilder.EnsureMarker("Tutorial_Protagonist", protagonistPosition, true);
        Transform protagonistStart = Level1TutorialMarkerBuilder.EnsureMarker("Tutorial_Protagonist_Start", protagonistStartPosition, true);
        Transform protagonistEnd = Level1TutorialMarkerBuilder.EnsureMarker("Tutorial_Protagonist_End", protagonistEndPosition, true);
        Transform enemyStop = Level1TutorialMarkerBuilder.EnsureMarker("Tutorial_Enemy_Stop", enemyStopPosition, true);

        ConfigureTutorialController(
            tutorial,
            spawner,
            guide,
            protagonist,
            protagonistStart,
            protagonistEnd,
            enemyStop);

        AssignFlowController(flow, tutorial);
        ConfigureWaveManager(waveManager, spawner);
        EnsureSceneInBuildSettings(GameplayScenePath);

        ProtagonistAnimationSetup.SetupFromSceneBuilder();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = "Updated Level 1 tutorial wiring in Gameplay.";

        if (FindCharacter("HA") == null)
            message += "\n\nWarning: Char_HA was not found. Add or repair Char_HA before final QA.";

        EditorUtility.DisplayDialog("Level 1 Tutorial Gameplay Builder", message, "OK");
        Debug.Log($"[Salinlahi] {message}");
    }

    private static Level1InteractiveTutorialController EnsureTutorialController()
    {
        Level1InteractiveTutorialController existing =
            Object.FindFirstObjectByType<Level1InteractiveTutorialController>();
        if (existing != null)
            return existing;

        GameObject go = new("Level1InteractiveTutorialController");
        Undo.RegisterCreatedObjectUndo(go, "Create Tutorial Controller");
        return go.AddComponent<Level1InteractiveTutorialController>();
    }

    private static void EnsureDirectPlayManagers()
    {
        EnsureManagerPrefab<GameManager>("Assets/Prefabs/Managers/[Manager] GameManager.prefab");
        EnsureManagerPrefab<ActiveEnemyTracker>("Assets/Prefabs/Managers/[Manager] ActiveEnemyTracker.prefab");
        EnsureManagerPrefab<EnemyPool>("Assets/Prefabs/Managers/[Manager] EnemyPool.prefab");
        EnsureManagerPrefab<RecognitionManager>("Assets/Prefabs/Managers/[Manager] RecognitionManager.prefab");
        EnsureManagerPrefab<CombatResolver>("Assets/Prefabs/Managers/[Manager] CombatResolver.prefab");
        EnsureManagerPrefab<ComboManager>("Assets/Prefabs/Managers/[Manager] ComboManager.prefab");
        EnsureManagerPrefab<AudioManager>("Assets/Prefabs/Managers/[Manager] AudioManager.prefab");
    }

    private static WaveSpawner EnsureWaveSpawner(Vector3 leftSpawnPosition, Vector3 centerSpawnPosition, Vector3 rightSpawnPosition)
    {
        WaveSpawner existing = Object.FindFirstObjectByType<WaveSpawner>();
        if (existing == null)
        {
            GameObject go = new("[Manager] WaveSpawner");
            Undo.RegisterCreatedObjectUndo(go, "Create WaveSpawner");
            existing = go.AddComponent<WaveSpawner>();
        }

        Transform left = Level1TutorialMarkerBuilder.EnsureMarker("SpawnPoint_01", leftSpawnPosition, true);
        Transform center = Level1TutorialMarkerBuilder.EnsureMarker("SpawnPoint_02", centerSpawnPosition, true);
        Transform right = Level1TutorialMarkerBuilder.EnsureMarker("SpawnPoint_03", rightSpawnPosition, true);

        SerializedObject serialized = new(existing);
        SerializedProperty spawnPoints = serialized.FindProperty("_spawnPoints");
        spawnPoints.arraySize = 3;
        spawnPoints.GetArrayElementAtIndex(0).objectReferenceValue = left;
        spawnPoints.GetArrayElementAtIndex(1).objectReferenceValue = center;
        spawnPoints.GetArrayElementAtIndex(2).objectReferenceValue = right;
        serialized.FindProperty("_fallbackEnemyData").objectReferenceValue = FindEnemyData();
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static WaveManager EnsureWaveManager(WaveSpawner spawner)
    {
        WaveManager existing = Object.FindFirstObjectByType<WaveManager>();
        if (existing == null)
        {
            GameObject go = new("[Manager] WaveManager");
            Undo.RegisterCreatedObjectUndo(go, "Create WaveManager");
            existing = go.AddComponent<WaveManager>();
        }

        ConfigureWaveManager(existing, spawner);
        return existing;
    }

    private static LevelFlowController EnsureLevelFlowController(WaveManager waveManager)
    {
        LevelFlowController existing = Object.FindFirstObjectByType<LevelFlowController>();
        if (existing == null)
        {
            GameObject go = new("[Manager] LevelFlowController");
            Undo.RegisterCreatedObjectUndo(go, "Create LevelFlowController");
            existing = go.AddComponent<LevelFlowController>();
        }

        SerializedObject serialized = new(existing);
        serialized.FindProperty("_waveManager").objectReferenceValue = waveManager;
        serialized.FindProperty("_dialogueController").objectReferenceValue = Object.FindFirstObjectByType<DialogueController>();
        serialized.FindProperty("_tutorialOverlayController").objectReferenceValue = Object.FindFirstObjectByType<TutorialOverlayController>();
        serialized.FindProperty("_victoryScreen").objectReferenceValue = Object.FindFirstObjectByType<VictoryScreenUI>();
        serialized.FindProperty("_defeatScreen").objectReferenceValue = Object.FindFirstObjectByType<DefeatScreenUI>();

        LevelConfigSO levelConfig = LoadLevel1Config();
        if (levelConfig != null)
            serialized.FindProperty("_levelConfig").objectReferenceValue = levelConfig;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(existing);
        return existing;
    }

    private static void EnsureManagerPrefab<T>(string prefabPath) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null)
            return;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[Salinlahi] Level1TutorialSceneBuilder: Missing manager prefab at {prefabPath}.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = prefab.name;
        EditorUtility.SetDirty(instance);
    }

    private static Level1TutorialGuideUI EnsureGuideUI(Canvas canvas)
    {
        Level1TutorialGuideUI existing = Object.FindFirstObjectByType<Level1TutorialGuideUI>();
        if (existing != null)
        {
            Level1TutorialUIBuilder.RepairGuideTextPositions(existing.transform);
            return existing;
        }

        if (canvas == null)
        {
            GameObject canvasObject = new("TutorialCanvas");
            Undo.RegisterCreatedObjectUndo(canvasObject, "Create Tutorial Canvas");
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.AddComponent<CanvasScaler>();
            canvasObject.AddComponent<GraphicRaycaster>();
        }

        GameObject root = new("Level1TutorialGuideUI");
        Undo.RegisterCreatedObjectUndo(root, "Create Tutorial Guide UI");
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Level1TutorialGuideUI guide = root.AddComponent<Level1TutorialGuideUI>();

        TextMeshProUGUI prompt = Level1TutorialUIBuilder.CreateText(root.transform, "PromptText", new Vector2(0.5f, 0.32f), 42, TextAlignmentOptions.Center);
        TextMeshProUGUI feedback = Level1TutorialUIBuilder.CreateText(root.transform, "FeedbackText", new Vector2(0.5f, 0.24f), 32, TextAlignmentOptions.Center);
        Button skip = Level1TutorialUIBuilder.CreateButton(root.transform);

        Image guideSpriteImage = Level1TutorialUIBuilder.CreateGuideSpriteImage(root.transform);
        LineRenderer guidePath = Level1TutorialUIBuilder.EnsureGuidePathRenderer(root.transform);
        Transform startDot = Level1TutorialUIBuilder.EnsureGuideDot(root.transform, "StartDot", Color.green);
        Transform directionArrow = Level1TutorialUIBuilder.EnsureGuideArrow(root.transform);
        GameObject assistParent = new("AssistAnimationParent");
        Undo.RegisterCreatedObjectUndo(assistParent, "Create Assist Animation Parent");
        assistParent.transform.SetParent(root.transform, false);
        RectTransform assistRect = assistParent.AddComponent<RectTransform>();
        assistRect.anchorMin = new Vector2(0.5f, 0.5f);
        assistRect.anchorMax = new Vector2(0.5f, 0.5f);
        assistRect.pivot = new Vector2(0.5f, 0.5f);
        assistRect.sizeDelta = Vector2.zero;
        assistRect.anchoredPosition = Vector2.zero;

        SerializedObject serialized = new(guide);
        serialized.FindProperty("_root").objectReferenceValue = root;
        serialized.FindProperty("_promptText").objectReferenceValue = prompt;
        serialized.FindProperty("_feedbackText").objectReferenceValue = feedback;
        serialized.FindProperty("_skipButton").objectReferenceValue = skip;
        serialized.FindProperty("_guideSpriteImage").objectReferenceValue = guideSpriteImage;
        serialized.FindProperty("_guidePathRenderer").objectReferenceValue = guidePath;
        serialized.FindProperty("_startDot").objectReferenceValue = startDot;
        serialized.FindProperty("_directionArrow").objectReferenceValue = directionArrow;
        serialized.FindProperty("_assistAnimationParent").objectReferenceValue = assistParent.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return guide;
    }

    private static void ConfigureTutorialController(
        Level1InteractiveTutorialController tutorial,
        WaveSpawner spawner,
        Level1TutorialGuideUI guide,
        Transform protagonist,
        Transform protagonistStart,
        Transform protagonistEnd,
        Transform enemyStop)
    {
        SerializedObject serialized = new(tutorial);
        serialized.FindProperty("_waveSpawner").objectReferenceValue = spawner;
        serialized.FindProperty("_fallbackTutorialEnemyData").objectReferenceValue = FindEnemyData();
        serialized.FindProperty("_guideUI").objectReferenceValue = guide;
        serialized.FindProperty("_protagonistWalkSeconds").floatValue = 1.75f;
        ConfigureHiddenDuringTutorial(serialized.FindProperty("_hideDuringTutorial"));

        Level1TutorialSequenceSO sequence = AssetDatabase.LoadAssetAtPath<Level1TutorialSequenceSO>(TutorialSequencePath);
        if (sequence != null)
        {
            serialized.FindProperty("_sequence").objectReferenceValue = sequence;
            Debug.Log("[Salinlahi] Level1TutorialSceneBuilder: Linked Level1TutorialSequence.asset.");

            if (enemyStop != null && sequence.steps != null)
            {
                for (int i = 0; i < sequence.steps.Length; i++)
                {
                    if (sequence.steps[i] == null) continue;
                    SerializedObject stepSerialized = new SerializedObject(sequence.steps[i]);
                    stepSerialized.FindProperty("enemyData").objectReferenceValue = FindEnemyData();
                    stepSerialized.FindProperty("stopPosition").vector3Value = enemyStop.position;
                    stepSerialized.FindProperty("promptFreezeDelaySeconds").floatValue = GetPromptFreezeDelay(sequence.steps[i].promptId);
                    SetTemplatePoints(stepSerialized.FindProperty("templatePoints"), sequence.steps[i].promptId);
                    stepSerialized.FindProperty("guideSprite").objectReferenceValue = FindGuideSprite(sequence.steps[i].promptId);
                    stepSerialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(sequence.steps[i]);
                }
                Debug.Log($"[Salinlahi] Level1TutorialSceneBuilder: Set stopPosition to {enemyStop.position} for all {sequence.steps.Length} tutorial steps.");
            }
        }
        else
        {
            Debug.LogWarning("[Salinlahi] Level1TutorialSceneBuilder: Level1TutorialSequence.asset not found. Falling back to inline steps.");
            ConfigureSteps(serialized.FindProperty("_steps"));
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(tutorial);
    }

    private static void ConfigureSteps(SerializedProperty steps)
    {
        string[] ids = { "BA", "SA", "LA", "HA" };
        string[] prompts =
        {
            "Draw BA. Start at the dot.",
            "Now draw SA.",
            "Draw LA next.",
            "Last one. Draw HA."
        };
        string[] success =
        {
            "Great job. Drawing protects the base.",
            "",
            "",
            ""
        };

        steps.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++)
        {
            SerializedProperty step = steps.GetArrayElementAtIndex(i);
            step.FindPropertyRelative("promptId").stringValue = ids[i];
            step.FindPropertyRelative("targetCharacter").objectReferenceValue = FindCharacter(ids[i]);
            step.FindPropertyRelative("enemyData").objectReferenceValue = FindEnemyData();
            step.FindPropertyRelative("guideSprite").objectReferenceValue = FindGuideSprite(ids[i]);
            step.FindPropertyRelative("tolerancePixels").floatValue = 15f;
            step.FindPropertyRelative("promptFreezeDelaySeconds").floatValue = GetPromptFreezeDelay(ids[i]);
            step.FindPropertyRelative("promptText").stringValue = prompts[i];
            step.FindPropertyRelative("successText").stringValue = success[i];
            step.FindPropertyRelative("idleHint").stringValue = "Trace the glowing guide.";
            step.FindPropertyRelative("strongHint").stringValue = "Start at the dot, then follow the arrow.";
            SetTemplatePoints(step.FindPropertyRelative("templatePoints"), ids[i]);
        }
    }

    private static float GetPromptFreezeDelay(string promptId)
    {
        return promptId == "BA" ? 2.2f : 2.05f;
    }

    private static void ConfigureHiddenDuringTutorial(SerializedProperty hiddenObjects)
    {
        GameObject[] targets =
        {
            GameObject.Find("HeartsPanel"),
            GameObject.Find("WaveText"),
            GameObject.Find("ComboText"),
            GameObject.Find("GlyphCounter")
        };

        hiddenObjects.arraySize = targets.Length;
        for (int i = 0; i < targets.Length; i++)
            hiddenObjects.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
    }

    private static void SetTemplatePoints(SerializedProperty points, string id)
    {
        Vector2[] template = id switch
        {
            "SA" => new[] { new Vector2(0f, 0f), new Vector2(48f, 0f), new Vector2(48f, 100f), new Vector2(128f, 86f) },
            "LA" => new[] { new Vector2(0f, 30f), new Vector2(55f, 12f), new Vector2(105f, 30f), new Vector2(72f, 120f) },
            "HA" => new[] { new Vector2(0f, 36f), new Vector2(48f, 24f), new Vector2(96f, 36f) },
            _ => new[] { new Vector2(0f, 50f), new Vector2(40f, 0f), new Vector2(88f, 50f), new Vector2(40f, 92f) }
        };

        points.arraySize = template.Length;
        for (int i = 0; i < template.Length; i++)
            points.GetArrayElementAtIndex(i).vector2Value = template[i];
    }

    private static void AssignFlowController(LevelFlowController flow, Level1InteractiveTutorialController tutorial)
    {
        SerializedObject serialized = new(flow);
        serialized.FindProperty("_level1InteractiveTutorialController").objectReferenceValue = tutorial;

        WaveManager waveManager = Object.FindFirstObjectByType<WaveManager>();
        if (waveManager != null)
            serialized.FindProperty("_waveManager").objectReferenceValue = waveManager;

        LevelConfigSO levelConfig = LoadLevel1Config();
        if (levelConfig != null)
            serialized.FindProperty("_levelConfig").objectReferenceValue = levelConfig;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(flow);
    }

    private static void ConfigureWaveManager(WaveManager waveManager, WaveSpawner spawner)
    {
        SerializedObject serialized = new(waveManager);
        serialized.FindProperty("_waitForExternalStart").boolValue = true;
        serialized.FindProperty("_spawner").objectReferenceValue = spawner;

        LevelConfigSO levelConfig = LoadLevel1Config();
        if (levelConfig != null)
            serialized.FindProperty("_levelConfig").objectReferenceValue = levelConfig;

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(waveManager);
    }

    private static LevelConfigSO LoadLevel1Config()
    {
        return AssetDatabase.LoadAssetAtPath<LevelConfigSO>(Level1ConfigPath);
    }

    private static void ResolveSpawnPositions(out Vector3 left, out Vector3 center, out Vector3 right)
    {
        Level1TutorialMarkerBuilder.ResolveSpawnPositions(out left, out center, out right);
    }

    private static BaybayinCharacterSO FindCharacter(string id)
    {
        string[] guids = AssetDatabase.FindAssets($"Char_{id} t:BaybayinCharacterSO", new[] { "Assets/ScriptableObjects/Characters" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BaybayinCharacterSO character = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(path);
            if (character != null && character.characterID == id)
                return character;
        }

        return null;
    }

    private static Sprite FindGuideSprite(string id)
    {
        BaybayinCharacterSO character = FindCharacter(id);
        return character != null ? character.displaySprite : null;
    }

    private static EnemyDataSO FindEnemyData()
    {
        EnemyDataSO soldado = AssetDatabase.LoadAssetAtPath<EnemyDataSO>("Assets/ScriptableObjects/EnemyData_Soldado.asset");
        if (soldado != null)
            return soldado;

        string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO", new[] { "Assets/ScriptableObjects" });
        if (guids.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    private static void EnsureSceneInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (scene.path == scenePath)
                return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[updated.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }
}
