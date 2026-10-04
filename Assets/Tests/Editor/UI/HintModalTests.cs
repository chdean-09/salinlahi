using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

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
        Assert.IsTrue(modal.ConfirmIsVisible);
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

    /// <summary>
    /// The dim overlay doubles as the tap-outside dismiss. Its Button is the same Cancel
    /// path the Cancel control fires, so the test invokes that listener directly rather
    /// than simulating a pointer — same handler, same no-spend contract as AC-2.
    /// </summary>
    [Test]
    public void TappingOutsideTheCard_CancelsWithoutSpending()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();
        modal.Open(session, "IBA", "different", () => session.RequestHint());

        Button backdrop = modal.transform.Find("DimOverlay")?.GetComponent<Button>();
        Assert.IsNotNull(backdrop, "The dim overlay must carry the tap-outside dismiss control.");
        backdrop.onClick.Invoke();

        Assert.IsFalse(modal.IsOpen, "An outside tap closes the card.");
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
        bool retried = false;
        modal.Open(session, "IBA", "different", () => session.RequestHint(), () => retried = true);

        Assert.AreEqual(HintModal.Mode.Exhausted, modal.CurrentMode);
        Assert.IsFalse(modal.ConfirmIsVisible, "Retry must be hidden when no hints remain.");
        Assert.IsFalse(modal.ConfirmIsInteractable);
        Assert.IsTrue(modal.CancelIsInteractable);
        Assert.AreEqual(HintModalCopy.CloseLabel, modal.CancelLabelText);
        StringAssert.DoesNotContain("Retry", modal.BodyText);
        Assert.AreEqual(HintModalCopy.ExhaustedButtonLabel, HintModal.HintControlLabel(session),
            "BTN-HINT's exhausted copy, verbatim.");
        Assert.AreEqual(HintModalCopy.ExhaustedBody, modal.BodyText,
            "AC-4: the exhausted state explains itself instead of the button silently "
            + "no-opping, which is all ChallengeSession.cs did before this ticket.");

        modal.Confirm();

        Assert.AreEqual(1, session.HintsUsed,
            "The exhausted card must not allow another purchase.");
        Assert.AreEqual(1, session.EmergencyHintsUsed);

        Button close = modal.transform.Find("Card/Actions/" + HintModalCopy.CancelLabel).GetComponent<Button>();
        close.onClick.Invoke();

        Assert.IsFalse(modal.IsOpen);
        Assert.IsFalse(retried, "Closing the exhausted card must not reset the checkpoint.");
        Assert.IsTrue(session.IsHintExhausted);
        Assert.AreEqual(1, session.HintsUsed);
    }

    [Test]
    public void ExhaustedBudget_LegacyRetryCannotResetCheckpoint()
    {
        ChallengeSession session = CreateTierFiveSession();
        session.RequestHint();
        bool retried = false;
        HintModal modal = CreateModal();
        modal.Open(session, "IBA", "different", () => session.RequestHint(), () => retried = true);

        modal.Retry();

        Assert.IsFalse(retried);
        Assert.IsTrue(session.IsHintExhausted);
    }

    [Test]
    public void ReopeningWithAvailableHints_RestoresConfirmButton()
    {
        ChallengeSession exhausted = CreateTierFiveSession();
        exhausted.RequestHint();
        HintModal modal = CreateModal();
        modal.Open(exhausted, "IBA", "different", () => exhausted.RequestHint());
        modal.Cancel();

        ChallengeSession available = CreateTierFiveSession();
        modal.Open(available, "IBA", "different", () => available.RequestHint());

        Assert.AreEqual(HintModal.Mode.Confirm, modal.CurrentMode);
        Assert.IsTrue(modal.ConfirmIsVisible);
        Assert.IsTrue(modal.ConfirmIsInteractable);
        Assert.AreEqual(HintModalCopy.ConfirmLabel, modal.ConfirmLabelText);
        modal.Confirm();
        Assert.AreEqual(1, available.HintsUsed);
    }

    [Test]
    public void RemainingHints_AreCenteredWithoutScoreCost()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();

        modal.Open(session, "IBA", "different", () => session.RequestHint());

        Assert.AreEqual("Natitirang pahiwatig: 1", modal.CostText);
        var remaining = modal.transform.Find("Card/Cost").GetComponent<TMPro.TextMeshProUGUI>();
        Assert.AreEqual(TMPro.TextAlignmentOptions.Center, remaining.alignment);
        AssertNothingSpent(session);
    }

    /// <summary>
    /// Hiding the cost disclosure must not change the score deduction shown in Results.
    /// </summary>
    [Test]
    public void ResultsPenaltyReadout_KeepsTheExistingScoreCost()
    {
        Assert.AreEqual("Gastos sa Pahiwatig -10", LevelResultsCopy.HintPenalty(10));
        StringAssert.DoesNotContain("star", LevelResultsCopy.HintPenaltyLabel.ToLowerInvariant());
        StringAssert.Contains(
            "10", HintModalCopy.CostLine(HintModalCopy.ScorePointsFromFraction(0.10f)),
            "The existing score-cost calculation remains unchanged.");
    }

    [Test]
    public void MissingMeaning_CannotSpendAHint()
    {
        ChallengeSession session = CreateTierFiveSession();
        HintModal modal = CreateModal();
        modal.Open(session, string.Empty, string.Empty, () => session.RequestHint());

        Assert.IsFalse(modal.ConfirmIsInteractable);
        Assert.AreEqual(HintModalCopy.NoHintAvailableBody, modal.BodyText);
        modal.Confirm();
        AssertNothingSpent(session);
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
