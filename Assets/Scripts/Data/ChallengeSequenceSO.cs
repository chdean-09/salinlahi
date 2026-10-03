using System;
using System.Collections.Generic;
using UnityEngine;

public enum ChallengeMode
{
    GuidedTracing,
    WordPlacement,
    SentenceRestoration,
    ParagraphRestoration,
    TimedMemory
}

public enum ChallengeCluePolicy
{
    Full,
    Reduced,
    Minimal
}

public enum ChallengeTokenRole
{
    Neutral,
    Focus,
    Mastery
}

[CreateAssetMenu(fileName = "ChallengeSequence", menuName = "Salinlahi/Challenge Sequence")]
public class ChallengeSequenceSO : ScriptableObject
{
    public string sequenceId;
    public string displayName;
    public ChallengeUnitDefinition[] units = Array.Empty<ChallengeUnitDefinition>();
}

[Serializable]
public class ChallengeUnitDefinition
{
    public string unitId;
    public ChallengeMode mode;
    public ChallengeCluePolicy cluePolicy = ChallengeCluePolicy.Full;
    public string prompt;
    public ChallengeTokenDefinition[] tokens = Array.Empty<ChallengeTokenDefinition>();
    public ChallengeSlotDefinition[] slots = Array.Empty<ChallengeSlotDefinition>();
    public string[] candidateOccurrenceIds = Array.Empty<string>();
    public Level1TutorialStepSO guidedStep;
    public float timerSeconds;
    public bool allowHint = true;
    public bool checkpointOnSuccess = true;
    public float memoryRevealSeconds = 1f;
    public int maxErrors = 3;
    public int heartPenalty = 1;
    [Tooltip("Word stableId this unit evidences (Assembly for placements, Meaning for sentence/paragraph/memory). Empty = record nothing.")]
    public string evidenceContentId = string.Empty;
}

[Serializable]
public class ChallengeTokenDefinition
{
    public string tokenId;
    public string displayText;
    public string occurrenceId;
    public ChallengeTokenRole role = ChallengeTokenRole.Neutral;
    public BaybayinCharacterSO targetCharacter;
    [Tooltip("Symbol stableId this token evidences (Form for guided tracing). Empty = record nothing.")]
    public string evidenceContentId = string.Empty;
}

[Serializable]
public class ChallengeSlotDefinition
{
    public string slotId;
    public string expectedOccurrenceId;
}

public sealed class ChallengeValidationResult
{
    public readonly List<string> Errors = new List<string>();
    public bool IsValid => Errors.Count == 0;
}

