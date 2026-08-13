using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ChallengeSessionTests
{
    private ChallengeSequenceSO _sequence;
    private BaybayinCharacterSO _baCharacter;
    private Level1TutorialStepSO _baStep;

    [SetUp]
    public void SetUp()
    {
        _baCharacter = Character("BA");
        _baStep = ScriptableObject.CreateInstance<Level1TutorialStepSO>();
        _baStep.targetCharacter = _baCharacter;
        _sequence = ScriptableObject.CreateInstance<ChallengeSequenceSO>();
        _sequence.sequenceId = "prototype";
        _sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "trace-ba",
                mode = ChallengeMode.GuidedTracing,
                guidedStep = _baStep,
                prompt = "Trace BA",
                tokens = new[] { Token("ba-token-1", "ba-1", _baCharacter) },
                maxErrors = 3,
                heartPenalty = 1
            },
            new ChallengeUnitDefinition
            {
                unitId = "restore-sentence",
                mode = ChallengeMode.SentenceRestoration,
                prompt = "Restore the sentence",
                slots = new[] { Slot("slot-1", "word-1"), Slot("slot-2", "word-2") },
                tokens = new[] { Token("word 1", "word-1"), Token("word 2", "word-2") },
                candidateOccurrenceIds = new[] { "word-2", "word-1" },
                maxErrors = 3,
                heartPenalty = 1
            }
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_sequence);
        Object.DestroyImmediate(_baCharacter);
        Object.DestroyImmediate(_baStep);
    }

    [Test]
    public void EnterSuccessAdvancesAndCompletesSequence()
    {
        ChallengeSession session = new ChallengeSession(_sequence);

        session.Enter();
        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        session.SubmitTrace("BA");
        Assert.That(session.CurrentUnitIndex, Is.EqualTo(1));
        session.SubmitRestoration(new[] { "word-1", "word-2" });

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Completed));
        Assert.That(session.CommittedOccurrenceIds, Is.EquivalentTo(new[] { "ba-1", "word-1", "word-2" }));
    }

    [Test]
    public void PlacementSubmissionAdvancesSentenceSlots()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.SubmitTrace("BA");

        session.SubmitPlacement("slot-1", "word-1");
        Assert.That(session.CurrentSlotIndex, Is.EqualTo(1));
        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));

        session.SubmitPlacement("slot-2", "word-2");

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Completed));
        Assert.That(session.CommittedOccurrenceIds, Is.EquivalentTo(new[] { "ba-1", "word-1", "word-2" }));
    }

    [Test]
    public void FirstAndSecondErrorsAreSupportiveRetries()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();

        session.SubmitTrace("wrong");
        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        Assert.That(session.Errors, Is.EqualTo(1));
        session.SubmitTrace("wrong-again");
        Assert.That(session.Errors, Is.EqualTo(2));
        Assert.That(session.HeartPenalties, Is.Zero);
    }

    [Test]
    public void ThirdErrorSpendsHeartAndResetsToCheckpoint()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.SubmitTrace("x");
        session.SubmitTrace("y");
        session.SubmitTrace("z");

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        Assert.That(session.HeartPenalties, Is.EqualTo(1));
        Assert.That(session.Errors, Is.Zero);
        Assert.That(session.CurrentSlotIndex, Is.Zero);
    }

    [Test]
    public void ZeroHeartsFailsAfterPenaltyWithoutResettingProgress()
    {
        ChallengeSession session = new ChallengeSession(_sequence, startingHearts: 1);
        session.Enter();

        session.SubmitTrace("x");
        session.SubmitTrace("y");
        session.SubmitTrace("z");

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Failed));
        Assert.That(session.HeartsRemaining, Is.Zero);
        Assert.That(session.HeartPenalties, Is.EqualTo(1));
        Assert.That(session.CurrentSlotIndex, Is.Zero);
        Assert.That(session.CommittedOccurrenceIds, Is.Empty);
    }

    [Test]
    public void SessionEventsExposeRetryPenaltyAndCheckpointTransitions()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        List<ChallengeSessionEvent> events = new List<ChallengeSessionEvent>();
        session.Changed += changed => events.Add(changed.LastEvent);
        session.Enter();
        events.Clear();

        session.SubmitTrace("wrong");
        Assert.That(events, Has.Some.EqualTo(ChallengeSessionEvent.SupportiveRetry));

        events.Clear();
        session.SubmitTrace("wrong");
        session.SubmitTrace("wrong-again");

        Assert.That(events, Has.Some.EqualTo(ChallengeSessionEvent.PenaltyApplied));
        Assert.That(events, Has.Some.EqualTo(ChallengeSessionEvent.CheckpointReset));
    }

    [Test]
    public void HintChangesClueWithoutCountingAsError()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();

        session.RequestHint();

        Assert.That(session.HintsUsed, Is.EqualTo(1));
        Assert.That(session.Errors, Is.Zero);
        Assert.That(session.CluePolicy, Is.EqualTo(ChallengeCluePolicy.Reduced));
        Assert.That(session.HintOccurrenceId, Is.EqualTo("ba-1"));
    }

    [Test]
    public void PauseResumePreservesProgressAndTimer()
    {
        _sequence.units[0].timerSeconds = 10f;
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.Tick(2f);
        session.Pause();
        session.SubmitTrace("ignored");
        session.Resume();

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        Assert.That(session.RemainingTime, Is.EqualTo(8f).Within(0.01f));
        Assert.That(session.Errors, Is.Zero);
    }

    [Test]
    public void RetryWhilePausedDoesNotResumeSession()
    {
        _sequence.units[0].timerSeconds = 10f;
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.Tick(2f);
        session.Pause();
        session.Retry();

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Paused));
        Assert.That(session.RemainingTime, Is.EqualTo(8f).Within(0.01f));
    }

    [Test]
    public void TimerExpiryPenalizesAndRestoresCheckpoint()
    {
        _sequence.units[0].timerSeconds = 1f;
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.Tick(1.1f);

        Assert.That(session.HeartPenalties, Is.EqualTo(1));
        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        Assert.That(session.RemainingTime, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void TimedMemoryRequiresRecallAfterRevealBeforeAcceptingPlacement()
    {
        _sequence.units = new[]
        {
            new ChallengeUnitDefinition
            {
                unitId = "timed-memory",
                mode = ChallengeMode.TimedMemory,
                prompt = "Remember the order",
                memoryRevealSeconds = 1f,
                timerSeconds = 5f,
                tokens = new[]
                {
                    Token("memory-one", "memory-one"),
                    Token("memory-two", "memory-two")
                },
                slots = new[]
                {
                    Slot("memory-slot-1", "memory-one"),
                    Slot("memory-slot-2", "memory-two")
                },
                candidateOccurrenceIds = new[] { "memory-two", "memory-one" }
            }
        };

        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();

        Assert.That(session.IsMemoryRevealActive, Is.True);
        session.SubmitPlacement("memory-slot-1", "memory-one");
        Assert.That(session.CurrentSlotIndex, Is.Zero);

        session.Tick(1f);

        Assert.That(session.IsMemoryRevealActive, Is.False);
        session.SubmitPlacement("memory-slot-1", "memory-one");
        session.SubmitPlacement("memory-slot-2", "memory-two");

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Completed));
    }

    [Test]
    public void ExitDiscardsUncommittedProgress()
    {
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.SubmitTrace("wrong");
        session.Exit();

        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Exited));
        Assert.That(session.CommittedOccurrenceIds, Is.Empty);
        Assert.That(session.Errors, Is.Zero);
    }

    [Test]
    public void RepeatedSyllablesRemainDistinctByOccurrenceId()
    {
        _sequence.units[0].tokens = new[]
        {
            Token("ba-token-1", "ba-occurrence-1", _baCharacter),
            Token("ba-token-2", "ba-occurrence-2", _baCharacter)
        };
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.SubmitTrace("BA");
        session.SubmitTrace("BA");

        Assert.That(session.CommittedOccurrenceIds, Is.EquivalentTo(new[] { "ba-occurrence-1", "ba-occurrence-2" }));
    }

    [Test]
    public void ThreeSyllableRestorationPreservesTokenOrder()
    {
        _sequence.units[0] = new ChallengeUnitDefinition
        {
            unitId = "three-syllables",
            mode = ChallengeMode.SentenceRestoration,
            slots = new[] { Slot("one", "one"), Slot("two", "two"), Slot("three", "three") },
            tokens = new[] { Token("one", "one"), Token("two", "two"), Token("three", "three") }
        };
        ChallengeSession session = new ChallengeSession(_sequence);
        session.Enter();
        session.SubmitRestoration(new[] { "three", "two", "one" });
        Assert.That(session.State, Is.EqualTo(ChallengeSessionState.Active));
        session.SubmitRestoration(new[] { "one", "two", "three" });
        Assert.That(session.CurrentUnitIndex, Is.EqualTo(1));
    }

    [Test]
    public void FocusAndMasteryRolesShareTheSameTokenStructure()
    {
        _sequence.units[0].tokens = new[]
        {
            new ChallengeTokenDefinition { tokenId = "focus-a", displayText = "focus", occurrenceId = "focus-a", role = ChallengeTokenRole.Focus, targetCharacter = _baCharacter },
            new ChallengeTokenDefinition { tokenId = "mastery-a", displayText = "mastery", occurrenceId = "mastery-a", role = ChallengeTokenRole.Mastery, targetCharacter = _baCharacter }
        };
        ChallengeValidationResult result = ChallengeSequenceValidator.Validate(_sequence);
        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
    }

    private static ChallengeTokenDefinition Token(string text, string occurrenceId, BaybayinCharacterSO targetCharacter = null)
    {
        return new ChallengeTokenDefinition { tokenId = text, displayText = text, occurrenceId = occurrenceId, targetCharacter = targetCharacter };
    }

    private static BaybayinCharacterSO Character(string characterId)
    {
        BaybayinCharacterSO character = ScriptableObject.CreateInstance<BaybayinCharacterSO>();
        character.characterID = characterId;
        return character;
    }

    private static ChallengeSlotDefinition Slot(string slotId, string expectedOccurrenceId)
    {
        return new ChallengeSlotDefinition { slotId = slotId, expectedOccurrenceId = expectedOccurrenceId };
    }
}
