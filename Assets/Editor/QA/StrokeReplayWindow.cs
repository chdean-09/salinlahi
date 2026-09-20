using System;
using System.Collections.Generic;
using Salinlahi.Debug.Sandbox;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Maps the small set of recorded human fixtures used by the Ugat Levels 1-5 QA pass. Keeping
/// this resolver separate from the window makes missing-file and id-normalisation behavior testable
/// without opening an EditorWindow or entering Play Mode.
/// </summary>
public static class QaStrokeSampleLibrary
{
    private const string FixtureRoot = "Assets/Tests/Fixtures/TestDraws/";

    private static readonly Dictionary<string, string> FixturePaths =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "A", FixtureRoot + "A_draw_01.txt" },
            { "EI", FixtureRoot + "EI_draw_01.txt" },
            { "BA", FixtureRoot + "BA_draw_01.txt" },
            { "MA", FixtureRoot + "MA_draw_01.txt" },
            { "NA", FixtureRoot + "NA_draw_01.txt" },
            { "TA", FixtureRoot + "TA_draw_01.txt" },
        };

    public static IReadOnlyList<string> SupportedCharacterIds { get; } =
        new[] { "A", "EI", "BA", "MA", "NA", "TA" };

    public static bool TryResolveAssetPath(string characterId, out string assetPath)
    {
        assetPath = null;
        string canonical = BaybayinIdCanonicalizer.Canonicalize(characterId);
        if (!FixturePaths.TryGetValue(canonical, out string candidate))
            return false;

        if (AssetDatabase.LoadAssetAtPath<TextAsset>(candidate) == null)
            return false;

        assetPath = candidate;
        return true;
    }

    public static bool TryLoadStrokes(
        string characterId,
        out List<List<Vector2>> strokes,
        out string error)
    {
        strokes = null;
        error = null;

        string canonical = BaybayinIdCanonicalizer.Canonicalize(characterId);
        if (!FixturePaths.TryGetValue(canonical, out string expectedPath))
        {
            error = $"No QA stroke fixture is mapped for '{characterId}'.";
            return false;
        }

        TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(expectedPath);
        if (asset == null)
        {
            error = $"QA stroke fixture is missing: {expectedPath}";
            return false;
        }

        strokes = StrokeTextParser.ParseStrokes(asset.text);
        if (StrokeValidation.IsRecognitionDegenerate(strokes))
        {
            strokes = null;
            error = $"QA stroke fixture is empty or degenerate: {expectedPath}";
            return false;
        }

        return true;
    }

    public static List<List<Vector2>> BuildMissSample()
    {
        // This is intentionally a non-degenerate shape so RecognitionManager runs its normal
        // scoring and failure path. A one-point tap is rejected before scoring and would leave the
        // panel showing the previous result, which makes a replay miss hard to verify.
        return new List<List<Vector2>>
        {
            new List<Vector2>
            {
                new Vector2(-1000f, -1f),
                new Vector2(1000f, 1f),
            },
            new List<Vector2> { new Vector2(-900f, 700f), new Vector2(900f, -700f) },
            new List<Vector2> { new Vector2(-900f, -700f), new Vector2(900f, 700f) },
            new List<Vector2> { new Vector2(-850f, 0f), new Vector2(850f, 0f) },
            new List<Vector2> { new Vector2(0f, -650f), new Vector2(0f, 650f) },
            new List<Vector2> { new Vector2(-500f, -500f), new Vector2(500f, 500f) },
        };
    }
}

/// <summary>
/// Editor-only QA seam. It submits recorded strokes through RecognitionManager so the normal
/// recognition, clue resolution, combat, restoration, HUD, wave, and victory paths remain under
/// test. It intentionally does not emulate physical touch capture or raise gameplay events itself.
/// </summary>
public sealed class StrokeReplayWindow : EditorWindow
{
    private static readonly string[] ReplayIds = { "A", "EI", "BA", "MA", "NA", "TA" };

    private Vector2 _scroll;
    private RecognitionResult _lastResult;
    private bool _hasLastResult;
    private bool _lastPassedThreshold;
    private float _lastThreshold;
    private string _statusMessage = "No replay submitted.";

