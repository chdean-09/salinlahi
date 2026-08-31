using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-148. Populates Level6_Config (`level.ugnayan.01`) with its focus words, cumulative symbol
/// pool, and the three requirement lists. It shipped as a shell: focusWords, cumulativeSymbolPool,
/// learningRequirements, practiceRequirements and masteryRequirements were all empty.
///
/// Everything here is DERIVED from existing authoritative data, not chosen:
///
///  * Focus slots AWA and GAWA come from the approved matrix
///    (docs/technical/TW-SPK-004-educational-content-matrix.md, Verified focus slots).
///  * Decompositions come verbatim from SALIN-148 AC2 — "the syllable order is A + WA and GA + WA".
///  * The cumulative pool is computed from each character's own `firstIntroductionLevelId`, taken in
///    the campaign catalog's symbol order. Nothing is hand-listed, so the pool cannot drift from the
///    character data. This also independently reproduces AC1 — "WA and GA are introduced while
///    previously learned A remains available" — rather than assuming it.
///  * The three requirement lists follow the shape every authored Ugat level uses, verified
///    identical across Levels 2-5: one entry per pool symbol, Instruction x1, Practice x2,
///    Mastery x1.
///
/// The only non-derived values are the two `meaning` strings, which are English developer- and
/// matrix-facing glosses rather than player-facing copy. See the PR note.
/// </summary>
public static class Ugnayan01LevelDataTool
{
    private const string LevelPath    = "Assets/ScriptableObjects/Levels/Level6_Config.asset";
    private const string CampaignPath = "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset";
    private const string LevelStableId = "level.ugnayan.01";

    [MenuItem("Salinlahi/SALIN-148/Populate Level 6 Data")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Level 6 (level.ugnayan.01) ===\n");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        var campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(CampaignPath);
        if (level == null || campaign == null)
        {
            Debug.LogError("Level or campaign config not found."); return;
        }

        // Ordered level identity, so "introduced at or before this level" is a real comparison
        // rather than a string guess.
        List<string> levelOrder = AssetDatabase
            .FindAssets("t:LevelConfigSO", new[] { "Assets/ScriptableObjects/Levels" })
            .Select(g => AssetDatabase.LoadAssetAtPath<LevelConfigSO>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null && !string.IsNullOrWhiteSpace(l.stableId))
            .OrderBy(l => l.levelNumber)
            .Select(l => l.stableId)
            .ToList();

        int cutoff = levelOrder.IndexOf(LevelStableId);
        if (cutoff < 0) { Debug.LogError($"{LevelStableId} not found in level order."); return; }
        log.AppendLine($"  level order position: {cutoff + 1} of {levelOrder.Count}");

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

        BaybayinCharacterSO A = Find(pool, "A"), WA = Find(pool, "WA"), GA = Find(pool, "GA");
        if (A == null || WA == null || GA == null)
        {
            Debug.LogError("AWA/GAWA need A, WA and GA in the pool."); return;
        }

        var so = new SerializedObject(level);

        // --- focus words -------------------------------------------------
        SerializedProperty focus = so.FindProperty("focusWords");
        focus.arraySize = 2;
        WriteFocus(focus.GetArrayElementAtIndex(0), $"{LevelStableId}.focus.01",
                   "AWA", "compassion", new[] { (A, "value.a"), (WA, "value.wa") });
        WriteFocus(focus.GetArrayElementAtIndex(1), $"{LevelStableId}.focus.02",
                   "GAWA", "action", new[] { (GA, "value.ga"), (WA, "value.wa") });
        log.AppendLine("  focusWords: AWA = A + WA, GAWA = GA + WA");

        // --- pool + requirements ----------------------------------------
        WriteSymbolList(so.FindProperty("cumulativeSymbolPool"), pool);
        WriteRequirements(so.FindProperty("learningRequirements"), pool, 0, 1);   // Instruction
        WriteRequirements(so.FindProperty("practiceRequirements"), pool, 1, 2);   // Practice
        WriteRequirements(so.FindProperty("masteryRequirements"), pool, 3, 1);    // Mastery
        log.AppendLine($"  learning={pool.Count}x Instruction(1)  " +
                       $"practice={pool.Count}x Practice(2)  mastery={pool.Count}x Mastery(1)");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();

        Debug.Log(log.ToString());
        File.WriteAllText("level6-data-report.txt", log.ToString());
    }

    private static BaybayinCharacterSO Find(List<BaybayinCharacterSO> pool, string id) =>
        pool.FirstOrDefault(s => s.characterID == id);

    private static void WriteFocus(SerializedProperty entry, string stableId, string word,
                                   string meaning, (BaybayinCharacterSO, string)[] decomposition)
    {
        entry.FindPropertyRelative("stableId").stringValue = stableId;
        entry.FindPropertyRelative("latinSpelling").stringValue = word;
        entry.FindPropertyRelative("displayLabel").stringValue = word;
        entry.FindPropertyRelative("meaning").stringValue = meaning;

        SerializedProperty d = entry.FindPropertyRelative("decomposition");
        d.arraySize = decomposition.Length;
        for (int i = 0; i < decomposition.Length; i++)
        {
            SerializedProperty s = d.GetArrayElementAtIndex(i);
            s.FindPropertyRelative("symbol").objectReferenceValue = decomposition[i].Item1;
            s.FindPropertyRelative("spokenValueId").stringValue = decomposition[i].Item2;
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

    /// <summary>A symbol's own first spoken value — never a guessed "value." + id string.</summary>
    private static string SpokenValueId(BaybayinCharacterSO symbol)
    {
        var so = new SerializedObject(symbol);
        SerializedProperty values = so.FindProperty("spokenValues");
        return values != null && values.arraySize > 0
            ? values.GetArrayElementAtIndex(0).FindPropertyRelative("stableId").stringValue
            : string.Empty;
    }
}
