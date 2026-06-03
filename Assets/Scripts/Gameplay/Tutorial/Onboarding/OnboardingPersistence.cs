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
        PlayerPrefs.SetInt(GetBeatIndexKey(levelNumber), clamped);
        PlayerPrefs.Save();
    }

    /// <summary>Returns the beat index to start the loop from on the next run.</summary>
    public static int GetResumeStartIndex()
    {
        return GetResumeStartIndex(LevelTutorialProgress.Level1TutorialLevelNumber);
    }

    public static int GetResumeStartIndex(int levelNumber)
    {
        int last = GetLastCompletedBeatIndex(levelNumber);
        return last < 0 ? 0 : last + 1;
    }

    /// <summary>Clears stored progress. Called when the full tutorial completes or on global reset.</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(ProgressManager.Level1FtueBeatIndexKey);
        PlayerPrefs.DeleteKey(ProgressManager.Level2AdvancedBeatIndexKey);
        PlayerPrefs.Save();
    }

    public static void Clear(int levelNumber)
    {
        PlayerPrefs.DeleteKey(GetBeatIndexKey(levelNumber));
        PlayerPrefs.Save();
    }

    private static string GetBeatIndexKey(int levelNumber)
    {
        if (levelNumber == LevelTutorialProgress.Level2TutorialLevelNumber)
            return ProgressManager.Level2AdvancedBeatIndexKey;

        return ProgressManager.Level1FtueBeatIndexKey;
    }
}
