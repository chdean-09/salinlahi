using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor window for QA testing the Level 1 interactive tutorial.
/// Provides: state inspection, jump-to-state, and runtime controller debugging.
/// </summary>
public class Level1TutorialDebugWindow : EditorWindow
{
    private const string WindowTitle = "Level 1 Tutorial QA";
    private const string MenuPath = "Salinlahi/Debug/Level 1 Tutorial QA Window";
    private const string GameplaySceneName = "Gameplay";

    private Vector2 _scrollPosition;
    private Level1InteractiveTutorialController _controller;

    [MenuItem(MenuPath)]
    public static void ShowWindow()
    {
        GetWindow<Level1TutorialDebugWindow>(false, WindowTitle, true);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Level 1 Tutorial QA", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        // Scene check
        string activeScene = SceneManager.GetActiveScene().name;
        if (activeScene != GameplaySceneName)
        {
            EditorGUILayout.HelpBox(
                $"Active scene is '{activeScene}'.\n" +
                $"Please open '{GameplaySceneName}' with SelectedLevel = 1 to use runtime controls.",
                MessageType.Warning);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Load Gameplay Scene"))
            {
                if (EditorApplication.isPlaying)
                    SceneManager.LoadScene(GameplaySceneName);
                else
                    EditorUtility.DisplayDialog("Play Mode Required", 
                        "Scene loading requires Play Mode. Press Play first.", "OK");
            }
            return;
        }

        // Runtime-only controls
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to access runtime tutorial controls.", MessageType.Info);
            return;
        }

        FindController();

        if (_controller == null)
        {
            EditorGUILayout.HelpBox(
                "Level1InteractiveTutorialController not found in scene.\n" +
                "Make sure the tutorial controller is present in the hierarchy.",
                MessageType.Error);
            return;
        }

        DrawStatusPanel();
        EditorGUILayout.Space(10);
        DrawControlsPanel();
        EditorGUILayout.Space(10);
        DrawJumpToStatePanel();
    }

    private void FindController()
    {
        if (_controller != null) return;
        _controller = FindFirstObjectByType<Level1InteractiveTutorialController>();
    }

    private void DrawStatusPanel()
    {
        EditorGUILayout.LabelField("Current Status", EditorStyles.boldLabel);
        
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup("State", _controller.State);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.LabelField("Is Configured", _controller.IsConfigured ? "Yes" : "No");
        EditorGUILayout.LabelField("Scene", SceneManager.GetActiveScene().name);
        EditorGUILayout.LabelField("Has Seen Tutorial", LevelTutorialProgress.HasSeenLevel1Tutorial().ToString());
    }

    private void DrawControlsPanel()
    {
        EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset Tutorial Progress"))
        {
            LevelTutorialProgress.ResetLevel1TutorialForTests();
            Debug.Log("[QA] Level 1 tutorial progress reset.");
        }

        if (GUILayout.Button("Mark Tutorial Seen"))
        {
            LevelTutorialProgress.MarkLevel1TutorialSeen();
            Debug.Log("[QA] Level 1 tutorial marked as seen.");
        }

        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Skip Controls", EditorStyles.miniBoldLabel);
        EditorGUI.BeginDisabledGroup(_controller.State != Level1TutorialState.DrawPrompt);
        if (GUILayout.Button("Force Skip (if unlocked)"))
        {
            // Use reflection to invoke the private RequestSkip, or expose it for tests
            var method = typeof(Level1InteractiveTutorialController).GetMethod("RequestSkip", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method?.Invoke(_controller, null);
            Debug.Log("[QA] Skip requested.");
        }
        EditorGUI.EndDisabledGroup();
    }

    private void DrawJumpToStatePanel()
    {
        EditorGUILayout.LabelField("Jump to State (Experimental)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Jumping to a state will try to set the controller's internal state. " +
            "This may not fully initialize all subsystems for that state.", 
            MessageType.Info);

        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
        
        foreach (Level1TutorialState state in System.Enum.GetValues(typeof(Level1TutorialState)))
        {
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginDisabledGroup(_controller.State == state);
            if (GUILayout.Button($"Jump to {state}", GUILayout.Width(200)))
            {
                JumpToState(state);
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField(GetStateDescription(state), EditorStyles.miniLabel);
            
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndScrollView();
    }

    private void JumpToState(Level1TutorialState targetState)
    {
        // This is a best-effort jump using reflection
        var stateField = typeof(Level1InteractiveTutorialController).GetField("_state", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (stateField != null)
        {
            stateField.SetValue(_controller, targetState);
            Debug.Log($"[QA] Jumped to state: {targetState}");
            
            // Trigger UI update if applicable
            if (targetState == Level1TutorialState.DrawPrompt)
            {
                Debug.Log("[QA] Note: DrawPrompt requires an active step. Use RunStep via test harness for full setup.");
            }
        }
        else
        {
            Debug.LogError("[QA] Could not find _state field via reflection.");
        }
    }

    private static string GetStateDescription(Level1TutorialState state)
    {
        return state switch
        {
            Level1TutorialState.Gate => "Initial guard check",
            Level1TutorialState.BaseIntro => "Base intro dialogue",
            Level1TutorialState.WalkIn => "Protagonist walks in",
            Level1TutorialState.EnemyIntro => "First enemy appears",
            Level1TutorialState.DrawPrompt => "Player draws syllable",
            Level1TutorialState.PracticeChain => "Subsequent enemies",
            Level1TutorialState.Release => "Tutorial complete",
            Level1TutorialState.Skipped => "Player skipped tutorial",
            _ => "Unknown"
        };
    }

    private void OnInspectorUpdate()
    {
        Repaint();
    }
}