public static class ChallengeSequenceValidator
{
    public static ChallengeValidationResult Validate(ChallengeSequenceSO sequence)
    {
        ChallengeValidationResult result = new ChallengeValidationResult();
        if (sequence == null)
        {
            result.Errors.Add("Challenge sequence is null.");
            return result;
        }

        HashSet<string> unitIds = new HashSet<string>();
        HashSet<string> tokenIds = new HashSet<string>();
        HashSet<string> slotIds = new HashSet<string>();
        HashSet<string> occurrenceIds = new HashSet<string>();
        if (sequence.units == null || sequence.units.Length == 0)
            result.Errors.Add("Challenge sequence must contain at least one unit.");

        for (int unitIndex = 0; sequence.units != null && unitIndex < sequence.units.Length; unitIndex++)
        {
            ChallengeUnitDefinition unit = sequence.units[unitIndex];
            if (unit == null)
            {
                result.Errors.Add($"Unit {unitIndex} is null.");
                continue;
            }

            AddUnique(result, unitIds, unit.unitId, $"unitId '{unit.unitId}'");
            if (unit.maxErrors <= 0)
                result.Errors.Add($"Unit '{unit.unitId}' must have a positive maxErrors value.");
            if (unit.heartPenalty < 0)
                result.Errors.Add($"Unit '{unit.unitId}' cannot have a negative heart penalty.");
            if (unit.mode == ChallengeMode.TimedMemory && unit.timerSeconds <= 0f)
                result.Errors.Add($"Timed unit '{unit.unitId}' must have a positive timerSeconds value.");
            if (unit.mode == ChallengeMode.TimedMemory && unit.memoryRevealSeconds < 0f)
                result.Errors.Add($"Timed memory unit '{unit.unitId}' cannot have a negative memoryRevealSeconds value.");
            if (unit.mode != ChallengeMode.TimedMemory && unit.timerSeconds < 0f)
                result.Errors.Add($"Unit '{unit.unitId}' cannot have a negative timerSeconds value.");

            HashSet<string> unitOccurrenceIds = new HashSet<string>();
            foreach (ChallengeTokenDefinition token in unit.tokens ?? Array.Empty<ChallengeTokenDefinition>())
            {
                if (token == null)
                {
                    result.Errors.Add($"Unit '{unit.unitId}' contains a null token.");
                    continue;
                }
                AddUnique(result, tokenIds, token.tokenId, $"tokenId '{token.tokenId}'");
                if (string.IsNullOrWhiteSpace(token.occurrenceId))
                    result.Errors.Add($"Unit '{unit.unitId}' contains a token without an occurrenceId.");
                else
                {
                    AddUnique(result, unitOccurrenceIds, token.occurrenceId, $"occurrenceId '{token.occurrenceId}' in unit");
                    AddUnique(result, occurrenceIds, token.occurrenceId, $"occurrenceId '{token.occurrenceId}'");
                }
            }

            if (unit.mode == ChallengeMode.GuidedTracing && unit.guidedStep != null && unit.guidedStep.targetCharacter != null && unit.tokens != null && unit.tokens.Length > 0)
            {
                BaybayinCharacterSO stepCharacter = unit.guidedStep.targetCharacter;
                BaybayinCharacterSO tokenCharacter = unit.tokens[0] == null ? null : unit.tokens[0].targetCharacter;
                if (tokenCharacter != null && !string.Equals(stepCharacter.characterID, tokenCharacter.characterID, StringComparison.OrdinalIgnoreCase))
                    result.Errors.Add($"Guided unit '{unit.unitId}' targetCharacter '{tokenCharacter.characterID}' does not match guidedStep target '{stepCharacter.characterID}'.");
            }

            if (unit.mode == ChallengeMode.GuidedTracing)
            {
                if (unit.guidedStep == null)
                    result.Errors.Add($"Guided unit '{unit.unitId}' must define a guidedStep.");
                foreach (ChallengeTokenDefinition token in unit.tokens ?? Array.Empty<ChallengeTokenDefinition>())
                {
                    if (token != null && (token.targetCharacter == null || string.IsNullOrWhiteSpace(token.targetCharacter.characterID)))
                        result.Errors.Add($"Guided unit '{unit.unitId}' token '{token.tokenId}' requires a canonical targetCharacter.characterID.");
                }
            }

            HashSet<string> unitSlotIds = new HashSet<string>();
            HashSet<string> unitExpectedOccurrenceIds = new HashSet<string>();
            foreach (ChallengeSlotDefinition slot in unit.slots ?? Array.Empty<ChallengeSlotDefinition>())
            {
                if (slot == null)
                {
                    result.Errors.Add($"Unit '{unit.unitId}' contains a null slot.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(slot.slotId))
                    result.Errors.Add($"Unit '{unit.unitId}' contains a slot without a slotId.");
                else
                {
                    AddUnique(result, slotIds, slot.slotId, $"slotId '{slot.slotId}'");
                    AddUnique(result, unitSlotIds, slot.slotId, $"slotId '{slot.slotId}' in unit");
                }
                if (string.IsNullOrWhiteSpace(slot.expectedOccurrenceId))
                    result.Errors.Add($"Slot '{slot.slotId}' is missing expectedOccurrenceId.");
                else if (!unitOccurrenceIds.Contains(slot.expectedOccurrenceId))
                    result.Errors.Add($"Slot '{slot.slotId}' references unknown occurrenceId '{slot.expectedOccurrenceId}'.");
                else if (!unitExpectedOccurrenceIds.Add(slot.expectedOccurrenceId))
                    result.Errors.Add($"Unit '{unit.unitId}' contains duplicate expected occurrenceId '{slot.expectedOccurrenceId}' across slots.");
            }

            foreach (string candidateOccurrenceId in unit.candidateOccurrenceIds ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(candidateOccurrenceId) || !unitOccurrenceIds.Contains(candidateOccurrenceId))
                    result.Errors.Add($"Unit '{unit.unitId}' candidate references unknown occurrenceId '{candidateOccurrenceId}'.");
            }

            HashSet<string> candidateIds = new HashSet<string>();
            foreach (string candidateOccurrenceId in unit.candidateOccurrenceIds ?? Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(candidateOccurrenceId) && !candidateIds.Add(candidateOccurrenceId))
                    result.Errors.Add($"Unit '{unit.unitId}' contains duplicate candidate occurrenceId '{candidateOccurrenceId}'.");
            }

            if (unit.mode == ChallengeMode.WordPlacement || unit.mode == ChallengeMode.SentenceRestoration || unit.mode == ChallengeMode.ParagraphRestoration || unit.mode == ChallengeMode.TimedMemory)
            {
                foreach (ChallengeSlotDefinition slot in unit.slots ?? Array.Empty<ChallengeSlotDefinition>())
                {
                    if (slot != null && !candidateIds.Contains(slot.expectedOccurrenceId))
                        result.Errors.Add($"Unit '{unit.unitId}' slot '{slot.slotId}' expected occurrenceId '{slot.expectedOccurrenceId}' is not selectable.");
                }
            }

            if ((unit.mode == ChallengeMode.WordPlacement || unit.mode == ChallengeMode.SentenceRestoration || unit.mode == ChallengeMode.ParagraphRestoration) && (unit.slots == null || unit.slots.Length == 0))
                result.Errors.Add($"Placement/restoration unit '{unit.unitId}' must define slots.");
            if (unit.mode == ChallengeMode.TimedMemory && (unit.slots == null || unit.slots.Length == 0))
                result.Errors.Add($"Timed memory unit '{unit.unitId}' must define recall slots.");
            if (unit.mode == ChallengeMode.GuidedTracing && (unit.tokens == null || unit.tokens.Length == 0))
                result.Errors.Add($"Guided tracing unit '{unit.unitId}' must define tokens.");
        }

        return result;
    }

    private static void AddUnique(ChallengeValidationResult result, HashSet<string> values, string value, string description)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add($"{description} cannot be empty.");
            return;
        }
        if (!values.Add(value))
            result.Errors.Add($"Duplicate {description}.");
    }
}
