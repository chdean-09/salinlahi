using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-149. Authors the Ugnayan Level 9 context challenge and wires it to Level9_Config.
///
/// Shape follows the authored Ugat sentence challenges (Ugat 3 and 4): mode SentenceRestoration,
/// prompt carrying an instruction line and the sentence with blanks, focus tokens plus decoys, and
/// one slot per blank in sentence order.
///
/// TWO BLANKS HERE, unlike Ugat 3 and 4, which were ruled down to one. Those two had authored copy
/// saying "isang salita lamang" -- only one word missing -- and their first focus word arrived
/// already known from the previous level. Neither applies here: AC3 says "Given both words complete
/// `Sinabi niyang OO at siya ang naging UNA sa pagtulong.`", and OO and UNA are both introduced by
/// this level, so neither is reinforcement.
///
/// Clue policy is Reduced, from AC2: "Given reduced guidance is configured, when the player draws,
/// then the game does not reveal the full answer before an allowed help condition is met."
///
/// Slot order matters: ChallengeSession.SubmitRestoration compares the submitted list against the
/// slots with SequenceEqual, so the slots run in sentence order -- OO, then UNA.
///
/// The sentence is verbatim from AC3 and is the team's Filipino. The INSTRUCTION line is mine and
/// needs SALIN-188 review: docs/content/ugnayan-levels-6-10-narrative.md is still a scaffold with
/// its Level 9 copy marked TO BE WRITTEN, so unlike the Ugat levels there was no authored line to
/// use. It is written in the register of the shipped Ugat prompts (a thematic sentence, then an
/// instruction).
/// </summary>
public static class Ugnayan09ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugnayan09_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level9_Config.asset";

    // AC3, verbatim, with the two focus words blanked.
    private const string Prompt =
        "Dalawang salita ang kulang sa alaala ng pagtulong. Ilagay mo ang bawat isa sa tamang puwang.\n\n" +
        "Sinabi niyang ______ at siya ang naging ______ sa pagtulong.";

    [MenuItem("Salinlahi/SALIN-149/Author Ugnayan 9 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugnayan 9 context challenge ===\n");

        // Load-and-mutate when the asset already exists. CreateAsset over an existing path reissues
        // the GUID, which would silently unwire Level9_Config.challengeSequence.
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.ugnayan.09";
        so.FindProperty("displayName").stringValue = "Ikasiyam na Alaala";

        SerializedProperty units = so.FindProperty("units");
        units.arraySize = 1;
        SerializedProperty unit = units.GetArrayElementAtIndex(0);

        unit.FindPropertyRelative("unitId").stringValue = "ugnayan09-restore-sentence";
        unit.FindPropertyRelative("mode").enumValueIndex = 2;         // SentenceRestoration
        unit.FindPropertyRelative("cluePolicy").enumValueIndex = 1;   // Reduced, per AC2
        unit.FindPropertyRelative("prompt").stringValue = Prompt;

        // Focus tokens first, then decoys. Decoys are drawn from words this level's own pool can
        // spell, so they are plausible rather than arbitrary.
        (string id, string text, int role)[] tokens =
        {
            ("ugnayan09-oo",         "OO",   1),
            ("ugnayan09-una",        "UNA",  1),
            ("ugnayan09-sana-decoy", "SANA", 0),
            ("ugnayan09-gana-decoy", "GANA", 0),
        };

        SerializedProperty t = unit.FindPropertyRelative("tokens");
        t.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
        {
            SerializedProperty e = t.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("tokenId").stringValue = tokens[i].id;
            e.FindPropertyRelative("displayText").stringValue = tokens[i].text;
            e.FindPropertyRelative("occurrenceId").stringValue = tokens[i].id;
            e.FindPropertyRelative("role").enumValueIndex = tokens[i].role;
            e.FindPropertyRelative("targetCharacter").objectReferenceValue = null;
            e.FindPropertyRelative("evidenceContentId").stringValue = string.Empty;
        }

        // Sentence order: OO fills the first blank, UNA the second.
        SerializedProperty slots = unit.FindPropertyRelative("slots");
        slots.arraySize = 2;
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("slotId").stringValue = "ugnayan09-slot-01";
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("expectedOccurrenceId").stringValue = "ugnayan09-oo";
        slots.GetArrayElementAtIndex(1).FindPropertyRelative("slotId").stringValue = "ugnayan09-slot-02";
        slots.GetArrayElementAtIndex(1).FindPropertyRelative("expectedOccurrenceId").stringValue = "ugnayan09-una";

        SerializedProperty candidates = unit.FindPropertyRelative("candidateOccurrenceIds");
        candidates.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
            candidates.GetArrayElementAtIndex(i).stringValue = tokens[i].id;

        unit.FindPropertyRelative("guidedStep").objectReferenceValue = null;
        unit.FindPropertyRelative("timerSeconds").intValue = 0;
        unit.FindPropertyRelative("allowHint").boolValue = true;
        unit.FindPropertyRelative("checkpointOnSuccess").boolValue = true;
        unit.FindPropertyRelative("memoryRevealSeconds").intValue = 1;
        unit.FindPropertyRelative("maxErrors").intValue = 3;
        unit.FindPropertyRelative("heartPenalty").intValue = 1;
        unit.FindPropertyRelative("evidenceContentId").stringValue = "level.ugnayan.04.focus.01";

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        log.AppendLine($"  {(created ? "created" : "updated")} {Path.GetFileName(AssetPath)}");
        log.AppendLine("  mode=SentenceRestoration  cluePolicy=Reduced  blanks=2");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"{LevelPath} not found."); return; }
        var lso = new SerializedObject(level);
        lso.FindProperty("challengeSequence").objectReferenceValue = sequence;
        lso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);

        AssetDatabase.SaveAssets();

        // Assert the inbound reference actually resolves; nothing else will tell us.
        var check = new SerializedObject(level);
        bool wired = check.FindProperty("challengeSequence").objectReferenceValue == sequence;
        log.AppendLine($"  Level9_Config.challengeSequence wired: {wired}");

        Debug.Log(log.ToString());
        File.WriteAllText("ugnayan09-challenge-report.txt", log.ToString());
    }
}
