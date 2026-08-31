using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-147, AC1 only. Authors the Ugat Level 5 context challenge and wires it to the level.
///
/// SCOPE. This covers AC1 — "when the player uses unlocked syllables, then IBA and MANA can be
/// restored". AC2 is deliberately NOT attempted: it needs the "canonical paragraph", which is not
/// written anywhere in the repository, and the "approved three-phase Paglimot extension", which is
/// SALIN-184's deliverable ("Author the three Paglimot mastery encounters") rather than a challenge
/// asset. AC3 is level-flow behaviour.
///
/// TWO UNITS, not one blank. Levels 3 and 4 were ruled to a single blank because their authored
/// copy said so — "isang salita lamang", "ang salitang nararapat". Level 5's copy says neither:
/// "Buuin mo kung ano iyon" is silent on count. More importantly the pedagogy differs — IBA and
/// MANA are both NEW words here, where Level 4's INA and AMA were both recalls and Level 3 showed
/// BATA precisely because it was already known. Blanking only one of two new words would leave the
/// other untested in the level that introduces it. This follows Level 2, the other level whose
/// words are both new, and keeps per-word Meaning evidence intact.
///
/// SYLLABLE BLANKS. AC1 says the player "uses unlocked syllables", so this completes words from
/// syllables as Level 2 does, rather than placing whole words as Levels 3 and 4 do. Level 5
/// introduces no new characters — the pool is unchanged (A, EI, BA, MA, NA, TA) — so mastery here
/// is forming NEW WORDS from known syllables, which is the era's culminating skill.
///
///   IBA  = EI + BA. Blanking EI puts it against A, the other vowel: a genuine vowel discrimination.
///   MANA = MA + NA. Blanking NA shows MA first, so completing it means resisting the obvious
///          repetition of the syllable already on screen.
/// </summary>
public static class Ugat05ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat05_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level5_Config.asset";

    private const string CopyFull =
        "Nagbago man ang panahon, may naiwan pa ring hindi kayang kunin ng Paglimot. " +
        "Buuin mo kung ano iyon.";
    private const string CopyInstruction = "Buuin mo kung ano iyon.";

    [MenuItem("Salinlahi/SALIN-147/Author Ugat 5 Context Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 5 context challenge (AC1) ===\n");

        var sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.sequenceId = "challenge.ugat.05";
        sequence.displayName = "Ikalimang Alaala";
        sequence.units = new[]
        {
            Unit("ugat05-complete-iba",  CopyFull,        "EI", new[] { "A", "MA" },  "level.ugat.05.focus.01"),
            Unit("ugat05-complete-mana", CopyInstruction, "NA", new[] { "MA", "TA" }, "level.ugat.05.focus.02"),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        AssetDatabase.CreateAsset(sequence, AssetPath);

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(result.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in result.Errors) log.AppendLine($"    ERROR {e}");

        foreach (var u in sequence.units)
        {
            log.AppendLine($"  {u.unitId} mode={u.mode} cluePolicy={u.cluePolicy}");
            log.AppendLine($"    prompt=\"{u.prompt}\"");
            log.AppendLine($"    target={u.slots[0].expectedOccurrenceId} " +
                           $"candidates=[{string.Join(", ", u.candidateOccurrenceIds)}] " +
                           $"evidence={u.evidenceContentId}");
        }

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"Level not found at {LevelPath}"); return; }
        var so = new SerializedObject(level);
        so.FindProperty("challengeSequence").objectReferenceValue = sequence;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        log.AppendLine($"  wired Level5_Config.challengeSequence -> {Path.GetFileName(AssetPath)}");
        log.AppendLine($"  challengePrototypeEnabled left at " +
                       $"{so.FindProperty("challengePrototypeEnabled").boolValue} (matches Ugat 1-4)");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("ugat05-challenge-report.txt", log.ToString());
    }

    private static ChallengeUnitDefinition Unit(
        string unitId, string prompt, string target, string[] decoys, string evidence)
    {
        string targetOcc = $"ugat05-{target.ToLowerInvariant()}";
        var tokens = new System.Collections.Generic.List<ChallengeTokenDefinition>
        {
            Token(targetOcc, target, ChallengeTokenRole.Focus),
        };
        var candidates = new System.Collections.Generic.List<string> { targetOcc };
        foreach (string d in decoys)
        {
            string occ = $"ugat05-{d.ToLowerInvariant()}-decoy-{unitId}";
            tokens.Add(Token(occ, d, ChallengeTokenRole.Neutral));
            candidates.Add(occ);
        }

        return new ChallengeUnitDefinition
        {
            unitId = unitId,
            mode = ChallengeMode.WordPlacement,
            // The era culmination, one step beyond Level 4's Reduced. See the PR note: this is an
            // inference from "without a fully guided trace sequence", not a confirmed decision.
            cluePolicy = ChallengeCluePolicy.Minimal,
            prompt = prompt,
            tokens = tokens.ToArray(),
            slots = new[]
            {
                new ChallengeSlotDefinition { slotId = $"{unitId}-slot", expectedOccurrenceId = targetOcc },
            },
            candidateOccurrenceIds = candidates.ToArray(),
            timerSeconds = 0f,
            allowHint = true,
            checkpointOnSuccess = true,
            memoryRevealSeconds = 1f,
            maxErrors = 3,
            heartPenalty = 1,
            evidenceContentId = evidence,
        };
    }

    private static ChallengeTokenDefinition Token(string occurrenceId, string display, ChallengeTokenRole role) =>
        new ChallengeTokenDefinition
        {
            tokenId = occurrenceId, displayText = display, occurrenceId = occurrenceId, role = role,
        };
}
