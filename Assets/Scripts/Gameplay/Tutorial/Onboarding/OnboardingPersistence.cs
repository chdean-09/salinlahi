using UnityEngine;

/// <summary>
/// Persists the Level 1 onboarding sequence progress so a quit mid-flow can resume
/// at the next un-completed beat. Backed by PlayerPrefs (key defined in ProgressManager).
/// </summary>
public static class OnboardingPersistence
{
    /// <summary>Sentinel: no beat has completed yet — start from index 0.</summary>
    public const int NoBeatCompleted = -1;

    /// <summary>Returns the index of the last completed beat, or -1 if none completed yet.</summary>
    public static int GetLastCompletedBeatIndex()
    {
        return PlayerPrefs.GetInt(ProgressManager.Level1FtueBeatIndexKey, NoBeatCompleted);
    }

    /// <summary>Records the last completed beat index. Negative values are clamped to -1.</summary>
    public static void SetLastCompletedBeatIndex(int index)
    {
        int clamped = index < NoBeatCompleted ? NoBeatCompleted : index;
        PlayerPrefs.SetInt(ProgressManager.Level1FtueBeatIndexKey, clamped);
        PlayerPrefs.Save();
    }

    /// <summary>Returns the beat index to start the loop from on the next run.</summary>
    public static int GetResumeStartIndex()
    {
        int last = GetLastCompletedBeatIndex();
        return last < 0 ? 0 : last + 1;
    }

    /// <summary>Clears stored progress. Called when the full tutorial completes or on global reset.</summary>
    public static void Clear()
    {
        PlayerPrefs.DeleteKey(ProgressManager.Level1FtueBeatIndexKey);
        PlayerPrefs.Save();
    }
}
