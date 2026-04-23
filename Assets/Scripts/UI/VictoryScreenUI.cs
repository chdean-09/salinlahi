using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class VictoryScreenUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _starCountText;
    [SerializeField] private GameObject[] _starIcons;

    [Header("Buttons")]
    [SerializeField] private Button _nextLevelButton;
    [SerializeField] private Button _levelSelectButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnLevelComplete += Show;

        if (_nextLevelButton != null)
            _nextLevelButton.onClick.AddListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
    }

    private void OnDisable()
    {
        EventBus.OnLevelComplete -= Show;

        if (_nextLevelButton != null)
            _nextLevelButton.onClick.RemoveListener(OnNextLevelPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
    }

    private void Show()
    {
        if (_panel != null)
            _panel.SetActive(true);

        int currentLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        int stars = ProgressManager.Instance != null
            ? ProgressManager.Instance.GetStars(currentLevel)
            : 0;

        if (_starCountText != null)
            _starCountText.text = $"{stars}/3";

        if (_starIcons != null)
        {
            for (int i = 0; i < _starIcons.Length; i++)
            {
                if (_starIcons[i] != null)
                    _starIcons[i].SetActive(i < stars);
            }
        }

        bool isLastLevel = currentLevel >= 15;
        if (_nextLevelButton != null)
            _nextLevelButton.gameObject.SetActive(!isLastLevel);

        DebugLogger.Log($"VictoryScreenUI: Level {currentLevel} complete with {stars} stars.");
    }

    private void OnNextLevelPressed()
    {
        int currentLevel = PlayerPrefs.GetInt(ProgressManager.SelectedLevelKey, 1);
        int nextLevel = currentLevel + 1;

        if (nextLevel > 15)
        {
            DebugLogger.LogWarning("VictoryScreenUI: No next level. Navigating to Level Select.");
            OnLevelSelectPressed();
            return;
        }

        PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, nextLevel);
        PlayerPrefs.Save();

        if (GameManager.Instance != null)
            GameManager.Instance.SetLevel(null);

        DebugLogger.Log($"VictoryScreenUI: Advancing to Level {nextLevel}");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        DebugLogger.Log("VictoryScreenUI: Level Select pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("VictoryScreenUI: SceneLoader not available.");
    }
}