using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Populates the Ugnayan level configs with their focus words, cumulative symbol pools, and the
/// three requirement lists. Every Ugnayan level shipped as a shell with all five fields empty.
///
/// Covers SALIN-148 (Level 6), SALIN-150 (Level 7), SALIN-151 (Level 8) and SALIN-152 (Level 10).
/// Note the ticket numbering does NOT track level numbers -- SALIN-149 is Level 9, not Level 7.
///
/// LEVEL 9 IS DELIBERATELY ABSENT. SALIN-149 AC1 requires the decompositions O + O and U + NA, which
/// needs distinct spoken values for O and for U. `Char_OU` carries exactly one, `value.ou`. Authoring
/// Level 9 would mean inventing a spoken value the character data does not define, so it is blocked
/// on that data gap rather than guessed at here. Same shape applies to `Char_EI` / `value.ei`.
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
public static class UgnayanLevelDataTool
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
    };

    [MenuItem("Salinlahi/Ugnayan/Populate Level Data")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugnayan level data ===\n");

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
        File.WriteAllText("ugnayan-level-data-report.txt", log.ToString());
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
            var syllables = new List<BaybayinCharacterSO>();
            foreach (string id in f.Syllables)
            {
                BaybayinCharacterSO s = pool.FirstOrDefault(p => p.characterID == id);
                if (s == null)
                {
                    Debug.LogError($"{spec.StableId}: {f.Word} needs {id}, which is not in the pool.");
                    return false;
                }
                syllables.Add(s);
            }

            WriteFocus(focus.GetArrayElementAtIndex(i),
                       $"{spec.StableId}.focus.{(i + 1):00}", f.Word, f.Meaning, syllables);
            log.AppendLine($"  focus {i + 1}: {f.Word} = {string.Join(" + ", f.Syllables)}" +
                           $"  (meaning \"{f.Meaning}\")");
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
                                   string meaning, List<BaybayinCharacterSO> decomposition)
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
            s.FindPropertyRelative("symbol").objectReferenceValue = decomposition[i];
            s.FindPropertyRelative("spokenValueId").stringValue = SpokenValueId(decomposition[i]);
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

    /// <summary>A symbol's own first spoken value -- never a guessed "value." + id string.</summary>
    private static string SpokenValueId(BaybayinCharacterSO symbol)
    {
        var so = new SerializedObject(symbol);
        SerializedProperty values = so.FindProperty("spokenValues");
        return values != null && values.arraySize > 0
            ? values.GetArrayElementAtIndex(0).FindPropertyRelative("stableId").stringValue
            : string.Empty;
    }
}
