using UnityEngine;

public static class ChallengeRuntimeState
{
    public static bool IsActive { get; private set; }
    public static int ActiveLevelNumber { get; private set; } = -1;
    public static bool IsCombatOverrideActive { get; private set; }
    public static bool IsDrawingInputLocked { get; private set; }

    public static void Begin(int levelNumber)
    {
        IsActive = true;
        ActiveLevelNumber = levelNumber;
        IsCombatOverrideActive = true;
        IsDrawingInputLocked = false;
    }

    public static void SetDrawingInputLocked(bool locked) => IsDrawingInputLocked = IsActive && locked;

    public static void Clear()
    {
        IsActive = false;
        ActiveLevelNumber = -1;
        IsCombatOverrideActive = false;
        IsDrawingInputLocked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnDomainReload() => Clear();
}
