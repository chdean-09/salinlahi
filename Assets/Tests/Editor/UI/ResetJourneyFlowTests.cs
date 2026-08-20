using NUnit.Framework;

namespace Salinlahi.Tests.Editor.UI
{
    public sealed class ResetJourneyFlowTests
    {
        [TestCase(SaveManagerMode.Uninitialized, false)]
        [TestCase(SaveManagerMode.Legacy, false)]
        [TestCase(SaveManagerMode.RevisedBlocked, false)]
        [TestCase(SaveManagerMode.RevisedReady, true)]
        public void CanOfferReset_OnlyForRevisedReady(SaveManagerMode mode, bool expected)
        {
            Assert.That(ResetJourneyFlow.CanOfferReset(mode), Is.EqualTo(expected));
        }

        [Test]
        public void Classify_AcceptedResults_Succeed()
        {
            Assert.That(ResetJourneyFlow.Classify(CampaignOutcomeCommitResult.Committed(null)),
                Is.EqualTo(ResetJourneyOutcome.Succeeded));
            Assert.That(ResetJourneyFlow.Classify(CampaignOutcomeCommitResult.AlreadyCommitted(null)),
                Is.EqualTo(ResetJourneyOutcome.Succeeded));
        }

        [Test]
        public void Classify_NonAcceptedResults_AreRetryableFailures()
        {
            Assert.That(ResetJourneyFlow.Classify(null),
                Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
            Assert.That(ResetJourneyFlow.Classify(CampaignOutcomeCommitResult.PendingRetry(
                null, CampaignSaveFailureCode.IoFailure, "io")),
                Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
            Assert.That(ResetJourneyFlow.Classify(CampaignOutcomeCommitResult.Rejected(
                null, CampaignSaveFailureCode.InvalidStructure, "bad")),
                Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
            Assert.That(ResetJourneyFlow.Classify(CampaignOutcomeCommitResult.Blocked(
                null, CampaignSaveFailureCode.IoFailure, "blocked")),
                Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
        }

        [Test]
        public void Execute_WhenModeIsNotReady_DoesNotClearAndFails()
        {
            int clears = 0;
            // The stale accepted result must NOT be misread as success (spec hardening).
            ResetJourneyOutcome outcome = ResetJourneyFlow.Execute(
                () => SaveManagerMode.Legacy,
                () => clears++,
                () => CampaignOutcomeCommitResult.Committed(null));

            Assert.That(outcome, Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
            Assert.That(clears, Is.EqualTo(0));
        }

        [Test]
        public void Execute_WhenReadyAndAccepted_ClearsOnceAndSucceeds()
        {
            int clears = 0;
            ResetJourneyOutcome outcome = ResetJourneyFlow.Execute(
                () => SaveManagerMode.RevisedReady,
                () => clears++,
                () => CampaignOutcomeCommitResult.Committed(null));

            Assert.That(outcome, Is.EqualTo(ResetJourneyOutcome.Succeeded));
            Assert.That(clears, Is.EqualTo(1));
        }

        [Test]
        public void Execute_WhenReadyButCommitFails_ReportsRetryableFailure()
        {
            ResetJourneyOutcome outcome = ResetJourneyFlow.Execute(
                () => SaveManagerMode.RevisedReady,
                () => { },
                () => CampaignOutcomeCommitResult.Blocked(null, CampaignSaveFailureCode.IoFailure, "io"));

            Assert.That(outcome, Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
        }

        [Test]
        public void Execute_WithMissingDelegates_FailsWithoutThrowing()
        {
            Assert.That(ResetJourneyFlow.Execute(null, null, null),
                Is.EqualTo(ResetJourneyOutcome.RetryableFailure));
        }

        [Test]
        public void ConfirmBody_MentionsEveryClearedAndKeptCategory()
        {
            // Pins AC1: the confirmation must explain what clears and what remains.
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("level progress"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("restored words"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("memories"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("character unlocks"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("Endless Mode"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("audio settings"));
            Assert.That(ResetJourneyFlow.ConfirmBody, Does.Contain("update history"));
        }
    }
}
