using UnityEngine;

public static class LevelTutorialProgress
{
    public const int TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const int Level1TutorialLevelNumber = ProgressManager.Level1FtueTutorialLevelNumber;
    public const int Level2TutorialLevelNumber = ProgressManager.Level2AdvancedTutorialLevelNumber;
    public const string Level1FtueSeenKey = ProgressManager.Level1FtueSeenKey;
    public const string Level2AdvancedSeenKey = ProgressManager.Level2AdvancedSeenKey;

    public static bool ShouldShowForLevel(LevelConfigSO levelConfig)
    {
        if (levelConfig == null)
            return false;

        return ShouldShowForLevelNumber(levelConfig.levelNumber);
    }

    public static bool ShouldShowForLevelNumber(int levelNumber)
    {
        if (levelNumber == Level1TutorialLevelNumber)
            return !HasSeenLevel1Tutorial();

        if (levelNumber == Level2TutorialLevelNumber)
            return !HasSeenLevel2Tutorial();

        return false;
    }

    public static bool HasSeenLevel1Tutorial()
    {
        return PlayerPrefs.GetInt(Level1FtueSeenKey, 0) == 1;
    }

    public static bool HasSeenLevel2Tutorial()
    {
        return PlayerPrefs.GetInt(Level2AdvancedSeenKey, 0) == 1;
    }

    public static bool HasSeenTutorialForLevel(int levelNumber)
    {
        if (levelNumber == Level1TutorialLevelNumber)
            return HasSeenLevel1Tutorial();

        if (levelNumber == Level2TutorialLevelNumber)
            return HasSeenLevel2Tutorial();

        return true;
    }

    public static void MarkLevel1TutorialSeen()
    {
        PlayerPrefs.SetInt(Level1FtueSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkLevel2TutorialSeen()
    {
        PlayerPrefs.SetInt(Level2AdvancedSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkTutorialSeen(int levelNumber)
    {
        if (levelNumber == Level1TutorialLevelNumber)
        {
            MarkLevel1TutorialSeen();
            return;
        }

        if (levelNumber == Level2TutorialLevelNumber)
            MarkLevel2TutorialSeen();
    }

#if UNITY_EDITOR
    public static void ResetLevel1TutorialForTests()
    {
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.DeleteKey(Level2AdvancedSeenKey);
        PlayerPrefs.Save();
    }
#endif
}
