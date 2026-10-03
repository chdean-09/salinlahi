/// <summary>
/// Production evidence sink: forwards challenge attempts into the unified
/// learning pipeline (SALIN-175). Evidence stays in the level recorder until the
/// AtomicSave phase commits it — the challenge never writes campaign progress.
/// </summary>
public sealed class ProgressManagerEvidenceSink : IChallengeEvidenceSink
{
    public void RecordAttempt(
        string contentId,
        LearningContentKind contentKind,
        MasteryDimension dimension,
        bool success,
        bool answerWasVisible)
    {
        if (ProgressManager.Instance == null || string.IsNullOrEmpty(contentId))
            return;

        ProgressManager.Instance.LevelEvidence.RecordAttempt(
            contentId, contentKind, dimension, success, answerWasVisible);
    }
}
