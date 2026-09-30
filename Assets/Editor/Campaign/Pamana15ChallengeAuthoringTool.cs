using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aligns Level 15's existing paragraph units, boss phases, and flow segments with the
/// current three-checkpoint ruling. Existing prompt and token copy is preserved for review.
/// </summary>
public static class Pamana15ChallengeAuthoringTool
{
    private const string SequencePath = "Assets/ScriptableObjects/Challenges/Challenge_Pamana15_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level15_Config.asset";
    private const string BossPath = "Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Kadiliman.asset";
    private const string YaEnemyName = "EnemyData_YaposngDilim";

    private static readonly string[] UnitOrder =
    {
        "pamana15-restore-line-01",
        "pamana15-restore-line-03",
        "pamana15-restore-line-02",
    };

    private static readonly string[][] PhaseCharacters =
    {
        new[] { "A", "EI", "BA", "MA", "NA", "TA" },
        new[] { "OU", "KA", "GA", "SA", "WA", "YA" },
        new[] { "DA", "RA", "HA", "LA", "NGA", "PA", "YA" },
    };

    private static readonly string[] PhaseNames = { "Ugat", "Ugnayan", "Lahat" };

    [MenuItem("Salinlahi/SALIN-158/Author Pamana 15 Campaign Flow")]
    public static void Apply()
    {
        ChallengeSequenceSO sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(SequencePath);
        LevelConfigSO level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        BossConfigSO boss = AssetDatabase.LoadAssetAtPath<BossConfigSO>(BossPath);
        if (sequence == null || level == null || boss == null)
        {
            Debug.LogError("Pamana 15 authoring stopped: the existing sequence, level, or boss asset is missing.");
            return;
        }

        if (!HasUnits(sequence, UnitOrder))
        {
            Debug.LogError("Pamana 15 authoring stopped: the three existing paragraph units were not found. Their copy was left untouched.");
            return;
        }

        Dictionary<string, EnemyDataSO> enemiesByCharacter = LoadEnemyRoster();
        string[] requiredCharacters = PhaseCharacters.SelectMany(group => group).Distinct().ToArray();
        string[] missing = requiredCharacters.Where(id => !enemiesByCharacter.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            Debug.LogError("Pamana 15 authoring stopped: no enemy is assigned for " + string.Join(", ", missing) + ".");
            return;
        }

        var log = new StringBuilder("=== Pamana 15 three-phase campaign flow ===\n");
        AuthorSequenceOrder(sequence, log);
        AuthorBossPhases(boss, enemiesByCharacter, log);
        AuthorLevelSegments(level, sequence, log);

        EditorUtility.SetDirty(sequence);
        EditorUtility.SetDirty(boss);
        EditorUtility.SetDirty(level);
        AssetDatabase.SaveAssets();

        LevelPhasePlan plan = LevelPhasePlan.FromConfig(level);
        log.AppendLine($"  level flow plan: segments={plan.SegmentCount}, invalid={plan.SegmentPlanInvalid}");
        log.AppendLine("  challenge prompts and token text preserved for language review.");
        Debug.Log(log.ToString());
    }

    private static bool HasUnits(ChallengeSequenceSO sequence, IReadOnlyList<string> ids)
    {
        if (sequence.units == null || sequence.units.Length != ids.Count)
            return false;

        var found = new HashSet<string>(sequence.units
            .Where(unit => unit != null)
            .Select(unit => unit.unitId));
        return ids.All(found.Contains);
    }

