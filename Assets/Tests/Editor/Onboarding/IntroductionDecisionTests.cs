using NUnit.Framework;

public class IntroductionDecisionTests
{
    [Test]
    public void OrdinaryIntroduction_IntroducesAndSuppresses()
    {
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: true, lessonArmsAbility: false, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.IntroduceAndSuppress, outcome);
        Assert.IsTrue(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void LessonIntroduction_IntroducesAndArms()
    {
        // Beat 2 requires the ability to fire during the introduction. This is the inversion.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: true, lessonArmsAbility: true, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.IntroduceAndArm, outcome);
        Assert.IsFalse(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void DeclinedWhileItsOwnLessonIsPending_DefersAndSuppresses()
    {
        // A type whose own lesson is still pending keeps its ability suppressed until that lesson
        // can run. Unrelated types do not feed this flag; their first appearances are independent.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: false, lessonArmsAbility: false, aLessonIsPending: true);

        Assert.AreEqual(IntroductionOutcome.DeferAndSuppress, outcome);
        Assert.IsTrue(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void DeclinedWithNoLessonPending_ArmsTheAbility()
    {
        // NEGATIVE CONTROL for the test above. This is the existing contract and it must not
        // regress: an ordinary declined claim leaves the ability armed, because an ability with
        // no card is a better failure than a card's worth of silence with the ability off.
        IntroductionOutcome outcome = IntroductionDecision.Resolve(
            claimAccepted: false, lessonArmsAbility: false, aLessonIsPending: false);

        Assert.AreEqual(IntroductionOutcome.None, outcome);
        Assert.IsFalse(IntroductionDecision.SuppressesAbility(outcome));
    }

    [Test]
    public void AnAcceptedClaimIgnoresThePendingFlag()
    {
        // The enemy whose lesson is pending is itself claim-accepted; it must not defer itself.
        Assert.AreEqual(
            IntroductionOutcome.IntroduceAndArm,
            IntroductionDecision.Resolve(true, lessonArmsAbility: true, aLessonIsPending: true));
    }

    [Test]
    public void AbilityRuleLatch_IsOnceUntilCleared()
    {
        EnemyIntroductionProgress.ResetForTests();
        Assert.IsFalse(EnemyIntroductionProgress.HasSeenAbilityRule());

        EnemyIntroductionProgress.MarkAbilityRuleSeen();
        Assert.IsTrue(EnemyIntroductionProgress.HasSeenAbilityRule());

        EnemyIntroductionProgress.ClearAllIntroduced();
        Assert.IsFalse(EnemyIntroductionProgress.HasSeenAbilityRule(),
            "New Journey must re-teach the rule.");
    }
}
