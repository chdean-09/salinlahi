using System;
using System.Collections.Generic;

/// <summary>
/// Read-only view of campaign learning progress. Each query builds its own result collection and
/// never exposes mutable save records.
/// </summary>
public sealed class LearningStateSnapshot
{
    private readonly CampaignProgressData _progress;
    private readonly CampaignConfigSO _campaign;

    public LearningStateSnapshot(CampaignProgressData progress, CampaignConfigSO campaign)
    {
        _progress = progress ?? new CampaignProgressData();
        _campaign = campaign;
    }

    public IReadOnlyCollection<string> IntroducedSymbolIds
    {
        get
        {
            var result = new List<string>();
            if (_progress.symbolMastery == null)
                return result.AsReadOnly();

            for (int i = 0; i < _progress.symbolMastery.Count; i++)
            {
                SymbolMasteryRecord record = _progress.symbolMastery[i];
                if (record != null && !string.IsNullOrEmpty(record.symbolId))
                    result.Add(record.symbolId);
            }
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }
    }

    public MasteryState GetSymbolState(string symbolId)
    {
        SymbolMasteryRecord record = FindSymbol(symbolId);
        return record == null
            ? MasteryState.None
            : MasteryEvaluator.Aggregate(record.dimensions, LearningContentKind.Symbol);
    }

    public MasteryState GetSymbolDimensionState(string symbolId, MasteryDimension dimension)
    {
        return GetDimensionState(FindSymbol(symbolId)?.dimensions, dimension);
    }

    public MasteryState GetWordState(string wordId)
    {
        WordMasteryRecord record = FindWord(wordId);
        return record == null
            ? MasteryState.None
            : MasteryEvaluator.Aggregate(record.dimensions, LearningContentKind.Word);
    }

    public MasteryState GetWordDimensionState(string wordId, MasteryDimension dimension)
    {
        return GetDimensionState(FindWord(wordId)?.dimensions, dimension);
    }

    public IReadOnlyList<ReviewDueItem> GetRequiredReviewItems(string levelId)
    {
        var result = new List<ReviewDueItem>();
        if (!TryGetCurrentLevelIndex(levelId, out int currentIndex))
            return result.AsReadOnly();

        IReadOnlyList<int> eraSizes = GetEraSizes();
        if (_progress.wordMastery != null)
        {
            for (int i = 0; i < _progress.wordMastery.Count; i++)
            {
                WordMasteryRecord word = _progress.wordMastery[i];
                if (word == null || !TryGetLevelIndex(word.sourceLevelId, out int sourceIndex))
                    continue;

                IReadOnlyList<ScheduledCheckpoint> due = ReviewScheduler.GetDue(
                    sourceIndex, currentIndex, eraSizes, word.satisfiedReviewCheckpoints,
                    _campaign?.learningTuning);
                for (int j = 0; j < due.Count; j++)
                    AddRequired(result, new ReviewDueItem(
                        word.wordId, LearningContentKind.Word,
                        GetWordState(word.wordId), due[j].Checkpoint, 0f));
            }
        }

        if (_campaign.TryGetLevel(levelId, out LevelConfigSO level))
        {
            AddRequirements(result, level.learningRequirements);
            AddRequirements(result, level.practiceRequirements);
            AddRequirements(result, level.masteryRequirements);
        }

        return result.AsReadOnly();
    }