    private static Dictionary<string, EnemyDataSO> LoadEnemyRoster()
    {
        var result = new Dictionary<string, EnemyDataSO>(StringComparer.OrdinalIgnoreCase);
        string[] guids = AssetDatabase.FindAssets("t:EnemyDataSO", new[] { "Assets/ScriptableObjects/Enemies" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyDataSO data = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(path);
            string characterId = data?.assignedCharacter?.characterID;
            if (string.IsNullOrWhiteSpace(characterId))
                continue;

            if (!result.TryGetValue(characterId, out EnemyDataSO existing)
                || IsPreferredEnemy(data, existing, characterId))
            {
                result[characterId] = data;
            }
        }

        return result;
    }

    private static bool IsPreferredEnemy(EnemyDataSO candidate, EnemyDataSO existing, string characterId)
    {
        if (characterId == "YA")
            return candidate.name == YaEnemyName;
        if (characterId == "HA")
            return candidate.name == "EnemyData_Hati";
        return existing == null;
    }

    private static void AuthorSequenceOrder(ChallengeSequenceSO sequence, StringBuilder log)
    {
        var so = new SerializedObject(sequence);
        SerializedProperty units = so.FindProperty("units");
        for (int destination = 0; destination < UnitOrder.Length; destination++)
        {
            int current = FindUnitIndex(units, UnitOrder[destination]);
            if (current < 0)
                throw new InvalidOperationException("A validated Level 15 paragraph unit disappeared during authoring.");
            if (current != destination)
                units.MoveArrayElement(current, destination);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("  paragraph checkpoint order: PAMANA, PAMANA, MALAYA (existing unit text retained).");
    }

    private static int FindUnitIndex(SerializedProperty units, string unitId)
    {
        for (int i = 0; i < units.arraySize; i++)
        {
            if (units.GetArrayElementAtIndex(i).FindPropertyRelative("unitId").stringValue == unitId)
                return i;
        }

        return -1;
    }

    private static void AuthorBossPhases(
        BossConfigSO boss, IReadOnlyDictionary<string, EnemyDataSO> enemies, StringBuilder log)
    {
        var so = new SerializedObject(boss);
        SerializedProperty phases = so.FindProperty("phases");
        phases.arraySize = PhaseNames.Length;

        for (int i = 0; i < PhaseNames.Length; i++)
        {
            SerializedProperty phase = phases.GetArrayElementAtIndex(i);
            phase.FindPropertyRelative("displayName").stringValue = PhaseNames[i];
            phase.FindPropertyRelative("summonPhaseDuration").floatValue = i == 2 ? 35f : 30f;
            phase.FindPropertyRelative("delayBetweenSummons").floatValue = 5f;
            phase.FindPropertyRelative("minionsPerSummonMin").intValue = 2;
            phase.FindPropertyRelative("minionsPerSummonMax").intValue = 3;
            phase.FindPropertyRelative("delayBetweenMinions").floatValue = 0.6f;
            phase.FindPropertyRelative("summonSpawnRange").vector2Value = new Vector2(2f, 0f);
            phase.FindPropertyRelative("requiredCharacterCount").intValue = i == 2 ? 5 : 4;
            phase.FindPropertyRelative("vulnerabilityTimer").floatValue = 12f;
            phase.FindPropertyRelative("movementPattern").enumValueIndex = (int)BossMovementPattern.Pace;
            phase.FindPropertyRelative("movementSpeed").floatValue = 1f;
            phase.FindPropertyRelative("paceHalfRange").floatValue = 1.5f;
            phase.FindPropertyRelative("teleportHalfRange").vector2Value = new Vector2(2f, 0f);

            WriteEnemyList(phase.FindPropertyRelative("guaranteedSummonEnemyTypes"),
                PhaseCharacters[i].Select(id => enemies[id]));
            IEnumerable<EnemyDataSO> phasePool = i == 2
                ? enemies.Values.Distinct()
                : PhaseCharacters[i].Select(id => enemies[id]);
            WriteEnemyList(phase.FindPropertyRelative("summonEnemyTypes"), phasePool);

            log.AppendLine($"  phase {i + 1}: {PhaseNames[i]}, guaranteed {string.Join(", ", PhaseCharacters[i])}");
        }

        WriteEnemyList(so.FindProperty("fallbackEnemyTypes"), enemies.Values.Distinct());
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WriteEnemyList(SerializedProperty list, IEnumerable<EnemyDataSO> values)
    {
        EnemyDataSO[] assets = values.Where(value => value != null).Distinct().ToArray();
        list.arraySize = assets.Length;
        for (int i = 0; i < assets.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
    }

    private static void AuthorLevelSegments(
        LevelConfigSO level, ChallengeSequenceSO sequence, StringBuilder log)
    {
        var so = new SerializedObject(level);
        so.FindProperty("challengeSequence").objectReferenceValue = sequence;
        SerializedProperty segments = so.FindProperty("flowSegments");
        segments.arraySize = UnitOrder.Length;

        for (int i = 0; i < UnitOrder.Length; i++)
        {
            SerializedProperty segment = segments.GetArrayElementAtIndex(i);
            segment.FindPropertyRelative("waveCount").intValue = 0;
            SerializedProperty unitIds = segment.FindPropertyRelative("challengeUnitIds");
            unitIds.arraySize = 1;
            unitIds.GetArrayElementAtIndex(0).stringValue = UnitOrder[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("  three zero-wave segments each pair one boss phase with one paragraph checkpoint.");
    }
}
