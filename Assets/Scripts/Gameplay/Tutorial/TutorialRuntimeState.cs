using UnityEngine;

public static class TutorialRuntimeState
{
    public static bool IsActive { get; private set; }
    public static int ActiveLevelNumber { get; private set; } = -1;
    public static bool IsCombatOverrideActive { get; private set; }
    public static bool IsDrawingInputLocked { get; private set; }

    /// <summary>
    /// True for exactly as long as the onboarding heart-loss demo owns the field.
    ///
    /// <para>
    /// <b>The one thing the demo must never do is cost a heart.</b> It stages a base hit for
    /// teaching — it drives the HUD shake and the camera flash through
    /// <c>EventBus.OnTutorialBaseHitDemo</c> and never calls <c>HeartSystem.LoseHeart</c>. That
    /// promise was still broken, because the demo also parks a REAL pooled enemy on top of the
    /// shrine and the way it removed it ran the ordinary defeat path. While this flag is up,
    /// <c>PlayerBase</c> declines real base hits and says so in the log, so a future change to the
    /// demo cannot quietly reintroduce the same class of bug.
    /// </para>
    ///
    /// <para>
    /// Deliberately NOT gated on <see cref="IsActive"/>, unlike the two setters below: the demo is
    /// the beat that runs before the tutorial has declared itself, and a guard that is only armed
    /// once something else is true is not a guard.
    /// </para>
    /// </summary>
    public static bool IsHeartLossDemoActive { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void DomainReloadInit()
    {
        Clear();
    }

    public static bool IsActiveForLevel(int levelNumber)
    {
        return IsActive && ActiveLevelNumber == levelNumber;
    }

    public static void Begin(int levelNumber)
    {
        IsActive = true;
        ActiveLevelNumber = levelNumber;
        IsCombatOverrideActive = false;
        IsDrawingInputLocked = false;
    }

    public static void SetCombatOverrideActive(bool active)
    {
        IsCombatOverrideActive = IsActive && active;
    }

    public static void SetDrawingInputLocked(bool locked)
    {
        IsDrawingInputLocked = IsActive && locked;
    }

    /// <summary>Opens and closes the heart-loss demo's no-real-damage window.</summary>
    public static void SetHeartLossDemoActive(bool active)
    {
        IsHeartLossDemoActive = active;
    }

    public static void Clear()
    {
        IsActive = false;
        ActiveLevelNumber = -1;
        IsCombatOverrideActive = false;
        IsDrawingInputLocked = false;
        IsHeartLossDemoActive = false;
    }
}
