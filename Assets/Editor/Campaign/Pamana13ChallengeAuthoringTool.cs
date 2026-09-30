using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors Level 13's approved SANGA/HARAYA sentence restoration and wires it to the level.
/// </summary>
public static class Pamana13ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana13_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level13_Config.asset";
    private const string Prompt =
        "Bawat salinlahi ay isang ______ na may sariling ______ para sa kinabukasan.";

    [MenuItem("Salinlahi/SALIN-155/Author Pamana 13 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Pamana 13 challenge ===\n");
        ChallengeSequenceSO sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.pamana.13";
        so.FindProperty("displayName").stringValue = "Ikatlong Alaala ng Pamana";

        SerializedProperty units = so.FindProperty("units");
        units.arraySize = 1;
        SerializedProperty unit = units.GetArrayElementAtIndex(0);
        unit.FindPropertyRelative("unitId").stringValue = "pamana13-sanga-haraya-sentence";
        unit.FindPropertyRelative("mode").enumValueIndex = 2;       // SentenceRestoration
        unit.FindPropertyRelative("cluePolicy").enumValueIndex = 1; // Reduced
        unit.FindPropertyRelative("prompt").stringValue = Prompt;

        (string id, string text, string evidenceId)[] tokens =
        {
            ("pamana13-sanga", "SANGA", "level.pamana.03.focus.01"),
            ("pamana13-haraya", "HARAYA", "level.pamana.03.focus.02"),
        };

        SerializedProperty tokenList = unit.FindPropertyRelative("tokens");
        tokenList.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
        {
            SerializedProperty token = tokenList.GetArrayElementAtIndex(i);
            token.FindPropertyRelative("tokenId").stringValue = tokens[i].id;
            token.FindPropertyRelative("displayText").stringValue = tokens[i].text;
            token.FindPropertyRelative("occurrenceId").stringValue = tokens[i].id;
            token.FindPropertyRelative("role").enumValueIndex = 1;
            token.FindPropertyRelative("targetCharacter").objectReferenceValue = null;
            token.FindPropertyRelative("evidenceContentId").stringValue = tokens[i].evidenceId;
        }

        SerializedProperty slots = unit.FindPropertyRelative("slots");
        slots.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
        {
            SerializedProperty slot = slots.GetArrayElementAtIndex(i);
            slot.FindPropertyRelative("slotId").stringValue = $"pamana13-slot-{i + 1:00}";
            slot.FindPropertyRelative("expectedOccurrenceId").stringValue = tokens[i].id;
        }

        SerializedProperty candidates = unit.FindPropertyRelative("candidateOccurrenceIds");
        candidates.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
            candidates.GetArrayElementAtIndex(i).stringValue = tokens[i].id;

        unit.FindPropertyRelative("guidedStep").objectReferenceValue = null;
        unit.FindPropertyRelative("timerSeconds").floatValue = 0f;
        unit.FindPropertyRelative("allowHint").boolValue = true;
        unit.FindPropertyRelative("checkpointOnSuccess").boolValue = true;
        unit.FindPropertyRelative("memoryRevealSeconds").floatValue = 0f;
        unit.FindPropertyRelative("maxErrors").intValue = 3;
        unit.FindPropertyRelative("heartPenalty").intValue = 1;
        unit.FindPropertyRelative("evidenceContentId").stringValue = "level.pamana.03.focus.01";

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);

        LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null)
        {
            Debug.LogError($"{LevelPath} not found.");
            return;
        }

        var levelSo = new SerializedObject(level);
        levelSo.FindProperty("challengeSequence").objectReferenceValue = sequence;
        levelSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();

        ChallengeValidationResult validation = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  {(created ? "created" : "updated")} {AssetPath}");
        log.AppendLine("  sentence slots: SANGA then HARAYA");
        log.AppendLine("  ChallengeSequenceValidator: " +
                       (validation.Errors.Count == 0
                           ? "no errors"
                           : string.Join(" | ", validation.Errors)));
        Debug.Log(log.ToString());
    }
}
