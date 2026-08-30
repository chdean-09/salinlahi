using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-206 follow-up — wires the seventeen authored corruption-enemy
/// EnemyDataSO assets into the places that actually spawn or list enemies.
/// The SALIN-206 drops imported the sheets and authored the data but no wave,
/// tutorial step, or almanac entry ever referenced them, so every level kept
/// spawning the colonial placeholder roster.
///
/// What it does, per the "the syllable IS the enemy's identity" design
/// (CORE GAME MECHANICS.xlsx, ENEMIES tab):
///
///   1. Level1..15 configs: a non-intermission wave whose enemyTypes are ALL
///      generic filler (Soldado / Soldier) has them replaced by the corrupted
///      forms of that wave's syllable list, and its characters list is CLEARED.
///      WaveSpawner's per-spawn character pick overrides an enemy's pinned
///      assignedCharacter, which would let a Mantsa spawn wearing BA; with
///      characters empty the spawner falls back to the pinned identity, so each
///      creature carries its own syllable and the wave's syllable mix is
///      preserved by mirroring it into enemyTypes.
///   2. Level 1 tutorial steps (BA/OU/HA) and the onboarding sequence's
///      heart-loss demo swap Soldado for the matching corrupted form.
///   3. AlmanacEnemyRegistry_Default gains the seventeen corruption entries
///      (existing entries untouched) so the new roster is discoverable.
///
/// DELIBERATELY UNTOUCHED: any wave containing a variant or era enemy. Those
/// carry mechanics and identity the corruption roster does not yet have —
/// Fraile phases, Maestro is a no-contact decoy, Capitan is a 2 HP shield,
/// Guardia runs at 2.25, General is a 3 HP commander, and Heitai/Kisha/Kempei/
/// Shokan carry the Japanese era — while all seventeen corrupted forms are
/// still identical 1.5-speed, 1 HP walkers with no signature ability (SALIN-206
/// left stats and abilities unset on purpose). Replacing those waves would
/// flatten the difficulty curve and drop Guardia/Capitan, which
/// SpanishEnemyVariantDataTests asserts must appear in Levels 3 and 4.
/// Mixing the two rosters inside one wave needs a spawner rule that lets a
/// pinned identity win over the wave roster; that is a product decision, so it
/// is left to the team rather than guessed at here.
///
/// RA maps to Daan-Lihis: DA and RA share one ancestral form and the enemy's
/// design owns that connection. Boss configs, intermission waves, and the
/// scene-level fallbackEnemyData are deliberately left alone.
/// </summary>
public static class CorruptionRosterWiringTool
{
    private const string EnemyFolder = "Assets/ScriptableObjects/Enemies";
    private const string LevelFolder = "Assets/ScriptableObjects/Levels";

    // Explicit list: Fraile and Maestro also pin BA, so the corruption set
    // cannot be derived from assignedCharacter alone.
    private static readonly string[] CorruptionAssets =
    {
        "EnemyData_AbongSimula",   // A
        "EnemyData_Bakod",         // BA
        "EnemyData_Daan-Lihis",    // DA (and RA)
        "EnemyData_Gapos",         // GA
        "EnemyData_Hati",          // HA
        "EnemyData_Iligaw",        // EI
        "EnemyData_Kadena",        // KA
        "EnemyData_Labo",          // LA
        "EnemyData_Mantsa",        // MA
        "EnemyData_NawalangMukha", // NA
        "EnemyData_Ngatngat",      // NGA
        "EnemyData_Punit",         // PA
        "EnemyData_Salungat",      // SA
        "EnemyData_Takip",         // TA
        "EnemyData_Uhaw",          // OU
        "EnemyData_Walang-Awa",    // WA
        "EnemyData_YaposngDilim",  // YA
    };

    [MenuItem("Salinlahi/Art/Wire Corruption Roster")]
    public static void Run()
    {
        Dictionary<string, EnemyDataSO> byCharacter = LoadCorruptionMap();
        if (byCharacter.Count == 0)
        {
            Debug.LogError("[CorruptionRosterWiring] No corruption EnemyDataSO assets found — nothing wired.");
            return;
        }

        int wavesWired = WireLevelWaves(byCharacter);
        int tutorialWired = WireTutorial(byCharacter);
        int almanacAdded = WireAlmanac();

        AssetDatabase.SaveAssets();
        Debug.Log($"[CorruptionRosterWiring] Done. waves={wavesWired} tutorialRefs={tutorialWired} almanacEntriesAdded={almanacAdded}");
    }

