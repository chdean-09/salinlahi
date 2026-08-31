using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-146. Authors the Ugat Level 4 context challenge and wires it to the level.
///
/// Level 4 is the recall level: INA and AMA were both taught in Level 1, so nothing new is being
/// introduced. The point is to demonstrate recall rather than repeat the guided tutorial.
///
///   AC1 "the level omits the configured guidance without hiding the two goals"
///       -> ChallengeCluePolicy.Reduced (confirmed as the intended reading of "fewer clues"),
///          while both target words stay in the candidate set so the goals remain visible.
///   AC2 "help supports recall without automatically completing the word"
///       -> allowHint stays true. Help is permitted; it must not auto-solve, which is runtime
///          behaviour rather than authoring.
///   AC3 sentence "Ang INA at AMA ang unang guro sa tahanan."
///       -> SentenceRestoration with two slots in sentence order, as Level 3.
///
/// The decoy is BATA, and the choice is deliberate. MATA ("eye") would be semantically absurd in a
/// sentence about who teaches in the home, so a learner could eliminate it without reading anything.
/// BATA ("child") is a family word that fits the sentence's shape and setting, so rejecting it
/// requires actually understanding that the sentence names the first TEACHERS, not the child. Under
/// Reduced clues that is the difference between testing recall and testing pattern-matching.
/// BATA has been known since Level 2, so nothing outside the pool (A, EI, BA, MA, NA, TA) appears.
/// </summary>
public static class Ugat04ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat04_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level4_Config.asset";

    [MenuItem("Salinlahi/SALIN-146/Author Ugat 4 Context Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 4 context challenge ===\n");

        var sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.sequenceId = "challenge.ugat.04";
        sequence.displayName = "Ikaapat na Alaala";

        const string ina = "ugat04-ina";
        const string ama = "ugat04-ama";
        const string bataDecoy = "ugat04-bata-decoy";

        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "ugat04-restore-sentence",
                mode = ChallengeMode.SentenceRestoration,
                cluePolicy = ChallengeCluePolicy.Reduced,   // AC1: fewer clues than Levels 1-3
                prompt = "Ang ______ at ______ ang unang guro sa tahanan.",
                tokens = new[]
                {
                    Token(ina, "INA", ChallengeTokenRole.Focus),
                    Token(ama, "AMA", ChallengeTokenRole.Focus),
                    Token(bataDecoy, "BATA", ChallengeTokenRole.Neutral),
                },
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "ugat04-slot-01", expectedOccurrenceId = ina },
                    new ChallengeSlotDefinition { slotId = "ugat04-slot-02", expectedOccurrenceId = ama },
                },
                candidateOccurrenceIds = new[] { ina, ama, bataDecoy },
                timerSeconds = 0f,
                allowHint = true,                            // AC2: help is permitted
                checkpointOnSuccess = true,
                memoryRevealSeconds = 1f,
                maxErrors = 3,
                heartPenalty = 1,
                // See the note on SALIN-144: one evidence id per unit, but this unit covers both
                // words. Neither is new here -- both are Level 1 words being recalled -- so the
                // first slot's word is used rather than pretending one is more central.
                evidenceContentId = "level.ugat.04.focus.01",
            },
        };

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        AssetDatabase.CreateAsset(sequence, AssetPath);

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(result.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in result.Errors) log.AppendLine($"    ERROR {e}");

        var u = sequence.units[0];
        log.AppendLine($"  {u.unitId} mode={u.mode} cluePolicy={u.cluePolicy} allowHint={u.allowHint}");
        log.AppendLine($"    prompt=\"{u.prompt}\"");
        foreach (var s in u.slots) log.AppendLine($"    slot {s.slotId} -> {s.expectedOccurrenceId}");
        log.AppendLine($"    candidates=[{string.Join(", ", u.candidateOccurrenceIds)}]");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"Level not found at {LevelPath}"); return; }
        var so = new SerializedObject(level);
        so.FindProperty("challengeSequence").objectReferenceValue = sequence;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        log.AppendLine($"  wired Level4_Config.challengeSequence -> {Path.GetFileName(AssetPath)}");
        log.AppendLine($"  challengePrototypeEnabled left at " +
                       $"{so.FindProperty("challengePrototypeEnabled").boolValue} (matches Ugat 1-3)");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("ugat04-challenge-report.txt", log.ToString());
    }

    private static ChallengeTokenDefinition Token(string occurrenceId, string display, ChallengeTokenRole role) =>
        new ChallengeTokenDefinition
        {
            tokenId = occurrenceId, displayText = display, occurrenceId = occurrenceId, role = role,
        };
}
