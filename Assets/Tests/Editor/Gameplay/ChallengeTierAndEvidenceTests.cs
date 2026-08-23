using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SALIN-181: tier-policy overlay and learning-evidence recording for
/// ChallengeSession. Legacy behavior (null policy, empty evidence ids) is pinned
/// by the pre-existing ChallengeSessionTests and must stay untouched.
/// </summary>
public class ChallengeTierAndEvidenceTests
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

    // -------------------------------------------------------------------------
    // Tier presets
    // -------------------------------------------------------------------------

    [TestCase(1)]
    [TestCase(2)]
    public void ForTier_LowTiers_DisableHeartPenaltiesAndEmergencyHints(int tier)
    {
        ChallengeTierPolicy policy = ChallengeTierPolicy.ForTier(tier);

        Assert.AreEqual(tier, policy.tier);
        Assert.IsFalse(policy.heartPenaltiesEnabled,
            "Tiers 1-2 provide supportive retries without a heart penalty.");
        Assert.IsFalse(policy.emergencyHintEnabled);
    }

    [TestCase(3)]
    [TestCase(4)]
    public void ForTier_MidTiers_EnablePenaltiesEveryThreeErrors(int tier)
    {
        ChallengeTierPolicy policy = ChallengeTierPolicy.ForTier(tier);

        Assert.IsTrue(policy.heartPenaltiesEnabled);
        Assert.AreEqual(3, policy.errorsPerPenalty);
        Assert.IsTrue(policy.checkpointResetOnPenalty);
        Assert.IsFalse(policy.emergencyHintEnabled);
    }

    [Test]
    public void ForTier_TierFive_GrantsOneEmergencyHintWithTenPercentPenalty()
    {
        ChallengeTierPolicy policy = ChallengeTierPolicy.ForTier(5);

        Assert.IsTrue(policy.heartPenaltiesEnabled);
        Assert.AreEqual(3, policy.errorsPerPenalty);
        Assert.IsTrue(policy.emergencyHintEnabled);
        Assert.AreEqual(1, policy.emergencyHintsPerAttempt);
        Assert.AreEqual(0.10f, policy.emergencyHintScorePenalty, 0.0001f);
    }

    // -------------------------------------------------------------------------
    // Tier behavior
    // -------------------------------------------------------------------------

    [Test]
    public void TierTwo_ErrorsNeverSpendHeartsOrResetProgress()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(2));
        session.Enter();
        session.SubmitPlacement("slot-1", "word-1");

        for (int i = 0; i < 5; i++)
            session.SubmitPlacement("slot-2", "wrong");

        Assert.AreEqual(0, session.HeartPenalties);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
        Assert.AreEqual(1, session.CurrentSlotIndex,
            "Supportive retries must not reset committed slot progress.");
    }

    [Test]
    public void TierFour_ThreeErrorsSpendOneHeartAndResetOnlyTheCheckpoint()
    {
        // unit.maxErrors is deliberately huge to prove the policy overrides unit data.
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(4), unitMaxErrors: 99);
        session.Enter();

        session.SubmitPlacement("slot-1", "wrong");
        session.SubmitPlacement("slot-1", "wrong");
        Assert.AreEqual(0, session.HeartPenalties, "Two errors stay supportive in tier 4.");

        session.SubmitPlacement("slot-1", "wrong");

        Assert.AreEqual(1, session.HeartPenalties,
            "Three incorrect placements cost exactly one heart.");
        Assert.AreEqual(0, session.Errors, "The checkpoint reset clears the error count.");
        Assert.AreEqual(0, session.CurrentSlotIndex);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    [Test]
    public void TierFive_EmergencyHintBudgetIsEnforcedPerAttempt()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();

        session.RequestHint();
        Assert.AreEqual(1, session.EmergencyHintsUsed);
        Assert.AreEqual(1, session.HintsUsed);

        session.RequestHint();

        Assert.AreEqual(1, session.EmergencyHintsUsed,
            "Tier 5 permits one emergency hint per level attempt.");
        Assert.AreEqual(1, session.HintsUsed, "The second request must be rejected outright.");
    }

    [Test]
    public void TierFive_EmergencyHintScorePenaltyAccumulatesFromPolicyFraction()
    {
        ChallengeSession session = CreateSession(ChallengeTierPolicy.ForTier(5));
        session.Enter();

        Assert.AreEqual(0f, session.EmergencyHintScorePenalty, 0.0001f);
        session.RequestHint();
        Assert.AreEqual(0.10f, session.EmergencyHintScorePenalty, 0.0001f,
            "Results (SALIN-202) consumes this recorded stat.");
    }

    [Test]
    public void TierFour_TimerExpiry_AppliesCheckpointPenaltyWithoutTerminalState()
    {
        ChallengeSession session = CreateTimedSession(ChallengeTierPolicy.ForTier(4));
        session.Enter();

        // Complete the first (untimed) unit so there is committed progress to protect.
        session.SubmitPlacement("slot-1", "word-1");
        session.SubmitPlacement("slot-2", "word-2");
        Assert.AreEqual(1, session.CurrentUnitIndex, "Setup: second unit must be open.");

        session.Tick(5f);

        Assert.AreEqual(1, session.HeartPenalties,
            "Timer expiry applies the configured checkpoint penalty.");
        Assert.AreEqual(ChallengeSessionState.Active, session.State,
            "Expiry must neither complete nor skip nor fail the unit outright.");
        Assert.AreEqual(1, session.CurrentUnitIndex, "The unit is retried, not skipped.");
        CollectionAssert.AreEquivalent(new[] { "word-1", "word-2" }, session.CommittedOccurrenceIds);
    }

    [Test]
    public void TierTwo_TimerExpiry_ResetsCheckpointWithoutHearts()
    {
        ChallengeSession session = CreateTimedSession(ChallengeTierPolicy.ForTier(2));
        session.Enter();
        session.SubmitPlacement("slot-1", "word-1");
        session.SubmitPlacement("slot-2", "word-2");

        session.Tick(5f);

        Assert.AreEqual(0, session.HeartPenalties);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    // -------------------------------------------------------------------------
    // Authored (production-shaped) policies: only the tier is set, every flag is
    // left at its serialized default, exactly as LevelConfigSO.challengePolicy
    // reaches the session through LevelFlowController.
    // -------------------------------------------------------------------------

    [TestCase(1)]
    [TestCase(2)]
    public void AuthoredLowTier_WithDefaultFlags_NeverSpendsHearts(int tier)
    {
        ChallengeSession session = CreateSession(new ChallengeTierPolicy { tier = tier });
        session.Enter();

        for (int i = 0; i < 5; i++)
            session.SubmitPlacement("slot-1", "wrong");

        Assert.AreEqual(0, session.HeartPenalties,
            "The authored tier must select the preset; the default heartPenaltiesEnabled = true is not authoring intent.");
        Assert.AreEqual(3, session.HeartsRemaining);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    [Test]
    public void AuthoredTierFive_WithDefaultFlags_GrantsTheEmergencyHintBudget()
    {
        ChallengeSession session = CreateSession(new ChallengeTierPolicy { tier = 5 });
        session.Enter();

        session.RequestHint();

        Assert.AreEqual(1, session.EmergencyHintsUsed,
            "The authored tier must select the preset; the default emergencyHintEnabled = false is not authoring intent.");
        Assert.AreEqual(0.10f, session.EmergencyHintScorePenalty, 0.0001f);

        session.RequestHint();

        Assert.AreEqual(1, session.EmergencyHintsUsed,
            "Tier 5 still permits only one emergency hint per level attempt.");
    }

    [Test]
    public void AuthoredTierFour_WithDefaultFlags_SpendsOneHeartEveryThreeErrors()
    {
        ChallengeSession session = CreateSession(new ChallengeTierPolicy { tier = 4 }, unitMaxErrors: 99);
        session.Enter();

        session.SubmitPlacement("slot-1", "wrong");
        session.SubmitPlacement("slot-1", "wrong");
        Assert.AreEqual(0, session.HeartPenalties);

        session.SubmitPlacement("slot-1", "wrong");

        Assert.AreEqual(1, session.HeartPenalties,
            "The tier-4 preset overrides the unit's own maxErrors.");
    }

    [Test]
    public void UnsetTier_KeepsTheRawSerializedFlags()
    {
        ChallengeTierPolicy authored = new ChallengeTierPolicy
        {
            tier = 0,
            heartPenaltiesEnabled = false,
            emergencyHintEnabled = true,
            emergencyHintsPerAttempt = 2,
        };
        ChallengeSession session = CreateSession(authored);
        session.Enter();

        for (int i = 0; i < 5; i++)
            session.SubmitPlacement("slot-1", "wrong");
        Assert.AreEqual(0, session.HeartPenalties,
            "Tier 0 is outside the preset range: the authored flags stand unchanged.");

        session.RequestHint();
        session.RequestHint();
        session.RequestHint();

        Assert.AreEqual(2, session.EmergencyHintsUsed,
            "Tier 0 keeps the authored hint budget instead of snapping to a preset.");
    }

    [Test]
    public void UnsetTierWithoutCheckpointReset_TimerExpiry_RestocksInsteadOfRepeating()
    {
        // The zero-initialized shape a legacy asset deserializes into: no tier and
        // checkpointResetOnPenalty left false.
        ChallengeSession session = CreateTimedSession(new ChallengeTierPolicy
        {
            tier = 0,
            checkpointResetOnPenalty = false,
        });
        session.Enter();
        session.SubmitPlacement("slot-1", "word-1");
        session.SubmitPlacement("slot-2", "word-2");
        Assert.AreEqual(1, session.CurrentUnitIndex, "Setup: the timed unit must be open.");

        int timeouts = 0;
        session.Changed += changed =>
        {
            if (changed.LastEvent == ChallengeSessionEvent.TimedOut)
                timeouts++;
        };

        session.Tick(5f);

        Assert.AreEqual(1, timeouts);
        Assert.AreEqual(1, session.HeartPenalties);
        Assert.AreEqual(2f, session.RemainingTime, 0.0001f,
            "Reopening in place must restock the unit's configured time.");

        for (int i = 0; i < 30; i++)
            session.Tick(0.016f);

        Assert.AreEqual(1, timeouts, "The expired clock must not re-fire on every tick.");
        Assert.AreEqual(1, session.HeartPenalties, "One expiry costs exactly one heart.");
        Assert.AreEqual(3 - 1, session.HeartsRemaining);
        Assert.AreEqual(ChallengeSessionState.Active, session.State);
    }

    [Test]
    public void UnsetTierWithoutCheckpointReset_Penalty_ClearsErrorsAndKeepsSlotProgress()
    {
        ChallengeSession session = CreateSession(new ChallengeTierPolicy
        {
            tier = 0,
            checkpointResetOnPenalty = false,
        });
        session.Enter();
        session.SubmitPlacement("slot-1", "word-1");

        for (int i = 0; i < 3; i++)
            session.SubmitPlacement("slot-2", "wrong");

        Assert.AreEqual(1, session.HeartPenalties);
        Assert.AreEqual(0, session.Errors, "Reopening in place must clear the spent error budget.");
        Assert.AreEqual(1, session.CurrentSlotIndex, "Reopening in place keeps slot progress.");

        session.SubmitPlacement("slot-2", "wrong");

        Assert.AreEqual(1, session.HeartPenalties,
            "The error budget restarts after a penalty instead of penalizing every further error.");
    }

    // -------------------------------------------------------------------------
    // Evidence recording
    // -------------------------------------------------------------------------

    [Test]
    public void Trace_RecordsFormEvidenceForTheTokenSymbol()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateTracingSession(sink);
        session.Enter();

        session.SubmitTrace("BA");

        Assert.AreEqual(1, sink.Records.Count);
        AssertRecord(sink.Records[0], "symbol.ba", LearningContentKind.Symbol,
            MasteryDimension.Form, success: true, answerWasVisible: false);
    }

    [Test]
    public void Trace_Error_RecordsFailedFormEvidence()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateTracingSession(sink);
        session.Enter();

        session.SubmitTrace("WRONG");

        Assert.AreEqual(1, sink.Records.Count);
        AssertRecord(sink.Records[0], "symbol.ba", LearningContentKind.Symbol,
            MasteryDimension.Form, success: false, answerWasVisible: false);
    }

    [Test]
    public void Placement_RecordsAssemblyEvidenceForTheUnitWord()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateSession(policy: null, sink: sink,
            mode: ChallengeMode.WordPlacement, unitEvidenceId: "word.ina");
        session.Enter();

        session.SubmitPlacement("slot-1", "word-1");

        Assert.AreEqual(1, sink.Records.Count);
        AssertRecord(sink.Records[0], "word.ina", LearningContentKind.Word,
            MasteryDimension.Assembly, success: true, answerWasVisible: false);
    }

    [Test]
    public void SentenceRestoration_RecordsMeaningEvidence()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateSession(policy: null, sink: sink,
            mode: ChallengeMode.SentenceRestoration, unitEvidenceId: "word.ina");
        session.Enter();

        session.SubmitRestoration(new[] { "word-1", "word-2" });

        Assert.AreEqual(1, sink.Records.Count);
        AssertRecord(sink.Records[0], "word.ina", LearningContentKind.Word,
            MasteryDimension.Meaning, success: true, answerWasVisible: false);
    }

    [Test]
    public void PlacementAfterHint_MarksTheAnswerAsVisible()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateSession(policy: null, sink: sink,
            mode: ChallengeMode.WordPlacement, unitEvidenceId: "word.ina");
        session.Enter();

        session.RequestHint();
        session.SubmitPlacement("slot-1", "word-1");

        Assert.AreEqual(1, sink.Records.Count);
        Assert.IsTrue(sink.Records[0].AnswerWasVisible,
            "A hinted answer is immediate retrieval, not recall.");
    }

    [Test]
    public void EmptyEvidenceIds_RecordNothing()
    {
        RecordingSink sink = new RecordingSink();
        ChallengeSession session = CreateSession(policy: null, sink: sink,
            mode: ChallengeMode.WordPlacement, unitEvidenceId: "");
        session.Enter();

        session.SubmitPlacement("slot-1", "word-1");
        session.SubmitPlacement("slot-2", "wrong");

        Assert.AreEqual(0, sink.Records.Count,
            "Legacy sequences without authored evidence ids must record nothing.");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void AssertRecord(
        EvidenceRecord record,
        string contentId,
        LearningContentKind kind,
        MasteryDimension dimension,
        bool success,
        bool answerWasVisible)
    {
        Assert.AreEqual(contentId, record.ContentId);
        Assert.AreEqual(kind, record.ContentKind);
        Assert.AreEqual(dimension, record.Dimension);
        Assert.AreEqual(success, record.Success);
        Assert.AreEqual(answerWasVisible, record.AnswerWasVisible);
    }

    private ChallengeSession CreateSession(
        ChallengeTierPolicy policy,
        int unitMaxErrors = 3,
        IChallengeEvidenceSink sink = null,
        ChallengeMode mode = ChallengeMode.WordPlacement,
        string unitEvidenceId = "")
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _objectsToDestroy.Add(sequence);
        sequence.sequenceId = "tier-tests";
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "unit-1",
                mode = mode,
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
                maxErrors = unitMaxErrors,
                heartPenalty = 1,
                evidenceContentId = unitEvidenceId,
            },
        };
        return new ChallengeSession(sequence, startingHearts: 3, policy: policy, evidence: sink);
    }

    private ChallengeSession CreateTimedSession(ChallengeTierPolicy policy)
    {
        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _objectsToDestroy.Add(sequence);
        sequence.sequenceId = "timed-tests";
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "warmup",
                mode = ChallengeMode.WordPlacement,
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "slot-1", expectedOccurrenceId = "word-1" },
                    new ChallengeSlotDefinition { slotId = "slot-2", expectedOccurrenceId = "word-2" },
                },
                maxErrors = 3,
                heartPenalty = 1,
            },
            new ChallengeUnitDefinition
            {
                unitId = "timed-sentence",
                mode = ChallengeMode.SentenceRestoration,
                slots = new[]
                {
                    new ChallengeSlotDefinition { slotId = "slot-3", expectedOccurrenceId = "word-3" },
                },
                timerSeconds = 2f,
                maxErrors = 3,
                heartPenalty = 1,
            },
        };
        return new ChallengeSession(sequence, startingHearts: 3, policy: policy);
    }

    private ChallengeSession CreateTracingSession(IChallengeEvidenceSink sink)
    {
        BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        character.characterID = "BA";
        _objectsToDestroy.Add(character);

        ChallengeSequenceSO sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _objectsToDestroy.Add(sequence);
        sequence.sequenceId = "trace-tests";
        sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "trace-ba",
                mode = ChallengeMode.GuidedTracing,
                tokens = new[]
                {
                    new ChallengeTokenDefinition
                    {
                        tokenId = "ba",
                        displayText = "ba",
                        occurrenceId = "ba-1",
                        targetCharacter = character,
                        evidenceContentId = "symbol.ba",
                    },
                },
                maxErrors = 3,
                heartPenalty = 1,
            },
        };
        return new ChallengeSession(sequence, startingHearts: 3, policy: null, evidence: sink);
    }

    private readonly struct EvidenceRecord
    {
        public EvidenceRecord(string contentId, LearningContentKind kind, MasteryDimension dimension, bool success, bool answerWasVisible)
        {
            ContentId = contentId;
            ContentKind = kind;
            Dimension = dimension;
            Success = success;
            AnswerWasVisible = answerWasVisible;
        }

        public string ContentId { get; }
        public LearningContentKind ContentKind { get; }
        public MasteryDimension Dimension { get; }
        public bool Success { get; }
        public bool AnswerWasVisible { get; }
    }

    private sealed class RecordingSink : IChallengeEvidenceSink
    {
        public readonly List<EvidenceRecord> Records = new List<EvidenceRecord>();

        public void RecordAttempt(string contentId, LearningContentKind contentKind, MasteryDimension dimension, bool success, bool answerWasVisible)
        {
            Records.Add(new EvidenceRecord(contentId, contentKind, dimension, success, answerWasVisible));
        }
    }
}
