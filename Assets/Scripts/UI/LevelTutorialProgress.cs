using UnityEngine;

public static class LevelTutorialProgress
{
    public const int TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const string Level1FtueSeenKey = ProgressManager.Level1FtueSeenKey;

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
        return PlayerPrefs.GetInt(Level1FtueSeenKey, 0) == 1;
    }

    public static void MarkLevel1TutorialSeen()
    {
        PlayerPrefs.SetInt(Level1FtueSeenKey, 1);
        PlayerPrefs.Save();
    }

#if UNITY_EDITOR
    public static void ResetLevel1TutorialForTests()
    {
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.Save();
    }
#endif
}
