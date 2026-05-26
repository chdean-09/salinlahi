using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives a single level select button slot.
/// Displays the level's baked-in numbered scroll sprite,
/// tints it grey when locked, and forwards taps to scene-load.
/// </summary>
public class LevelButton : MonoBehaviour
{
    [Header("Core")]
    [SerializeField] private Button _button;
    [Tooltip("The scroll Image on this button. Sprite is set from LevelConfigSO.numberSprite.")]
    [SerializeField] private Image _scrollImage;

    [Header("State Visuals")]
    [Tooltip("Shown only when the level is locked (e.g. a lock icon overlay).")]
    [SerializeField] private GameObject _lockIcon;
    [Tooltip("Shown only when the level is completed (e.g. a star/check badge).")]
    [SerializeField] private GameObject _completionBadge;

    [Header("Colors")]
    [SerializeField] private Color _unlockedColor = Color.white;
    [SerializeField] private Color _lockedColor   = new Color(0.55f, 0.55f, 0.55f, 1f);

    private LevelConfigSO _config;
    private bool _isUnlocked;

    /// <summary>
    /// Configures this button for the given level config and progress state.
    /// Safe to call repeatedly — listeners are deduplicated.
    /// </summary>
    public void Setup(LevelConfigSO config, bool isUnlocked, bool isCompleted)
    {
        _config     = config;
        _isUnlocked = isUnlocked;

        if (_scrollImage != null)
        {
            if (config.numberSprite != null)
                _scrollImage.sprite = config.numberSprite;
            else
                DebugLogger.LogWarning($"LevelButton: {config.name} has no numberSprite assigned.");

            _scrollImage.color = isUnlocked ? _unlockedColor : _lockedColor;
        }

        if (_lockIcon != null)
            _lockIcon.SetActive(!isUnlocked);

        if (_completionBadge != null)
            _completionBadge.SetActive(isCompleted);

        if (_button != null)
        {
            _button.interactable = isUnlocked;
            _button.onClick.RemoveListener(OnPressed);
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