    public IReadOnlyList<ReviewDueItem> GetSuggestedPracticeItems(string levelId, int maxCount)
    {
        var result = new List<ReviewDueItem>();
        if (maxCount <= 0)
            return result.AsReadOnly();

        int currentIndex = TryGetCurrentLevelIndex(levelId, out int resolvedIndex)
            ? resolvedIndex : int.MaxValue;
        IReadOnlyList<int> eraSizes = GetEraSizes();

        if (_progress.symbolMastery != null)
        {
            for (int i = 0; i < _progress.symbolMastery.Count; i++)
            {
                SymbolMasteryRecord record = _progress.symbolMastery[i];
                if (record == null)
                    continue;
                result.Add(CreateSuggestedSymbol(record));
            }
        }

        if (_progress.wordMastery != null)
        {
            for (int i = 0; i < _progress.wordMastery.Count; i++)
            {
                WordMasteryRecord record = _progress.wordMastery[i];
                if (record == null)
                    continue;
                int overdue = 0;
                if (TryGetLevelIndex(record.sourceLevelId, out int sourceIndex))
                    overdue = ReviewScheduler.GetDue(sourceIndex, currentIndex, eraSizes,
                        record.satisfiedReviewCheckpoints, _campaign?.learningTuning).Count;
                result.Add(CreateSuggestedWord(record, overdue));
            }
        }

        result.Sort((left, right) =>
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0 ? priority : string.CompareOrdinal(left.ContentId, right.ContentId);
        });
        if (result.Count > maxCount)
            result.RemoveRange(maxCount, result.Count - maxCount);
        return result.AsReadOnly();
    }

    private ReviewDueItem CreateSuggestedSymbol(SymbolMasteryRecord record)
    {
        return new ReviewDueItem(record.symbolId, LearningContentKind.Symbol,
            MasteryEvaluator.Aggregate(record.dimensions, LearningContentKind.Symbol), null,
            PracticePriority.Compute(Accuracy(record.dimensions, LearningContentKind.Symbol),
                GetSymbolState(record.symbolId), 0, _campaign?.learningTuning));
    }

    private ReviewDueItem CreateSuggestedWord(WordMasteryRecord record, int overdue)
    {
        return new ReviewDueItem(record.wordId, LearningContentKind.Word,
            MasteryEvaluator.Aggregate(record.dimensions, LearningContentKind.Word), null,
            PracticePriority.Compute(Accuracy(record.dimensions, LearningContentKind.Word),
                GetWordState(record.wordId), overdue, _campaign?.learningTuning));
    }

    private void AddRequirements(List<ReviewDueItem> result, List<ContentRequirement> requirements)
    {
        if (requirements == null)
            return;
        for (int i = 0; i < requirements.Count; i++)
        {
            ContentRequirement requirement = requirements[i];
            string contentId = requirement?.symbolValue?.symbol?.stableId;
            if (string.IsNullOrEmpty(contentId))
                continue;
            AddRequired(result, new ReviewDueItem(contentId, LearningContentKind.Symbol,
                GetSymbolState(contentId), null, 0f));
        }
    }

    private static void AddRequired(List<ReviewDueItem> result, ReviewDueItem item)
    {
        string checkpoint = item.Checkpoint?.ToString() ?? string.Empty;
        for (int i = 0; i < result.Count; i++)
        {
            if (string.Equals(result[i].ContentId, item.ContentId, StringComparison.Ordinal) &&
                string.Equals(result[i].Checkpoint?.ToString() ?? string.Empty, checkpoint,
                    StringComparison.Ordinal))
                return;
        }
        result.Add(item);
    }

    private SymbolMasteryRecord FindSymbol(string symbolId)
    {
        if (_progress.symbolMastery == null)
            return null;
        for (int i = 0; i < _progress.symbolMastery.Count; i++)
            if (string.Equals(_progress.symbolMastery[i]?.symbolId, symbolId, StringComparison.Ordinal))
                return _progress.symbolMastery[i];
        return null;
    }

    private WordMasteryRecord FindWord(string wordId)
    {
        if (_progress.wordMastery == null)
            return null;
        for (int i = 0; i < _progress.wordMastery.Count; i++)
            if (string.Equals(_progress.wordMastery[i]?.wordId, wordId, StringComparison.Ordinal))
                return _progress.wordMastery[i];
        return null;
    }

    private static MasteryState GetDimensionState(
        List<DimensionEvidence> dimensions, MasteryDimension dimension)
    {
        if (dimensions == null)
            return MasteryState.None;
        for (int i = 0; i < dimensions.Count; i++)
            if (dimensions[i] != null && dimensions[i].dimension == dimension)
                return dimensions[i].highWaterState;
        return MasteryState.None;
    }

    private static float Accuracy(
        List<DimensionEvidence> dimensions, LearningContentKind kind)
    {
        int attempts = 0;
        int successes = 0;
        int dimensionCount = dimensions?.Count ?? 0;
        for (int i = 0; i < dimensionCount; i++)
        {
            DimensionEvidence evidence = dimensions[i];
            if (evidence == null || !MasteryDimensions.IsApplicable(kind, evidence.dimension))
                continue;
            attempts += evidence.immediateAttempts + evidence.delayedAttempts;
            successes += evidence.immediateSuccesses + evidence.delayedSuccesses;
        }
        return PracticePriority.AccuracyOrDefault(attempts, successes);
    }

    private bool TryGetCurrentLevelIndex(string levelId, out int index)
    {
        return TryGetLevelIndex(levelId, out index);
    }

    private bool TryGetLevelIndex(string levelId, out int index)
    {
        index = 0;
        int current = 0;
        if (_campaign?.eras == null)
            return false;
        for (int i = 0; i < _campaign.eras.Count; i++)
        {
            EraConfigSO era = _campaign.eras[i];
            if (era?.levels == null)
                continue;
            for (int j = 0; j < era.levels.Count; j++)
            {
                if (string.Equals(era.levels[j]?.stableId, levelId, StringComparison.Ordinal))
                {
                    index = current;
                    return true;
                }
                current++;
            }
        }
        return false;
    }

    private IReadOnlyList<int> GetEraSizes()
    {
        var sizes = new List<int>();
        if (_campaign?.eras == null)
            return sizes;
        for (int i = 0; i < _campaign.eras.Count; i++)
            sizes.Add(_campaign.eras[i]?.levels?.Count ?? 0);
        return sizes;
    }
}
