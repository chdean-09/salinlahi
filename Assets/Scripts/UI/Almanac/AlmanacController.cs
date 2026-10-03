using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Almanac scene orchestrator. Builds the Enemies grid, opens/closes the detail scroll, returns
/// HOME, and renders the progress counter. Enemy discovery is read via AlmanacEnemyDiscovery.
/// The Almanac is enemies-only: every enemy carries one Baybayin symbol, so the symbol is shown
/// inside each enemy's detail scroll rather than on a page of its own.
/// Pure counter logic is in static methods for EditMode tests.
/// </summary>
public class AlmanacController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private AlmanacEnemyRegistrySO _enemyRegistry;

    [Header("Grid")]
    [Tooltip("GridLayoutGroup content (under a ScrollRect) the enemy cells are built into.")]
    [SerializeField] private Transform _enemiesGrid;
    [SerializeField] private AlmanacCell _cellPrefab;

    [Header("Counters")]
    [SerializeField] private TextMeshProUGUI _enemiesCounter;

    [Header("Detail")]
    [SerializeField] private AlmanacDetailScroll _detailScroll;

    [Header("Debug")]
    [Tooltip("DEBUG ONLY — leave OFF for a shipping build. Reveals every enemy without discovering " +
             "it first, so the art and detail scrolls can be reviewed. Real discovery state is " +
             "untouched: uncheck this and the Almanac gates normally again.")]
    [SerializeField] private bool _revealAllForDebug;

    [Header("Nav")]
    [SerializeField] private Button _homeButton;

    private bool _enemiesBuilt;

    private void Start()
    {
        if (_homeButton != null) _homeButton.onClick.AddListener(OnHome);
        if (_detailScroll != null)
        {
            _detailScroll.OnShown += HideNav;
            _detailScroll.OnHidden += ShowNav;
        }

        BuildEnemiesIfNeeded();
        DebugLogger.Log("AlmanacController: Initialized");
    }

    private void OnDestroy()
    {
        if (_homeButton != null) _homeButton.onClick.RemoveListener(OnHome);
        if (_detailScroll != null)
        {
            _detailScroll.OnShown -= HideNav;
            _detailScroll.OnHidden -= ShowNav;
        }
    }

    // Hide the HOME button while a detail scroll is open; restore it when the scroll closes.
    private void HideNav() => SetNavVisible(false);
    private void ShowNav() => SetNavVisible(true);

    private void SetNavVisible(bool visible)
    {
        if (_homeButton != null) _homeButton.gameObject.SetActive(visible);
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
            // chapters, so they stay silhouetted until that chapter's content ships.
            bool revealed = ResolveRevealed(
                IsEntryDiscovered(captured) && IsSpanishEra(captured.enemyData), _revealAllForDebug);
            Sprite portrait = captured.ResolvePortrait();
            Sprite glyph = captured.ResolveGlyph();
            string glyphLabel = captured.ResolveGlyphLabel();
            string title = captured.ResolveDisplayName();
            string desc = captured.ResolveDescription();

            AlmanacCell cell = Instantiate(_cellPrefab, _enemiesGrid);
            cell.Setup(portrait, revealed, captured.IsBoss, () =>
                _detailScroll?.Show(portrait, title, desc, glyph, glyphLabel));
        }

        _enemiesBuilt = true;
        RefreshEnemiesCounter();
    }

    private void RefreshEnemiesCounter()
    {
        if (_enemiesCounter == null || _enemyRegistry == null) return;
        // Mirror the reveal gate: a non-Spanish-era enemy stays silhouetted and is not "Discovered" yet.
        int discovered = CountDiscoveredEnemies(
            _enemyRegistry.entries,
            entry => ResolveRevealed(
                IsEntryDiscovered(entry) && IsSpanishEra(entry.enemyData), _revealAllForDebug));
        _enemiesCounter.text = FormatCounter("Discovered", discovered, _enemyRegistry.entries.Count);
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

    public static int CountDiscoveredEnemies(
        IReadOnlyList<AlmanacEnemyEntry> entries, Func<AlmanacEnemyEntry, bool> isDiscovered)
    {
        if (entries == null || isDiscovered == null) return 0;
        int n = 0;
        foreach (AlmanacEnemyEntry e in entries)
            if (e != null && isDiscovered(e)) n++;
        return n;
    }

    // Bosses are gated by boss tutorial acknowledgment; regular enemies use the temp stub.
    private static bool IsEntryDiscovered(AlmanacEnemyEntry entry)
    {
        if (entry == null) return false;
        if (entry.IsBoss) return AlmanacEnemyDiscovery.IsBossDiscovered(entry.bossConfig);
        return AlmanacEnemyDiscovery.IsDiscovered(entry.enemyData);
    }

    public static string FormatCounter(string label, int revealed, int total) => $"{label} {revealed}/{total}";

    // The single gate the debug reveal flag flows through, so the cells and the counter can never
    // disagree about what is revealed.
    public static bool ResolveRevealed(bool actuallyRevealed, bool revealAll) => actuallyRevealed || revealAll;

    // A non-Spanish-era enemy is treated as not-yet-revealed in the Almanac: it renders as a
    // silhouette until that chapter's content ships.
    public static bool IsSpanishEra(EnemyDataSO data) => data != null && data.era == Era.Spanish;
}