    private static Dictionary<string, EnemyDataSO> LoadCorruptionMap()
    {
        var map = new Dictionary<string, EnemyDataSO>();
        foreach (string name in CorruptionAssets)
        {
            var so = AssetDatabase.LoadAssetAtPath<EnemyDataSO>($"{EnemyFolder}/{name}.asset");
            if (so == null || so.assignedCharacter == null)
            {
                Debug.LogError($"[CorruptionRosterWiring] Missing or unpinned: {name}");
                continue;
            }
            map[so.assignedCharacter.characterID] = so;
        }

        // DA and RA share Daan-Lihis.
        if (map.TryGetValue("DA", out EnemyDataSO daanLihis))
            map["RA"] = daanLihis;

        return map;
    }

    private static int WireLevelWaves(Dictionary<string, EnemyDataSO> byCharacter)
    {
        int wavesWired = 0;
        for (int levelNumber = 1; levelNumber <= 15; levelNumber++)
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
                $"{LevelFolder}/Level{levelNumber}_Config.asset");
            if (config == null || config.waves == null || config.waves.Count == 0)
            {
                Debug.Log($"[CorruptionRosterWiring] Level {levelNumber}: no waves (boss-only or missing) — skipped.");
                continue;
            }

            // Collect first, then widen the level roster, THEN write the waves.
            // LevelConfigSO.OnValidate calls ReconcileWavesToRoster, which prunes
            // every wave's enemyTypes down to allowedEnemyTypes — so an enemy
            // written into a wave without being on the roster is silently
            // stripped back out the moment the asset is marked dirty.
            var neededOnRoster = new List<EnemyDataSO>();
            for (int w = 0; w < config.waves.Count; w++)
            {
                WaveDefinition scan = config.waves[w];
                if (scan == null || scan.isIntermissionWave || !IsGenericFillerOnly(scan))
                    continue;
                foreach (EnemyDataSO enemy in MapWave(scan, config, byCharacter, out _))
                {
                    if (!neededOnRoster.Contains(enemy))
                        neededOnRoster.Add(enemy);
                }
            }

            bool changed = false;
            if (config.allowedEnemyTypes == null)
                config.allowedEnemyTypes = new List<EnemyDataSO>();
            foreach (EnemyDataSO enemy in neededOnRoster)
            {
                if (!config.allowedEnemyTypes.Contains(enemy))
                {
                    config.allowedEnemyTypes.Add(enemy);
                    changed = true;
                }
            }

            for (int w = 0; w < config.waves.Count; w++)
            {
                WaveDefinition wave = config.waves[w];
                if (wave == null || wave.isIntermissionWave)
                    continue;

                if (!IsGenericFillerOnly(wave))
                {
                    Debug.Log($"[CorruptionRosterWiring] Level {levelNumber} wave {w}: keeps a variant/era enemy — left untouched.");
                    continue;
                }

                List<EnemyDataSO> mapped = MapWave(wave, config, byCharacter, out List<string> missing);

                if (mapped.Count == 0)
                {
                    Debug.LogWarning($"[CorruptionRosterWiring] Level {levelNumber} wave {w}: no mappable syllables (missing: {string.Join(",", missing)}) — left untouched.");
                    continue;
                }
                if (missing.Count > 0)
                    Debug.LogWarning($"[CorruptionRosterWiring] Level {levelNumber} wave {w}: no corrupted form for {string.Join(",", missing)} — those syllables dropped from the wave.");

                wave.enemyTypes = mapped;
                // Cleared so the spawner falls back to each enemy's pinned
                // syllable instead of overriding it per spawn.
                wave.characters = new List<BaybayinCharacterSO>();
                changed = true;
                wavesWired++;
            }

