using UnityEngine;

/// <summary>
/// Persists onboarding sequence progress so a quit mid-flow can resume at the next
/// un-completed beat. Backed by PlayerPrefs keys defined in ProgressManager.
/// </summary>
public static class OnboardingPersistence
{
    /// <summary>Sentinel: no beat has completed yet — start from index 0.</summary>
    public const int NoBeatCompleted = -1;

    /// <summary>Returns the index of the last completed beat, or -1 if none completed yet.</summary>
    public static int GetLastCompletedBeatIndex()
    {
        return GetLastCompletedBeatIndex(LevelTutorialProgress.Level1TutorialLevelNumber);
    }

    public static int GetLastCompletedBeatIndex(int levelNumber)
    {
        if (UsesRevisedProgress())
        {
            TutorialProgressRecord record = SaveManager.Instance.Repository.GetTutorialProgress(GetStableLevelId(levelNumber));
            return record == null ? NoBeatCompleted : record.lastCompletedBeatIndex;
        }
        return PlayerPrefs.GetInt(GetBeatIndexKey(levelNumber), NoBeatCompleted);
    }

    /// <summary>Records the last completed beat index. Negative values are clamped to -1.</summary>
    public static void SetLastCompletedBeatIndex(int index)
    {
        SetLastCompletedBeatIndex(LevelTutorialProgress.Level1TutorialLevelNumber, index);
    }

    public static void SetLastCompletedBeatIndex(int levelNumber, int index)
    {
        int clamped = index < NoBeatCompleted ? NoBeatCompleted : index;
        if (UsesRevisedProgress())
        {
            SaveManager.Instance.Repository.TryRecordTutorialProgress(
                GetStableLevelId(levelNumber), false, clamped);
            return;
        }
        PlayerPrefs.SetInt(GetBeatIndexKey(levelNumber), clamped);
        PlayerPrefs.Save();
    }

    /// <summary>Returns the beat index to start the loop from on the next run.</summary>
    public static int GetResumeStartIndex()
    {
        return GetResumeStartIndex(LevelTutorialProgress.Level1TutorialLevelNumber);
    }

    /// <summary>
    /// The beat index the onboarding loop should start from.
    ///
    /// Delegates to <see cref="LevelTutorialProgress.ResolveTutorialStartBeatIndex"/> because a
    /// FORCED REPLAY must start at zero, and the stored index cannot express that on its own.
    /// The controller writes the index of every beat it finishes — including Release — so a
    /// completed tutorial leaves the index pointing one past the last beat. A level whose gate is
    /// held open by <c>alwaysShowTutorial</c> would then be admitted and immediately run ZERO
    /// beats, which on screen is indistinguishable from the tutorial being broken. On the revised
    /// save path it is worse: <see cref="Clear"/> returns early there and the index rides a
    /// monotonic ratchet, so nothing can ever bring it back down.
    ///
    /// An interrupted FIRST run still resumes where it stopped — only a replay is reset.
    /// </summary>
    public static int GetResumeStartIndex(int levelNumber)
    {
        return LevelTutorialProgress.ResolveTutorialStartBeatIndex(
            levelNumber, GetLastCompletedBeatIndex(levelNumber));
    }

    /// <summary>Clears stored progress. Called when the full tutorial completes or on global reset.</summary>
    public static void Clear()
    {
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(ProgressManager.Level1FtueBeatIndexKey);
        PlayerPrefs.DeleteKey(ProgressManager.Level2AdvancedBeatIndexKey);
        PlayerPrefs.Save();
    }

    public static void Clear(int levelNumber)
    {
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(GetBeatIndexKey(levelNumber));
        PlayerPrefs.Save();
    }

    private static string GetBeatIndexKey(int levelNumber)
    {
        if (levelNumber == LevelTutorialProgress.Level2TutorialLevelNumber)
            return ProgressManager.Level2AdvancedBeatIndexKey;

        return ProgressManager.Level1FtueBeatIndexKey;
    }

    private static bool UsesRevisedProgress()
    {
        return SaveManager.Instance != null && SaveManager.Instance.Mode == SaveManagerMode.RevisedReady &&
            SaveManager.Instance.Repository != null;
    }

    private static string GetStableLevelId(int levelNumber)
    {
        return levelNumber >= 1 && levelNumber <= ContentIdentity.RevisedLevelIds.Count
            ? ContentIdentity.RevisedLevelIds[levelNumber - 1]
            : null;
    }
}
