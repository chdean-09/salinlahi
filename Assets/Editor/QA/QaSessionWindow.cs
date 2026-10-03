using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Editor-only entry point for repeatable campaign QA. It loads the real authored scene and
/// selects levels through GameManager and SceneLoader, while QaSessionContext redirects save
/// writes and one-time tutorial state into an isolated EditorPrefs profile.
/// </summary>
public sealed class QaSessionWindow : EditorWindow
{
    private const string BootstrapScenePath = "Assets/_Scenes/Bootstrap.unity";
    private const string GameplayScenePath = "Assets/_Scenes/Gameplay.unity";
    private const string LevelOneTutorialScenePath = "Assets/_Scenes/Level_01_Tutorial.unity";
    private const string PreviousStartSceneKey = "Salinlahi.QA.PreviousPlayModeStartScene";

    private Vector2 _scroll;
    private int _selectedIndex;
    private int _selectedButtonIndex;
    private string _screenshotSlug = "issue";
    private string _status = "Select a level to inspect its live configuration or start a QA run.";
    private LevelConfigSO[] _levels = Array.Empty<LevelConfigSO>();

    [MenuItem("Salinlahi/QA/Session _F12")]
    public static void Open()
    {
        QaSessionWindow window = GetWindow<QaSessionWindow>("QA Session");
        window.minSize = new Vector2(390f, 560f);
        window.Show();
    }

