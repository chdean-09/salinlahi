using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authors the approved Pamana 14 sentence restoration and guarantees a review RA spawn.
/// The challenge sentence is the approved ALAALA/MAHALAGA wording. RA stays a separate
/// character from DA and appears in the first authored wave through WaveDefinition's
/// guaranteedCharacters list.
/// </summary>
public static class Pamana14ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana14_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level14_Config.asset";

    private const float TimerSeconds = 45f;         // untuned, see class note
    private const float MemoryRevealSeconds = 5f;   // untuned, see class note

    // AC4 verbatim, with the two focus words blanked:
    // "Ang ALAALA ay MAHALAGA dahil dito nagsisimula ang pagkilala sa ating pinagmulan."
    private const string Prompt =
        "Titingnan mo muna ang buong pangungusap, pagkatapos ay maglalaho ito. " +
        "Ibalik mo ang dalawang salitang nawala, mula sa iyong alaala.\n\n" +
        "Ang ______ ay ______ dahil dito nagsisimula ang pagkilala sa ating pinagmulan.";

    [MenuItem("Salinlahi/SALIN-156/Author Pamana 14 Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Pamana 14 context challenge ===\n");

        // Load-and-mutate when it already exists: CreateAsset over an existing path reissues the
        // GUID and would silently unwire Level14_Config.challengeSequence.
        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        bool created = sequence == null;
        if (created)
        {
            sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
            AssetDatabase.CreateAsset(sequence, AssetPath);
        }

        var so = new SerializedObject(sequence);
        so.FindProperty("sequenceId").stringValue = "challenge.pamana.14";
        so.FindProperty("displayName").stringValue = "Ikaapat na Alaala ng Pamana";

        SerializedProperty units = so.FindProperty("units");
        units.arraySize = 1;
        SerializedProperty unit = units.GetArrayElementAtIndex(0);

        unit.FindPropertyRelative("unitId").stringValue = "pamana14-timed-recall";
        unit.FindPropertyRelative("mode").enumValueIndex = 2;         // SentenceRestoration -- D1
        unit.FindPropertyRelative("cluePolicy").enumValueIndex = 1;   // Reduced -- AC2, not a choice
        unit.FindPropertyRelative("prompt").stringValue = Prompt;

        (string id, string text, int role)[] tokens =
        {
            ("pamana14-alaala",        "ALAALA",   1),
            ("pamana14-mahalaga",      "MAHALAGA", 1),
            ("pamana14-halaga-decoy",  "HALAGA",   0),
            ("pamana14-dala-decoy",    "DALA",     0),
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

        // Sentence order: ALAALA fills the first blank, MAHALAGA the second. TimedMemory walks slots
        // by index through SubmitPlacement, so this order is the answer order.
        SerializedProperty slots = unit.FindPropertyRelative("slots");
        slots.arraySize = 2;
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("slotId").stringValue = "pamana14-slot-01";
        slots.GetArrayElementAtIndex(0).FindPropertyRelative("expectedOccurrenceId").stringValue = "pamana14-alaala";
        slots.GetArrayElementAtIndex(1).FindPropertyRelative("slotId").stringValue = "pamana14-slot-02";
        slots.GetArrayElementAtIndex(1).FindPropertyRelative("expectedOccurrenceId").stringValue = "pamana14-mahalaga";

        SerializedProperty candidates = unit.FindPropertyRelative("candidateOccurrenceIds");
        candidates.arraySize = tokens.Length;
        for (int i = 0; i < tokens.Length; i++)
            candidates.GetArrayElementAtIndex(i).stringValue = tokens[i].id;

        unit.FindPropertyRelative("guidedStep").objectReferenceValue = null;
        unit.FindPropertyRelative("timerSeconds").floatValue = TimerSeconds;
        unit.FindPropertyRelative("allowHint").boolValue = true;
        unit.FindPropertyRelative("checkpointOnSuccess").boolValue = true;
        unit.FindPropertyRelative("memoryRevealSeconds").floatValue = MemoryRevealSeconds;
        unit.FindPropertyRelative("maxErrors").intValue = 3;
        unit.FindPropertyRelative("heartPenalty").intValue = 1;
        unit.FindPropertyRelative("evidenceContentId").stringValue = "level.pamana.04.focus.01";

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sequence);

        log.AppendLine($"  {(created ? "created" : "updated")} {Path.GetFileName(AssetPath)}");
        log.AppendLine($"  mode=SentenceRestoration cluePolicy=Reduced blanks=2 " +
                       $"timer={TimerSeconds}s reveal={MemoryRevealSeconds}s");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"{LevelPath} not found."); return; }
        var lso = new SerializedObject(level);
        lso.FindProperty("challengeSequence").objectReferenceValue = sequence;
        if (!AddGuaranteedRaSpawn(lso, log))
            return;
        lso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);

        AssetDatabase.SaveAssets();

        var check = new SerializedObject(level);
        log.AppendLine("  Level14_Config.challengeSequence wired: " +
                       (check.FindProperty("challengeSequence").objectReferenceValue == sequence));

        // The mode carries validation rules no other authored challenge has to satisfy, so assert
        // them here rather than discovering a broken level at runtime.
        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  ChallengeSequenceSO.Validate(): " +
                       (result.Errors.Count == 0 ? "no errors" : string.Join(" | ", result.Errors)));

        Debug.Log(log.ToString());
    }

    private static bool AddGuaranteedRaSpawn(SerializedObject level, StringBuilder log)
    {
        const string raCharacterPath = "Assets/ScriptableObjects/Characters/Char_RA.asset";
        const string raEnemyPath = "Assets/ScriptableObjects/Enemies/EnemyData_Ragasa.asset";
        BaybayinCharacterSO ra = AssetDatabase.LoadAssetAtPath<BaybayinCharacterSO>(raCharacterPath);
        EnemyDataSO raEnemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(raEnemyPath);
        SerializedProperty waves = level.FindProperty("_authoredWaves");
        if (ra == null || raEnemy == null || waves == null || waves.arraySize == 0)
        {
            Debug.LogError("Pamana 14 cannot guarantee RA: character, Ragasa, or authored waves are missing.");
            return false;
        }

        SerializedProperty wave = waves.GetArrayElementAtIndex(0);
        SerializedProperty guaranteed = wave.FindPropertyRelative("guaranteedCharacters");
        AddReferenceIfMissing(guaranteed, ra);

        AddReferenceIfMissing(wave.FindPropertyRelative("characters"), ra);
        AddReferenceIfMissing(wave.FindPropertyRelative("enemyTypes"), raEnemy);
        if (wave.FindPropertyRelative("enemyCount").intValue < 1)
            wave.FindPropertyRelative("enemyCount").intValue = 1;

        log.AppendLine("  First wave guarantees one RA carrier (Ragasa).");
        return true;
    }

    private static void AddReferenceIfMissing(SerializedProperty list, Object value)
    {
        if (list == null || value == null)
            return;

        for (int i = 0; i < list.arraySize; i++)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == value)
                return;
        }

        int index = list.arraySize++;
        list.GetArrayElementAtIndex(index).objectReferenceValue = value;
    }
}
