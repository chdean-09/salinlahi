using System;
using System.Collections.Generic;

public enum MasteryDimension { Form, Sound, Assembly, Meaning }

public enum MasteryState { None = 0, Introduced, Practiced, Recalled, Mastered }

public enum LearningContentKind { Symbol, Word }

/// <summary>
/// Deliberately avoids the bare value Practice, because ContentRequirementKind.Practice already
/// exists on a different axis in FocusWordDefinition.cs.
/// </summary>
public enum LearningSessionKind { LevelAttempt, FreePractice, ScheduledReview }

[Serializable]
public sealed class DimensionEvidence
{
    public MasteryDimension dimension;
    public int immediateSuccesses;
    public int immediateAttempts;
    public int delayedSuccesses;
    public int delayedAttempts;
    public int delayedSessionCount;
    public MasteryState highWaterState;
    public string lastEvidenceLevelId;
}

[Serializable]
public sealed class SymbolMasteryRecord
{
    public string symbolId;
    public string introducedAtLevelId;
    public List<DimensionEvidence> dimensions = new List<DimensionEvidence>();
}

[Serializable]
public sealed class WordMasteryRecord
{
    public string wordId;
    public string sourceLevelId;
    public List<string> satisfiedReviewCheckpoints = new List<string>();
    public List<DimensionEvidence> dimensions = new List<DimensionEvidence>();
}

[Serializable]
public sealed class LearningEvidenceBatch
{
    public string levelId;
    public LearningSessionKind sessionKind;
    public List<string> instructedContentIds = new List<string>();
    public List<LearningEvidenceEntry> entries = new List<LearningEvidenceEntry>();
}

/// <summary>
/// A session summary for one (contentId, dimension) pair, not one attempt. A batch carries at most
/// one entry per pair. Invariant: 0 &lt;= retrievalSuccessCount &lt;= successCount &lt;= attemptCount.
/// </summary>
[Serializable]
public sealed class LearningEvidenceEntry
{
    public string contentId;
    public LearningContentKind contentKind;
    public MasteryDimension dimension;
    public int attemptCount;
    public int successCount;
    public int retrievalSuccessCount;
}
