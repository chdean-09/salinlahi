using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SALIN-231. Construct-and-inspect guard for the hint cost modal.
///
/// ⚠️ WHY NOT A SerializedObject SCENE-WIRING GUARD. Nothing in this stack is scene-wired:
/// ChallengeModeUI and ChallengeFlowController appear in no .unity file and are built at
/// runtime, and HintModal carries no [SerializeField] at all. A SerializedObject read here
/// would assert nothing and report a false green — the argument
/// MemoryArchiveSceneWiringTests.cs:21-30 already makes. So the modal is built for real and
/// inspected for real instead.
///
/// ⚠️ THE ASSERTION-THAT-CANNOT-FAIL TRAP. "Opening consumes nothing" and "cancel consumes
/// nothing" both pass trivially against a modal that never calls into the session at all —
/// they would prove nothing on their own. ConfirmConsumesExactlyOneHint is their positive
/// control: same fixture, same session, confirm instead of cancel, opposite outcome. If the
/// confirm path were broken, that test fails and the two no-op tests are shown to be live.
/// </summary>
public class HintModalTests
{
    private readonly List<Object> _objectsToDestroy = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = _objectsToDestroy.Count - 1; i >= 0; i--)
        {
            if (_objectsToDestroy[i] != null)
                Object.DestroyImmediate(_objectsToDestroy[i]);
        }

        _objectsToDestroy.Clear();
    }

    [Test]
    public void OpeningTheModal_ConsumesNothing()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();

        modal.Open(session, "IBA", "different", () => session.RequestHint());

        Assert.IsTrue(modal.IsOpen);
        Assert.AreEqual(HintModal.Mode.Confirm, modal.CurrentMode);
        AssertNothingSpent(session);
        Assert.IsTrue(modal.ConfirmIsInteractable,
            "A hint is available, so the confirm control must be live — otherwise the "
            + "cancel test below would pass for the wrong reason.");
    }

    [Test]
    public void Cancel_IsANoOp()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();
        modal.Open(session, "IBA", "different", () => session.RequestHint());

        modal.Cancel();

        Assert.IsFalse(modal.IsOpen);
        AssertNothingSpent(session);
        Assert.IsTrue(session.CanRequestHint, "The hint must still be there to buy.");
    }

    /// <summary>The positive control for <see cref="Cancel_IsANoOp"/>.</summary>
    [Test]
    public void Confirm_ConsumesExactlyOneHint()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();
        modal.Open(session, "IBA", "different", () => session.RequestHint());

        modal.Confirm();

        Assert.AreEqual(1, session.HintsUsed);
        Assert.AreEqual(1, session.EmergencyHintsUsed);
        Assert.AreEqual(0.10f, session.EmergencyHintScorePenalty, 0.0001f,
            "The penalty Results renders comes from exactly this.");
        Assert.AreEqual(HintModal.Mode.Revealed, modal.CurrentMode);
        StringAssert.Contains("different", modal.BodyText,
            "Confirming buys the meaning, which is the one hint type the authored data serves.");
        StringAssert.DoesNotContain("w1", modal.BodyText,
            "AC-5: a hint explains the word. It must not hand over the answer token, which is "
            + "what the old free hint did (ChallengeModeUI returned \"Hint: {displayText}\").");
    }

    [Test]
    public void ExhaustedBudget_ShowsNoHintsLeftAndCannotBuyAnother()
    {
        ChallengeSession session = CreateTierFiveSession();
        session.RequestHint();
        Assert.IsTrue(session.IsHintExhausted, "Setup: tier 5 allows exactly one.");

        HintModal modal = CreateModal();
        modal.Open(session, "IBA", "different", () => session.RequestHint());

        Assert.AreEqual(HintModal.Mode.Exhausted, modal.CurrentMode);
        Assert.AreEqual(HintModalCopy.ExhaustedButtonLabel, HintModal.HintControlLabel(session),
            "BTN-HINT's exhausted copy, verbatim.");
        Assert.AreEqual(HintModalCopy.ExhaustedBody, modal.BodyText,
            "AC-4: the exhausted state explains itself instead of the button silently "
            + "no-opping, which is all ChallengeSession.cs did before this ticket.");

        modal.Confirm();

        Assert.AreEqual(1, session.HintsUsed,
            "The exhausted card offers Retry, not a second purchase.");
        Assert.AreEqual(1, session.EmergencyHintsUsed);
    }

    [Test]
    public void CostIsDisclosedBeforeUse_InScoreNotStars()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();

        modal.Open(session, "IBA", "different", () => session.RequestHint());

        StringAssert.Contains("10", modal.CostText,
            "AC-1: the cost is stated before the hint is used, and it is the effective "
            + "policy's 0.10 rendered as 10 points of the 0-100 score.");
        StringAssert.DoesNotContain("star", modal.CostText.ToLowerInvariant(),
            "docs/audit/BACKLOG.md:293 calls this a \"star cost\". The engine charges SCORE: "
            + "LevelResultsCalculator derives stars from hearts and the accuracies alone and "
            + "applies the penalty to metric.score only. Copy saying \"stars\" would lie.");
        AssertNothingSpent(session);
    }

    /// <summary>
    /// AC-3's copy half. The Results screen readout and the modal's pre-use disclosure must
    /// agree on both the number and the unit, or the player is quoted one price and charged
    /// another.
    /// </summary>
    [Test]
    public void ResultsPenaltyReadout_MatchesTheDisclosedCostAndItsUnit()
    {
        Assert.AreEqual("Hint cost -10", LevelResultsCopy.HintPenalty(10));
        StringAssert.DoesNotContain("star", LevelResultsCopy.HintPenaltyLabel.ToLowerInvariant());
        StringAssert.Contains(
            "10", HintModalCopy.CostLine(HintModalCopy.ScorePointsFromFraction(0.10f)),
            "The modal quotes the same 10 the Results screen deducts.");
    }

    private static void AssertNothingSpent(ChallengeSession session)
    {
        Assert.AreEqual(0, session.HintsUsed);
        Assert.AreEqual(0, session.EmergencyHintsUsed);
        Assert.AreEqual(0f, session.EmergencyHintScorePenalty, 0.0001f);
        Assert.AreEqual(ChallengeCluePolicy.Full, session.CluePolicy, "No clue may be degraded.");
        Assert.IsNull(session.HintOccurrenceId);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    private HintModal CreateModal()
    {
        // The host carries a Canvas so BuildIfNeeded adopts it instead of creating one and
        // re-parenting the modal out from under the fixture — which would orphan a canvas
        // per test. Tearing down the host therefore tears down everything the modal built.
        // DestroyImmediate, not Destroy: Destroy is deferred in EditMode and would leak the
        // objects while the test still passed.
        var host = new GameObject("HintModalTestHost", typeof(RectTransform), typeof(Canvas));
        _objectsToDestroy.Add(host);
        return HintModal.CreateRuntime(host.transform);
    }

    private ChallengeSession CreateTierFiveSession()
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _objectsToDestroy.Add(sequence);
        sequence.sequenceId = "hint-modal-tests";
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "unit-1",
                mode = ChallengeMode.WordPlacement,
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "slot-1", expectedOccurrenceId = "word-1" },
                },
                tokens = new[]
                {
                    new ChallengeTokenDefinition { tokenId = "w1", displayText = "w1", occurrenceId = "word-1" },
                },
                maxErrors = 3,
                heartPenalty = 1,
                evidenceContentId = "level.ugat.05.focus.01",
            },
        };
        ChallengeSession session = new ChallengeSession(
            sequence, startingHearts: 3, policy: ChallengeTierPolicy.ForTier(5));
        session.Enter();
        return session;
    }
}
