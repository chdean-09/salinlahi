using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level Select UI with lock/unlock states and star ratings.
/// Reads progress from ProgressManager and displays locked levels,
/// unlocked levels, and earned stars per level.
/// </summary>
public class LevelSelectUI : MonoBehaviour
{
    [Header("Level Buttons")]
    [SerializeField] private Button _level1Button;
    [SerializeField] private Button _level2Button;
    [SerializeField] private Button _level3Button;
    [SerializeField] private Button _level4Button;
    [SerializeField] private Button _level5Button;

    [Header("Lock Overlays")]
    [SerializeField] private GameObject _level1LockOverlay;
    [SerializeField] private GameObject _level2LockOverlay;
    [SerializeField] private GameObject _level3LockOverlay;
    [SerializeField] private GameObject _level4LockOverlay;
    [SerializeField] private GameObject _level5LockOverlay;

    [Header("Star Images (3 per level - 15 total)")]
    [SerializeField] private Image _level1Star1;
    [SerializeField] private Image _level1Star2;
    [SerializeField] private Image _level1Star3;
    [SerializeField] private Image _level2Star1;
    [SerializeField] private Image _level2Star2;
    [SerializeField] private Image _level2Star3;
    [SerializeField] private Image _level3Star1;
    [SerializeField] private Image _level3Star2;
    [SerializeField] private Image _level3Star3;
    [SerializeField] private Image _level4Star1;
    [SerializeField] private Image _level4Star2;
    [SerializeField] private Image _level4Star3;
    [SerializeField] private Image _level5Star1;
    [SerializeField] private Image _level5Star2;
    [SerializeField] private Image _level5Star3;

    [Header("Star Sprites")]
    [Tooltip("Sprite to show for earned stars")]
    [SerializeField] private Sprite _starFilledSprite;
    [Tooltip("Sprite to show for unearned stars (optional - will be hidden if null)")]
    [SerializeField] private Sprite _starEmptySprite;

    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    // Arrays for easier iteration
    private Button[] _levelButtons;
    private GameObject[] _lockOverlays;
    private Image[][] _starImages;

    private void Awake()
    {
        // Initialize arrays
        _levelButtons = new Button[] { _level1Button, _level2Button, _level3Button, _level4Button, _level5Button };
        _lockOverlays = new GameObject[] { _level1LockOverlay, _level2LockOverlay, _level3LockOverlay, _level4LockOverlay, _level5LockOverlay };
        _starImages = new Image[][]
        {
            new Image[] { _level1Star1, _level1Star2, _level1Star3 },
            new Image[] { _level2Star1, _level2Star2, _level2Star3 },
            new Image[] { _level3Star1, _level3Star2, _level3Star3 },
            new Image[] { _level4Star1, _level4Star2, _level4Star3 },
            new Image[] { _level5Star1, _level5Star2, _level5Star3 }
        };
    }

    private void Start()
    {
        // Initialize button states based on progress
        RefreshLevelButtons();

        // Subscribe to button events
        for (int i = 0; i < _levelButtons.Length; i++)
        {
            int levelNumber = i + 1; // Capture for closure
            if (_levelButtons[i] != null)
                _levelButtons[i].onClick.AddListener(() => OnLevelSelected(levelNumber));
        }

        if (_backButton != null)
            _backButton.onClick.AddListener(OnBackPressed);

        DebugLogger.Log("LevelSelectUI: Initialized");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (_levelButtons != null)
        {
            foreach (var button in _levelButtons)
            {
                if (button != null)
                    button.onClick.RemoveAllListeners();
            }
        }

        if (_backButton != null)
            _backButton.onClick.RemoveAllListeners();
    }

    /// <summary>
    /// Refreshes all level buttons based on current progress.
    /// Called on Start and can be called externally to refresh after progress changes.
    /// </summary>
    public void RefreshLevelButtons()
    {
        for (int i = 0; i < _levelButtons.Length; i++)
        {
            int levelNumber = i + 1;
            UpdateLevelButtonState(levelNumber);
        }
    }

    /// <summary>
    /// Updates the visual state of a single level button.
    /// </summary>
    private void UpdateLevelButtonState(int levelNumber)
    {
        int index = levelNumber - 1;
        if (index < 0 || index >= _levelButtons.Length)
            return;

        Button button = _levelButtons[index];
        GameObject lockOverlay = _lockOverlays != null && index < _lockOverlays.Length ? _lockOverlays[index] : null;
        Image[] stars = _starImages != null && index < _starImages.Length ? _starImages[index] : null;

        if (button == null)
            return;

        // Check if level is unlocked (default to true if ProgressManager not available)
        bool unlocked = true;
        if (ProgressManager.Instance != null)
        {
            unlocked = ProgressManager.Instance.IsLevelUnlocked(levelNumber);
        }
        else
        {
            DebugLogger.LogWarning("LevelSelectUI: ProgressManager not available. Defaulting all levels to unlocked.");
        }

        // Set button interactable state
        button.interactable = unlocked;

        // Show/hide lock overlay
        if (lockOverlay != null)
            lockOverlay.SetActive(!unlocked);

        // Update star display (only show stars for unlocked levels that have been completed)
        if (stars != null && unlocked && ProgressManager.Instance != null)
        {
            int starCount = ProgressManager.Instance.GetStars(levelNumber);
            UpdateStarDisplay(stars, starCount);
        }
        else if (stars != null)
        {
            // Hide all stars for locked levels or when stars array is empty
            HideAllStars(stars);
        }
    }

    /// <summary>
    /// Updates the star images for a level.
    /// </summary>
    private void UpdateStarDisplay(Image[] stars, int starCount)
    {
        if (stars == null || stars.Length == 0)
            return;

        for (int i = 0; i < stars.Length; i++)
        {
            Image starImage = stars[i];
            if (starImage == null)
                continue;

            // Show star as filled if we have enough stars
            bool shouldBeFilled = i < starCount;
            
            if (shouldBeFilled)
            {
                // Show filled star
                starImage.sprite = _starFilledSprite;
                starImage.gameObject.SetActive(true);
            }
            else
            {
                // Show empty star only if we have a sprite for it
                if (_starEmptySprite != null)
                {
                    starImage.sprite = _starEmptySprite;
                    starImage.gameObject.SetActive(true);
                }
                else
                {
                    // Hide unearned stars if no empty sprite assigned
                    starImage.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// Hides all star images for a level.
    /// </summary>
    private void HideAllStars(Image[] stars)
    {
        if (stars == null)
            return;

        foreach (var starImage in stars)
        {
            if (starImage != null)
                starImage.gameObject.SetActive(false);
        }
    }

    private void OnLevelSelected(int levelNumber)
    {
        // Defensive check: verify level is actually unlocked
        if (ProgressManager.Instance != null && !ProgressManager.Instance.IsLevelUnlocked(levelNumber))
        {
            DebugLogger.Log($"LevelSelectUI: Level {levelNumber} is locked. Ignoring click.");
            return;
        }

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
