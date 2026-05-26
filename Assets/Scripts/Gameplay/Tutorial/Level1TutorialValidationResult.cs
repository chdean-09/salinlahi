public readonly struct Level1TutorialValidationResult
{
    public Level1TutorialValidationResult(bool isCorrect, Level1TutorialValidationFailure failure)
    {
        IsCorrect = isCorrect;
        Failure = failure;
    }

    public bool IsCorrect { get; }
    public Level1TutorialValidationFailure Failure { get; }

    public static Level1TutorialValidationResult Correct()
        => new(true, Level1TutorialValidationFailure.None);

    public static Level1TutorialValidationResult Incorrect(Level1TutorialValidationFailure failure)
        => new(false, failure);
}