    [InitializeOnLoadMethod]
    private static void RestoreForActiveQaRun()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlaying && QaSessionContext.IsActive)
                Open();
        };
    }

    private void OnEnable()
    {
        LoadLevels();
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.update -= Repaint;
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("Campaign QA Session", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "QA progress is kept in an isolated EditorPrefs profile and the campaign save coordinator "
            + "uses in-memory storage. Player saves and PlayerPrefs remain untouched.",
            MessageType.Info);

        if (_levels.Length == 0)
        {
            EditorGUILayout.HelpBox("No LevelConfigSO assets were found under Assets/ScriptableObjects/Levels.", MessageType.Error);
            if (GUILayout.Button("Refresh levels")) LoadLevels();
            EditorGUILayout.EndScrollView();
            return;
        }

        _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _levels.Length - 1);
        _selectedIndex = EditorGUILayout.Popup("Level", _selectedIndex, _levels.Select(LevelLabel).ToArray());
        LevelConfigSO level = _levels[_selectedIndex];

        EditorGUILayout.Space(5f);
        DrawConfiguration(level);

        EditorGUILayout.Space(8f);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            if (GUILayout.Button("Load Level Scene + Capture Baseline", GUILayout.Height(30f)))
                LoadSceneAndCaptureBaseline(level);

            if (GUILayout.Button("Start Selected Level in Play Mode", GUILayout.Height(34f)))
                StartSelectedLevel(level);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Start New QA Campaign"))
            {
                QaSessionContext.ClearCampaign();
                QaSessionContext.StartNewCampaign();
                _status = "Started a fresh isolated QA progress profile.";
            }
            if (GUILayout.Button("Clear QA Campaign"))
            {
                QaSessionContext.ClearCampaign();
                _status = "Cleared isolated QA progress. Player progress was not changed.";
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(8f);
        DrawRuntimeControls(level);
        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(_status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawConfiguration(LevelConfigSO level)
    {
        EditorGUILayout.LabelField("Live Level Asset", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Title", $"{level.chapterName} / {level.levelNumber}: {level.levelName}");
        EditorGUILayout.LabelField("Asset", AssetDatabase.GetAssetPath(level));

        string glyphs = level.allowedCharacters == null
            ? "None configured"
            : string.Join(", ", level.allowedCharacters.Where(x => x != null).Select(x => x.characterID));
        EditorGUILayout.LabelField("Permitted glyphs", glyphs, EditorStyles.wordWrappedLabel);

        var enemyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (level.allowedEnemyTypes != null)
            foreach (EnemyDataSO enemy in level.allowedEnemyTypes)
                if (enemy != null) enemyNames.Add(EnemyTypeName(enemy));
        if (level.waves != null)
            foreach (WaveDefinition wave in level.waves)
                if (wave?.enemyTypes != null)
                    foreach (EnemyDataSO enemy in wave.enemyTypes)
                        if (enemy != null) enemyNames.Add(EnemyTypeName(enemy));
        if (level.bossConfig != null)
        {
            if (level.bossConfig.bossEnemyData != null) enemyNames.Add(EnemyTypeName(level.bossConfig.bossEnemyData));
            if (level.bossConfig.phases != null)
                foreach (BossPhase phase in level.bossConfig.phases)
                    if (phase?.summonEnemyTypes != null)
                        foreach (EnemyDataSO enemy in phase.summonEnemyTypes)
                            if (enemy != null) enemyNames.Add(EnemyTypeName(enemy));
        }
        EditorGUILayout.LabelField("Configured enemy types", enemyNames.Count > 0
            ? string.Join(", ", enemyNames.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            : "None authored", EditorStyles.wordWrappedLabel);

        string targets = level.focusWords == null
            ? "No focus words"
            : string.Join(" · ", level.focusWords.Where(x => x != null).Select(x => x.displayLabel));
        EditorGUILayout.LabelField("Target text", targets, EditorStyles.wordWrappedLabel);
        EditorGUILayout.LabelField("Authored waves", level.waves != null ? level.waves.Count.ToString() : "0 / generated");
        EditorGUILayout.LabelField("Flow segments", level.flowSegments != null ? level.flowSegments.Count.ToString() : "0 (unsegmented)");
        if (level.flowSegments != null && level.flowSegments.Count > 0)
            for (int i = 0; i < level.flowSegments.Count; i++)
            {
                LevelFlowSegment segment = level.flowSegments[i];
                EditorGUILayout.LabelField($"  Segment {i + 1}",
                    $"{segment.waveCount} waves → {string.Join(", ", segment.challengeUnitIds ?? Array.Empty<string>())}");
            }
    }

    private void DrawRuntimeControls(LevelConfigSO level)
    {
        EditorGUILayout.LabelField("Play Mode QA Controls", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Start a QA level to enable cutscene, input, and stroke diagnostics.", MessageType.Info);
            return;
        }

        EventSystem eventSystem = EventSystem.current;
        Mouse mouse = Mouse.current;
        Vector2 position = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
        string currentState = GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "GameManager unavailable";
        CutscenePlayer cutscene = FindFirstObjectByType<CutscenePlayer>(FindObjectsInactive.Include);
        EditorGUILayout.LabelField("Runtime", $"{SceneManager.GetActiveScene().name} · {currentState}");
        EditorGUILayout.LabelField("Mouse", mouse != null
            ? $"{mouse.name} · position {position.x:F0}, {position.y:F0} · left {(mouse.leftButton.isPressed ? "down" : "up")}"
            : "No Mouse device reported by Input System");
        EditorGUILayout.LabelField("EventSystem", eventSystem != null
            ? $"{eventSystem.name} · module {(eventSystem.currentInputModule != null ? eventSystem.currentInputModule.GetType().Name : "none")}"
            : "No active EventSystem");
        EditorGUILayout.LabelField("Cutscene", cutscene != null && cutscene.IsPlaying ? "Active" : "Inactive");

        DrawActiveUiButtonInvoker();

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(cutscene == null || !cutscene.IsPlaying))
        {
            if (GUILayout.Button("Advance Cutscene Panel"))
            {
                bool advanced = cutscene.TryAdvanceFromQa();
                QaSessionContext.Record("qa-control", "cutscene advance requested through OnTap callback; result=" + advanced);
                _status = advanced ? "Called the same OnTap handler used by the cutscene Button." : "No active cutscene panel.";
            }
        }
        if (GUILayout.Button("Record UI Raycast"))
        {
            _status = CaptureRaycastSnapshot();
            QaSessionContext.Record("qa-input-snapshot", _status);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Replay Current Clue")) ReplayCurrentClue();
        if (GUILayout.Button("Submit Miss")) SubmitMiss();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField("Recorded stroke fixtures", EditorStyles.boldLabel);
        IReadOnlyList<string> ids = QaStrokeSampleLibrary.SupportedCharacterIds;
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (GUILayout.Button(id == "EI" ? "E / I" : id)) ReplaySymbol(id);
        }

        EditorGUILayout.BeginHorizontal();
        _screenshotSlug = EditorGUILayout.TextField("Screenshot slug", _screenshotSlug);
        if (GUILayout.Button("Capture", GUILayout.Width(80f)))
        {
            string path = QaScreenshotCapture.Capture(level.levelNumber, _screenshotSlug);
            _status = path != null ? "Capturing: " + path : "Screenshot request failed.";
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        bool protection = EditorGUILayout.ToggleLeft(
            "QA protection: freeze enemies and block base damage",
            Salinlahi.Debug.Sandbox.SandboxMode.IsQaProtectionEnabled);
        if (EditorGUI.EndChangeCheck())
            Salinlahi.Debug.Sandbox.SandboxMode.SetQaProtectionEnabled(protection);

        EditorGUILayout.LabelField("Recorded events", QaSessionContext.EventTrace.Split('\n').Length.ToString());
    }

    private void DrawActiveUiButtonInvoker()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Where(button => button != null && button.gameObject.activeInHierarchy && button.IsInteractable())
            .OrderBy(button => GetHierarchyPath(button.transform), StringComparer.Ordinal)
            .ToArray();

        EditorGUILayout.LabelField("Active UI callback", EditorStyles.boldLabel);
        if (buttons.Length == 0)
        {
            EditorGUILayout.HelpBox("No active, interactable UI Buttons were found.", MessageType.Info);
            return;
        }

        string[] labels = buttons.Select(button =>
            $"{GetHierarchyPath(button.transform)} ({button.onClick.GetPersistentEventCount()} persistent listener(s))")
            .ToArray();
        _selectedButtonIndex = Mathf.Clamp(_selectedButtonIndex, 0, buttons.Length - 1);
        _selectedButtonIndex = EditorGUILayout.Popup("Button", _selectedButtonIndex, labels);
        EditorGUILayout.HelpBox(
            "This sends a QA-only pointer-click event to the selected Button and runs its registered callback. It does not test physical mouse delivery.",
            MessageType.Warning);

        if (GUILayout.Button("Invoke Selected Button Pointer Callback"))
        {
            Button button = buttons[_selectedButtonIndex];
            EventSystem eventSystem = EventSystem.current;
            if (button == null || !button.gameObject.activeInHierarchy || !button.IsInteractable() || eventSystem == null)
            {
                _status = "The selected Button or active EventSystem is no longer available.";
                return;
            }

            var eventData = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerPress = button.gameObject,
                pointerClick = button.gameObject
            };
            bool handled = ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerClickHandler);
            string path = GetHierarchyPath(button.transform);
            QaSessionContext.Record("qa-ui-callback", $"button={path}; programmatic pointer click; handled={handled}; hardware input not tested");
            _status = handled
                ? $"Invoked the registered pointer-click handler for {path}. Physical input remains unverified."
                : $"No pointer-click handler ran for {path}.";
        }
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var names = new Stack<string>();
        while (transform != null)
        {
            names.Push(transform.name);
            transform = transform.parent;
        }
        return string.Join("/", names);
    }

    private void LoadSceneAndCaptureBaseline(LevelConfigSO level)
    {
        string scenePath = level.levelNumber == 1 ? LevelOneTutorialScenePath : GameplayScenePath;
        try
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            _status = $"Loaded {scenePath} in Edit Mode. Capturing the baseline image.";
            EditorApplication.delayCall += () =>
            {
                string path = QaScreenshotCapture.Capture(level.levelNumber, "baseline");
                _status = path != null ? "Baseline capture requested: " + path : "Baseline capture failed.";
                Repaint();
            };
        }
        catch (Exception exception)
        {
            _status = $"Could not load {scenePath}: {exception.Message}";
            Debug.LogException(exception);
        }
    }

    private void StartSelectedLevel(LevelConfigSO level)
    {
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath))
        {
            _status = "Bootstrap scene is missing; QA Play Mode was not started.";
            return;
        }

        if (string.IsNullOrWhiteSpace(QaSessionContext.ProfileId))
            QaSessionContext.StartNewCampaign();
        QaSessionContext.BeginLevel(level.levelNumber, AssetDatabase.GetAssetPath(level));

        SceneAsset previousStartScene = EditorSceneManager.playModeStartScene;
        EditorPrefs.SetString(PreviousStartSceneKey,
            previousStartScene != null ? AssetDatabase.GetAssetPath(previousStartScene) : string.Empty);
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath);
        _status = $"Starting isolated QA run for Level {level.levelNumber} from Bootstrap.";
        EditorApplication.isPlaying = true;
    }

    private void LoadLevels()
    {
        _levels = AssetDatabase.FindAssets("t:LevelConfigSO", new[] { "Assets/ScriptableObjects/Levels" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<LevelConfigSO>)
            .Where(x => x != null)
            .OrderBy(x => x.levelNumber)
            .ToArray();
        _selectedIndex = Array.FindIndex(_levels, x => x.levelNumber == QaSessionContext.SelectedLevelNumber);
        if (_selectedIndex < 0) _selectedIndex = 0;
    }

    private static string LevelLabel(LevelConfigSO level)
    {
        return $"Level {level.levelNumber}: {level.levelName}";
    }

    private static string EnemyTypeName(EnemyDataSO enemy)
    {
        if (enemy == null) return "UNKNOWN";
        if (!string.IsNullOrWhiteSpace(enemy.displayName)) return enemy.displayName;
        return string.IsNullOrWhiteSpace(enemy.enemyID) ? enemy.name : enemy.enemyID;
    }

    private void ReplayCurrentClue()
    {
        Enemy clue = ActiveClueDirector.Instance != null ? ActiveClueDirector.Instance.CurrentClue : null;
        ReplaySymbol(clue?.Character?.characterID);
    }

    private void ReplaySymbol(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            _status = "No current clue glyph was found.";
            return;
        }
        if (!QaStrokeSampleLibrary.TryLoadStrokes(id, out List<List<Vector2>> strokes, out string error))
        {
            _status = error;
            return;
        }
        if (RecognitionManager.Instance == null)
        {
            _status = "RecognitionManager is not active in Play Mode.";
            return;
        }

        RecognitionManager.Instance.Recognize(strokes);
        QaSessionContext.Record("qa-control", "replayed fixture glyph=" + id);
        _status = "Submitted the stored " + id + " stroke fixture through RecognitionManager.";
    }

    private void SubmitMiss()
    {
        if (RecognitionManager.Instance == null)
        {
            _status = "RecognitionManager is not active in Play Mode.";
            return;
        }

        RecognitionManager.Instance.Recognize(QaStrokeSampleLibrary.BuildMissSample());
        QaSessionContext.Record("qa-control", "submitted deliberate invalid stroke sample");
        _status = "Submitted a deliberately incorrect stroke sample.";
    }

    private static string CaptureRaycastSnapshot()
    {
        Mouse mouse = Mouse.current;
        EventSystem eventSystem = EventSystem.current;
        if (mouse == null || eventSystem == null)
            return $"mouse={(mouse != null ? mouse.name : "missing")}; EventSystem={(eventSystem != null ? eventSystem.name : "missing")}";

        Vector2 position = mouse.position.ReadValue();
        var eventData = new PointerEventData(eventSystem) { position = position };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(eventData, results);
        string hits = results.Count == 0
            ? "none"
            : string.Join(" | ", results.Take(5).Select(x => x.gameObject.name));
        GraphicRaycaster[] raycasters = FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None);
        return $"mouse={mouse.name}; position=({position.x:F0},{position.y:F0}); "
            + $"leftDown={mouse.leftButton.isPressed}; pointerOverUI={eventSystem.IsPointerOverGameObject()}; "
            + $"raycasters={raycasters.Length}; hits={hits}";
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            ExportRuntimeTrace();
            RestorePlayModeStartScene();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Salinlahi.Debug.Sandbox.SandboxMode.SetQaProtectionEnabled(false);
        }
    }

    private static void RestorePlayModeStartScene()
    {
        if (!EditorPrefs.HasKey(PreviousStartSceneKey)) return;
        string path = EditorPrefs.GetString(PreviousStartSceneKey, string.Empty);
        EditorSceneManager.playModeStartScene = string.IsNullOrWhiteSpace(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        EditorPrefs.DeleteKey(PreviousStartSceneKey);
    }

    private static void ExportRuntimeTrace()
    {
        if (!QaSessionContext.IsActive)
            return;

        int number = QaSessionContext.SelectedLevelNumber;
        string trace = QaSessionContext.EventTrace;
        QaSessionContext.EndLevel();
        if (number < 1 || number > 15 || string.IsNullOrWhiteSpace(trace))
            return;

        string root = Directory.GetParent(Application.dataPath).FullName;
        string reportPath = Path.Combine(root, "QA", $"level-{number}-report.md");
        if (!File.Exists(reportPath))
            return;

        string heading = "## QA Runtime Event Trace — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        var output = new StringBuilder();
        output.AppendLine();
        output.AppendLine(heading);
        output.AppendLine();
        output.AppendLine("Events recorded by the Editor QA session. Timestamps are UTC ISO-8601 and seconds since Play Mode started.");
        output.AppendLine();
        output.AppendLine("```text");
        output.AppendLine(trace);
        output.AppendLine("```");
        File.AppendAllText(reportPath, output.ToString());
        Debug.Log($"[QA] Appended {trace.Split('\n').Length} runtime event(s) to {reportPath}");
    }
}

