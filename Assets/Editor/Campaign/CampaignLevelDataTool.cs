using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Populates level configs with their focus words, cumulative symbol pools, the three requirement
/// lists, and the final restoration syllable. Every level past Ugat 5 shipped as a shell with all of
/// those empty.
///
/// Covers Ugnayan -- SALIN-148 (L6), SALIN-150 (L7), SALIN-151 (L8), SALIN-149 (L9), SALIN-152 (L10)
/// -- and Pamana, starting with SALIN-153 (L11). Note the ticket numbering does NOT track level
/// numbers: SALIN-149 is Level 9, not Level 7.
///
/// LEVEL 9 WAS BRIEFLY THOUGHT BLOCKED, AND IS NOT. SALIN-149 AC1 asks for the decompositions
/// O + O and U + NA "using the basic O/U character defined by the plan". That phrase is the answer,
/// not the problem: the plan defines ONE shared vowel symbol, `Char_OU`, and O versus U is a
/// romanisation of the same glyph rather than two spoken values to invent.
///
/// The precedent already shipped. Level 1 authors `INA` as `EI + NA` with `value.ei` -- the shared
/// vowel character carrying its single shared spoken value for a word romanised with "I". `OO` and
/// `UNA` follow that exactly, with `value.ou`.
///
/// Everything below is DERIVED from existing authoritative data, not chosen:
///
///  * Focus slots come from the approved matrix's 30-slot table
///    (docs/technical/TW-SPK-004-educational-content-matrix.md, rows 6-10).
///  * Decompositions come verbatim from each ticket's acceptance criteria, except SANA and SAYA --
///    SALIN-152 never states them. Those two are marked below and need confirming.
///  * Each cumulative pool is computed from the characters' own `firstIntroductionLevelId` values in
///    campaign catalog order. Nothing is hand-listed, so a pool cannot drift from the character data.
///  * Spoken value ids are read from each symbol's own `spokenValues[0].stableId` -- never a
///    guessed "value." + id string.
///  * The three requirement lists follow the shape every authored Ugat level uses, verified
///    identical across Levels 2-5: one entry per pool symbol, Instruction x1, Practice x2,
///    Mastery x1.
///
/// The `meaning` strings are English developer- and matrix-facing glosses rather than player-facing
/// copy, and are the only authored values here. See the PR note.
/// </summary>
public static class CampaignLevelDataTool
{
    private const string CampaignPath = "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";

    private sealed class FocusSpec
    {
        public string Word;
        public string Meaning;
        public string[] Syllables;
    }

    private sealed class LevelSpec
    {
        public string AssetPath;
        public string StableId;
        public string Ticket;
        public FocusSpec[] Focus;
        /// Matrix "Workbook last syllable" column, rows 6-10.
        public string FinalSyllable;
    }

