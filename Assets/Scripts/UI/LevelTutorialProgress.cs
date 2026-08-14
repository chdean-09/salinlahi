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
        if (UsesRevisedProgress())
            return HasSeenRevisedLevel(Level1TutorialLevelNumber);
        return PlayerPrefs.GetInt(Level1FtueSeenKey, 0) == 1;
    }

    public static bool HasSeenLevel2Tutorial()
    {
        if (UsesRevisedProgress())
            return HasSeenRevisedLevel(Level2TutorialLevelNumber);
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
        if (UsesRevisedProgress())
        {
            SaveManager.Instance.Repository.TryRecordTutorialProgress(GetStableLevelId(Level1TutorialLevelNumber), true, -1);
            return;
        }
        PlayerPrefs.SetInt(Level1FtueSeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void MarkLevel2TutorialSeen()
    {
        if (UsesRevisedProgress())
        {
            SaveManager.Instance.Repository.TryRecordTutorialProgress(GetStableLevelId(Level2TutorialLevelNumber), true, -1);
            return;
        }
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

    private static bool HasSeenRevisedLevel(int levelNumber)
    {
        TutorialProgressRecord record = SaveManager.Instance.Repository.GetTutorialProgress(GetStableLevelId(levelNumber));
        return record != null && record.seen;
    }

#if UNITY_EDITOR
    public static void ResetLevel1TutorialForTests()
    {
        if (UsesRevisedProgress())
            return;
        PlayerPrefs.DeleteKey(Level1FtueSeenKey);
        PlayerPrefs.DeleteKey(Level2AdvancedSeenKey);
        PlayerPrefs.Save();
    }
#endif
}
