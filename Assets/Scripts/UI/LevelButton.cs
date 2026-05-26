using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelButton : MonoBehaviour
{
    private static readonly Color LockedButtonColor = new(0.42f, 0.39f, 0.32f, 0.75f);
    private static readonly Color ActiveTextColor = new(0.7019608f, 0.5019608f, 0.07450981f, 1f);
    private static readonly Color LockedTextColor = new(0.38f, 0.34f, 0.26f, 1f);
    private static readonly Color TextShadowColor = new(0.06f, 0.035f, 0.01f, 1f);
    private static readonly Vector2 TextShadowOffset = new(5f, -5f);

    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _levelNumberText;
    [SerializeField] private GameObject _lockIcon;
    [SerializeField] private GameObject _completionBadge;

    private LevelConfigSO _config;
    private bool _isUnlocked;
    private Color _activeButtonColor = Color.white;

    private void Awake()
    {
        if (_button != null && _button.targetGraphic != null)
            _activeButtonColor = _button.targetGraphic.color;
    }

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
            ApplyVisualState(isUnlocked);
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

    private void ApplyVisualState(bool isUnlocked)
    {
        if (_button.targetGraphic != null)
            _button.targetGraphic.color = isUnlocked ? _activeButtonColor : LockedButtonColor;

        if (_levelNumberText != null)
        {
            _levelNumberText.color = isUnlocked ? ActiveTextColor : LockedTextColor;
            EnsureTextShadow(_levelNumberText);
        }

        Text legacyLabel = _button.GetComponentInChildren<Text>(true);
        if (legacyLabel != null)
        {
            legacyLabel.color = isUnlocked ? ActiveTextColor : LockedTextColor;
            EnsureTextShadow(legacyLabel);
        }
    }

    private static void EnsureTextShadow(Graphic label)
    {
        Shadow shadow = label.GetComponent<Shadow>();
        if (shadow == null)
            shadow = label.gameObject.AddComponent<Shadow>();

        shadow.effectColor = TextShadowColor;
        shadow.effectDistance = TextShadowOffset;
        shadow.useGraphicAlpha = true;
    }
}
