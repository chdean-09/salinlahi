using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DefeatScreenUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI _heartCountText;

    [Header("Buttons")]
    [SerializeField] private Button _retryButton;
    [SerializeField] private Button _levelSelectButton;

    [Header("Panel")]
    [SerializeField] private GameObject _panel;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        EventBus.OnGameOver += Show;

        if (_retryButton != null)
            _retryButton.onClick.AddListener(OnRetryPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
    }

    private void OnDisable()
    {
        EventBus.OnGameOver -= Show;

        if (_retryButton != null)
            _retryButton.onClick.RemoveListener(OnRetryPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
    }

    private void Show()
    {
        if (_panel != null)
            _panel.SetActive(true);

        int hearts = GameManager.Instance != null ? GameManager.Instance.LastDefeatHearts : 0;
        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 3;

        if (_heartCountText != null)
            _heartCountText.text = $"{hearts}/{maxHearts}";

        DebugLogger.Log($"DefeatScreenUI: Showing defeat. Hearts: {hearts}/{maxHearts}");
    }

    private void OnRetryPressed()
    {
        DebugLogger.Log("DefeatScreenUI: Retry pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        DebugLogger.Log("DefeatScreenUI: Level Select pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }
}