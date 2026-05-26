public static class TutorialRuntimeState
{
    public static bool IsActive { get; private set; }
    public static int ActiveLevelNumber { get; private set; } = -1;
    public static bool IsCombatOverrideActive { get; private set; }

    public static bool IsActiveForLevel(int levelNumber)
    {
        return IsActive && ActiveLevelNumber == levelNumber;
    }

    public static void Begin(int levelNumber)
    {
        IsActive = true;
        ActiveLevelNumber = levelNumber;
        IsCombatOverrideActive = false;
    }

    public static void SetCombatOverrideActive(bool active)
    {
        IsCombatOverrideActive = IsActive && active;
    }

    public static void Clear()
    {
        IsActive = false;
        ActiveLevelNumber = -1;
        IsCombatOverrideActive = false;
    }
}
