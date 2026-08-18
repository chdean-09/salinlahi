using System;
using System.Collections.Generic;

/// <summary>
/// Applies one validated evidence batch to campaign progress. Instruction is the only thing that
/// creates a record, and it seeds every applicable dimension at Introduced.
/// </summary>
public static class LearningProgressWriter
{
    public static void Apply(
        CampaignProgressData progress, LearningEvidenceBatch batch, LearningTuningSO tuning)
    {
        if (progress == null || batch == null || tuning == null)
            return;

        if (progress.symbolMastery == null)
            progress.symbolMastery = new List<SymbolMasteryRecord>();
        if (progress.wordMastery == null)
            progress.wordMastery = new List<WordMasteryRecord>();

        SeedInstructedContent(progress, batch);
        ApplyEntries(progress, batch, tuning);
        Sort(progress);
    }

    private static void SeedInstructedContent(
        CampaignProgressData progress, LearningEvidenceBatch batch)
    {
        if (batch.instructedContentIds == null)
            return;

        for (int i = 0; i < batch.instructedContentIds.Count; i++)
        {
            string contentId = batch.instructedContentIds[i];
            if (string.IsNullOrEmpty(contentId))
                continue;

            LearningContentKind kind = ResolveKind(batch, contentId);
            CreateRecord(progress, contentId, kind, batch.levelId);
        }
    }

    private static void ApplyEntries(
        CampaignProgressData progress, LearningEvidenceBatch batch, LearningTuningSO tuning)
    {
        if (batch.entries == null)
            return;

        for (int i = 0; i < batch.entries.Count; i++)
        {
            LearningEvidenceEntry entry = batch.entries[i];
            if (entry == null ||
                !MasteryDimensions.IsApplicable(entry.contentKind, entry.dimension))
                continue;

            List<DimensionEvidence> dimensions =
                FindDimensions(progress, entry.contentId, entry.contentKind);
            if (dimensions == null)
                continue;

            DimensionEvidence evidence = Find(dimensions, entry.dimension);
            if (evidence == null)
                continue;

            int attempts = Math.Max(0, entry.attemptCount);
            int successes = Math.Min(Math.Max(0, entry.successCount), attempts);
            int retrievals = Math.Min(Math.Max(0, entry.retrievalSuccessCount), successes);

            if (IsInstructed(batch, entry.contentId))
            {
                evidence.immediateAttempts += attempts;
                evidence.immediateSuccesses += successes;
            }
            else
            {
                evidence.delayedAttempts += attempts;
                evidence.delayedSuccesses += retrievals;
                if (retrievals > 0)
                    evidence.delayedSessionCount++;
            }

            if (attempts > 0)
                evidence.lastEvidenceLevelId = batch.levelId;
            evidence.highWaterState = MasteryEvaluator.Evaluate(evidence, tuning);
        }
    }

    private static void CreateRecord(
        CampaignProgressData progress, string contentId, LearningContentKind kind, string levelId)
    {
        if (kind == LearningContentKind.Word)
        {
            WordMasteryRecord word = FindWord(progress, contentId);
            if (word == null)
            {
                word = new WordMasteryRecord { wordId = contentId, sourceLevelId = levelId };
                progress.wordMastery.Add(word);
            }
            EnsureDimensions(word.dimensions, kind);
            return;
        }

        SymbolMasteryRecord symbol = FindSymbol(progress, contentId);
        if (symbol == null)
        {
            symbol = new SymbolMasteryRecord
            {
                symbolId = contentId,
                introducedAtLevelId = levelId,
            };
            progress.symbolMastery.Add(symbol);
        }
        EnsureDimensions(symbol.dimensions, kind);
    }

    private static List<DimensionEvidence> FindDimensions(
        CampaignProgressData progress, string contentId, LearningContentKind kind)
    {
        if (kind == LearningContentKind.Word)
            return FindWord(progress, contentId)?.dimensions;
        return FindSymbol(progress, contentId)?.dimensions;
    }

    private static void EnsureDimensions(
        List<DimensionEvidence> dimensions, LearningContentKind kind)
    {
        if (dimensions == null)
            return;

        IReadOnlyList<MasteryDimension> applicable = MasteryDimensions.For(kind);
        for (int i = 0; i < applicable.Count; i++)
        {
            if (Find(dimensions, applicable[i]) != null)
                continue;
            dimensions.Add(new DimensionEvidence
            {
                dimension = applicable[i],
                highWaterState = MasteryState.Introduced,
            });
        }
        dimensions.Sort((left, right) => left.dimension.CompareTo(right.dimension));
    }

    private static LearningContentKind ResolveKind(
        LearningEvidenceBatch batch, string contentId)
    {
        if (batch.entries != null)
        {
            for (int i = 0; i < batch.entries.Count; i++)
            {
                LearningEvidenceEntry entry = batch.entries[i];
                if (entry != null && string.Equals(entry.contentId, contentId, StringComparison.Ordinal))
                    return entry.contentKind;
            }
        }

        return contentId.StartsWith("level.", StringComparison.Ordinal)
            ? LearningContentKind.Word
            : LearningContentKind.Symbol;
    }

    private static bool IsInstructed(LearningEvidenceBatch batch, string contentId)
    {
        if (batch.instructedContentIds == null)
            return false;
        for (int i = 0; i < batch.instructedContentIds.Count; i++)
            if (string.Equals(batch.instructedContentIds[i], contentId, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static DimensionEvidence Find(
        List<DimensionEvidence> dimensions, MasteryDimension dimension)
    {
        for (int i = 0; i < dimensions.Count; i++)
            if (dimensions[i] != null && dimensions[i].dimension == dimension)
                return dimensions[i];
        return null;
    }

    private static SymbolMasteryRecord FindSymbol(CampaignProgressData progress, string symbolId)
    {
        for (int i = 0; i < progress.symbolMastery.Count; i++)
            if (string.Equals(progress.symbolMastery[i]?.symbolId, symbolId, StringComparison.Ordinal))
                return progress.symbolMastery[i];
        return null;
    }

    private static WordMasteryRecord FindWord(CampaignProgressData progress, string wordId)
    {
        for (int i = 0; i < progress.wordMastery.Count; i++)
            if (string.Equals(progress.wordMastery[i]?.wordId, wordId, StringComparison.Ordinal))
                return progress.wordMastery[i];
        return null;
    }

    private static void Sort(CampaignProgressData progress)
    {
        progress.symbolMastery.Sort(
            (left, right) => string.CompareOrdinal(left?.symbolId, right?.symbolId));
        progress.wordMastery.Sort(
            (left, right) => string.CompareOrdinal(left?.wordId, right?.wordId));
    }
}
