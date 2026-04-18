using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu utilities for debugging progress.
/// Place in Editor folder - will be stripped from runtime builds.
/// </summary>
public static class ProgressDebugMenu
{
    [MenuItem("Salinlahi/Debug/Clear Progress")]
    public static void ClearProgress()
    {
        // Use ProgressManager if available in play mode, otherwise manipulate PlayerPrefs directly
        if (Application.isPlaying && ProgressManager.Instance != null)
        {
            ProgressManager.Instance.ClearAllProgress();
        }
        else
        {
            ClearProgressDirectly();
        }

        // Show confirmation dialog
        EditorUtility.DisplayDialog("Progress Cleared", 
            "All Salinlahi progress data has been cleared from PlayerPrefs.", 
            "OK");
    }

    [MenuItem("Salinlahi/Debug/Unlock All Levels")]
    public static void UnlockAll()
    {
        // Use ProgressManager if available in play mode, otherwise manipulate PlayerPrefs directly
        if (Application.isPlaying && ProgressManager.Instance != null)
        {
            ProgressManager.Instance.UnlockAllLevels();
        }
        else
        {
            UnlockAllDirectly();
        }

        // Show confirmation dialog
        EditorUtility.DisplayDialog("Levels Unlocked", 
            "All levels (1-5) have been unlocked.", 
            "OK");
    }

    [MenuItem("Salinlahi/Debug/Show Progress")]
    public static void ShowProgress()
    {
        // Use ProgressManager if available in play mode, otherwise read PlayerPrefs directly
        if (Application.isPlaying && ProgressManager.Instance != null)
        {
            ShowProgressViaManager();
        }
        else
        {
            ShowProgressDirectly();
        }
    }

    #region Direct PlayerPrefs Manipulation (Editor Mode)

    private static void ClearProgressDirectly()
    {
        const string KeyPrefix = "salinlahi.progress.";
        const int TotalLevels = 5;

        for (int i = 1; i <= TotalLevels; i++)
        {
            PlayerPrefs.DeleteKey($"{KeyPrefix}unlocked.{i}");
            PlayerPrefs.DeleteKey($"{KeyPrefix}stars.{i}");
        }
        PlayerPrefs.DeleteKey(ProgressManager.EndlessModeKey);

        PlayerPrefs.Save();
        Debug.Log("[Salinlahi] Progress cleared (direct).");
    }

    private static void UnlockAllDirectly()
    {
        const string KeyPrefix = "salinlahi.progress.";
        const int TotalLevels = 5;

        for (int i = 1; i <= TotalLevels; i++)
        {
            PlayerPrefs.SetInt($"{KeyPrefix}unlocked.{i}", 1);
        }

        PlayerPrefs.Save();
        Debug.Log("[Salinlahi] All levels unlocked (direct).");
    }

    private static void ShowProgressDirectly()
    {
        const string KeyPrefix = "salinlahi.progress.";
        const int TotalLevels = 5;

        string progressInfo = "Current Progress:\n\n";

        for (int i = 1; i <= TotalLevels; i++)
        {
            bool unlocked = PlayerPrefs.GetInt($"{KeyPrefix}unlocked.{i}", i == 1 ? 1 : 0) == 1;
            int stars = PlayerPrefs.GetInt($"{KeyPrefix}stars.{i}", 0);

            progressInfo += $"Level {i}: {(unlocked ? "UNLOCKED" : "LOCKED")}";
            if (unlocked && stars > 0)
            {
                progressInfo += $" - {stars} star{(stars != 1 ? "s" : "")}";
            }
            progressInfo += "\n";
        }

        bool endlessUnlocked = PlayerPrefs.GetInt(ProgressManager.EndlessModeKey, 0) == 1;
        progressInfo += $"\nEndless Mode: {(endlessUnlocked ? "UNLOCKED" : "LOCKED")}";

        Debug.Log($"[Salinlahi] {progressInfo}");
        EditorUtility.DisplayDialog("Current Progress", progressInfo, "OK");
    }

    #endregion

    #region ProgressManager Integration (Play Mode)

    private static void ShowProgressViaManager()
    {
        const int TotalLevels = 5;

        string progressInfo = "Current Progress:\n\n";

        for (int i = 1; i <= TotalLevels; i++)
        {
            bool unlocked = ProgressManager.Instance.IsLevelUnlocked(i);
            int stars = ProgressManager.Instance.GetStars(i);

            progressInfo += $"Level {i}: {(unlocked ? "UNLOCKED" : "LOCKED")}";
            if (unlocked && stars > 0)
            {
                progressInfo += $" - {stars} star{(stars != 1 ? "s" : "")}";
            }
            progressInfo += "\n";
        }

        bool endlessUnlocked = ProgressManager.Instance.IsEndlessModeUnlocked();
        progressInfo += $"\nEndless Mode: {(endlessUnlocked ? "UNLOCKED" : "LOCKED")}";

        int totalStars = ProgressManager.Instance.GetTotalStars();
        progressInfo += $"\n\nTotal Stars: {totalStars}/{TotalLevels * 3}";

        Debug.Log($"[Salinlahi] {progressInfo}");
        EditorUtility.DisplayDialog("Current Progress", progressInfo, "OK");
    }

    #endregion
}
