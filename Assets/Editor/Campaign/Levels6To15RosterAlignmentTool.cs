using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-216 (audit T04) — makes every Levels 6-15 combat roster equal that level's cumulative
/// symbol pool, and authors the one pool that was never written (Level 13's was an empty list).
///
/// Source of truth is symbol metadata, never a retyped table: a level may use every catalogue
/// symbol whose <c>firstIntroductionLevelId</c> sits at or before it in
/// <see cref="ContentIdentity.RevisedLevelIds"/> — the same expression
/// <c>CampaignConfigValidator.ExpectedPoolSymbolIds</c> uses, so the result is correct by
/// construction and satisfies COMBAT_ROSTER_INVALID / CUMULATIVE_POOL_INVALID by definition.
/// It also makes SALIN-217 (T05, RA as an 18th symbol introduced at Level 13) a plain re-run of
/// this menu item rather than a fresh hand edit.
///
/// Why a tool and not hand-edited YAML: <see cref="LevelConfigSO"/> prunes each wave's characters
/// against allowedCharacters in OnValidate (PruneToRoster), so shrinking a roster silently deletes
/// authored wave glyphs. This tool performs the prune itself and logs every dropped glyph, so the
/// change is visible in review instead of appearing at the next asset import.
///
/// Level 6 additionally gets explicit wave characters. All four of its waves shipped with
/// <c>characters: []</c>, and <c>WaveSpawner.SelectCharacterForSpawn</c> then falls back to
/// <c>EnemyDataSO.assignedCharacter</c> — a path no roster gates — so Level 6 was demanding KA,
/// SA, HA, LA, NGA and PA from its corruption enemies regardless of its roster. Authoring the
/// wave lists follows the SALIN-204 precedent (commit dd980c71, Level 4).
///
/// Deliberately narrow: it writes only allowedCharacters, the Level 13 cumulativeSymbolPool, and
/// waves[].characters. Requirements, focus words, finalRestorationValue, enemy rosters and media
/// belong to other tickets. Idempotent — re-running rewrites the same references.
/// </summary>
public static class Levels6To15RosterAlignmentTool
{
    private const string MenuPath = "Salinlahi/Campaign/Align Levels 6-15 Rosters";
    private const string CampaignPath =
        "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

    /// <summary>First level in scope, as a zero-based index into RevisedLevelIds.</summary>
    private const int FirstGlobalIndex = 5;

    [MenuItem(MenuPath)]
    public static void Run()
    {
        CampaignConfigSO campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
        if (campaign == null)
        {
            Debug.LogError("[SALIN-216] Could not load " + CampaignPath);
            return;
        }

        int aligned = 0;
        for (int globalIndex = FirstGlobalIndex;
             globalIndex < ContentIdentity.RevisedLevelIds.Count;
             globalIndex++)
        {
            string levelId = ContentIdentity.RevisedLevelIds[globalIndex];
            if (!campaign.TryGetLevel(levelId, out LevelConfigSO level))
            {
                Debug.LogError("[SALIN-216] Level not found: " + levelId);
                continue;
            }

            List<BaybayinCharacterSO> expected = ExpectedSymbols(campaign, globalIndex);
            AlignRoster(level, levelId, expected);
            AlignCumulativePool(level, levelId, expected);
            ReconcileWaves(level, levelId, expected);

            EditorUtility.SetDirty(level);
            aligned++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SALIN-216] Aligned " + aligned + " level(s), Levels 6-15.");
    }

    /// <summary>
    /// Every catalogue symbol introduced at or before <paramref name="globalIndex"/>, in
    /// ContentIdentity.RevisedSymbolIds order so the written list reads like the pools that
    /// SALIN-204 authored on Levels 1-5.
    /// </summary>
    private static List<BaybayinCharacterSO> ExpectedSymbols(
        CampaignConfigSO campaign, int globalIndex)
    {
        var expected = new List<BaybayinCharacterSO>();
        foreach (string symbolId in ContentIdentity.RevisedSymbolIds)
        {
            if (!campaign.TryGetSymbol(symbolId, out BaybayinCharacterSO symbol))
                continue;

            int introductionIndex = IndexOf(
                ContentIdentity.RevisedLevelIds, symbol.firstIntroductionLevelId);
            if (introductionIndex >= 0 && introductionIndex <= globalIndex)
                expected.Add(symbol);
        }

        return expected;
    }

