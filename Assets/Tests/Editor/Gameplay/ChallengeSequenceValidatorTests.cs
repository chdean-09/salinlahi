using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class ChallengeSequenceValidatorTests
{
    [Test]
    public void DuplicateIdsAreRejected()
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "duplicate",
                mode = ChallengeMode.GuidedTracing,
                tokens = new[] { new ChallengeTokenDefinition { tokenId = "a", occurrenceId = "same" } }
            },
            new ChallengeUnitDefinition
            {
                unitId = "duplicate",
                mode = ChallengeMode.GuidedTracing,
                tokens = new[] { new ChallengeTokenDefinition { tokenId = "b", occurrenceId = "same" } }
            }
        };

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("unitId"));
        Assert.That(result.Errors, Has.Some.Contains("occurrenceId"));
        Object.DestroyImmediate(sequence);
    }

    [Test]
    public void FifteenSourceRowShapesCanUseOneModel()
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        sequence.units = new ChallengeUnitDefinition[15];
        List<BaybayinCharacterSO> characters = new List<BaybayinCharacterSO>();
        for (int i = 0; i < sequence.units.Length; i++)
        {
            BaybayinCharacterSO targetCharacter = null;
            if ((i % 5) == (int)ChallengeMode.GuidedTracing)
            {
                targetCharacter = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
                targetCharacter.characterID = "CHAR-" + i;
                characters.Add(targetCharacter);
            }
            sequence.units[i] = new ChallengeUnitDefinition
            {
                unitId = "unit-" + i,
                mode = (ChallengeMode)(i % 5),
                timerSeconds = (i % 5) == (int)ChallengeMode.TimedMemory ? 30f : 0f,
                tokens = new[] { new ChallengeTokenDefinition { tokenId = "token-" + i, occurrenceId = "occurrence-" + i, targetCharacter = targetCharacter } },
                slots = new[] { new ChallengeSlotDefinition { slotId = "slot-" + i, expectedOccurrenceId = "occurrence-" + i } },
                candidateOccurrenceIds = new[] { "occurrence-" + i }
            };
        }

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        foreach (BaybayinCharacterSO character in characters)
            Object.DestroyImmediate(character);
        Object.DestroyImmediate(sequence);
    }

    [Test]
    public void CrossReferencesMustUseKnownOccurrenceIdsAndMatchingGuidedCharacter()
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        BaybayinCharacterSO ba = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        BaybayinCharacterSO ou = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        ba.characterID = "BA";
        ou.characterID = "OU";
        Level1TutorialStepSO step = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
        step.targetCharacter = ou;
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "guided-ba",
                mode = ChallengeMode.GuidedTracing,
                guidedStep = step,
                tokens = new[]
                {
                    new ChallengeTokenDefinition { tokenId = "BA", occurrenceId = "ba-1", targetCharacter = ba }
                }
            },
            new ChallengeUnitDefinition
            {
                unitId = "restore",
                mode = ChallengeMode.SentenceRestoration,
                tokens = new[] { new ChallengeTokenDefinition { tokenId = "word-1", occurrenceId = "word-1" } },
                slots = new[] { new ChallengeSlotDefinition { slotId = "slot-1", expectedOccurrenceId = "missing" } },
                candidateOccurrenceIds = new[] { "missing" }
            }
        };

        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("does not match guidedStep"));
        Assert.That(result.Errors, Has.Some.Contains("unknown occurrenceId"));
        Object.DestroyImmediate(step);
        Object.DestroyImmediate(ba);
        Object.DestroyImmediate(ou);
        Object.DestroyImmediate(sequence);
    }

    [Test]
    public void PrototypeAssetPassesValidation()
    {
        ChallengeSequenceSO sequence = AssetDatabase.LoadAssetAtPath<ChallengeSequenceSO>("Assets/ScriptableObjects/Challenges/Level1_ChallengeSequence.asset");

        Assert.That(sequence, Is.Not.Null);
        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(sequence);
        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
    }
}
