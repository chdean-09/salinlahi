using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-158. Authors the Pamana Level 15 context challenge and wires it to Level15_Config.
///
/// SCOPE: THIS COVERS AC2 ONLY. SALIN-158 is the largest level ticket in the backlog -- seven
/// acceptance criteria spanning the final challenge, the Paglimot encounter, the ending sequence,
/// the completed-journey state, save/restore across an app reopen, and a constraint on Endless Mode
/// controls. Only AC2 is level-content:
///
///   AC2  "PAMANA and MALAYA can be completed using the approved basic character set"  -> HERE.
///   AC1  PA taught and practised before PAMANA assesses it  -> satisfied by the generated
///        requirement lists; the validator's PaInstructionOrderInvalid rule checks exactly this.
///   AC3  restore "the configured final paragraph across all three phases"  -> BLOCKED. The
///        paragraph does not exist. It is also required by SALIN-147 AC2 and SALIN-152 AC2, so one
///        piece of writing unblocks three tickets. It would use ChallengeMode.ParagraphRestoration,
///        which no authored challenge uses yet.
///   AC4  the "memory becoming inheritance" ending message  -> narrative copy, not yet written.
///   AC5  completed-journey state with review, replay and Credits  -> runtime and UI.
///   AC6  that state surviving an app reopen  -> save/restore.
///   AC7  no enabled control may promise Endless Mode  -> UI constraint. Nothing in the copy below
///        gestures at content beyond the ending, which is the part of AC7 that touches this asset.
///
/// Shape follows the era-opener form used by Levels 11 and 12: WordPlacement with two units, one per
/// focus word. AC2 describes word forming rather than a sentence, and unlike SALIN-155 and SALIN-156
/// this ticket quotes no sentence to restore.
///
/// Clue policy is Reduced, consistent with Levels 11, 12 and 14. Level 15 introduces PA -- the
/// seventeenth and final symbol -- and AC1 explicitly wants guided instruction and practice before
/// assessment, so withholding clues on a symbol the player just met would work against the ticket's
/// own criterion. WORTH SURFACING: Minimal is now the only clue policy never used anywhere in the
/// campaign, and every level that might have justified it introduces new symbols. Whether Minimal
/// has a home at all is a design question the backlog has not answered.
///
/// DECOYS are chosen to punish skimming. MANA is a suffix of PAMANA, and SAYA rhymes with MALAYA and
/// shares its final syllable. Both are earlier-level words the 17-symbol pool can still spell.
///
/// THE PROMPT COPY IS MINE AND SHOULD BE REPLACED -- fourth instance, after Levels 11, 12 and 14.
/// docs/content/pamana-levels-11-15-narrative.md holds Level 15's copy as TO BE WRITTEN. SALIN-188
/// was reopened on 2026-09-01 and gates all of it.
/// </summary>
public static class Pamana15ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana15_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level15_Config.asset";

    private sealed class UnitSpec
    {
        public string UnitId, Prompt, EvidenceId, FocusId, FocusText;
        public (string id, string text)[] Decoys;
    }

    private static readonly UnitSpec[] Units =
    {
        new UnitSpec {
            UnitId = "pamana15-complete-pamana",
            Prompt = "Ito ang huling salitang ibinigay sa iyo ng mga nauna. " +
                     "Buuin mo ang pangalan ng iniwan nila sa iyo.",
            FocusId = "pamana15-pamana", FocusText = "PAMANA",
            Decoys = new[] { ("pamana15-mana-decoy", "MANA"), ("pamana15-alaala-decoy", "ALAALA") },
            EvidenceId = "level.pamana.05.focus.01",
        },
        new UnitSpec {
            UnitId = "pamana15-complete-malaya",
            Prompt = "Hindi na makukuha ng Paglimot ang naibalik mo. " +
                     "Buuin mo ang salitang nagsasabi kung ano ka na ngayon.",
            FocusId = "pamana15-malaya", FocusText = "MALAYA",
            Decoys = new[] { ("pamana15-saya-decoy", "SAYA"), ("pamana15-mahalaga-decoy", "MAHALAGA") },
            EvidenceId = "level.pamana.05.focus.02",
        },
    };

    [MenuItem("Salinlahi/SALIN-158/Author Pamana 15 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Pamana 15 context challenge (AC2 only) ===\n");

        // Load-and-mutate when it already exists: CreateAsset over an existing path reissues the
        // GUID and would silently unwire Level15_Config.challengeSequence.
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.pamana.15";
        so.FindProperty("displayName").stringValue = "Huling Alaala";

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
            unit.FindPropertyRelative("timerSeconds").floatValue = 0f;
            unit.FindPropertyRelative("allowHint").boolValue = true;
            unit.FindPropertyRelative("checkpointOnSuccess").boolValue = true;
            unit.FindPropertyRelative("memoryRevealSeconds").floatValue = 1f;
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
        log.AppendLine("  Level15_Config.challengeSequence wired: " +
                       (check.FindProperty("challengeSequence").objectReferenceValue == sequence));

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine("  ChallengeSequenceValidator: " +
                       (result.Errors.Count == 0 ? "no errors" : string.Join(" | ", result.Errors)));

        Debug.Log(log.ToString());
        File.WriteAllText("pamana15-challenge-report.txt", log.ToString());
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
