using UnityEngine;
using UnityEngine.UI;

// Placeholder for SALIN-23: satisfies AC-1/AC-2 by providing a valid LevelSelect scene destination.
// Full level grid with lock/unlock/complete states is implemented in SALIN-43.
public class LevelSelectUI : MonoBehaviour
{
    [Header("Level Buttons")]
    [SerializeField] private Button _level1Button;
    [SerializeField] private Button _level2Button;
    [SerializeField] private Button _level3Button;
    [SerializeField] private Button _level4Button;
    [SerializeField] private Button _level5Button;
    
    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    private void Start()
    {
        // Subscribe to button events
        if (_level1Button != null)
            _level1Button.onClick.AddListener(() => OnLevelSelected(1));
        if (_level2Button != null)
            _level2Button.onClick.AddListener(() => OnLevelSelected(2));
        if (_level3Button != null)
            _level3Button.onClick.AddListener(() => OnLevelSelected(3));
        if (_level4Button != null)
            _level4Button.onClick.AddListener(() => OnLevelSelected(4));
        if (_level5Button != null)
            _level5Button.onClick.AddListener(() => OnLevelSelected(5));

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackPressed);

        DebugLogger.Log("LevelSelectUI: Initialized");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_level1Button != null)
            _level1Button.onClick.RemoveAllListeners();
        if (_level2Button != null)
            _level2Button.onClick.RemoveAllListeners();
        if (_level3Button != null)
            _level3Button.onClick.RemoveAllListeners();
        if (_level4Button != null)
            _level4Button.onClick.RemoveAllListeners();
        if (_level5Button != null)
            _level5Button.onClick.RemoveAllListeners();
        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

    private void OnLevelSelected(int levelNumber)
    {
        DebugLogger.Log($"LevelSelectUI: Level {levelNumber} selected");

        // Store selected level for gameplay scene
        PlayerPrefs.SetInt("SelectedLevel", levelNumber);
        PlayerPrefs.Save();

        // Load gameplay scene
        SceneLoader.Instance.LoadGameplay();
    }

    private void OnBackPressed()
    {
        DebugLogger.Log("LevelSelectUI: Back to main menu");
        SceneLoader.Instance.LoadMainMenu();
    }
}