    private static readonly LevelSpec[] Levels =
    {
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level6_Config.asset",
            StableId  = "level.ugnayan.01",
            Ticket    = "SALIN-148",
            FinalSyllable = "WA",
            Focus = new[]
            {
                // SALIN-148 AC2: "the syllable order is A + WA and GA + WA".
                new FocusSpec { Word = "AWA",  Meaning = "compassion", Syllables = new[] { "A",  "WA" } },
                new FocusSpec { Word = "GAWA", Meaning = "action",     Syllables = new[] { "GA", "WA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level7_Config.asset",
            StableId  = "level.ugnayan.02",
            Ticket    = "SALIN-150",
            FinalSyllable = "MA",
            Focus = new[]
            {
                // SALIN-150 AC1: "SAMA is SA + MA and KASAMA is KA + SA + MA".
                new FocusSpec { Word = "SAMA",   Meaning = "to join",   Syllables = new[] { "SA", "MA" } },
                new FocusSpec { Word = "KASAMA", Meaning = "companion", Syllables = new[] { "KA", "SA", "MA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level8_Config.asset",
            StableId  = "level.ugnayan.03",
            Ticket    = "SALIN-151",
            FinalSyllable = "YA",
            Focus = new[]
            {
                // SALIN-151 AC2: "the player restores GA + NA and KA + YA".
                new FocusSpec { Word = "GANA", Meaning = "drive",      Syllables = new[] { "GA", "NA" } },
                new FocusSpec { Word = "KAYA", Meaning = "capability", Syllables = new[] { "KA", "YA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level9_Config.asset",
            StableId  = "level.ugnayan.04",
            Ticket    = "SALIN-149",
            FinalSyllable = "NA",
            Focus = new[]
            {
                // SALIN-149 AC1: "their decompositions are O + O and U + NA using the basic O/U
                // character defined by the plan". Both O and U resolve to the shared OU symbol.
                new FocusSpec { Word = "OO",  Meaning = "yes",   Syllables = new[] { "OU", "OU" } },
                new FocusSpec { Word = "UNA", Meaning = "first", Syllables = new[] { "OU", "NA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level10_Config.asset",
            StableId  = "level.ugnayan.05",
            Ticket    = "SALIN-152",
            FinalSyllable = "YA",
            Focus = new[]
            {
                // SALIN-152 states no decompositions. These two are DERIVED from the syllable
                // structure and need confirming; the meanings follow AC1's "hope and joy return".
                new FocusSpec { Word = "SANA", Meaning = "hope", Syllables = new[] { "SA", "NA" } },
                new FocusSpec { Word = "SAYA", Meaning = "joy",  Syllables = new[] { "SA", "YA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level11_Config.asset",
            StableId  = "level.pamana.01",
            Ticket    = "SALIN-153",
            FinalSyllable = "MA",
            Focus = new[]
            {
                // SALIN-153 AC2: "DALA is DA + LA and DAMA is DA + MA".
                // Both start on DA, which is the only symbol carrying two spoken values
                // (value.da and value.ra). SpokenValueId takes the first, value.da, which is the
                // reading both of these words use. AC1 calls the taught unit "DA/RA", one identity.
                new FocusSpec { Word = "DALA", Meaning = "to carry", Syllables = new[] { "DA", "LA" } },
                new FocusSpec { Word = "DAMA", Meaning = "to feel",  Syllables = new[] { "DA", "MA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level12_Config.asset",
            StableId  = "level.pamana.02",
            Ticket    = "SALIN-154",
            FinalSyllable = "GA",
            Focus = new[]
            {
                // SALIN-154 AC1: "HANGA is HA + NGA and HALAGA is HA + LA + GA".
                // HALAGA reuses GA, learned back in Ugnayan. AC2 requires that reuse to happen
                // "without a duplicate introduction" -- which the derived pool gives for free, since
                // GA entered at level.ugnayan.01 and simply stays in every later pool.
                new FocusSpec { Word = "HANGA",  Meaning = "admiration", Syllables = new[] { "HA", "NGA" } },
                new FocusSpec { Word = "HALAGA", Meaning = "worth",      Syllables = new[] { "HA", "LA", "GA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level14_Config.asset",
            StableId  = "level.pamana.04",
            Ticket    = "SALIN-156",
            FinalSyllable = "GA",
            Focus = new[]
            {
                // SALIN-156 states no decompositions, only that "repeated syllables are represented
                // accurately and in order" (AC1). Both are derived and spell-checked against the
                // written form: A+LA+A+LA reads "alaala", MA+HA+LA+GA reads "mahalaga".
                //
                // ALAALA is FOUR syllables, not five. The trailing vowel of "alaala" is the inherent
                // vowel of the second LA, not a further standalone A -- A+LA+A+LA+A would read
                // "alaalaa". The Pamana scaffold guessed five and is corrected in this change.
                new FocusSpec { Word = "ALAALA",   Meaning = "memory",    Syllables = new[] { "A", "LA", "A", "LA" } },
                new FocusSpec { Word = "MAHALAGA", Meaning = "precious",  Syllables = new[] { "MA", "HA", "LA", "GA" } },
            },
        },
        new LevelSpec
        {
            AssetPath = "Assets/ScriptableObjects/Levels/Level15_Config.asset",
            StableId  = "level.pamana.05",
            Ticket    = "SALIN-158",
            // PA, NOT the matrix's "Workbook last syllable" column, which reads YA for row 15.
            // That column is the last syllable of the level's second focus word, and it matches
            // finalRestorationValue for every other level -- but the finale is a deliberate special
            // case in code: CampaignConfigValidator requires level.pamana.05 to restore
            // symbol.pa / value.pa, with its own error message. Following the matrix here produced
            // a FINAL_RESTORATION_INVALID that only the validator caught.
            FinalSyllable = "PA",
            Focus = new[]
            {
                // SALIN-158 states no decompositions. Both are derived and spell-checked:
                // PA+MA+NA reads "pamana", MA+LA+YA reads "malaya".
                //
                // This level introduces PA, the seventeenth and last symbol, so its pool is the
                // ENTIRE taught set -- the same 17 that BossConfig_Kadiliman requires as draws.
                //
                // AC1 requires PA instruction before PAMANA assesses it. The generated requirement
                // lists satisfy the validator's PaInstructionOrderInvalid rule structurally: PA's
                // first learningRequirements entry is Instruction, and PA also appears in
                // practiceRequirements, masteryRequirements and in the PAMANA focus word.
                new FocusSpec { Word = "PAMANA", Meaning = "inheritance", Syllables = new[] { "PA", "MA", "NA" } },
                new FocusSpec { Word = "MALAYA", Meaning = "free",        Syllables = new[] { "MA", "LA", "YA" } },
            },
        },
    };

    [MenuItem("Salinlahi/Campaign/Populate Level Data")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Campaign level data ===\n");

        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
        if (campaign == null) { Debug.LogError("Campaign config not found."); return; }

        // Ordered level identity, so "introduced at or before this level" is a real comparison
        // rather than a string guess.
        List<string> levelOrder = AssetDatabase
            .FindAssets("t:LevelConfigSO", new[] { "Assets/ScriptableObjects/Levels" })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelConfigSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.stableId))
            .OrderBy(l => l.levelNumber)
            .Select(l => l.stableId)
            .ToList();

        foreach (LevelSpec spec in Levels)
        {
            if (!ApplyLevel(spec, campaign, levelOrder, log)) return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("campaign-level-data-report.txt", log.ToString());
    }

    private static bool ApplyLevel(LevelSpec spec, CampaignConfigSO campaign,
                                   List<string> levelOrder, StringBuilder log)
    {
        // Load and mutate. Never CreateAsset over an existing path -- that reissues the GUID and
        // silently unwires every reference to it.
        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(spec.AssetPath);
        if (level == null) { Debug.LogError($"{spec.AssetPath} not found."); return false; }

        log.AppendLine($"\n{spec.Ticket}  {spec.StableId}  ({Path.GetFileName(spec.AssetPath)})");

        int cutoff = levelOrder.IndexOf(spec.StableId);
        if (cutoff < 0) { Debug.LogError($"{spec.StableId} not in level order."); return false; }

        // Pool = catalog order, filtered to symbols introduced at or before this level.
        var pool = new List<BaybayinCharacterSO>();
        foreach (BaybayinCharacterSO symbol in campaign.symbols)
        {
            if (symbol == null) continue;
            int intro = levelOrder.IndexOf(symbol.firstIntroductionLevelId ?? string.Empty);
            if (intro < 0)
            {
                log.AppendLine($"  SKIP {symbol.characterID}: firstIntroductionLevelId " +
                               $"'{symbol.firstIntroductionLevelId}' is unset or unknown");
                continue;
            }
            if (intro <= cutoff) pool.Add(symbol);
        }
        log.AppendLine($"  pool ({pool.Count}): {string.Join(", ", pool.Select(s => s.characterID))}");

        var so = new SerializedObject(level);

        SerializedProperty focus = so.FindProperty("focusWords");
        focus.arraySize = spec.Focus.Length;
        for (int i = 0; i < spec.Focus.Length; i++)
        {
            FocusSpec f = spec.Focus[i];
            var syllables = new List<(BaybayinCharacterSO Symbol, string ValueId)>();
            foreach (string token in f.Syllables)
            {
                if (!TryResolveSyllable(pool, token, out BaybayinCharacterSO s, out string valueId))
                {
                    Debug.LogError($"{spec.StableId}: {f.Word} needs '{token}', which no pool symbol " +
                                   "provides as a character id or as a spoken value.");
                    return false;
                }
                syllables.Add((s, valueId));
            }

            WriteFocus(focus.GetArrayElementAtIndex(i),
                       $"{spec.StableId}.focus.{(i + 1):00}", f.Word, f.Meaning, syllables);

            // Log the resolved symbol and value per syllable, not just the token, so a contextual
            // reading like RA -> DA/value.ra is visible in the report rather than silent.
            string resolved = string.Join(" + ", syllables.Select(x => $"{x.Symbol.characterID}({x.ValueId})"));
            log.AppendLine($"  focus {i + 1}: {f.Word} = {string.Join(" + ", f.Syllables)}" +
                           $"  ->  {resolved}  (meaning \"{f.Meaning}\")");
        }

        // Final restoration syllable, from the matrix's "Workbook last syllable" column.
        BaybayinCharacterSO final = pool.FirstOrDefault(p => p.characterID == spec.FinalSyllable);
        if (final == null)
        {
            Debug.LogError($"{spec.StableId}: final syllable {spec.FinalSyllable} not in pool.");
            return false;
        }
        SerializedProperty fr = so.FindProperty("finalRestorationValue");
        fr.FindPropertyRelative("symbol").objectReferenceValue = final;
        fr.FindPropertyRelative("spokenValueId").stringValue = SpokenValueId(final);
        log.AppendLine($"  finalRestorationValue: {final.characterID} ({SpokenValueId(final)})");

        WriteSymbolList(so.FindProperty("cumulativeSymbolPool"), pool);
        WriteRequirements(so.FindProperty("learningRequirements"), pool, 0, 1);   // Instruction
        WriteRequirements(so.FindProperty("practiceRequirements"), pool, 1, 2);   // Practice
        WriteRequirements(so.FindProperty("masteryRequirements"), pool, 3, 1);    // Mastery
        log.AppendLine($"  learning={pool.Count}x Instruction(1)  " +
                       $"practice={pool.Count}x Practice(2)  mastery={pool.Count}x Mastery(1)");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        return true;
    }

    private static void WriteFocus(SerializedProperty entry, string stableId, string word,
                                   string meaning,
                                   List<(BaybayinCharacterSO Symbol, string ValueId)> decomposition)
    {
        entry.FindPropertyRelative("stableId").stringValue = stableId;
        entry.FindPropertyRelative("latinSpelling").stringValue = word;
        entry.FindPropertyRelative("displayLabel").stringValue = word;
        entry.FindPropertyRelative("meaning").stringValue = meaning;

        SerializedProperty d = entry.FindPropertyRelative("decomposition");
        d.arraySize = decomposition.Count;
        for (int i = 0; i < decomposition.Count; i++)
        {
            SerializedProperty s = d.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("symbol").objectReferenceValue = decomposition[i].Symbol;
            s.FindPropertyRelative("spokenValueId").stringValue = decomposition[i].ValueId;
        }

        // Media stays unassigned, exactly as every authored Ugat focus word has it.
        SerializedProperty m = entry.FindPropertyRelative("media");
        foreach (string field in new[] { "contextImage", "narrationClip", "dialogue", "cutscene" })
            m.FindPropertyRelative(field).objectReferenceValue = null;
    }

    private static void WriteSymbolList(SerializedProperty list, List<BaybayinCharacterSO> pool)
    {
        list.arraySize = pool.Count;
        for (int i = 0; i < pool.Count; i++)
        {
            SerializedProperty s = list.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("symbol").objectReferenceValue = pool[i];
            s.FindPropertyRelative("spokenValueId").stringValue = SpokenValueId(pool[i]);
        }
    }

    private static void WriteRequirements(SerializedProperty list, List<BaybayinCharacterSO> pool,
                                          int kind, int successes)
    {
        list.arraySize = pool.Count;
        for (int i = 0; i < pool.Count; i++)
        {
            SerializedProperty r = list.GetArrayElementAtIndex(i);
            r.FindPropertyRelative("kind").enumValueIndex = kind;
            r.FindPropertyRelative("requiredSuccesses").intValue = successes;
            SerializedProperty sv = r.FindPropertyRelative("symbolValue");
            sv.FindPropertyRelative("symbol").objectReferenceValue = pool[i];
            sv.FindPropertyRelative("spokenValueId").stringValue = SpokenValueId(pool[i]);
        }
    }

    /// <summary>
    /// Resolves one decomposition token to the symbol that carries it and the spoken value it should
    /// be written with.
    ///
    /// A token normally names a symbol's own characterID: "HA" resolves to Char_HA with its first
    /// spoken value. A token may instead name a CONTEXTUAL READING that some symbol carries as a
    /// later spoken value -- "RA" resolves to Char_DA with value.ra, because RA is not a separate
    /// taught symbol. DA and RA share one glyph with two readings, which is the whole point of the
    /// 17-visual / 18-spoken model (SALIN-212).
    ///
    /// SALIN-155's HARAYA = HA + RA + YA is the only place in the campaign that needs this, but the
    /// contextual case is resolved FROM THE CHARACTER DATA rather than from a hardcoded DA/RA alias,
    /// so any contextual value added later works without touching this method.
    ///
    /// A character id always wins over a contextual value, so a token naming a real symbol can never
    /// be captured by some other symbol's spoken value.
    /// </summary>
    public static bool TryResolveSyllable(IReadOnlyList<BaybayinCharacterSO> pool, string token,
                                          out BaybayinCharacterSO symbol, out string spokenValueId)
    {
        symbol = null;
        spokenValueId = null;
        if (pool == null || string.IsNullOrWhiteSpace(token)) return false;

        foreach (BaybayinCharacterSO candidate in pool)
        {
            if (candidate == null || candidate.characterID != token) continue;
            symbol = candidate;
            spokenValueId = SpokenValueId(candidate);
            return true;
        }

        string wanted = "value." + token.ToLowerInvariant();
        foreach (BaybayinCharacterSO candidate in pool)
        {
            if (candidate == null) continue;
            foreach (string id in SpokenValueIds(candidate))
            {
                if (id != wanted) continue;
                symbol = candidate;
                spokenValueId = id;
                return true;
            }
        }

        return false;
    }

    /// <summary>A symbol's own first spoken value -- never a guessed "value." + id string.</summary>
    private static string SpokenValueId(BaybayinCharacterSO symbol)
    {
        foreach (string id in SpokenValueIds(symbol)) return id;
        return string.Empty;
    }

    /// <summary>Every spoken value id a symbol declares, in authored order.</summary>
    private static IEnumerable<string> SpokenValueIds(BaybayinCharacterSO symbol)
    {
        if (symbol == null) yield break;
        var so = new SerializedObject(symbol);
        SerializedProperty values = so.FindProperty("spokenValues");
        if (values == null) yield break;
        for (int i = 0; i < values.arraySize; i++)
            yield return values.GetArrayElementAtIndex(i).FindPropertyRelative("stableId").stringValue;
    }
}
