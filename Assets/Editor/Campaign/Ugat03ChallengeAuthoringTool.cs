using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-144. Authors the Ugat Level 3 context challenge and wires it to the level.
///
/// Unlike Level 2, which completes words from syllables, Level 3 restores two WORDS into a
/// sentence, so this is SentenceRestoration rather than WordPlacement.
///
/// Shape follows the acceptance criteria:
///
///   AC1 "when `Ang mabuting BATA ay gumagawa ng TAMA.` is presented, then its two blanks clearly
///        correspond to BATA and TAMA"
///   AC2 "when it is placed in the sentence, then the visible sentence updates WITHOUT BYPASSING
///        the second target"
///
/// That second clause is why this is ONE unit with TWO slots rather than two units. ChallengeSession
/// walks slots by _currentSlotIndex for placement, and SubmitRestoration compares the submitted
/// occurrence list against the slots with SequenceEqual — so slot order IS sentence order, and the
/// second blank cannot be skipped.
///
/// The decoy is MATA, and it is chosen rather than arbitrary:
///   * TAMA is TA + MA; MATA is MA + TA — the same two syllables in the opposite order, so it tests
///     whether the player reads syllable ORDER rather than recognising a pair of shapes.
///   * BATA is BA + TA; MATA is MA + TA — differing only in the first syllable.
/// One decoy therefore genuinely threatens both blanks, and the player already met MATA in Level 2,
/// satisfying "without requiring unsupported characters" (pool: A, EI, BA, MA, NA, TA).
/// </summary>
public static class Ugat03ChallengeAuthoringTool
{
    private const string AssetPath = "Assets/ScriptableObjects/Challenges/Challenge_Ugat03_Context.asset";
    private const string LevelPath = "Assets/ScriptableObjects/Levels/Level3_Config.asset";

    [MenuItem("Salinlahi/SALIN-144/Author Ugat 3 Context Challenge")]
    public static void Apply()
    {
        var log = new StringBuilder("=== Ugat 3 context challenge ===\n");

        var sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.sequenceId = "challenge.ugat.03";
        sequence.displayName = "Ikatlong Alaala";

        const string bata = "ugat03-bata";
        const string tama = "ugat03-tama";
        const string mataDecoy = "ugat03-mata-decoy";

        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "ugat03-restore-sentence",
                mode = ChallengeMode.SentenceRestoration,
                // Level 3 still teaches; SALIN-146 (Level 4) is the "fewer clues" level.
                cluePolicy = ChallengeCluePolicy.Full,
                prompt = "Ang mabuting ______ ay gumagawa ng ______.",
                tokens = new[]
                {
                    Token(bata, "BATA", ChallengeTokenRole.Focus),
                    Token(tama, "TAMA", ChallengeTokenRole.Focus),
                    Token(mataDecoy, "MATA", ChallengeTokenRole.Neutral),
                },
                // Slot order IS sentence order: BATA first, TAMA second.
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "ugat03-slot-01", expectedOccurrenceId = bata },
                    new ChallengeSlotDefinition { slotId = "ugat03-slot-02", expectedOccurrenceId = tama },
                },
                candidateOccurrenceIds = new[] { bata, tama, mataDecoy },
                timerSeconds = 0f,
                allowHint = true,
                checkpointOnSuccess = true,
                memoryRevealSeconds = 1f,
                maxErrors = 3,
                heartPenalty = 1,
                // One evidence id per unit. TAMA is the word Level 3 introduces; BATA already
                // carries Meaning evidence from Level 2, where it was a focus word.
                evidenceContentId = "level.ugat.03.focus.02",
            },
        };

        Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
        AssetDatabase.CreateAsset(sequence, AssetPath);

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        log.AppendLine($"  validator: {(result.IsValid ? "PASS" : "FAIL")}");
        foreach (string e in result.Errors) log.AppendLine($"    ERROR {e}");

        var u = sequence.units[0];
        log.AppendLine($"  {u.unitId} mode={u.mode} prompt=\"{u.prompt}\"");
        foreach (var s in u.slots) log.AppendLine($"    slot {s.slotId} -> {s.expectedOccurrenceId}");
        log.AppendLine($"    candidates=[{string.Join(", ", u.candidateOccurrenceIds)}]");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"Level not found at {LevelPath}"); return; }
        var so = new SerializedObject(level);
        so.FindProperty("challengeSequence").objectReferenceValue = sequence;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(level);
        log.AppendLine($"  wired Level3_Config.challengeSequence -> {Path.GetFileName(AssetPath)}");
        log.AppendLine($"  challengePrototypeEnabled left at " +
                       $"{so.FindProperty("challengePrototypeEnabled").boolValue} (matches Ugat 1 and 2)");

        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        File.WriteAllText("ugat03-challenge-report.txt", log.ToString());
    }

    private static ChallengeTokenDefinition Token(string occurrenceId, string display, ChallengeTokenRole role) =>
        new ChallengeTokenDefinition
        {
            tokenId = occurrenceId, displayText = display, occurrenceId = occurrenceId, role = role,
        };
}
