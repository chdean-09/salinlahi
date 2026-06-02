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
    [Tooltip("BAYBAYIN tab.")]
    [SerializeField] private Button _charactersTabButton;
    [Tooltip("ENEMIES tab.")]
    [SerializeField] private Button _enemiesTabButton;

    private bool _charactersBuilt;
    private bool _enemiesBuilt;

    private void OnEnable() => EventBus.OnCharacterUnlocked += HandleCharacterUnlocked;
    private void OnDisable() => EventBus.OnCharacterUnlocked -= HandleCharacterUnlocked;

    private void Start()
    {
        if (_homeButton != null) _homeButton.onClick.AddListener(OnHome);
        if (_charactersTabButton != null) _charactersTabButton.onClick.AddListener(ShowCharacters);
        if (_enemiesTabButton != null) _enemiesTabButton.onClick.AddListener(ShowEnemies);

        ShowCharacters();
        DebugLogger.Log("AlmanacController: Initialized");
    }

    private void OnDestroy()
    {
        if (_homeButton != null) _homeButton.onClick.RemoveListener(OnHome);
        if (_charactersTabButton != null) _charactersTabButton.onClick.RemoveListener(ShowCharacters);
        if (_enemiesTabButton != null) _enemiesTabButton.onClick.RemoveListener(ShowEnemies);
    }

    public void ShowCharacters()
    {
        BuildCharactersIfNeeded();
        if (_charactersPage != null) _charactersPage.SetActive(true);
        if (_enemiesPage != null) _enemiesPage.SetActive(false);
    }

    public void ShowEnemies()
    {
        BuildEnemiesIfNeeded();
        if (_enemiesPage != null) _enemiesPage.SetActive(true);
        if (_charactersPage != null) _charactersPage.SetActive(false);
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

            AlmanacCell cell = Instantiate(_cellPrefab, _charactersGrid);
            cell.Setup(captured.displaySprite, revealed, isBoss: false, () =>
                _detailScroll?.Show(captured.displaySprite, $"\"{captured.characterID}\"", captured.description));
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
            bool revealed = AlmanacEnemyDiscovery.IsDiscovered(captured.enemyData);
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
        int discovered = CountDiscoveredEnemies(_enemyRegistry.entries, AlmanacEnemyDiscovery.IsDiscovered);
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
}
