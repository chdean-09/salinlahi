using System.Collections.Generic;

/// <summary>
/// SALIN-202: derives the reward lists an accepted level completion commits —
/// the previously always-empty unlockedSymbolIds / unlockedMemoryIds /
/// claimedRewardIds on CampaignProgressOutcome. Replay safety is owned by the
/// outcome coordinator's applied-receipt union; this resolver is pure derivation.
/// </summary>
public sealed class RewardGrant
{
    public RewardGrant(
        IReadOnlyList<string> unlockedSymbolIds,
        IReadOnlyList<string> unlockedMemoryIds,
        IReadOnlyList<string> claimedRewardIds)
    {
        UnlockedSymbolIds = unlockedSymbolIds;
        UnlockedMemoryIds = unlockedMemoryIds;
        ClaimedRewardIds = claimedRewardIds;
    }

    public IReadOnlyList<string> UnlockedSymbolIds { get; }
    public IReadOnlyList<string> UnlockedMemoryIds { get; }
    public IReadOnlyList<string> ClaimedRewardIds { get; }
}

public static class LevelRewardResolver
{
    public const string MemoryRewardPrefix = "memory.";

    public static RewardGrant Resolve(LevelConfigSO level)
    {
        var unlockedSymbols = new List<string>();
        var unlockedMemories = new List<string>();
        var claimedRewards = new List<string>();
        if (level == null)
            return new RewardGrant(unlockedSymbols, unlockedMemories, claimedRewards);

        if (level.cumulativeSymbolPool != null)
        {
            foreach (SymbolValueReference reference in level.cumulativeSymbolPool)
            {
                BaybayinCharacterSO symbol = reference?.symbol;
                if (symbol == null || string.IsNullOrEmpty(symbol.stableId))
                    continue;
                if (symbol.firstIntroductionLevelId == level.stableId
                    && !unlockedSymbols.Contains(symbol.stableId))
                    unlockedSymbols.Add(symbol.stableId);
            }
        }

        if (level.rewardIds != null)
        {
            foreach (string rewardId in level.rewardIds)
            {
                if (string.IsNullOrWhiteSpace(rewardId))
                    continue;
                claimedRewards.Add(rewardId);
                if (rewardId.StartsWith(MemoryRewardPrefix, System.StringComparison.Ordinal))
                    unlockedMemories.Add(rewardId);
            }
        }

        return new RewardGrant(unlockedSymbols, unlockedMemories, claimedRewards);
    }
}