[InitializeOnLoad]
internal static class QaSessionEditorHooks
{
    private static bool _eventsSubscribed;
    private static bool _levelEntryRequested;
    private static bool _lastMousePressed;
    private static double _entryRetryAt;
    private static int _entryAttemptCount;

    static QaSessionEditorHooks()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        EditorApplication.update += Tick;
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        _levelEntryRequested = false;
        _entryRetryAt = 0d;
        _entryAttemptCount = 0;
        if (state == PlayModeStateChange.EnteredPlayMode)
            QaSessionContext.BeginPlayTiming();
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode)
            UnsubscribeEvents();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !QaSessionContext.IsActive)
        {
            UnsubscribeEvents();
            return;
        }

        SubscribeEvents();
        TryEnterSelectedLevel();

        Mouse mouse = Mouse.current;
        bool pressed = mouse != null && mouse.leftButton.wasPressedThisFrame;
        if (pressed && !_lastMousePressed)
            RecordMouseClick(mouse);
        _lastMousePressed = pressed;
    }

    private static void TryEnterSelectedLevel()
    {
        if (GameManager.Instance == null || SceneLoader.Instance == null)
            return;

        // BootstrapLoader still owns the first MainMenu load immediately after the active
        // scene changes. Do not let QA race its fade coroutine: SceneLoader ignores concurrent
        // requests, so the previous implementation marked the entry as requested even though
        // LoadLevelOneTutorial/LoadGameplay had been rejected.
        if (SceneLoader.Instance.IsLoading && !_levelEntryRequested)
            return;

        if (_levelEntryRequested)
        {
            if (SceneManager.GetActiveScene().name != "MainMenu" || SceneLoader.Instance.IsLoading)
                return;
            if (EditorApplication.timeSinceStartup < _entryRetryAt)
                return;

            QaSessionContext.Record("qa-access", $"scene entry attempt {_entryAttemptCount} returned to MainMenu; retrying");
            _levelEntryRequested = false;
        }

        if (_entryAttemptCount >= 3)
        {
            QaSessionContext.Record("qa-access", "scene entry stopped after three attempts; inspect the Console and current UI");
            _levelEntryRequested = true;
            return;
        }

        if (!QaSessionContext.TryGetSelectedLevel(out LevelConfigSO level))
        {
            QaSessionContext.Record("qa-access", "selected LevelConfigSO could not be loaded");
            return;
        }

        if (SceneManager.GetActiveScene().name != "MainMenu")
            return;

        GameManager.Instance.DiscardPausedRunSnapshot();
        GameManager.Instance.SetLevel(level);
        bool selected = ProgressManager.Instance != null
            && ProgressManager.Instance.TrySetSelectedLevel(level);
        QaSessionContext.Record("qa-entry", $"level={level.levelNumber}; normal selection accepted={selected}");
        _levelEntryRequested = true;
        _entryAttemptCount++;
        _entryRetryAt = EditorApplication.timeSinceStartup + 5d;

        if (level.levelNumber == 1)
            SceneLoader.Instance.LoadLevelOneTutorial();
        else
            SceneLoader.Instance.LoadGameplay();
    }

    private static void RecordMouseClick(Mouse mouse)
    {
        EventSystem eventSystem = EventSystem.current;
        Vector2 position = mouse.position.ReadValue();
        string hits = "no active EventSystem";
        if (eventSystem != null)
        {
            var eventData = new PointerEventData(eventSystem) { position = position };
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
            hits = results.Count == 0
                ? "no UI raycast hits"
                : string.Join(" | ", results.Take(5).Select(x => x.gameObject.name));
        }

        QaSessionContext.Record("input-mouse", $"left click position=({position.x:F0},{position.y:F0}); {hits}");
    }

    private static void SubscribeEvents()
    {
        if (_eventsSubscribed) return;
        EventBus.OnRecognitionResolved += OnRecognitionResolved;
        EventBus.OnDrawingMissed += OnDrawingMissed;
        EventBus.OnDrawingFailed += OnDrawingFailed;
        EventBus.OnEnemyTargeted += OnEnemyTargeted;
        EventBus.OnSingleAttackHit += OnSingleAttackHit;
        EventBus.OnChainAttackHit += OnChainAttackHit;
        EventBus.OnAOETriggered += OnAoeTriggered;
        EventBus.OnEnemyDefeated += OnEnemyDefeated;
        EventBus.OnBaseHit += OnBaseHit;
        EventBus.OnBaseDamageApplied += OnBaseDamageApplied;
        EventBus.OnHeartsChanged += OnHeartsChanged;
        EventBus.OnWaveStarted += OnWaveStarted;
        EventBus.OnWaveCleared += OnWaveCleared;
        EventBus.OnFocusWordRestorationComplete += OnInstantWin;
        EventBus.OnLevelComplete += OnLevelComplete;
        EventBus.OnGameOver += OnGameOver;
        EventBus.OnCutsceneStarted += OnCutsceneStarted;
        EventBus.OnCutsceneComplete += OnCutsceneComplete;
        EventBus.OnBossStarted += OnBossStarted;
        EventBus.OnBossPhaseStarted += OnBossPhaseStarted;
        EventBus.OnBossDamaged += OnBossDamaged;
        EventBus.OnBossDefeated += OnBossDefeated;
        _eventsSubscribed = true;
    }

    private static void UnsubscribeEvents()
    {
        if (!_eventsSubscribed) return;
        EventBus.OnRecognitionResolved -= OnRecognitionResolved;
        EventBus.OnDrawingMissed -= OnDrawingMissed;
        EventBus.OnDrawingFailed -= OnDrawingFailed;
        EventBus.OnEnemyTargeted -= OnEnemyTargeted;
        EventBus.OnSingleAttackHit -= OnSingleAttackHit;
        EventBus.OnChainAttackHit -= OnChainAttackHit;
        EventBus.OnAOETriggered -= OnAoeTriggered;
        EventBus.OnEnemyDefeated -= OnEnemyDefeated;
        EventBus.OnBaseHit -= OnBaseHit;
        EventBus.OnBaseDamageApplied -= OnBaseDamageApplied;
        EventBus.OnHeartsChanged -= OnHeartsChanged;
        EventBus.OnWaveStarted -= OnWaveStarted;
        EventBus.OnWaveCleared -= OnWaveCleared;
        EventBus.OnFocusWordRestorationComplete -= OnInstantWin;
        EventBus.OnLevelComplete -= OnLevelComplete;
        EventBus.OnGameOver -= OnGameOver;
        EventBus.OnCutsceneStarted -= OnCutsceneStarted;
        EventBus.OnCutsceneComplete -= OnCutsceneComplete;
        EventBus.OnBossStarted -= OnBossStarted;
        EventBus.OnBossPhaseStarted -= OnBossPhaseStarted;
        EventBus.OnBossDamaged -= OnBossDamaged;
        EventBus.OnBossDefeated -= OnBossDefeated;
        _eventsSubscribed = false;
    }

    private static void OnRecognitionResolved(RecognitionResult result, bool passed, float threshold)
        => QaSessionContext.Record("recognition", $"glyph={result.characterID}; score={result.score:F3}; threshold={threshold:F3}; passed={passed}");
    private static void OnDrawingMissed() => QaSessionContext.Record("combat", "drawing-missed");
    private static void OnDrawingFailed() => QaSessionContext.Record("recognition", "drawing-failed");
    private static void OnEnemyTargeted(Enemy enemy) => QaSessionContext.Record("target", DescribeEnemy(enemy));
    private static void OnSingleAttackHit(Enemy enemy) => QaSessionContext.Record("combat-hit", DescribeEnemy(enemy));
    private static void OnChainAttackHit(IReadOnlyList<Enemy> enemies)
        => QaSessionContext.Record("combat-chain", "count=" + (enemies != null ? enemies.Count : 0));
    private static void OnAoeTriggered(int count) => QaSessionContext.Record("combat-aoe", "defeated=" + count);
    private static void OnEnemyDefeated(BaybayinCharacterSO character)
        => QaSessionContext.Record("enemy-defeated", character != null ? character.characterID : "UNKNOWN");
    private static void OnBaseHit(int damage) => QaSessionContext.Record("base-hit", "announced damage=" + damage);
    private static void OnBaseDamageApplied(int amount) => QaSessionContext.Record("base-damage", "applied=" + amount);
    private static void OnHeartsChanged(int hearts) => QaSessionContext.Record("base-hp", "hearts=" + hearts);
    private static void OnWaveStarted(int index) => QaSessionContext.Record("wave-start", "index=" + index);
    private static void OnWaveCleared(int index) => QaSessionContext.Record("wave-clear", "index=" + index);
    private static void OnInstantWin() => QaSessionContext.Record("outcome", "focus-word-restoration-complete");
    private static void OnLevelComplete() => QaSessionContext.Record("outcome", "level-complete");
    private static void OnGameOver() => QaSessionContext.Record("outcome", "game-over");
    private static void OnCutsceneStarted() => QaSessionContext.Record("cutscene", "started");
    private static void OnCutsceneComplete() => QaSessionContext.Record("cutscene", "completed");
    private static void OnBossStarted(BossConfigSO config) => QaSessionContext.Record("boss", "started=" + (config != null ? config.bossName : "UNKNOWN"));
    private static void OnBossPhaseStarted(int index) => QaSessionContext.Record("boss-phase", "started index=" + index);
    private static void OnBossDamaged(int index, int hp) => QaSessionContext.Record("boss-damage", $"phase={index}; hp={hp}");
    private static void OnBossDefeated() => QaSessionContext.Record("boss", "defeated");

    private static string DescribeEnemy(Enemy enemy)
    {
        if (enemy == null) return "enemy=UNKNOWN";
        string type = enemy.Data != null
            ? (string.IsNullOrWhiteSpace(enemy.Data.displayName) ? enemy.Data.enemyID : enemy.Data.displayName)
            : enemy.name;
        string glyph = enemy.Character != null ? enemy.Character.characterID : "UNKNOWN";
        return $"type={type}; glyph={glyph}; health={enemy.CurrentHealth}";
    }
}