            if (changed)
                EditorUtility.SetDirty(config);
        }
        return wavesWired;
    }

    /// <summary>
    /// The corrupted forms of a wave's syllables, in roster order, de-duplicated.
    /// Falls back to the level roster when the wave lists no characters of its own.
    /// </summary>
    private static List<EnemyDataSO> MapWave(
        WaveDefinition wave,
        LevelConfigSO config,
        Dictionary<string, EnemyDataSO> byCharacter,
        out List<string> missing)
    {
        List<BaybayinCharacterSO> source =
            wave.characters != null && wave.characters.Count > 0
                ? wave.characters
                : config.allowedCharacters;

        var mapped = new List<EnemyDataSO>();
        missing = new List<string>();
        if (source == null)
            return mapped;

        foreach (BaybayinCharacterSO character in source)
        {
            if (character == null) continue;
            if (byCharacter.TryGetValue(character.characterID, out EnemyDataSO enemy))
            {
                if (!mapped.Contains(enemy))
                    mapped.Add(enemy);
            }
            else
            {
                missing.Add(character.characterID);
            }
        }
        return mapped;
    }

    /// <summary>
    /// True when every enemy in the wave is interchangeable filler, i.e. a plain
    /// walker with no variant flag, no era identity, and nothing pinned. Only
    /// those waves are safe to hand wholesale to the corruption roster.
    /// </summary>
    private static bool IsGenericFillerOnly(WaveDefinition wave)
    {
        if (wave.enemyTypes == null || wave.enemyTypes.Count == 0)
            return false;

        foreach (EnemyDataSO enemy in wave.enemyTypes)
        {
            if (enemy == null)
                continue;
            if (enemy.enemyID != "soldado" && enemy.enemyID != "soldier")
                return false;
        }
        return true;
    }

    private static int WireTutorial(Dictionary<string, EnemyDataSO> byCharacter)
    {
        int wired = 0;
        foreach (string stepName in new[] { "Level1TutorialStep_BA", "Level1TutorialStep_OU", "Level1TutorialStep_HA" })
        {
            var step = AssetDatabase.LoadAssetAtPath<Level1TutorialStepSO>(
                $"Assets/ScriptableObjects/Tutorial/{stepName}.asset");
            if (step == null || step.targetCharacter == null)
            {
                Debug.LogWarning($"[CorruptionRosterWiring] Tutorial step missing or has no targetCharacter: {stepName}");
                continue;
            }
            if (byCharacter.TryGetValue(step.targetCharacter.characterID, out EnemyDataSO enemy)
                && step.enemyData != enemy)
            {
                step.enemyData = enemy;
                EditorUtility.SetDirty(step);
                wired++;
            }
        }

        var sequence = AssetDatabase.LoadAssetAtPath<OnboardingSequenceSO>(
            "Assets/ScriptableObjects/Tutorial/Level1OnboardingSequence.asset");
        if (sequence != null && sequence.heartLossDemoEnemyData != null)
        {
            BaybayinCharacterSO demoChar = sequence.heartLossDemoEnemyData.assignedCharacter;
            // The demo enemy's pinned syllable (if any) picks its corrupted
            // form; otherwise mirror the BA teaching step's creature.
            string key = demoChar != null ? demoChar.characterID : "BA";
            if (byCharacter.TryGetValue(key, out EnemyDataSO demoEnemy)
                && sequence.heartLossDemoEnemyData != demoEnemy)
            {
                sequence.heartLossDemoEnemyData = demoEnemy;
                EditorUtility.SetDirty(sequence);
                wired++;
            }
        }
        return wired;
    }

    private static int WireAlmanac()
    {
        var registry = AssetDatabase.LoadAssetAtPath<AlmanacEnemyRegistrySO>(
            "Assets/ScriptableObjects/Almanac/AlmanacEnemyRegistry_Default.asset");
        if (registry == null)
        {
            Debug.LogWarning("[CorruptionRosterWiring] Almanac registry not found — corruption enemies stay undiscoverable.");
            return 0;
        }

        var present = new HashSet<EnemyDataSO>();
        foreach (AlmanacEnemyEntry entry in registry.entries)
        {
            if (entry != null && entry.enemyData != null)
                present.Add(entry.enemyData);
        }

        int added = 0;
        foreach (string name in CorruptionAssets)
        {
            var so = AssetDatabase.LoadAssetAtPath<EnemyDataSO>($"{EnemyFolder}/{name}.asset");
            if (so == null || present.Contains(so))
                continue;
            registry.entries.Add(new AlmanacEnemyEntry { enemyData = so });
            added++;
        }

        if (added > 0)
            EditorUtility.SetDirty(registry);
        return added;
    }
}