    private static void AlignRoster(
        LevelConfigSO level, string levelId, List<BaybayinCharacterSO> expected)
    {
        var before = level.allowedCharacters == null
            ? new List<BaybayinCharacterSO>()
            : new List<BaybayinCharacterSO>(level.allowedCharacters);

        if (before.Count == expected.Count && !before.Except(expected).Any())
        {
            Debug.Log("[SALIN-216] " + levelId + " allowedCharacters already correct (" +
                      expected.Count + "), skipped.");
            return;
        }

        level.allowedCharacters = new List<BaybayinCharacterSO>(expected);
        Debug.Log("[SALIN-216] " + levelId + " allowedCharacters " + before.Count + " -> " +
                  expected.Count + ": [" + Describe(before) + "] -> [" + Describe(expected) + "]");
    }

    /// <summary>
    /// Only Level 13 needs this — its cumulativeSymbolPool is an empty list; the other nine are
    /// already correct and rewriting them would be diff noise with no behaviour change.
    /// </summary>
    private static void AlignCumulativePool(
        LevelConfigSO level, string levelId, List<BaybayinCharacterSO> expected)
    {
        if (level.cumulativeSymbolPool != null && level.cumulativeSymbolPool.Count > 0)
        {
            Debug.Log("[SALIN-216] " + levelId + " cumulativeSymbolPool already authored (" +
                      level.cumulativeSymbolPool.Count + "), skipped.");
            return;
        }

        level.cumulativeSymbolPool = expected.Select(Reference).ToList();
        Debug.Log("[SALIN-216] " + levelId + " cumulativeSymbolPool 0 -> " +
                  level.cumulativeSymbolPool.Count + " (was empty).");
    }

    /// <summary>
    /// Does the OnValidate prune here, loudly, and fills in Level 6's empty wave lists so the
    /// spawner never falls through to an enemy's default glyph.
    /// </summary>
    private static void ReconcileWaves(
        LevelConfigSO level, string levelId, List<BaybayinCharacterSO> expected)
    {
        if (level.waves == null)
            return;

        for (int i = 0; i < level.waves.Count; i++)
        {
            WaveDefinition wave = level.waves[i];
            if (wave == null)
                continue;

            if (wave.characters == null || wave.characters.Count == 0)
            {
                wave.characters = new List<BaybayinCharacterSO>(expected);
                Debug.Log("[SALIN-216] " + levelId + " wave " + (i + 1) +
                          " characters [] -> [" + Describe(expected) + "]");
                continue;
            }

            var dropped = wave.characters.Where(c => c == null || !expected.Contains(c)).ToList();
            if (dropped.Count == 0)
                continue;

            wave.characters = wave.characters.Where(expected.Contains).ToList();
            Debug.Log("[SALIN-216] " + levelId + " wave " + (i + 1) + " dropped untaught [" +
                      Describe(dropped) + "], kept " + wave.characters.Count + ".");

            if (wave.characters.Count == 0)
            {
                // Would fall back to EnemyDataSO.assignedCharacter, which no roster gates.
                wave.characters = new List<BaybayinCharacterSO>(expected);
                Debug.LogWarning("[SALIN-216] " + levelId + " wave " + (i + 1) +
                                 " emptied by the prune; refilled from the taught pool.");
            }
        }
    }

    /// <summary>
    /// Pairs a symbol with its spoken value. symbol.dara carries two (value.da / value.ra); it
    /// takes value.da, matching the pools already authored on Levels 11, 12, 14 and 15.
    /// </summary>
    private static SymbolValueReference Reference(BaybayinCharacterSO symbol)
    {
        string spokenValueId = symbol.stableId == ContentIdentity.RevisedDaraSymbolId
            ? ContentIdentity.RevisedDaSpokenValueId
            : symbol.spokenValues[0].stableId;

        return new SymbolValueReference { symbol = symbol, spokenValueId = spokenValueId };
    }

    private static int IndexOf(IReadOnlyList<string> values, string value)
    {
        if (string.IsNullOrEmpty(value))
            return -1;

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], value, System.StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private static string Describe(IEnumerable<BaybayinCharacterSO> symbols)
    {
        return string.Join(", ", symbols.Select(s => s == null ? "<null>" : s.stableId));
    }
}
