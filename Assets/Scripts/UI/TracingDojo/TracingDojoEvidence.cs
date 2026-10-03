using System;
using System.Collections.Generic;

/// <summary>
/// Pure decision layer for the Tracing Dojo: "does this recognition result count, and as what
/// evidence?" Extracted from the controller so the rule is unit-testable without a MonoBehaviour.
///
/// Form only. OnResolved plays the pronunciation clip after a correct trace — reinforcement the
/// learner hears, not a match the learner makes. Recording it as Sound would inflate the dimension
/// with no retrieval behind it; Sound arrives with SALIN-159's screen.
/// </summary>
public static class TracingDojoEvidence
{
    public static LearningEvidenceEntry Resolve(
        string selectedStableId, string recognizedStableId, bool passedThreshold)
    {
        bool matched = passedThreshold &&
            !string.IsNullOrEmpty(selectedStableId) &&
            string.Equals(selectedStableId, recognizedStableId, StringComparison.Ordinal);

        return new LearningEvidenceEntry
        {
            contentId = selectedStableId,
            contentKind = LearningContentKind.Symbol,
            dimension = MasteryDimension.Form,
            attemptCount = 1,
            successCount = matched ? 1 : 0,

            // The dojo never instructs, so a success here is always a delayed retrieval.
            retrievalSuccessCount = matched ? 1 : 0,
        };
    }

    /// <summary>
    /// Maps whatever identity the recognizer produced onto a canonical stableId. RecognitionResult
    /// still carries the legacy character ID, so fall back to the symbol catalogue's legacyAliases.
    /// </summary>
    public static string ResolveStableId(CampaignConfigSO campaign, string recognizedId)
    {
        if (campaign == null || string.IsNullOrEmpty(recognizedId))
            return null;
        if (campaign.TryGetSymbol(recognizedId, out _))
            return recognizedId;

        List<BaybayinCharacterSO> symbols = campaign.symbols;
        if (symbols == null)
            return null;

        for (int i = 0; i < symbols.Count; i++)
        {
            BaybayinCharacterSO symbol = symbols[i];
            if (symbol == null)
                continue;
            if (string.Equals(symbol.characterID, recognizedId, StringComparison.OrdinalIgnoreCase))
                return symbol.stableId;
            if (symbol.legacyAliases == null)
                continue;
            for (int j = 0; j < symbol.legacyAliases.Count; j++)
                if (string.Equals(symbol.legacyAliases[j], recognizedId, StringComparison.OrdinalIgnoreCase))
                    return symbol.stableId;
        }

        return null;
    }
}
