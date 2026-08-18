using System;
using System.Collections.Generic;

/// <summary>
/// Session-scoped accumulator. It folds per-attempt answer visibility into the persisted count
/// shape and owns no persistence or scene lifetime.
/// </summary>
public sealed class LearningEvidenceRecorder
{
    private readonly string _levelId;
    private readonly LearningSessionKind _sessionKind;
    private readonly HashSet<string> _instructedContentIds =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, LearningEvidenceEntry> _entries =
        new Dictionary<string, LearningEvidenceEntry>(StringComparer.Ordinal);

    public LearningEvidenceRecorder(string levelId, LearningSessionKind sessionKind)
    {
        _levelId = levelId;
        _sessionKind = sessionKind;
    }

    public void RecordInstruction(string contentId, LearningContentKind contentKind)
    {
        if (!string.IsNullOrEmpty(contentId))
            _instructedContentIds.Add(contentId);
    }

    public void RecordAttempt(
        string contentId,
        LearningContentKind contentKind,
        MasteryDimension dimension,
        bool success,
        bool answerWasVisible)
    {
        if (string.IsNullOrEmpty(contentId))
            return;

        string key = contentId + "|" + dimension;
        if (!_entries.TryGetValue(key, out LearningEvidenceEntry entry))
        {
            entry = new LearningEvidenceEntry
            {
                contentId = contentId,
                contentKind = contentKind,
                dimension = dimension,
            };
            _entries.Add(key, entry);
        }

        entry.attemptCount++;
        if (!success)
            return;

        entry.successCount++;
        if (!answerWasVisible)
            entry.retrievalSuccessCount++;
    }

    public LearningEvidenceBatch Build()
    {
        var entries = new List<LearningEvidenceEntry>(_entries.Values);
        entries.Sort((left, right) =>
        {
            int content = string.CompareOrdinal(left.contentId, right.contentId);
            return content != 0 ? content : left.dimension.CompareTo(right.dimension);
        });

        var instructed = new List<string>(_instructedContentIds);
        instructed.Sort(StringComparer.Ordinal);
        return new LearningEvidenceBatch
        {
            levelId = _levelId,
            sessionKind = _sessionKind,
            instructedContentIds = instructed,
            entries = entries,
        };
    }

    public void Reset()
    {
        _instructedContentIds.Clear();
        _entries.Clear();
    }
}
