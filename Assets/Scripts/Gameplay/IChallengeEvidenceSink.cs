/// <summary>
/// Receives learning-evidence attempts from a <see cref="ChallengeSession"/>
/// (SALIN-181). Pure C# so the session stays Unity-free and EditMode-testable;
/// the production sink forwards to ProgressManager's level evidence recorder.
/// Content identity comes from authored data: a token's evidenceContentId records
/// as a Symbol, a unit's evidenceContentId as a Word. Empty ids record nothing.
/// </summary>
public interface IChallengeEvidenceSink
{
    void RecordAttempt(
        string contentId,
        LearningContentKind contentKind,
        MasteryDimension dimension,
        bool success,
        bool answerWasVisible);
}
