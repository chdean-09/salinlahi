using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-147 revision. Ugat 5's clue policy is Reduced, not Minimal.
///
/// Minimal was authored as an inference — the era culmination, one step beyond Level 4 — and
/// flagged as unconfirmed. Ruled 2026-09-01 in favour of Reduced: "without a fully guided trace
/// sequence" is already satisfied by Reduced, so the Ugat clue curve tops out there rather than
/// escalating a further step at the last level.
///
/// Mutates the existing asset rather than recreating it: AssetDatabase.CreateAsset on an existing
/// path would issue a new GUID and break Level5_Config's challengeSequence reference.
/// </summary>
public static class Ugat05ReducedCluesTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat05_Context.asset";

    [MenuItem("Salinlahi/SALIN-147/Set Ugat 5 Clue Policy To Reduced")]
    public static void Apply()
    {
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        if (sequence == null) { Debug.LogError($"Not found: {AssetPath}"); return; }

        var log = new StringBuilder("=== Ugat 5 clue policy ===\n");
        var so = new SerializedObject(sequence);
        SerializedProperty units = so.FindProperty("units");

        for (int i = 0; i < units.arraySize; i++)
        {
            SerializedProperty unit = units.GetArrayElementAtIndex(i);
            SerializedProperty policy = unit.FindPropertyRelative("cluePolicy");
            string id = unit.FindPropertyRelative("unitId").stringValue;
            log.AppendLine($"  {id}: {(ChallengeCluePolicy)policy.enumValueIndex} -> {ChallengeCluePolicy.Reduced}");
            policy.enumValueIndex = (int)ChallengeCluePolicy.Reduced;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();

        ChallengeValidationResult r = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(r.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in r.Errors) log.AppendLine($"    ERROR {e}");

        // The reference must survive; a broken GUID here would silently unwire the level.
        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(
            "Assets/ScriptableObjects/Levels/Level5_Config.asset");
        var lso = new SerializedObject(level);
        Object wired = lso.FindProperty("challengeSequence").objectReferenceValue;
        log.AppendLine($"  Level5_Config.challengeSequence still wired: {wired == sequence}");

        Debug.Log(log.ToString());
        File.WriteAllText("ugat05-clue-report.txt", log.ToString());
    }
}
