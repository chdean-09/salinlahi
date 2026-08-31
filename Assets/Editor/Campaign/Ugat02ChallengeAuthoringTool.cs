using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-145. Authors the Ugat Level 2 context challenge and wires it to the level.
///
/// Level 2 already carries its focus words — BATA (BA + TA, "child") and MATA (MA + TA, "eye") —
/// but shipped with `challengeSequence: {fileID: 0}`, so the Context Challenge beat of
/// LF-CONTRACT-v2 had nothing to run. Only Ugat 1 had a challenge asset.
///
/// Design follows the ticket's acceptance criteria rather than invention:
///
///   AC1 "BATA is decomposed as BA + TA and MATA as MA + TA" -> the two units mirror that split.
///   AC2 "the player supplies the correct missing syllables ... without requiring unsupported
///        characters" -> every token is drawn from Level 2's cumulativeSymbolPool
///        (A, EI, BA, MA, NA, TA). Nothing outside the pool appears, as target or as decoy.
///
/// Which syllable is blanked is a teaching choice: each unit blanks the character Level 2
/// INTRODUCES (BA, then TA) and uses a Level 1 character as the decoy (MA, then NA). That way the
/// challenge exercises what this level taught, and every distractor is something the player has
/// already met rather than a character they have never seen.
///
/// Mode is WordPlacement, matching Ugat 1 — the player places a token into a slot. The level's
/// `challengePrototypeEnabled` flag is deliberately left at 0, exactly as Ugat 1 has it: turning
/// it on activates ChallengeSequenceValidator's authoring gate on a level whose focus-word media
/// (contextImage, narrationClip, dialogue, cutscene) is still unassigned, which would fail for
/// reasons unrelated to this challenge.
/// </summary>
public static class Ugat02ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat02_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level2_Config.asset";

    [MenuItem("Salinlahi/SALIN-145/Author Ugat 2 Context Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 2 context challenge ===\n");

        var sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.sequenceId = "challenge.ugat.02";
        sequence.displayName = "Ikalawang Alaala";
        sequence.units = new[]
        {
            BuildUnit(
                unitId:   "ugat02-complete-bata",
                prompt:   "Buuin ang salitang BATA.",
                target:   "BA",                       // introduced this level
                decoy:    "MA",                       // known from Level 1 (AMA)
                evidence: "level.ugat.02.focus.01"),
            BuildUnit(
                unitId:   "ugat02-complete-mata",
                prompt:   "Buuin ang salitang MATA.",
                target:   "TA",                       // introduced this level
                decoy:    "NA",                       // known from Level 1 (INA)
                evidence: "level.ugat.02.focus.02"),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        AssetDatabase.CreateAsset(sequence, AssetPath);

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(result.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in result.Errors) log.AppendLine($"    ERROR {e}");

        foreach (var u in sequence.units)
            log.AppendLine($"  {u.unitId,-24} mode={u.mode} prompt=\"{u.prompt}\" " +
                           $"target={u.slots[0].expectedOccurrenceId} " +
                           $"candidates=[{string.Join(", ", u.candidateOccurrenceIds)}]");

        // Wire it to the level, matching how Ugat 1 references its own challenge.
        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"Level not found at {LevelPath}"); return; }

        var so = new SerializedObject(level);
        so.FindProperty("challengeSequence").objectReferenceValue = sequence;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);

        log.AppendLine($"  wired Level2_Config.challengeSequence -> {Path.GetFileName(AssetPath)}");
        log.AppendLine($"  challengePrototypeEnabled left at " +
                       $"{so.FindProperty("challengePrototypeEnabled").boolValue} (matches Ugat 1)");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("ugat02-challenge-report.txt", log.ToString());
    }

    private static ChallengeUnitDefinition BuildUnit(
        string unitId, string prompt, string target, string decoy, string evidence)
    {
        string targetOcc = $"ugat02-{target.ToLowerInvariant()}";
        string decoyOcc  = $"ugat02-{decoy.ToLowerInvariant()}-decoy";

        return new ChallengeUnitDefinition
        {
            unitId = unitId,
            mode = ChallengeMode.WordPlacement,
            cluePolicy = ChallengeCluePolicy.Full,      // Level 2 still teaches; clues stay full
            prompt = prompt,
            tokens = new[]
            {
                new ChallengeTokenDefinition
                {
                    tokenId = targetOcc, displayText = target, occurrenceId = targetOcc,
                    role = ChallengeTokenRole.Focus,
                },
                new ChallengeTokenDefinition
                {
                    tokenId = decoyOcc, displayText = decoy, occurrenceId = decoyOcc,
                    role = ChallengeTokenRole.Neutral,
                },
            },
            slots = new[]
            {
                new ChallengeSlotDefinition
                {
                    slotId = $"{unitId}-slot", expectedOccurrenceId = targetOcc,
                },
            },
            candidateOccurrenceIds = new[] { targetOcc, decoyOcc },
            timerSeconds = 0f,
            allowHint = true,
            checkpointOnSuccess = true,
            memoryRevealSeconds = 1f,
            maxErrors = 3,
            heartPenalty = 1,
            evidenceContentId = evidence,
        };
    }
}
