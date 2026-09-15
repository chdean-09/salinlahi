/// <summary>What a spawn does about its type's introduction, and what that implies for the ability.</summary>
public enum IntroductionOutcome
{
    /// <summary>Not an introduction. Ability armed — the ordinary case.</summary>
    None = 0,

    /// <summary>Four-step card plays; the ability is inert this spawn and arms on a later one.</summary>
    IntroduceAndSuppress = 1,

    /// <summary>Eight-beat lesson plays; the ability fires during it. Beat 2 depends on this.</summary>
    IntroduceAndArm = 2,

    /// <summary>Held back so a pending lesson lands first. Ability suppressed until it does.</summary>
    DeferAndSuppress = 3,
}

/// <summary>
/// Resolves a spawn's introduction outcome. Pure and UnityEngine-free so the rule — including the
/// one that inverts the existing arming contract — is asserted in EditMode.
///
/// <para>
/// <b>Two opposite rules live here and both are correct.</b> An ordinary declined claim leaves the
/// ability ARMED: an ability with no card is a better failure than a card's worth of silence with
/// the ability switched off. A claim declined because a lesson is pending SUPPRESSES: that decline
/// is deliberate, and the whole point of the lesson is that the player meets an ability only after
/// being told abilities exist. Do not collapse these two into one branch.
/// </para>
/// </summary>
public static class IntroductionDecision
{
    public static IntroductionOutcome Resolve(
        bool claimAccepted, bool lessonArmsAbility, bool aLessonIsPending)
    {
        if (claimAccepted)
        {
            return lessonArmsAbility
                ? IntroductionOutcome.IntroduceAndArm
                : IntroductionOutcome.IntroduceAndSuppress;
        }

        return aLessonIsPending
            ? IntroductionOutcome.DeferAndSuppress
            : IntroductionOutcome.None;
    }

    public static bool SuppressesAbility(IntroductionOutcome outcome)
    {
        return outcome == IntroductionOutcome.IntroduceAndSuppress
            || outcome == IntroductionOutcome.DeferAndSuppress;
    }
}
