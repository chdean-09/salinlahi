using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Select UI driven by a serialized list of EraConfigSO entries.
/// Shows the era's LevelButtons, swapping the background sprite,
/// banner sprite, and the level scrolls when the player navigates eras.
/// Prev/Next arrow buttons remain visible at era edges; their interactable
/// flag is toggled and Unity's Button ColorBlock disabled-color tints them grey.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    // ---------------------------------------------------------------
    // Inspector
    // ---------------------------------------------------------------

    [Header("Era Data")]
    [SerializeField] private List<EraConfigSO> _eras = new();

    [Header("Scene Refs")]
    [SerializeField] private Image _eraBackgroundImage;
    [SerializeField] private Image _eraBannerImage;
    [Tooltip("The five LevelButton instances in the scene, in display order (slot 1..slot 5).")]
    [SerializeField] private List<LevelButton> _levelButtons = new();

    [Header("Navigation")]
    [SerializeField] private Button _prevEraButton;
    [SerializeField] private Button _nextEraButton;

    [Header("Back")]
    [SerializeField] private Button _backButton;

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    private int _currentEraIndex = 0;

    // ---------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------

    private void Start()
    {
        if (_prevEraButton != null)
            _prevEraButton.onClick.AddListener(OnPrevEra);

        if (_nextEraButton != null)
            _nextEraButton.onClick.AddListener(OnNextEra);

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackPressed);

        ShowEra(_currentEraIndex);

        DebugLogger.Log("LevelSelectUI: Initialized");
    }

    private void OnDestroy()
    {
        if (_prevEraButton != null)
            _prevEraButton.onClick.RemoveAllListeners();

        if (_nextEraButton != null)
            _nextEraButton.onClick.RemoveAllListeners();

        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

    // ---------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------

    /// <summary>
    /// Shows the given era's background, banner, and level buttons,
    /// then refreshes the prev/next arrow interactable state.
    /// </summary>
    public void ShowEra(int eraIndex)
    {
        if (_eras == null || _eras.Count == 0)
        {
            DebugLogger.LogError("LevelSelectUI: _eras list is empty.");
            return;
        }

        _currentEraIndex = Mathf.Clamp(eraIndex, 0, _eras.Count - 1);
        EraConfigSO era  = _eras[_currentEraIndex];

        if (_eraBackgroundImage != null && era.backgroundSprite != null)
            _eraBackgroundImage.sprite = era.backgroundSprite;

        if (_eraBannerImage != null && era.bannerSprite != null)
            _eraBannerImage.sprite = era.bannerSprite;

        bool pmAvailable = ProgressManager.Instance != null;
        if (!pmAvailable)
            DebugLogger.LogWarning("LevelSelectUI: ProgressManager not available. Defaulting all levels to unlocked.");

        for (int i = 0; i < _levelButtons.Count; i++)
        {
            LevelButton button = _levelButtons[i];
            if (button == null) continue;

            bool hasLevel = (i < era.levels.Count && era.levels[i] != null);
            if (!hasLevel)
            {
                button.gameObject.SetActive(false);
                continue;
            }

            LevelConfigSO levelConfig = era.levels[i];

            bool unlocked = true;
            // bool completed = false;

            if (pmAvailable)
            {
                unlocked = ProgressManager.Instance.IsLevelUnlocked(levelConfig.levelNumber);
                // completed = ProgressManager.Instance.IsLevelCompleted(levelConfig.levelNumber);
            }

            button.gameObject.SetActive(true);
            button.Setup(levelConfig, unlocked, false);
        }

        UpdateNavigationButtons();

        DebugLogger.Log($"LevelSelectUI: Showing era {_currentEraIndex} — {era.eraName}");
    }

    /// <summary>
    /// Refreshes the current era's buttons based on latest progress data.
    /// Call this after any progress change (e.g. level completion).
    /// </summary>
    public void RefreshLevelButtons() => ShowEra(_currentEraIndex);

    // ---------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------

    private void UpdateNavigationButtons()
    {
        if (_prevEraButton != null)
            _prevEraButton.interactable = _currentEraIndex > 0;

        if (_nextEraButton != null)
            _nextEraButton.interactable = _currentEraIndex < _eras.Count - 1;
    }

    // ---------------------------------------------------------------
    // Button callbacks
    // ---------------------------------------------------------------

    private void OnPrevEra()
    {
        if (_currentEraIndex <= 0) return;
        ShowEra(_currentEraIndex - 1);
    }

    private void OnNextEra()
    {
        if (_currentEraIndex >= _eras.Count - 1) return;
        ShowEra(_currentEraIndex + 1);
    }

    private void OnBackPressed()
    {
        AudioManager.Instance?.PlayMenuExitButtonClick();
        DebugLogger.Log("LevelSelectUI: Back to main menu");

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            DebugLogger.LogError("LevelSelectUI: SceneLoader not available. Cannot load MainMenu.");
    }
}
