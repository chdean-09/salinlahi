using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-207. Authors the Kadiliman final encounter.
///
/// It shipped as a stub: 1 phase, requiredCharacterCount 3, summonEnemyTypes empty — against a
/// design of "all 18 characters in a timed sequence" with summons from all three eras. As
/// configured the final boss was easier than El Inquisidor at Level 5, which has 3 phases and 10
/// required draws, and it summoned nothing at all.
///
/// The shape here follows the GDD: four phases that walk forward through the three eras of
/// corruption and then combine them, summing to 18 required draws.
///
///   P1 Spanish -> P2 American -> P3 Japanese -> P4 all three, elites included
///   required draws 4 + 4 + 5 + 5 = 18
///
/// Pacing is deliberately generous, per RISK-14 in doc 11: start boss timers loose and tighten on
/// playtest feedback. Every number below is a starting point for playtesting, not a tuned value.
/// </summary>
public static class KadilimanEncounterAuthoringTool
{
    private const string ConfigPath = "Assets/ScriptableObjects/Enemies/Boss Configs/BossConfig_Kadiliman.asset";
    private const string EnemyFolder = "Assets/ScriptableObjects/Enemies";

    private sealed class PhaseSpec
    {
        public string Era;
        public float Duration, DelaySummons, DelayMinions, VulnTimer, MoveSpeed, PaceHalf;
        public int MinionsMin, MinionsMax, Required, MovementPattern;
        public Vector2 SpawnRange, TeleportHalf;
        public string[] Summons;
    }

    private static readonly PhaseSpec[] Phases =
    {
        new PhaseSpec {
            Era = "Spanish", Duration = 30f, DelaySummons = 6f, MinionsMin = 1, MinionsMax = 2,
            DelayMinions = 0.8f, Required = 4, VulnTimer = 25f, MovementPattern = 1, MoveSpeed = 1.0f,
            PaceHalf = 2.5f, SpawnRange = new Vector2(3f, 1.5f), TeleportHalf = new Vector2(2f, 0f),
            Summons = new[] { "Soldado", "Fraile" },
        },
        new PhaseSpec {
            Era = "American", Duration = 30f, DelaySummons = 5f, MinionsMin = 2, MinionsMax = 3,
            DelayMinions = 0.8f, Required = 4, VulnTimer = 24f, MovementPattern = 1, MoveSpeed = 1.3f,
            PaceHalf = 2.5f, SpawnRange = new Vector2(3f, 1.5f), TeleportHalf = new Vector2(2.5f, 0f),
            Summons = new[] { "Soldier", "Maestro", "Pensionado" },
        },
        new PhaseSpec {
            Era = "Japanese", Duration = 30f, DelaySummons = 4f, MinionsMin = 2, MinionsMax = 3,
            DelayMinions = 0.7f, Required = 5, VulnTimer = 22f, MovementPattern = 2, MoveSpeed = 0f,
            PaceHalf = 2f, SpawnRange = new Vector2(3.5f, 1f), TeleportHalf = new Vector2(2.5f, 2.5f),
            Summons = new[] { "Heitai", "Kisha", "Kempei" },
        },
        new PhaseSpec {
            Era = "All three eras", Duration = 35f, DelaySummons = 3.5f, MinionsMin = 3, MinionsMax = 4,
            DelayMinions = 0.6f, Required = 5, VulnTimer = 22f, MovementPattern = 2, MoveSpeed = 0f,
            PaceHalf = 2f, SpawnRange = new Vector2(3.5f, 1f), TeleportHalf = new Vector2(3f, 3f),
            Summons = new[] { "Capitan", "General", "Shokan", "Soldado", "Heitai" },
        },
    };

    private static readonly string[] Fallbacks = { "Soldado", "Soldier", "Heitai" };

    private const string Description =
        "Kadiliman is the Darkness itself, the embodiment of cultural forgetting. A formless " +
        "shadow entity that combines all three eras of corruption.\n\n\n\n" +
        "Power: Summons enemies from every era. Drawing all 18 characters in a timed sequence " +
        "restores Baybayin to the world.";

