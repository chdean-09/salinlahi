using UnityEngine;

public static class LevelTutorialProgress
{
    public const int TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const string Level1FtueSeenKey = ProgressManager.Level1FtueSeenKey;
    public const string Level1FirstEnemyGuidedKey = ProgressManager.Level1FirstEnemyGuidedKey;
    public const string Level1FirstEnemyDefeatedKey = ProgressManager.Level1FirstEnemyDefeatedKey;
    public const string Level1BaseHpExplainedKey = ProgressManager.Level1BaseHpExplainedKey;
    public const string Level1Wave1ClearExplainedKey = ProgressManager.Level1Wave1ClearExplainedKey;
    public const string Level1WorldIntroSeenKey = ProgressManager.Level1WorldIntroSeenKey;
    public const string Level1OnboardingCompleteKey = ProgressManager.Level1OnboardingCompleteKey;
    public const string Level1TraceAssistShownCountKey = ProgressManager.Level1TraceAssistShownCountKey;
    public const string Level1RecentDrawFailuresKey = ProgressManager.Level1RecentDrawFailuresKey;

    public static bool ShouldShowForLevel(LevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return false;

        return ShouldShowForLevelNumber(levelConfig.levelNumber);
    }

    public static bool ShouldShowForLevelNumber(int levelNumber)
    {
        return levelNumber == TutorialLevelNumber && !HasSeenLevel1Tutorial();
    }

    public static bool HasSeenLevel1Tutorial()
    {
        return HasSeen(Level1FtueSeenKey);
    }

    public static bool HasSeenLevel1FirstEnemyGuided()
    {
        return HasSeen(Level1FirstEnemyGuidedKey);
    }

    public static bool HasSeenLevel1FirstEnemyDefeated()
    {
        return HasSeen(Level1FirstEnemyDefeatedKey);
    }

    public static bool HasSeenLevel1BaseHpExplained()
    {
        return HasSeen(Level1BaseHpExplainedKey);
    }

    public static bool HasSeenLevel1Wave1ClearExplained()
    {
        return HasSeen(Level1Wave1ClearExplainedKey);
    }

    public static bool HasSeenLevel1WorldIntro()
    {
        return HasSeen(Level1WorldIntroSeenKey);
    }

    public static bool HasCompletedLevel1Onboarding()
    {
        return HasSeen(Level1OnboardingCompleteKey);
    }

    public static int GetLevel1TraceAssistShownCount()
    {
        return PlayerPrefs.GetInt(Level1TraceAssistShownCountKey, 0);
    }

    public static int GetLevel1RecentDrawFailures()
    {
        return PlayerPrefs.GetInt(Level1RecentDrawFailuresKey, 0);
    }

    public static void MarkLevel1TutorialSeen()
    {
        MarkSeen(Level1FtueSeenKey);
    }

    public static void MarkLevel1FirstEnemyGuided()
    {
        MarkSeen(Level1FirstEnemyGuidedKey);
    }

    public static void MarkLevel1FirstEnemyDefeated()
    {
        MarkSeen(Level1FirstEnemyDefeatedKey);
    }

    public static void MarkLevel1BaseHpExplained()
    {
        MarkSeen(Level1BaseHpExplainedKey);
    }

    public static void MarkLevel1Wave1ClearExplained()
    {
        MarkSeen(Level1Wave1ClearExplainedKey);
    }

    public static void MarkLevel1WorldIntroSeen()
    {
        MarkSeen(Level1WorldIntroSeenKey);
    }

    public static void MarkLevel1OnboardingComplete()
    {
        MarkSeen(Level1OnboardingCompleteKey);
    }

    public static int IncrementLevel1TraceAssistShownCount()
    {
        return IncrementInt(Level1TraceAssistShownCountKey);
    }

    public static int IncrementLevel1RecentDrawFailures()
    {
        return IncrementInt(Level1RecentDrawFailuresKey);
    }

    public static void ResetLevel1RecentDrawFailures()
    {
        PlayerPrefs.DeleteKey(Level1RecentDrawFailuresKey);
        PlayerPrefs.Save();
    }

    private static bool HasSeen(string key)
    {
        return PlayerPrefs.GetInt(key, 0) == 1;
    }

    private static void MarkSeen(string key)
    {
        PlayerPrefs.SetInt(key, 1);
        PlayerPrefs.Save();
    }

    private static int IncrementInt(string key)
    {
        int value = PlayerPrefs.GetInt(key, 0) + 1;
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        return value;
    }

#if UNITY_EDITOR
    public static void ResetLevel1TutorialForTests()
    {
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.DeleteKey(Level1FirstEnemyGuidedKey);
        PlayerPrefs.DeleteKey(Level1FirstEnemyDefeatedKey);
        PlayerPrefs.DeleteKey(Level1BaseHpExplainedKey);
        PlayerPrefs.DeleteKey(Level1Wave1ClearExplainedKey);
        PlayerPrefs.DeleteKey(Level1WorldIntroSeenKey);
        PlayerPrefs.DeleteKey(Level1OnboardingCompleteKey);
        PlayerPrefs.DeleteKey(Level1TraceAssistShownCountKey);
        PlayerPrefs.DeleteKey(Level1RecentDrawFailuresKey);
        PlayerPrefs.Save();
    }
#endif
}
