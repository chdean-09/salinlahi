using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-154. Authors the Pamana Level 12 context challenge and wires it to Level12_Config.
///
/// Same shape as Pamana 11: WordPlacement with two units, one per focus word. SALIN-154 supplies no
/// sentence -- unlike SALIN-155 and SALIN-156, whose acceptance criteria quote one -- and AC3 speaks
/// of "both words are complete", so the two-unit form fits. Ugat 2 and Ugat 5 use the same shape.
///
/// Clue policy is Reduced, matching Level 11. Level 12 still INTRODUCES symbols (HA and NGA), so the
/// same reasoning applies: withholding clues on symbols the player is meeting for the first time
/// works against the level's instructional purpose. Minimal has never been used anywhere in the
/// campaign and is still unruled for Levels 12, 13 and 15; it most plausibly belongs on a level that
/// introduces nothing new.
///
/// DECOYS reinforce AC2. That criterion is about GA being reused "without a duplicate introduction",
/// so every decoy here is a word from an earlier level that this level's own 16-symbol pool can
/// still spell: AWA and KASAMA from Ugnayan, DALA and DAMA from Level 11. None is a focus word of a
/// LATER level, which would preview content the player has not reached.
///
/// THE PROMPT COPY IS MINE AND NEEDS REPLACING. docs/content/pamana-levels-11-15-narrative.md exists
/// now but its Level 12 lines are still TO BE WRITTEN. That document's own warning is that Pamana
/// work will keep producing implementer-written prompts until it is authored -- this is the second
/// instance, after Level 11. Both should be replaced from the document, not from here. SALIN-188
/// gates them.
/// </summary>
public static class Pamana12ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana12_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level12_Config.asset";

    private sealed class UnitSpec
    {
        public string UnitId, Prompt, EvidenceId, FocusId, FocusText;
        public (string id, string text)[] Decoys;
    }

    private static readonly UnitSpec[] Units =
    {
        new UnitSpec {
            UnitId = "pamana12-complete-hanga",
            Prompt = "May mga tao na nag-ingat sa sulat nang walang humihingi sa kanila. " +
                     "Buuin mo ang salitang nararapat sa kanila.",
            FocusId = "pamana12-hanga", FocusText = "HANGA",
            Decoys = new[] { ("pamana12-awa-decoy", "AWA"), ("pamana12-dama-decoy", "DAMA") },
            EvidenceId = "level.pamana.02.focus.01",
        },
        new UnitSpec {
            UnitId = "pamana12-complete-halaga",
            Prompt = "Hindi nasusukat sa ginto ang kanilang iniingatan. " +
                     "Buuin mo ang salitang nagsasabi kung ano ang taglay nito.",
            FocusId = "pamana12-halaga", FocusText = "HALAGA",
            Decoys = new[] { ("pamana12-kasama-decoy", "KASAMA"), ("pamana12-dala-decoy", "DALA") },
            EvidenceId = "level.pamana.02.focus.02",
        },
    };

    [MenuItem("Salinlahi/SALIN-154/Author Pamana 12 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Pamana 12 context challenge ===\n");

        // Load-and-mutate when it already exists: CreateAsset over an existing path reissues the
        // GUID and would silently unwire Level12_Config.challengeSequence.
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.pamana.12";
        so.FindProperty("displayName").stringValue = "Ikalawang Alaala ng Pamana";

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
                           "mode=WordPlacement cluePolicy=Reduced");
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

        var check = new SerializedObject(level);
        log.AppendLine("  Level12_Config.challengeSequence wired: " +
                       (check.FindProperty("challengeSequence").objectReferenceValue == sequence));

        Debug.Log(log.ToString());
        File.WriteAllText("pamana12-challenge-report.txt", log.ToString());
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