    [MenuItem("Salinlahi/QA/Stroke Replay")]
    public static void Open()
    {
        StrokeReplayWindow window = GetWindow<StrokeReplayWindow>("Stroke Replay");
        window.minSize = new Vector2(320f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        EventBus.OnRecognitionResolved += HandleRecognitionResolved;
        EditorApplication.update += Repaint;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private void OnDisable()
    {
        EventBus.OnRecognitionResolved -= HandleRecognitionResolved;
        EditorApplication.update -= Repaint;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        SandboxMode.SetQaProtectionEnabled(false);
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.LabelField("Ugat QA Stroke Replay", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Recorded human strokes are submitted through the real recognizer. This validates "
            + "recognition and downstream gameplay; physical touch capture still needs a manual pass.",
            MessageType.Info);

        bool canReplay = CanReplay(out string reason);
        using (new EditorGUI.DisabledScope(!canReplay))
        {
            if (GUILayout.Button("Replay Current Clue", GUILayout.Height(34f)))
                ReplayCurrentClue();

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Recorded samples", EditorStyles.boldLabel);
            for (int i = 0; i < ReplayIds.Length; i++)
            {
                string id = ReplayIds[i];
                string label = id == "EI" ? "E/I" : id;
                if (GUILayout.Button(label))
                    ReplaySymbol(id);
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("Submit Miss"))
                SubmitMiss();
        }

        if (!canReplay)
            EditorGUILayout.HelpBox(reason, MessageType.Warning);

        EditorGUILayout.Space(6f);
        EditorGUI.BeginChangeCheck();
        bool qaProtection = EditorGUILayout.ToggleLeft(
            "QA protection (freeze enemies + block base damage)",
            SandboxMode.IsQaProtectionEnabled);
        if (EditorGUI.EndChangeCheck())
            SandboxMode.SetQaProtectionEnabled(qaProtection);

        if (SandboxMode.IsQaProtectionEnabled)
        {
            EditorGUILayout.HelpBox(
                "QA protection is Editor-only. Normal waves, recognition, restoration, challenge "
                + "boards, victory, save, and results flow remain active.",
                MessageType.Info);
        }

        EditorGUILayout.Space(8f);
        DrawRuntimeStatus();
        EditorGUILayout.EndScrollView();
    }

    private bool CanReplay(out string reason)
    {
        if (!EditorApplication.isPlaying)
        {
            reason = "Enter Play Mode to replay a stroke.";
            return false;
        }

        if (GameManager.Instance == null)
        {
            reason = "GameManager is not available in the active Play Mode scene.";
            return false;
        }

        if (!GameManager.Instance.AcceptsDrawingInput)
        {
            reason = "Drawing input is currently suppressed or the level is not accepting input.";
            return false;
        }

        if (RecognitionManager.Instance == null)
        {
            reason = "RecognitionManager is not available in the active Play Mode scene.";
            return false;
        }

        reason = null;
        return true;
    }

    private void ReplayCurrentClue()
    {
        Enemy clue = ActiveClueDirector.Instance != null
            ? ActiveClueDirector.Instance.CurrentClue
            : null;
        string characterId = clue?.Character?.characterID;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            _statusMessage = "No active clue with a supported character is currently marked.";
            return;
        }

        ReplaySymbol(characterId);
    }

    private void ReplaySymbol(string characterId)
    {
        if (!QaStrokeSampleLibrary.TryLoadStrokes(characterId,
                out List<List<Vector2>> strokes, out string error))
        {
            _statusMessage = error;
            return;
        }

        RecognitionManager.Instance.Recognize(strokes);
        _statusMessage = $"Submitted recorded {BaybayinIdCanonicalizer.Canonicalize(characterId)} sample.";
        Repaint();
    }

    private void SubmitMiss()
    {
        if (RecognitionManager.Instance == null)
        {
            _statusMessage = "RecognitionManager is not available.";
            return;
        }

        RecognitionManager.Instance.Recognize(QaStrokeSampleLibrary.BuildMissSample());
        _statusMessage = "Submitted a deliberately incorrect miss.";
        Repaint();
    }

    private void HandleRecognitionResolved(RecognitionResult result, bool passedThreshold, float threshold)
    {
        _lastResult = result;
        _hasLastResult = true;
        _lastPassedThreshold = passedThreshold;
        _lastThreshold = threshold;
        Repaint();
    }

    private void DrawRuntimeStatus()
    {
        EditorGUILayout.LabelField("Runtime status", EditorStyles.boldLabel);
        LevelConfigSO level = GameManager.Instance != null ? GameManager.Instance.CurrentLevel : null;
        WaveManager waves = FindFirstObjectByType<WaveManager>();
        RestorationObjectiveController objective = RestorationObjectiveController.Active
            ?? FindFirstObjectByType<RestorationObjectiveController>(FindObjectsInactive.Include);
        HeartSystem hearts = FindFirstObjectByType<HeartSystem>(FindObjectsInactive.Include);
        Enemy clue = ActiveClueDirector.Instance != null
            ? ActiveClueDirector.Instance.CurrentClue
            : null;

        EditorGUILayout.LabelField("Play Mode", EditorApplication.isPlaying ? "Yes" : "No");
        EditorGUILayout.LabelField(
            "Game state",
            GameManager.Instance != null ? GameManager.Instance.CurrentState.ToString() : "—");
        EditorGUILayout.LabelField(
            "Drawing input",
            GameManager.Instance != null
                ? (GameManager.Instance.AcceptsDrawingInput ? "Enabled" : "Blocked")
                : "—");
        EditorGUILayout.LabelField("Level", level != null ? $"{level.levelNumber}: {level.levelName}" : "—");
        EditorGUILayout.LabelField("Wave", waves != null ? $"{waves.CurrentWaveIndex + 1} (spawned {waves.CurrentWaveSpawnedCount})" : "—");
        EditorGUILayout.LabelField("Current clue", clue?.Character?.characterID ?? "—");
        EditorGUILayout.LabelField(
            "Objective",
            objective != null && objective.State != null
                ? $"{objective.State.RestoredTargetCount}/{objective.State.TargetCount}"
                : "—");
        EditorGUILayout.LabelField("Hearts", hearts != null ? hearts.GetCurrentHearts().ToString() : "—");
        EditorGUILayout.LabelField(
            "QA protection",
            SandboxMode.IsQaProtectionEnabled ? "Enabled" : "Off");
        EditorGUILayout.LabelField("Status", _statusMessage ?? "—");

        if (!_hasLastResult)
        {
            EditorGUILayout.LabelField("Last recognition", "—");
            return;
        }

        EditorGUILayout.LabelField(
            "Last recognition",
            $"{_lastResult.characterID}  score {_lastResult.score:F3}  "
            + $"threshold {_lastThreshold:F3}  "
            + (_lastPassedThreshold ? "PASS" : "FAIL"));
    }

    private static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode
            || state == PlayModeStateChange.EnteredEditMode)
        {
            SandboxMode.SetQaProtectionEnabled(false);
        }
    }
}
