using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CampaignConfig", menuName = "Salinlahi/Campaign Config")]
public sealed class CampaignConfigSO : ScriptableObject
{
    public CampaignIdentityManifest manifest = CampaignIdentityManifest.CreateRevisedV1();
    public CampaignTuning tuning = new();

    [Tooltip("Mastery thresholds and review offsets. Required - validation fails when absent.")]
    public LearningTuningSO learningTuning;
    public List<BaybayinCharacterSO> symbols = new();
    public List<EraConfigSO> eras = new();

    public bool TryGetEra(string stableId, out EraConfigSO result)
    {
        result = null;
        if (!ContentIdentity.IsCanonical(stableId) || eras == null)
            return false;

        int matchCount = 0;
        for (int i = 0; i < eras.Count; i++)
        {
            EraConfigSO candidate = eras[i];
            if (candidate == null || !string.Equals(candidate.stableId, stableId, StringComparison.Ordinal))
                continue;

            matchCount++;
            result = candidate;
        }

        if (matchCount != 1)
        {
            result = null;
            return false;
        }

        return true;
    }

    public bool TryGetLevel(string stableId, out LevelConfigSO result)
    {
        result = null;
        if (!ContentIdentity.IsCanonical(stableId) || eras == null)
            return false;

        int matchCount = 0;
        for (int i = 0; i < eras.Count; i++)
        {
            EraConfigSO era = eras[i];
            if (era == null || era.levels == null)
                continue;

            for (int j = 0; j < era.levels.Count; j++)
            {
                LevelConfigSO candidate = era.levels[j];
                if (candidate == null || !string.Equals(candidate.stableId, stableId, StringComparison.Ordinal))
                    continue;

                matchCount++;
                result = candidate;
            }
        }

        if (matchCount != 1)
        {
            result = null;
            return false;
        }

        return true;
    }

    public bool TryGetSymbol(string stableId, out BaybayinCharacterSO result)
    {
        result = null;
        if (!ContentIdentity.IsCanonical(stableId) || symbols == null)
            return false;

        int matchCount = 0;
        for (int i = 0; i < symbols.Count; i++)
        {
            BaybayinCharacterSO candidate = symbols[i];
            if (candidate == null || !string.Equals(candidate.stableId, stableId, StringComparison.Ordinal))
                continue;

            matchCount++;
            result = candidate;
        }

        if (matchCount != 1)
        {
            result = null;
            return false;
        }

        return true;
    }

    public bool TryGetSpokenValue(
        string symbolId,
        string spokenValueId,
        out SpokenValueDefinition result)
    {
        result = null;
        if (!ContentIdentity.IsCanonical(spokenValueId) ||
            !TryGetSymbol(symbolId, out BaybayinCharacterSO symbol))
            return false;

        return symbol.TryGetSpokenValue(spokenValueId, out result);
    }
}
