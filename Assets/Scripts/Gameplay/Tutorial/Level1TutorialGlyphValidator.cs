using System.Collections.Generic;
using UnityEngine;

public sealed class Level1TutorialGlyphValidator
{
    public Level1TutorialValidationResult Validate(
        string targetCharacterId,
        RecognitionResult recognitionResult,
        bool passedRecognitionThreshold)
    {
        if (string.IsNullOrWhiteSpace(targetCharacterId))
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.NoPrompt);

        if (!passedRecognitionThreshold)
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.RecognitionFailed);

        if (!string.Equals(
                recognitionResult.characterID,
                targetCharacterId,
                System.StringComparison.OrdinalIgnoreCase))
        {
            return Level1TutorialValidationResult.Incorrect(Level1TutorialValidationFailure.WrongCharacter);
        }

        return Level1TutorialValidationResult.Correct();
    }
}
