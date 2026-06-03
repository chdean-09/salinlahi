using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Almanac scene orchestrator. Builds the Characters and Enemies grids, toggles the two pages,
/// opens/closes the shared detail scroll, returns HOME, and renders the progress counters.
/// Reads unlock state via CharacterUnlockProgress and enemy discovery via AlmanacEnemyDiscovery.
/// Re-binds the characters grid when OnCharacterUnlocked fires (keeps the debug hook correct and
/// future-proofs in-gameplay unlocks). Pure counter logic is in static methods for EditMode tests.
/// </summary>
public class AlmanacController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterRegistrySO _characterRegistry;
    [SerializeField] private AlmanacEnemyRegistrySO _enemyRegistry;

    [Header("Pages")]
    [SerializeField] private GameObject _charactersPage;
    [SerializeField] private GameObject _enemiesPage;
    [Tooltip("GridLayoutGroup content (under a ScrollRect) the character cells are built into.")]
    [SerializeField] private Transform _charactersGrid;
    [Tooltip("GridLayoutGroup content (under a ScrollRect) the enemy cells are built into.")]
    [SerializeField] private Transform _enemiesGrid;
    [SerializeField] private AlmanacCell _cellPrefab;

    [Header("Counters")]
    [SerializeField] private TextMeshProUGUI _charactersCounter;
    [SerializeField] private TextMeshProUGUI _enemiesCounter;

    [Header("Detail")]
    [SerializeField] private AlmanacDetailScroll _detailScroll;

    [Header("Nav")]
    [SerializeField] private Button _homeButton;
    [Tooltip("Single toggle button that switches between the two pages. Its icon names the page it switches TO.")]
    [SerializeField] private Button _tabToggleButton;
    [Tooltip("Image on the toggle button. Its sprite is swapped to the name of the page it switches TO.")]
    [SerializeField] private Image _tabToggleIcon;
    [Tooltip("Sprite shown on the toggle when it would switch to the Baybayin page (Baybayin.png).")]
    [SerializeField] private Sprite _charactersTabSprite;
    [Tooltip("Sprite shown on the toggle when it would switch to the Enemies page (Enemies.png).")]
    [SerializeField] private Sprite _enemiesTabSprite;

    private bool _charactersBuilt;
    private bool _enemiesBuilt;
    private bool _showingCharacters;

    private void OnEnable() => EventBus.OnCharacterUnlocked += HandleCharacterUnlocked;
    private void OnDisable() => EventBus.OnCharacterUnlocked -= HandleCharacterUnlocked;

    private void Start()
    {
        if (_homeButton != null) _homeButton.onClick.AddListener(OnHome);
        if (_tabToggleButton != null) _tabToggleButton.onClick.AddListener(ToggleTab);
        if (_detailScroll != null)
        {
            _detailScroll.OnShown += HideNav;
            _detailScroll.OnHidden += ShowNav;
        }

        ShowCharacters();
        DebugLogger.Log("AlmanacController: Initialized");
    }

    private void OnDestroy()
    {
        if (_homeButton != null) _homeButton.onClick.RemoveListener(OnHome);
        if (_tabToggleButton != null) _tabToggleButton.onClick.RemoveListener(ToggleTab);
        if (_detailScroll != null)
        {
            _detailScroll.OnShown -= HideNav;
            _detailScroll.OnHidden -= ShowNav;
        }
    }

    // Hide the HOME and tab-toggle buttons while a detail scroll is open; restore them when it closes.
    private void HideNav() => SetNavVisible(false);
    private void ShowNav() => SetNavVisible(true);

    private void SetNavVisible(bool visible)
    {
        if (_homeButton != null) _homeButton.gameObject.SetActive(visible);
        if (_tabToggleButton != null) _tabToggleButton.gameObject.SetActive(visible);
    }

    public void ShowCharacters()
    {
        BuildCharactersIfNeeded();
        if (_charactersPage != null) _charactersPage.SetActive(true);
        if (_enemiesPage != null) _enemiesPage.SetActive(false);
        _showingCharacters = true;
        RefreshTabToggle();
    }

    public void ShowEnemies()
    {
        BuildEnemiesIfNeeded();
        if (_enemiesPage != null) _enemiesPage.SetActive(true);
        if (_charactersPage != null) _charactersPage.SetActive(false);
        _showingCharacters = false;
        RefreshTabToggle();
    }

    private void ToggleTab()
    {
        AudioManager.Instance?.PlayMenuButtonClick();
        if (_showingCharacters) ShowEnemies();
        else ShowCharacters();
    }

    // The toggle's icon names the page it switches TO: on Characters it shows the ENEMIES sprite, and vice versa.
    private void RefreshTabToggle()
    {
        if (_tabToggleIcon != null)
            _tabToggleIcon.sprite = _showingCharacters ? _enemiesTabSprite : _charactersTabSprite;
    }

    private void BuildCharactersIfNeeded()
    {
        if (_charactersBuilt) return;
        if (_characterRegistry == null || _cellPrefab == null || _charactersGrid == null)
        {
            DebugLogger.LogWarning("AlmanacController: Characters page not fully wired.");
            return;
        }

        foreach (BaybayinCharacterSO c in _characterRegistry.All)
        {
            if (c == null) continue;
            BaybayinCharacterSO captured = c;
            bool revealed = CharacterUnlockProgress.HasUnlocked(captured);
            Sprite glyph = captured.almanacSprite != null ? captured.almanacSprite : captured.displaySprite;

            AlmanacCell cell = Instantiate(_cellPrefab, _charactersGrid);
            cell.Setup(glyph, revealed, isBoss: false, () =>
                _detailScroll?.Show(glyph, $"\"{captured.characterID}\"", captured.description));
        }

        _charactersBuilt = true;
        RefreshCharactersCounter();
    }

    private void BuildEnemiesIfNeeded()
    {
        if (_enemiesBuilt) return;
        if (_enemyRegistry == null || _cellPrefab == null || _enemiesGrid == null)
        {
            DebugLogger.LogWarning("AlmanacController: Enemies page not fully wired.");
            return;
        }

        foreach (AlmanacEnemyEntry entry in _enemyRegistry.entries)
        {
            if (entry == null) continue;
            AlmanacEnemyEntry captured = entry;
            // "currently" gate: enemies outside the Spanish era are placeholders for unfinished
            // chapters, so they read as locked '?' cells (like a locked Baybayin character).
            bool revealed = AlmanacEnemyDiscovery.IsDiscovered(captured.enemyData)
                            && IsSpanishEra(captured.enemyData);
            Sprite portrait = captured.ResolvePortrait();
            string title = captured.ResolveDisplayName();
            string desc = captured.ResolveDescription();

            AlmanacCell cell = Instantiate(_cellPrefab, _enemiesGrid);
            cell.Setup(portrait, revealed, captured.IsBoss, () =>
                _detailScroll?.Show(portrait, title, desc));
        }

        _enemiesBuilt = true;
        RefreshEnemiesCounter();
    }

    private void RefreshCharactersCounter()
    {
        if (_charactersCounter == null || _characterRegistry == null) return;
        int unlocked = CountUnlockedCharacters(_characterRegistry.All, CharacterUnlockProgress.HasUnlocked);
        _charactersCounter.text = FormatCounter("Learned", unlocked, _characterRegistry.All.Count);
    }

    private void RefreshEnemiesCounter()
    {
        if (_enemiesCounter == null || _enemyRegistry == null) return;
        // Mirror the reveal gate: a non-Spanish-era enemy shows as '?' and is not "Discovered" yet.
        int discovered = CountDiscoveredEnemies(
            _enemyRegistry.entries,
            data => AlmanacEnemyDiscovery.IsDiscovered(data) && IsSpanishEra(data));
        _enemiesCounter.text = FormatCounter("Discovered", discovered, _enemyRegistry.entries.Count);
    }

    private void HandleCharacterUnlocked(BaybayinCharacterSO _)
    {
        _charactersBuilt = false;
        ClearChildren(_charactersGrid);
        BuildCharactersIfNeeded();
    }

    private static void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void OnHome()
    {
        AudioManager.Instance?.PlayMenuExitButtonClick();
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadMainMenu();
        else
            DebugLogger.LogError("AlmanacController: SceneLoader not available. Cannot load MainMenu.");
    }

    // ---------------------------------------------------------------
    // Pure, testable helpers
    // ---------------------------------------------------------------

    public static int CountUnlockedCharacters(
        IReadOnlyList<BaybayinCharacterSO> all, Func<BaybayinCharacterSO, bool> isUnlocked)
    {
        if (all == null || isUnlocked == null) return 0;
        int n = 0;
        foreach (BaybayinCharacterSO c in all)
            if (c != null && isUnlocked(c)) n++;
        return n;
    }

    public static int CountDiscoveredEnemies(
        IReadOnlyList<AlmanacEnemyEntry> entries, Func<EnemyDataSO, bool> isDiscovered)
    {
        if (entries == null || isDiscovered == null) return 0;
        int n = 0;
        foreach (AlmanacEnemyEntry e in entries)
            if (e != null && isDiscovered(e.enemyData)) n++;
        return n;
    }

    public static string FormatCounter(string label, int revealed, int total) => $"{label} {revealed}/{total}";

    // A non-Spanish-era enemy is treated as not-yet-revealed in the Almanac: it renders as a
    // locked '?' (mirroring a locked Baybayin character) until that chapter's content ships.
    public static bool IsSpanishEra(EnemyDataSO data) => data != null && data.era == Era.Spanish;
}
