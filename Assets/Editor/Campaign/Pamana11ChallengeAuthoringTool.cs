using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-153. Authors the Pamana Level 11 context challenge and wires it to Level11_Config.
///
/// Shape follows the authored ERA-OPENER challenges rather than the sentence ones. Ugat 2 and Ugat 5
/// both use WordPlacement with two units, one per focus word; Ugat 3, Ugat 4 and Ugnayan 9 use
/// SentenceRestoration with a single unit because their tickets supply a sentence. SALIN-153 supplies
/// no sentence, and AC3 speaks of "both words are restored", so the two-unit opener shape fits.
///
/// TWO DECISIONS HERE ARE MINE AND NEED CONFIRMING.
///
/// 1. Clue policy is Reduced, not Minimal. The escalation so far runs Full (Ugat 1-3) then Reduced
///    (Ugat 4, Ugat 5, Ugnayan 9), so Minimal would be the next step for a final-era level. But
///    Level 11 INTRODUCES DA/RA and LA -- the user story asks for "guided instruction for DALA and
///    DAMA" -- and withholding clues on symbols the player is meeting for the first time works
///    against the level's own instructional purpose. Reduced keeps the era's difficulty step without
///    doing that. Minimal likely belongs on a later Pamana level that introduces nothing new.
///
/// 2. The prompt copy. There is no Pamana narrative document at all: docs/content/ holds Ugat and
///    Ugnayan only, so unlike every Ugat level there was no authored Filipino line to use, and unlike
///    Ugnayan 9 the ticket supplies no sentence either. Both prompts below are mine, written in the
///    register of the shipped Ugat prompts. SALIN-188 gates them.
///
/// Decoys are words this level's own 14-symbol pool can spell, so they are plausible rather than
/// arbitrary, and they are drawn from earlier eras to reinforce AC1's "reused from earlier eras".
/// </summary>
public static class Pamana11ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana11_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level11_Config.asset";

    private sealed class UnitSpec
    {
        public string UnitId, Prompt, EvidenceId, FocusId, FocusText;
        public (string id, string text)[] Decoys;
    }

    private static readonly UnitSpec[] Units =
    {
        new UnitSpec {
            UnitId = "pamana11-complete-dala",
            Prompt = "May dala kang alaala mula sa mga naunang panahon. Buuin mo ang salitang " +
                     "nagsasabi kung ano ang ginagawa mo.",
            FocusId = "pamana11-dala", FocusText = "DALA",
            Decoys = new[] { ("pamana11-mana-decoy", "MANA"), ("pamana11-sama-decoy", "SAMA") },
            EvidenceId = "level.pamana.01.focus.01",
        },
        new UnitSpec {
            UnitId = "pamana11-complete-dama",
            Prompt = "Hindi sapat na dalhin lamang ang alaala. Buuin mo ang salitang nagsasabi " +
                     "kung paano mo ito nauunawaan.",
            FocusId = "pamana11-dama", FocusText = "DAMA",
            Decoys = new[] { ("pamana11-mata-decoy", "MATA"), ("pamana11-gawa-decoy", "GAWA") },
            EvidenceId = "level.pamana.01.focus.02",
        },
    };

    [MenuItem("Salinlahi/SALIN-153/Author Pamana 11 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Pamana 11 context challenge ===\n");

        // Load-and-mutate when it already exists: CreateAsset over an existing path reissues the
        // GUID and would silently unwire Level11_Config.challengeSequence.
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.pamana.11";
        so.FindProperty("displayName").stringValue = "Unang Alaala ng Pamana";

        SerializedProperty units = so.FindProperty("units");
        units.arraySize = Units.Length;

        for (int u = 0; u < Units.Length; u++)
        {
            UnitSpec spec = Units[u];
            SerializedProperty unit = units.GetArrayElementAtIndex(u);

            unit.FindPropertyRelative("unitId").stringValue = spec.UnitId;
            unit.FindPropertyRelative("mode").enumValueIndex = 1;         // WordPlacement
            unit.FindPropertyRelative("cluePolicy").enumValueIndex = 1;   // Reduced -- see class note
            unit.FindPropertyRelative("prompt").stringValue = spec.Prompt;

            SerializedProperty t = unit.FindPropertyRelative("tokens");
            t.arraySize = 1 + spec.Decoys.Length;
            WriteToken(t.GetArrayElementAtIndex(0), spec.FocusId, spec.FocusText, 1);
            for (int d = 0; d < spec.Decoys.Length; d++)
                WriteToken(t.GetArrayElementAtIndex(d + 1), spec.Decoys[d].id, spec.Decoys[d].text, 0);

            SerializedProperty slots = unit.FindPropertyRelative("slots");
            slots.arraySize = 1;
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("slotId").stringValue = spec.UnitId + "-slot";
            slots.GetArrayElementAtIndex(0).FindPropertyRelative("expectedOccurrenceId").stringValue = spec.FocusId;

            SerializedProperty candidates = unit.FindPropertyRelative("candidateOccurrenceIds");
            candidates.arraySize = t.arraySize;
            candidates.GetArrayElementAtIndex(0).stringValue = spec.FocusId;
            for (int d = 0; d < spec.Decoys.Length; d++)
                candidates.GetArrayElementAtIndex(d + 1).stringValue = spec.Decoys[d].id;

            unit.FindPropertyRelative("guidedStep").objectReferenceValue = null;
            unit.FindPropertyRelative("timerSeconds").intValue = 0;
            unit.FindPropertyRelative("allowHint").boolValue = true;
            unit.FindPropertyRelative("checkpointOnSuccess").boolValue = true;
            unit.FindPropertyRelative("memoryRevealSeconds").intValue = 1;
            unit.FindPropertyRelative("maxErrors").intValue = 3;
            unit.FindPropertyRelative("heartPenalty").intValue = 1;
            unit.FindPropertyRelative("evidenceContentId").stringValue = spec.EvidenceId;

            log.AppendLine($"  unit {u + 1}: {spec.FocusText} (+{spec.Decoys.Length} decoys)  " +
                           $"mode=WordPlacement cluePolicy=Reduced");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        log.AppendLine($"  {(created ? "created" : "updated")} {Path.GetFileName(AssetPath)}");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"{LevelPath} not found."); return; }
        var lso = new SerializedObject(level);
        lso.FindProperty("challengeSequence").objectReferenceValue = sequence;
        lso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);

        AssetDatabase.SaveAssets();

        // Assert the inbound reference resolves; nothing else will tell us.
        var check = new SerializedObject(level);
        log.AppendLine("  Level11_Config.challengeSequence wired: " +
                       (check.FindProperty("challengeSequence").objectReferenceValue == sequence));

        Debug.Log(log.ToString());
        File.WriteAllText("pamana11-challenge-report.txt", log.ToString());
    }

    private static void WriteToken(SerializedProperty e, string id, string text, int role)
    {
        e.FindPropertyRelative("tokenId").stringValue = id;
        e.FindPropertyRelative("displayText").stringValue = text;
        e.FindPropertyRelative("occurrenceId").stringValue = id;
        e.FindPropertyRelative("role").enumValueIndex = role;
        e.FindPropertyRelative("targetCharacter").objectReferenceValue = null;
        e.FindPropertyRelative("evidenceContentId").stringValue = string.Empty;
    }
}
