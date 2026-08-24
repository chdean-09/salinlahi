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

    [Header("Lock Notice")]
    [Tooltip("SALIN-137 AC2 surface. Optional — resolved or built at runtime when unwired.")]
    [SerializeField] private LevelLockNoticePanel _lockNoticePanel;

    // ---------------------------------------------------------------
    // State
    // ---------------------------------------------------------------

    private int _currentEraIndex = 0;
    private List<EraConfigSO> _resolvedEras;

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
    ///
    /// SALIN-137 AC3: this IS the refresh path. Level Select is a separate scene, so the
    /// lock/unlock/completed state is re-read from <see cref="ProgressManager"/> every
    /// time the screen is entered (<c>Start</c> -> <c>ShowEra</c>) and every time the era
    /// arrows move. There is deliberately no in-scene "refresh" hook, because progress
    /// can only change while the player is in Gameplay — i.e. while this scene is gone.
    /// </summary>
    public void ShowEra(int eraIndex)
    {
        List<EraConfigSO> eras = ResolveEras();
        if (eras.Count == 0)
        {
            DebugLogger.LogError("LevelSelectUI: _eras list is empty.");
            return;
        }

        // SALIN-137: era navigation resets any prerequisite explanation still on screen.
        // Only a panel that already exists needs hiding — calling ResolveLockNoticePanel()
        // here would build the runtime fallback hierarchy on every Level Select entry even
        // when no locked scroll is ever pressed. An authored panel hides itself in Awake.
        _lockNoticePanel?.Hide();

        _currentEraIndex = Mathf.Clamp(eraIndex, 0, eras.Count - 1);
        EraConfigSO era  = eras[_currentEraIndex];

        if (_eraBackgroundImage != null && era.backgroundSprite != null)
            _eraBackgroundImage.sprite = era.backgroundSprite;

        if (_eraBannerImage != null && era.bannerSprite != null)
            _eraBannerImage.sprite = era.bannerSprite;

        bool pmAvailable = ProgressManager.Instance != null;
        if (!pmAvailable)
            DebugLogger.LogWarning("LevelSelectUI: ProgressManager not available. Defaulting all levels to unlocked.");

        // SALIN-136: identify the journey's next meaningful level so the player can
        // clearly see where to continue (no highlight once the journey is complete).
        int nextLevelNumber = -1;
        if (pmAvailable)
        {
            JourneyEntryKind entryKind = ProgressManager.Instance.GetJourneyEntryPoint(out int entryLevel);
            if (entryKind == JourneyEntryKind.NewJourney || entryKind == JourneyEntryKind.ContinueLevel)
                nextLevelNumber = entryLevel;
        }

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
            bool completed = false;

            if (pmAvailable)
            {
                unlocked = ProgressManager.Instance.IsLevelUnlocked(levelConfig.levelNumber);
                completed = ProgressManager.Instance.IsLevelCompleted(levelConfig.levelNumber);
            }

            button.gameObject.SetActive(true);
            // SALIN-137 AC2: the handler is attached unconditionally but only fires on a
            // press of a *locked* scroll, so the prerequisite is resolved lazily — on
            // press, never once per button per era render.
            button.SetLockedPressHandler(HandleLockedLevelPressed);
            button.Setup(levelConfig, unlocked, completed);
            button.SetHighlighted(unlocked && levelConfig.levelNumber == nextLevelNumber);
        }

        UpdateNavigationButtons();

        DebugLogger.Log($"LevelSelectUI: Showing era {_currentEraIndex} — {era.eraName}");
    }

    // SALIN-137: a `RefreshLevelButtons()` wrapper used to live here. It had zero callers
    // in C#, .unity, or .prefab, and its doc comment ("call this after any progress
    // change") promised a refresh mechanism that does not exist. Removed rather than left
    // implying behaviour: see ShowEra above for the real AC3 refresh path (scene re-entry).

    // ---------------------------------------------------------------
    // Private helpers
    // ---------------------------------------------------------------

    private void UpdateNavigationButtons()
    {
        int eraCount = ResolveEras().Count;

        if (_prevEraButton != null)
            _prevEraButton.interactable = _currentEraIndex > 0;

        if (_nextEraButton != null)
            _nextEraButton.interactable = _currentEraIndex < eraCount - 1;
    }

    /// <summary>
    /// SALIN-137 AC3: resolves which eras this screen can show.
    ///
    /// The serialized <c>_eras</c> list in <c>Assets/_Scenes/LevelSelect.unity</c>
    /// currently holds only <c>Era_01</c>, while <c>CampaignConfig_RevisedV1</c>
    /// configures three — so era progression would be undemonstrable from the scene
    /// alone. When the campaign configures more eras than the scene authored, the
    /// campaign's own order wins. That is the same order
    /// <see cref="CampaignSaveValidator.GetConfiguredLevelIds"/> flattens, and therefore
    /// the same order the unlock rule advances through, so the screen and the rule can
    /// never disagree.
    ///
    /// The fallback is KEPT rather than gated to fully-authored eras. Levels 6-15 have no
    /// <c>numberSprite</c>, but <see cref="LevelButton.Setup"/> now clears the sprite
    /// instead of leaving the previous era's numbered scroll behind, so the worst case is
    /// a blank placeholder scroll with the correct lock / unlock / completed state. Gating
    /// would instead hide two thirds of the campaign and leave the era arrows permanently
    /// disabled, making AC3 undemonstrable on the very screen it must be shown from —
    /// a strictly worse outcome than a missing numeral.
    ///
    /// OWED SCENE WORK: assigning Era_02 and Era_03 to <c>_eras</c> in the Inspector
    /// makes this fallback inert. It is kept as the safety net for legacy/blocked mode,
    /// where <c>SaveManager.Campaign</c> is unavailable.
    /// OWED ART: numbered scroll sprites for levels 6-15 (Assets/Art/UI/level6..15.png).
    /// </summary>
    private List<EraConfigSO> ResolveEras()
    {
        if (_resolvedEras != null)
            return _resolvedEras;

        List<EraConfigSO> authored = CompactEras(_eras);
        bool campaignAvailable = SaveManager.Instance != null && SaveManager.Instance.Campaign != null;
        List<EraConfigSO> configured = CompactEras(
            campaignAvailable ? SaveManager.Instance.Campaign.eras : null);

        List<EraConfigSO> resolved = authored;
        if (configured.Count > authored.Count)
        {
            DebugLogger.LogWarning(
                $"LevelSelectUI: scene _eras lists {authored.Count} era(s) but the campaign configures " +
                $"{configured.Count}. Falling back to the campaign era order. " +
                "Assign the missing eras in LevelSelect.unity.");
            resolved = configured;
        }

        // Cache only once the campaign has actually been consulted. SaveManager may still
        // be initializing on the first render; caching a campaign-less answer would pin
        // the screen to the short scene list for the rest of its lifetime.
        if (campaignAvailable)
            _resolvedEras = resolved;

        return resolved;
    }

    private static List<EraConfigSO> CompactEras(List<EraConfigSO> source)
    {
        var result = new List<EraConfigSO>();
        if (source == null)
            return result;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null)
                result.Add(source[i]);
        return result;
    }

    /// <summary>
    /// SALIN-137 AC2: a locked scroll was pressed. The game stays on Level Select and
    /// the single immediately preceding requirement is explained.
    /// </summary>
    private void HandleLockedLevelPressed(LevelConfigSO config)
    {
        LevelLockNoticePanel panel = ResolveLockNoticePanel();
        if (panel == null || config == null)
            return;

        if (ProgressManager.Instance == null)
        {
            panel.Hide();
            return;
        }

        LevelLockState state = ProgressManager.Instance.GetLevelLockState(
            config.levelNumber, out int requiredLevelNumber, out bool crossesEra);

        // Nothing to explain when the level is actually reachable, or when the save is
        // blocked/unclassifiable — CampaignSaveNoticePanel already owns that story, and
        // naming a prerequisite there would blame the wrong cause.
        if (state != LevelLockState.Locked || requiredLevelNumber < 1)
        {
            panel.Hide();
            return;
        }

        panel.PresentPrerequisite(requiredLevelNumber, crossesEra, FindEraNameForLevel(requiredLevelNumber));
    }

    /// <summary>
    /// Display name of the era owning the given level number, or <c>null</c>. Used only
    /// for era-crossing copy; a null name degrades to the plain level-number wording.
    /// </summary>
    private string FindEraNameForLevel(int levelNumber)
    {
        List<EraConfigSO> eras = ResolveEras();
        for (int i = 0; i < eras.Count; i++)
        {
            List<LevelConfigSO> levels = eras[i].levels;
            if (levels == null) continue;
            for (int j = 0; j < levels.Count; j++)
                if (levels[j] != null && levels[j].levelNumber == levelNumber)
                    return eras[i].eraName;
        }
        return null;
    }

    /// <summary>
    /// SALIN-137: finds the notice panel, or creates one. The Level Select scene authors
    /// no notice surface today, so without this AC2 has nowhere to render. Assigning
    /// <c>_lockNoticePanel</c> in the Inspector makes the runtime creation inert.
    ///
    /// Called ONLY from <see cref="HandleLockedLevelPressed"/>, so the fallback hierarchy
    /// is built lazily on the first locked press rather than on every Level Select entry.
    /// </summary>
    private LevelLockNoticePanel ResolveLockNoticePanel()
    {
        if (_lockNoticePanel != null)
            return _lockNoticePanel;

        _lockNoticePanel = FindFirstObjectByType<LevelLockNoticePanel>(FindObjectsInactive.Include);
        if (_lockNoticePanel != null)
            return _lockNoticePanel;

        var host = new GameObject("[Runtime] LevelLockNoticePanel");
        host.transform.SetParent(transform, worldPositionStays: false);
        _lockNoticePanel = host.AddComponent<LevelLockNoticePanel>();
        return _lockNoticePanel;
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
        if (_currentEraIndex >= ResolveEras().Count - 1) return;
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
