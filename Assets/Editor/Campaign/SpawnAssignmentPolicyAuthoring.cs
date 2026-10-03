using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors each level's <see cref="SpawnAssignmentPolicy"/> so the spawn schedule reproduces the
/// length that level was already authored for.
///
/// The floor is derived, not guessed. Expected spawns to finish a target of S slots is
/// S * (minSpawnsBeforeNeeded + 1/neededWeight), so spending exactly a level's authored enemy
/// budget means:
///
///     minSpawnsBeforeNeeded = round(budget / S - 1/neededWeight)
///
/// Against the five authored levels (all of which target two two-syllable words, S = 4) that gives
/// the table below, whose predicted durations land within a few seconds of each level's own
/// authored spawn window plus overhead:
///
///     Lv  budget  mean interval  authored+overhead  floor  predicted
///     1   24      4.29s          128s               4      128s
///     2   18      3.33s           85s               2       78s
///     3   22      2.98s           91s               4       96s
///     4   30      3.93s          143s               6      151s
///     5   21      3.12s           91s               3       87s
///
/// Run from Salinlahi/Campaign/Author Spawn Assignment Policies. Written as an Editor command
/// rather than hand-edited asset YAML so Unity owns the serialization.
///
/// Design source: docs/design/spawn-assignment-system.md.
/// </summary>
public static class SpawnAssignmentPolicyAuthoring
{
    /// <summary>Level number to anti-rush floor. See the derivation above.</summary>
    private static readonly Dictionary<int, int> FloorByLevel = new Dictionary<int, int>
    {
        { 1, 4 },
        { 2, 2 },
        { 3, 4 },
        { 4, 6 },
        { 5, 3 },
    };

    [MenuItem("Salinlahi/Campaign/Author Spawn Assignment Policies")]
    public static void Author()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO");
        int updated = 0;
        var report = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            if (level == null || !level.activeClueCombatEnabled)
                continue;

            if (!FloorByLevel.TryGetValue(level.levelNumber, out int floor))
            {
                report.Add($"  {level.name}: no authored floor for level {level.levelNumber}, left at default.");
                continue;
            }

            level.spawnAssignmentPolicy ??= new SpawnAssignmentPolicy();
            SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy;

            policy.minSpawnsBeforeNeeded = floor;
            policy.Sanitize();

            EditorUtility.SetDirty(level);
            updated++;
            report.Add($"  {level.name}: minSpawnsBeforeNeeded = {floor} "
                + $"(slots {CountSlots(level)}, budget {CountBudget(level)}).");
        }

        if (updated > 0)
            AssetDatabase.SaveAssets();

        DebugLogger.Log($"SpawnAssignmentPolicyAuthoring: updated {updated} level config(s).\n"
            + string.Join("\n", report));
    }

    /// <summary>
    /// Reports levels whose authored floor no longer matches their wave budget, which happens as
    /// soon as someone retunes enemyCount. Read-only.
    /// </summary>
    [MenuItem("Salinlahi/Campaign/Validate Spawn Assignment Policies")]
    public static void Validate()
    {
        string[] guids = AssetDatabase.FindAssets("t:LevelConfigSO");
        var report = new List<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(path);
            if (level == null || !level.activeClueCombatEnabled || level.spawnAssignmentPolicy == null)
                continue;

            int slots = CountSlots(level);
            int budget = CountBudget(level);
            if (slots == 0 || budget == 0)
                continue;

            SpawnAssignmentPolicy policy = level.spawnAssignmentPolicy;
            float weight = policy.neededWeight > 0f ? policy.neededWeight : 0.5f;
            int suggested = Mathf.Max(1, Mathf.RoundToInt(budget / (float)slots - 1f / weight));

            if (suggested == policy.minSpawnsBeforeNeeded)
                continue;

            report.Add($"  {level.name}: minSpawnsBeforeNeeded is {policy.minSpawnsBeforeNeeded}, "
                + $"but its {budget}-enemy budget over {slots} slots suggests {suggested}. "
                + "The level will rely on restoration overflow to finish.");
        }

        if (report.Count == 0)
        {
            DebugLogger.Log("SpawnAssignmentPolicyAuthoring: every active-clue level's floor matches its wave budget.");
            return;
        }

        DebugLogger.LogWarning("SpawnAssignmentPolicyAuthoring: floor/budget mismatches:\n"
            + string.Join("\n", report));
    }

    private static int CountSlots(LevelConfigSO level)
    {
        if (level.focusWords == null)
            return 0;

        int slots = 0;
        for (int i = 0; i < level.focusWords.Count; i++)
        {
            FocusWordDefinition word = level.focusWords[i];
            if (word?.decomposition == null)
                continue;

            for (int j = 0; j < word.decomposition.Count; j++)
            {
                if (word.decomposition[j]?.symbol != null)
                    slots++;
            }
        }

        return slots;
    }

    private static int CountBudget(LevelConfigSO level)
    {
        if (level.waves == null)
            return 0;

        int budget = 0;
        for (int i = 0; i < level.waves.Count; i++)
        {
            WaveDefinition wave = level.waves[i];
            if (wave != null && !wave.isIntermissionWave)
                budget += Mathf.Max(0, wave.enemyCount);
        }

        return budget;
    }
}
