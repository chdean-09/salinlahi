using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SALIN-231. The read-only hint-economy queries the cost modal asks BEFORE the player
/// confirms: is a hint available, how many are left, what does one cost.
///
/// The behaviour of RequestHint itself is pinned elsewhere and deliberately unchanged —
/// ChallengeTierAndEvidenceTests TierFive_EmergencyHintBudgetIsEnforcedPerAttempt,
/// AuthoredTierFive_WithDefaultFlags_GrantsTheEmergencyHintBudget and
/// UnsetTier_KeepsTheRawSerializedFlags all still pass untouched. This file covers only
/// the queries, and the fact that asking spends nothing.
/// </summary>
public class ChallengeHintPolicyQueryTests
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
    public void TierFive_FreshSession_ReportsOneHintAvailable()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();

        Assert.IsTrue(session.HintBudgetIsLimited, "Tier 5 meters hints.");
        Assert.IsTrue(session.CanRequestHint);
        Assert.AreEqual(1, session.EmergencyHintsRemaining);
        Assert.IsFalse(session.IsHintExhausted);
    }

    [Test]
    public void TierFive_AfterTheOneHint_ReportsExhausted()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();

        session.RequestHint();

        Assert.IsFalse(session.CanRequestHint,
            "The query must agree with the guard RequestHint applies, or the modal offers a "
            + "confirm button that silently does nothing.");
        Assert.IsTrue(session.IsHintExhausted);
        Assert.AreEqual(0, session.EmergencyHintsRemaining);
    }

    [Test]
    public void TierFive_CostFractionIsTheTenPercentTheCalculatorSubtracts()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();

        Assert.AreEqual(0.10f, session.EmergencyHintCostFraction, 0.0001f,
            "This is the number the modal discloses before use, and it must be the same "
            + "fraction LevelResultsCalculator subtracts from metric.score.");
        Assert.AreEqual(10, HintModalCopy.ScorePointsFromFraction(session.EmergencyHintCostFraction),
            "Displayed as points of the 0-100 score.");
    }

    // -------------------------------------------------------------------------
    // ⚠️ THE MOST IMPORTANT TEST IN THIS TICKET.
    //
    // ChallengeSession.ResolveEffectivePolicy DISCARDS every serialized sibling flag
    // whenever challengePolicy.tier is 1-5 and substitutes ChallengeTierPolicy.ForTier(tier).
    // All fifteen levels carry a tier, so the serialized flags are dead on all of them:
    // Level5_Config.asset reads `emergencyHintEnabled: 0` beside `tier: 5`, and the hint is
    // nonetheless LIVE there. A previous agent read that 0 off the asset and concluded the
    // hint was off on Level 5 — the exact wrong conclusion this test exists to catch.
    //
    // The policy below is shaped like the real authored asset: tier 5, every sibling flag
    // set to the "off" value. If anyone ever "fixes" the queries to read the serialized
    // flags instead of the effective policy, every assertion here flips — limited becomes
    // false, remaining becomes Unlimited, the cost becomes 0. That is the point.
    // -------------------------------------------------------------------------
    [Test]
    public void AuthoredTierFive_WithSerializedFlagsOff_StillReportsTheEffectiveBudget()
    {
        ChallengeTierPolicy authored = new ChallengeTierPolicy
        {
            tier = 5,
            emergencyHintEnabled = false,
            emergencyHintsPerAttempt = 0,
            emergencyHintScorePenalty = 0f,
        };
        ChallengeSession session = CreateSession(authored);
        session.Enter();

        Assert.IsTrue(session.HintBudgetIsLimited,
            "tier 5 selects the preset. Reading the serialized emergencyHintEnabled = false "
            + "would disable the hint economy on the ONLY demo-reachable level that has one.");
        Assert.AreEqual(1, session.EmergencyHintsRemaining,
            "The preset's emergencyHintsPerAttempt = 1 wins over the serialized 0.");
        Assert.AreEqual(0.10f, session.EmergencyHintCostFraction, 0.0001f,
            "The preset's 0.10 wins over the serialized 0f. A modal reading the serialized "
            + "value would tell the player the hint is free and then charge them.");
        Assert.IsTrue(session.CanRequestHint);
        Assert.IsFalse(session.IsHintExhausted);
    }

    /// <summary>
    /// The control for the test above: below tier 5 the budget genuinely is unmetered, so
    /// a change that simply hard-coded "limited = true" would satisfy the trap test alone.
    /// </summary>
    [Test]
    public void TierThree_ReportsNoBudgetAndGivesUnlimitedFreeHints()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(3));
        session.Enter();

        Assert.IsFalse(session.HintBudgetIsLimited);
        Assert.AreEqual(0f, session.EmergencyHintCostFraction, 0.0001f);
        Assert.AreEqual(ChallengeSession.UnlimitedHints, session.EmergencyHintsRemaining);
        Assert.IsFalse(session.IsHintExhausted, "Nothing to exhaust when nothing is metered.");

        session.RequestHint();
        session.RequestHint();

        Assert.AreEqual(2, session.HintsUsed, "Tiers 1-4 give unlimited free hints.");
        Assert.AreEqual(0, session.EmergencyHintsUsed, "No emergency budget was ever charged.");
    }

    [Test]
    public void ReadingTheQueries_NeverConsumesAHint()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();
        ChallengeCluePolicy cluesBefore = session.CluePolicy;

        for (int i = 0; i < 5; i++)
        {
            _ = session.HintBudgetIsLimited;
            _ = session.EmergencyHintsRemaining;
            _ = session.EmergencyHintCostFraction;
            _ = session.IsHintExhausted;
            _ = session.CanRequestHint;
        }

        Assert.AreEqual(0, session.HintsUsed, "The modal reads these to build its disclosure.");
        Assert.AreEqual(0, session.EmergencyHintsUsed);
        Assert.AreEqual(0f, session.EmergencyHintScorePenalty, 0.0001f);
        Assert.AreEqual(cluesBefore, session.CluePolicy, "No clue may be degraded by a question.");
        Assert.IsNull(session.HintOccurrenceId);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    private ChallengeSession CreateSession(ChallengeTierPolicy policy)
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _objectsToDestroy.Add(sequence);
        sequence.sequenceId = "hint-query-tests";
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "unit-1",
                mode = ChallengeMode.WordPlacement,
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "slot-1", expectedOccurrenceId = "word-1" },
                    new ChallengeSlotDefinition { slotId = "slot-2", expectedOccurrenceId = "word-2" },
                },
                tokens = new[]
                {
                    new ChallengeTokenDefinition { tokenId = "w1", displayText = "w1", occurrenceId = "word-1" },
                    new ChallengeTokenDefinition { tokenId = "w2", displayText = "w2", occurrenceId = "word-2" },
                },
                maxErrors = 3,
                heartPenalty = 1,
                evidenceContentId = "level.ugat.05.focus.01",
            },
        };
        return new ChallengeSession(sequence, startingHearts: 3, policy: policy);
    }
}