    [MenuItem("Salinlahi/SALIN-207/Author Kadiliman Encounter")]
    public static void Apply()
    {
        var config = AssetDatabase.LoadAssetAtPath<BossConfigSO>(ConfigPath);
        if (config == null) { Debug.LogError($"Kadiliman config not found at {ConfigPath}"); return; }

        var so = new SerializedObject(config);
        var log = new StringBuilder("=== Kadiliman encounter ===\n");

        SerializedProperty phases = so.FindProperty("phases");
        log.AppendLine($"  before: phases={phases.arraySize} totalRequired={TotalRequired(phases)}");

        phases.arraySize = Phases.Length;
        int total = 0;

        for (int i = 0; i < Phases.Length; i++)
        {
            PhaseSpec spec = Phases[i];
            SerializedProperty p = phases.GetArrayElementAtIndex(i);

            p.FindPropertyRelative("summonPhaseDuration").floatValue = spec.Duration;
            p.FindPropertyRelative("delayBetweenSummons").floatValue = spec.DelaySummons;
            p.FindPropertyRelative("minionsPerSummonMin").intValue = spec.MinionsMin;
            p.FindPropertyRelative("minionsPerSummonMax").intValue = spec.MinionsMax;
            p.FindPropertyRelative("delayBetweenMinions").floatValue = spec.DelayMinions;
            p.FindPropertyRelative("summonSpawnRange").vector2Value = spec.SpawnRange;
            p.FindPropertyRelative("requiredCharacterCount").intValue = spec.Required;
            p.FindPropertyRelative("vulnerabilityTimer").floatValue = spec.VulnTimer;
            p.FindPropertyRelative("movementPattern").enumValueIndex = spec.MovementPattern;
            p.FindPropertyRelative("movementSpeed").floatValue = spec.MoveSpeed;
            p.FindPropertyRelative("paceHalfRange").floatValue = spec.PaceHalf;
            p.FindPropertyRelative("teleportHalfRange").vector2Value = spec.TeleportHalf;

            SerializedProperty summons = p.FindPropertyRelative("summonEnemyTypes");
            summons.arraySize = spec.Summons.Length;
            for (int s = 0; s < spec.Summons.Length; s++)
                summons.GetArrayElementAtIndex(s).objectReferenceValue = LoadEnemy(spec.Summons[s]);

            total += spec.Required;
            log.AppendLine($"  P{i + 1} {spec.Era,-14} required={spec.Required} vuln={spec.VulnTimer} " +
                           $"summons=[{string.Join(", ", spec.Summons)}]");
        }

        SerializedProperty fallback = so.FindProperty("fallbackEnemyTypes");
        fallback.arraySize = Fallbacks.Length;
        for (int i = 0; i < Fallbacks.Length; i++)
            fallback.GetArrayElementAtIndex(i).objectReferenceValue = LoadEnemy(Fallbacks[i]);

        so.FindProperty("summonHorizontalBounds").vector2Value = new Vector2(-2f, 2f);

        SerializedProperty desc = so.FindProperty("description");
        if (string.IsNullOrWhiteSpace(desc.stringValue)) desc.stringValue = Description;

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        log.AppendLine($"  after: phases={Phases.Length} totalRequired={total}");
        log.AppendLine($"  (El Inquisidor, the FIRST boss, is 3 phases / 10 required)");
        if (so.FindProperty("audioBank").objectReferenceValue == null)
            log.AppendLine("  NOTE: audioBank is still empty — no Kadiliman bank exists.");
        if (so.FindProperty("bossSprite").objectReferenceValue == null)
            log.AppendLine("  NOTE: bossSprite is still empty — needs art.");

        Debug.Log(log.ToString());
        File.WriteAllText("kadiliman-report.txt", log.ToString());
    }

    private static int TotalRequired(SerializedProperty phases)
    {
        int t = 0;
        for (int i = 0; i < phases.arraySize; i++)
            t += phases.GetArrayElementAtIndex(i).FindPropertyRelative("requiredCharacterCount").intValue;
        return t;
    }

    private static EnemyDataSO LoadEnemy(string name)
    {
        var e = AssetDatabase.LoadAssetAtPath<EnemyDataSO>($"{EnemyFolder}/EnemyData_{name}.asset");
        if (e == null) Debug.LogError($"KadilimanEncounter: missing EnemyData_{name}.asset");
        return e;
    }
}
