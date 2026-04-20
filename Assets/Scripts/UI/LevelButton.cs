using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _levelNumberText;
    [SerializeField] private GameObject _lockIcon;
    [SerializeField] private GameObject _completionBadge;

    private LevelConfigSO _config;
    private bool _isUnlocked;

    public void Setup(LevelConfigSO config, bool isUnlocked, bool isCompleted)
    {
        _config = config;
        _isUnlocked = isUnlocked;

        if (_levelNumberText != null)
            _levelNumberText.text = config.levelNumber.ToString();

        if (_lockIcon != null)
            _lockIcon.SetActive(!isUnlocked);

        if (_completionBadge != null)
            _completionBadge.SetActive(isCompleted);

        if (_button != null)
        {
            _button.interactable = isUnlocked;
            _button.onClick.RemoveListener(OnPressed); // Prevent stacking on repeated Setup calls
            _button.onClick.AddListener(OnPressed);
        }
    }

    private void OnPressed()
    {
        if (_config == null || !_isUnlocked) return;

        DebugLogger.Log($"LevelButton: Level {_config.levelNumber} selected");

        PlayerPrefs.SetInt(ProgressManager.SelectedLevelKey, _config.levelNumber);
        PlayerPrefs.Save();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.DiscardPausedRunSnapshot();
            GameManager.Instance.SetLevel(_config);
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadGameplay();
        else
            DebugLogger.LogError("LevelButton: SceneLoader not available. Cannot load Gameplay.");
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(OnPressed);
    }
}
