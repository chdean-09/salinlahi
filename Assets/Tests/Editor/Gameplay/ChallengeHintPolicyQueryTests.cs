using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;

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

/// <summary>Hint meanings follow each authored blank, including words recalled from earlier levels.</summary>
public class ChallengeHintContentTests
{
    private GameObject _host;
    private ChallengeFlowController _controller;
    private CampaignConfigSO _campaign;

    [SetUp]
    public void SetUp()
    {
        _campaign = AssetDatabase.LoadAssetAtPath<CampaignConfigSO>(
            "Assets/ScriptableObjects/Campaign/CampaignConfig_RevisedV1.asset");
        Assert.IsNotNull(_campaign);
        _host = new GameObject("HintContentTestHost");
        _controller = _host.AddComponent<ChallengeFlowController>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_host);

    [TestCase(5, 1, "INA,AMA")]
    [TestCase(5, 2, "TAMA")]
    [TestCase(1, 0, "INA")]
    [TestCase(1, 1, "AMA")]
    [TestCase(2, 0, "BATA")]
    [TestCase(2, 1, "MATA")]
    [TestCase(3, 0, "BATA,TAMA")]
    [TestCase(4, 0, "INA,AMA")]
    [TestCase(5, 0, "IBA,MANA")]
    [TestCase(6, 0, "AWA")]
    [TestCase(6, 1, "GAWA")]
    [TestCase(7, 0, "SAMA")]
    [TestCase(7, 1, "KASAMA")]
    [TestCase(8, 0, "GANA")]
    [TestCase(8, 1, "KAYA")]
    [TestCase(9, 0, "OO")]
    [TestCase(9, 1, "UNA")]
    [TestCase(10, 0, "SANA")]
    [TestCase(10, 1, "SAYA")]
    [TestCase(10, 2, "SANA")]
    [TestCase(11, 0, "DALA")]
    [TestCase(11, 1, "DAMA")]
    [TestCase(12, 0, "HANGA")]
    [TestCase(12, 1, "HALAGA")]
    [TestCase(13, 0, "SANGA")]
    [TestCase(13, 1, "HARAYA")]
    [TestCase(14, 0, "ALAALA,MAHALAGA")]
    [TestCase(15, 0, "PAMANA")]
    [TestCase(15, 1, "PAMANA")]
    [TestCase(15, 2, "MALAYA")]
    public void AuthoredChallenge_EachBlankHasTheIntendedHint(int levelNumber, int unitIndex, string expectedWords)
    {
        LevelConfigSO level = FindLevel(levelNumber);
        _controller.SetLevelHintWords(level, _campaign);
        ChallengeUnitDefinition unit = level.challengeSequence.units[unitIndex];
        string[] expected = expectedWords.Split(',');
        Assert.AreEqual(expected.Length, unit.slots.Length);
        for (int slot = 0; slot < expected.Length; slot++)
        {
            FocusWordDefinition word = _controller.ResolveHintWord(unit, unit.slots[slot].expectedOccurrenceId);
            Assert.IsNotNull(word, $"Level {levelNumber}, unit {unitIndex}, slot {slot}");
            Assert.AreEqual(expected[slot], word.latinSpelling);
            Assert.IsFalse(string.IsNullOrWhiteSpace(word.meaning));
        }
    }

    [Test]
    public void AllShippedHintEnabledSlots_HaveAuthoredSynonyms()
    {
        List<LevelConfigSO> levels = EnemyDebutLookup.FlattenInCampaignOrder(_campaign);
        Assert.AreEqual(15, levels.Count);
        foreach (LevelConfigSO level in levels)
        {
            Assert.IsNotNull(level.challengeSequence, level.name);
            _controller.SetLevelHintWords(level, _campaign);
            foreach (ChallengeUnitDefinition unit in level.challengeSequence.units)
            {
                if (!unit.allowHint)
                    continue;
                foreach (ChallengeSlotDefinition slot in unit.slots)
                {
                    FocusWordDefinition word = _controller.ResolveHintWord(unit, slot.expectedOccurrenceId);
                    Assert.IsNotNull(word, $"{level.name}/{unit.unitId}/{slot.slotId}");
                    string hint = _controller.ResolveHintText(unit, slot.expectedOccurrenceId);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(hint));
                    Assert.AreEqual(word.hintSynonyms, hint);
                    StringAssert.DoesNotContain("______", hint);
                    StringAssert.DoesNotMatch(@"\b" + System.Text.RegularExpressions.Regex.Escape(word.latinSpelling)
                        + @"\b", hint.ToUpperInvariant());
                }
            }
        }
    }

    [Test]
    public void PurchasedHint_RemainsForItsOriginalBlankAfterProgress()
    {
        LevelConfigSO level = FindLevel(3);
        _controller.SetLevelHintWords(level, _campaign);
        var session = new ChallengeSession(level.challengeSequence, 3, ChallengeTierPolicy.ForTier(3));
        session.Enter();
        session.RequestHint();
        string purchasedOccurrence = session.HintOccurrenceId;
        ChallengeUnitDefinition unit = session.CurrentUnitDefinition;
        session.SubmitPlacement(unit.slots[0].slotId, unit.slots[0].expectedOccurrenceId);
        Assert.AreEqual(1, session.CurrentSlotIndex);
        Assert.AreEqual("BATA", _controller.ResolveHintWord(unit, purchasedOccurrence).latinSpelling);
        Assert.AreEqual("TAMA", _controller.ResolveHintWord(unit, unit.slots[1].expectedOccurrenceId).latinSpelling);
    }

    [TestCase(ChallengeMode.GuidedTracing)]
    [TestCase(ChallengeMode.WordPlacement)]
    public void UnknownToken_DoesNotFallBackToUnrelatedEvidenceWord(ChallengeMode mode)
    {
        _controller.SetLevelHintWords(FindLevel(5), _campaign);
        var unit = new ChallengeUnitDefinition
        {
            mode = mode,
            evidenceContentId = "level.ugat.05.focus.02",
            tokens = new[] { new ChallengeTokenDefinition { occurrenceId = "unknown", displayText = "UNKNOWN" } },
        };
        Assert.IsNull(_controller.ResolveHintWord(unit, "unknown"));
        Assert.IsNull(_controller.ResolveHintWord(unit, "missing"));
    }

    [Test]
    public void SwitchingLevels_DoesNotKeepWordsFromLaterLessons()
    {
        _controller.SetLevelHintWords(FindLevel(14), _campaign);
        _controller.SetLevelHintWords(FindLevel(1), _campaign);
        var unit = new ChallengeUnitDefinition
        {
            tokens = new[] { new ChallengeTokenDefinition { occurrenceId = "later", displayText = "MAHALAGA" } },
        };
        Assert.IsNull(_controller.ResolveHintWord(unit, "later"));
    }

    private LevelConfigSO FindLevel(int number)
    {
        foreach (LevelConfigSO level in EnemyDebutLookup.FlattenInCampaignOrder(_campaign))
            if (level.levelNumber == number)
                return level;
        Assert.Fail($"Missing level {number}");
        return null;
    }
}
