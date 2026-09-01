using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// SALIN-156. Authors the Pamana Level 14 context challenge and wires it to Level14_Config.
///
/// THIS IS THE FIRST TimedMemory UNIT IN THE GAME. Every other authored challenge is GuidedTracing,
/// WordPlacement or SentenceRestoration. The mode is fully implemented -- it is not being introduced
/// here, only used for the first time:
///
///   * ChallengeSession shows the sentence for memoryRevealSeconds and BLOCKS submission while it is
///     revealed (SubmitPlacement returns early when IsMemoryRevealActive), then hides it and takes
///     slot-by-slot recall under timerSeconds.
///   * On expiry the session sets State = TimedOut, raises the TimedOut event and calls
///     ApplyPenalty(), and a checkpoint restore resets _remainingTime from _checkpointTime. That is
///     AC3's "the current checkpoint applies the approved penalty ... and the level remains
///     recoverable", satisfied by the runtime rather than by anything authored here.
///   * ChallengeSequenceSO validation additionally requires, for this mode only, a positive
///     timerSeconds, a non-negative memoryRevealSeconds, and at least one recall slot.
///
/// Mode is TimedMemory rather than SentenceRestoration despite AC4 quoting a sentence, because AC3
/// calls it "the timed memory sentence" and only this mode carries a timer and a reveal window.
/// Note the two modes also submit differently: SentenceRestoration compares a whole submitted list
/// with SequenceEqual, while TimedMemory walks slots one at a time through SubmitPlacement. Slot
/// order still matters, so the slots run in sentence order.
///
/// Clue policy is Reduced, and here that is NOT my choice -- AC2 states "reduced guidance is active".
///
/// THE TIMER VALUES ARE UNTUNED STARTING POINTS. Per RISK-14 in doc 11, boss and timed content starts
/// loose and tightens on playtest feedback. 45s to place two words after a 5s read of the sentence is
/// deliberately generous. Neither number comes from the ticket, and this is the only level in the
/// game with a fail-by-timeout state, so there is no precedent to calibrate against.
///
/// DECOY CHOICE IS PEDAGOGICAL. HALAGA sits against MAHALAGA on purpose: they differ only by the MA
/// prefix, so a player skimming the first syllables cannot pass. The others are earlier-level words
/// this level's own pool can still spell. None is a Level 15 focus word, which would preview the
/// ending.
///
/// THE PROMPT COPY IS MINE AND SHOULD BE REPLACED -- third instance, after Levels 11 and 12. The
/// SENTENCE is not: it is quoted verbatim from AC4 and is the team's Filipino.
/// docs/content/pamana-levels-11-15-narrative.md holds Level 14's copy as TO BE WRITTEN.
/// SALIN-188 gates it.
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
        unit.FindPropertyRelative("mode").enumValueIndex = 4;         // TimedMemory
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
        log.AppendLine($"  mode=TimedMemory cluePolicy=Reduced blanks=2 " +
                       $"timer={TimerSeconds}s reveal={MemoryRevealSeconds}s");

        var level = AssetDatabase.LoadAssetAtPath<LevelConfigSO>(LevelPath);
        if (level == null) { Debug.LogError($"{LevelPath} not found."); return; }
        var lso = new SerializedObject(level);
        lso.FindProperty("challengeSequence").objectReferenceValue = sequence;
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
        File.WriteAllText("pamana14-challenge-report.txt", log.ToString());
    }
}
