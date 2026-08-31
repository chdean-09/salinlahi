using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-146 revision. Level 4 restores ONE word, not two — the same ruling applied to Level 3.
///
/// The authored copy reads "Piliin mo ang salitaNG nararapat" — singular — while the original AC
/// named both INA and AMA. Ruled 2026-09-01 in favour of the copy, consistent with SALIN-144.
///
/// The ruling does not by itself say WHICH word stays visible, and Level 4 differs from Level 3
/// here: neither INA nor AMA is new, so there is no "already taught last level, shown as
/// reinforcement" word to leave in place. This follows the rule Level 3 established rather than
/// inventing a second one — show focus.01, blank focus.02:
///
///   Ang INA at ______ ang unang guro sa tahanan.
///
/// INA anchors the sentence and AMA is recalled. Under Reduced clues the player still has no
/// picture to lean on, so this remains a recall test, not a reading test.
///
/// With one restored word, evidenceContentId is level.ugat.04.focus.02 (AMA) and is simply correct.
/// Level 4 was the last Ugat level restoring two words, so the per-slot evidence schema change
/// flagged on SALIN-144 and SALIN-146 is no longer needed for this era.
/// </summary>
public static class Ugat04SingleBlankTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat04_Context.asset";

    private const string Prompt =
        "Wala nang larawang gagabay sa iyo. Piliin mo ang salitang nararapat, mula lamang sa iyong alaala." +
        "\nAng INA at ______ ang unang guro sa tahanan.";

    [MenuItem("Salinlahi/SALIN-146/Revise Ugat 4 To One Blank")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 4 -> single blank ===\n");

        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        if (sequence == null) { Debug.LogError($"Not found: {AssetPath}"); return; }

        var before = sequence.units[0];
        log.AppendLine($"  before: slots={before.slots.Length} candidates={before.candidateOccurrenceIds.Length}");
        log.AppendLine($"          prompt=\"{before.prompt.Replace("\n", " | ")}\"");

        const string ama = "ugat04-ama";
        const string bataDecoy = "ugat04-bata-decoy";
        const string mataDecoy = "ugat04-mata-decoy";

        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "ugat04-restore-sentence",
                mode = ChallengeMode.SentenceRestoration,
                cluePolicy = ChallengeCluePolicy.Reduced,   // unchanged: this is still the recall level
                prompt = Prompt,
                tokens = new[]
                {
                    Token(ama, "AMA", ChallengeTokenRole.Focus),
                    // Family word that fits the sentence's setting, so rejecting it needs meaning,
                    // not shape-matching.
                    Token(bataDecoy, "BATA", ChallengeTokenRole.Neutral),
                    // Shares the MA syllable with AMA.
                    Token(mataDecoy, "MATA", ChallengeTokenRole.Neutral),
                },
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "ugat04-slot-01", expectedOccurrenceId = ama },
                },
                candidateOccurrenceIds = new[] { ama, bataDecoy, mataDecoy },
                timerSeconds = 0f,
                allowHint = true,
                checkpointOnSuccess = true,
                memoryRevealSeconds = 1f,
                maxErrors = 3,
                heartPenalty = 1,
                evidenceContentId = "level.ugat.04.focus.02",
            },
        };

        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();

        var after = sequence.units[0];
        log.AppendLine($"  after:  slots={after.slots.Length} candidates=[{string.Join(", ", after.candidateOccurrenceIds)}]");
        log.AppendLine($"          prompt=\"{after.prompt.Replace("\n", " | ")}\"");
        log.AppendLine($"          cluePolicy={after.cluePolicy} evidence={after.evidenceContentId}");

        ChallengeValidationResult r = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(r.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in r.Errors) log.AppendLine($"    ERROR {e}");

        Debug.Log(log.ToString());
        File.WriteAllText("ugat04-revision-report.txt", log.ToString());
    }

    private static ChallengeTokenDefinition Token(string occurrenceId, string display, ChallengeTokenRole role) =>
        new ChallengeTokenDefinition
        {
            tokenId = occurrenceId, displayText = display, occurrenceId = occurrenceId, role = role,
        };
}
