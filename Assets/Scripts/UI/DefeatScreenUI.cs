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

    [Tooltip("Gameplay HUD root, hidden while the defeat overlay is up. Safe to leave unwired.")]
    [SerializeField] private GameObject _hudRoot;

    private void Awake()
    {
        if (_panel != null) _panel.SetActive(false);
    }

    private void OnEnable()
    {
        if (_retryButton != null)
            _retryButton.onClick.AddListener(OnRetryPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.AddListener(OnLevelSelectPressed);
    }

    private void OnDisable()
    {
        if (_retryButton != null)
            _retryButton.onClick.RemoveListener(OnRetryPressed);
        if (_levelSelectButton != null)
            _levelSelectButton.onClick.RemoveListener(OnLevelSelectPressed);
    }

    public void Show()
    {
        if (_panel != null)
            _panel.SetActive(true);

        // The wave label, the spent hearts and the pause button were painting straight through
        // the 85%-opaque defeat background. Sibling order cannot settle it -- the HUD sits under
        // its own Canvas -- so the HUD is taken down instead. Both buttons below leave the scene,
        // so nothing has to put it back: the reload does.
        if (_hudRoot != null)
            _hudRoot.SetActive(false);

        int hearts = GameManager.Instance != null ? GameManager.Instance.LastDefeatHearts : 0;
        HeartSystem heartSystem = FindFirstObjectByType<HeartSystem>();
        int maxHearts = heartSystem != null ? heartSystem.GetMaxHearts() : 3;

        if (_heartCountText != null)
            _heartCountText.text = $"{hearts}/{maxHearts}";

        DebugLogger.Log($"DefeatScreenUI: Showing defeat. Hearts: {hearts}/{maxHearts}");
    }

    private void OnRetryPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("DefeatScreenUI: Retry pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }

    private void OnLevelSelectPressed()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        DebugLogger.Log("DefeatScreenUI: Level Select pressed");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadLevelSelect();
        else
            DebugLogger.LogError("DefeatScreenUI: SceneLoader not available.");
    }
}
