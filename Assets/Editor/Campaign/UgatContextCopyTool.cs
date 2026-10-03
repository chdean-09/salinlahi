using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-145 / SALIN-144 / SALIN-146 follow-up. Replaces the invented challenge prompts on Ugat
/// Levels 2-4 with the team's authored context copy.
///
/// The copy lives in docs/content/ugat-levels-2-5-narrative.md under each level's "Context copy"
/// heading and predates these challenge assets. The original prompts here were written without
/// finding it — the Filipino was not wrong, but it was not the team's, and this is exactly the
/// wording the SALIN-188 language review exists to protect.
///
/// Where the mechanic needs the sentence visible (Levels 3 and 4 blank words inside a sentence),
/// the authored copy leads and the sentence follows on its own line. The copy is the framing; the
/// sentence is the thing being restored, and the player cannot fill blanks they cannot see.
///
/// Level 2 keeps its two units so per-word Meaning evidence survives (focus.01 and focus.02
/// separately). The authored copy frames the whole challenge, so unit 1 carries it in full and
/// unit 2 carries its instruction sentence — both the team's words, verbatim.
///
/// KNOWN CONFLICT, deliberately not resolved here: Level 3's authored copy says "Isang salita
/// LAMANG ang kulang" — only ONE word is missing — while SALIN-144's AC1 mandates "its TWO blanks
/// clearly correspond to BATA and TAMA". The slot structure follows the ticket; the wording follows
/// the doc. One of the two needs amending and that is a content decision, not an authoring one.
/// </summary>
public static class UgatContextCopyTool
{
    private const string L2 = "Assets/ScriptableObjects/Challenges/Challenge_Ugat02_Context.asset";
    private const string L3 = "Assets/ScriptableObjects/Challenges/Challenge_Ugat03_Context.asset";
    private const string L4 = "Assets/ScriptableObjects/Challenges/Challenge_Ugat04_Context.asset";

    private const string CopyL2 =
        "Hindi makikita ang mukha ng bata hangga't walang MATA. " +
        "Buuin mo ang dalawang salita upang mabuo ang larawan.";
    private const string CopyL2Instruction =
        "Buuin mo ang dalawang salita upang mabuo ang larawan.";
    private const string CopyL3 =
        "Isang salita lamang ang kulang sa pangungusap ni Ama. " +
        "Ilagay mo ang tamang salita sa tamang puwang.";
    private const string CopyL4 =
        "Wala nang larawang gagabay sa iyo. " +
        "Piliin mo ang salitang nararapat, mula lamang sa iyong alaala.";

    [MenuItem("Salinlahi/SALIN-145/Apply Authored Ugat Context Copy")]
    public static void Apply()
    {
        var log = new StringBuilder("=== authored context copy ===\n");

        SetPrompts(L2, log, CopyL2, CopyL2Instruction);
        SetPrompts(L3, log, CopyL3 + "\nAng mabuting ______ ay gumagawa ng ______.");
        SetPrompts(L4, log, CopyL4 + "\nAng ______ at ______ ang unang guro sa tahanan.");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("ugat-copy-report.txt", log.ToString());
    }

    private static void SetPrompts(string path, StringBuilder log, params string[] prompts)
    {
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(path);
        if (sequence == null) { log.AppendLine($"  MISSING {path}"); return; }

        log.AppendLine($"  {Path.GetFileNameWithoutExtension(path)} ({sequence.units.Length} unit(s))");
        var so = new SerializedObject(sequence);
        SerializedProperty units = so.FindProperty("units");

        for (int i = 0; i < units.arraySize && i < prompts.Length; i++)
        {
            SerializedProperty p = units.GetArrayElementAtIndex(i).FindPropertyRelative("prompt");
            log.AppendLine($"    unit {i}: \"{p.stringValue}\"");
            p.stringValue = prompts[i];
            log.AppendLine($"         -> \"{prompts[i].Replace("\n", " | ")}\"");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);

        ChallengeValidationResult r = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"    validator: {(r.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in r.Errors) log.AppendLine($"      ERROR {e}");
    }
}
