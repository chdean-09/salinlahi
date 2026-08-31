using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-144 revision. Level 3 restores ONE word, not two.
///
/// The authored context copy says "Isang salita lamang ang kulang sa pangungusap ni Ama" — only one
/// word is missing — while SALIN-144's original AC1 called for two blanks. Ruled 2026-09-01 in
/// favour of the copy: the doc is right, the AC needs amending.
///
/// That reading is also the stronger teaching design. The sentence is
/// "Ang mabuting BATA ay gumagawa ng TAMA." BATA was a Level 2 focus word, so it arrives already
/// known and now appears in full as reinforcement; TAMA is the word Level 3 introduces and is the
/// only thing the player must recall. The level's teaching point stops competing with itself.
///
/// It also dissolves the evidenceContentId compromise for this level. With one restored word,
/// level.ugat.03.focus.02 (TAMA) is simply correct rather than a tiebreak between two words
/// sharing a single evidence field.
///
/// Decoys are MATA and AMA, both known and both in-pool (A, EI, BA, MA, NA, TA):
///   MATA is MA + TA — the same two syllables as TAMA in the opposite order, so it tests whether
///        the player reads syllable ORDER rather than recognising a pair of shapes.
///   AMA  shares the MA syllable, and gives a third option so a wrong reading is not a coin flip.
/// </summary>
public static class Ugat03SingleBlankTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat03_Context.asset";

    private const string Prompt =
        "Isang salita lamang ang kulang sa pangungusap ni Ama. Ilagay mo ang tamang salita sa tamang puwang." +
        "\nAng mabuting BATA ay gumagawa ng ______.";

    [MenuItem("Salinlahi/SALIN-144/Revise Ugat 3 To One Blank")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 3 -> single blank ===\n");

        var sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>(AssetPath);
        if (sequence == null) { Debug.LogError($"Not found: {AssetPath}"); return; }

        var before = sequence.units[0];
        log.AppendLine($"  before: slots={before.slots.Length} candidates={before.candidateOccurrenceIds.Length}");
        log.AppendLine($"          prompt=\"{before.prompt.Replace("\n", " | ")}\"");

        const string tama = "ugat03-tama";
        const string mataDecoy = "ugat03-mata-decoy";
        const string amaDecoy = "ugat03-ama-decoy";

        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "ugat03-restore-sentence",
                mode = ChallengeMode.SentenceRestoration,
                cluePolicy = ChallengeCluePolicy.Full,
                prompt = Prompt,
                tokens = new[]
                {
                    Token(tama, "TAMA", ChallengeTokenRole.Focus),
                    Token(mataDecoy, "MATA", ChallengeTokenRole.Neutral),
                    Token(amaDecoy, "AMA", ChallengeTokenRole.Neutral),
                },
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "ugat03-slot-01", expectedOccurrenceId = tama },
                },
                candidateOccurrenceIds = new[] { tama, mataDecoy, amaDecoy },
                timerSeconds = 0f,
                allowHint = true,
                checkpointOnSuccess = true,
                memoryRevealSeconds = 1f,
                maxErrors = 3,
                heartPenalty = 1,
                // Now unambiguous: TAMA is the only restored word.
                evidenceContentId = "level.ugat.03.focus.02",
            },
        };

        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();

        var after = sequence.units[0];
        log.AppendLine($"  after:  slots={after.slots.Length} candidates=[{string.Join(", ", after.candidateOccurrenceIds)}]");
        log.AppendLine($"          prompt=\"{after.prompt.Replace("\n", " | ")}\"");
        log.AppendLine($"          evidence={after.evidenceContentId}");

        ChallengeValidationResult r = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(r.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in r.Errors) log.AppendLine($"    ERROR {e}");

        Debug.Log(log.ToString());
        File.WriteAllText("ugat03-revision-report.txt", log.ToString());
    }

    private static ChallengeTokenDefinition Token(string occurrenceId, string display, ChallengeTokenRole role) =>
        new ChallengeTokenDefinition
        {
            tokenId = occurrenceId, displayText = display, occurrenceId = occurrenceId, role = role,
        };
}
