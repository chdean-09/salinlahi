using System;
using System.Collections.Generic;

/// <summary>
/// SALIN-137: the three player-visible progression states a level can be in on the
/// Level Select screen, plus a defensive fallback for data that cannot be classified.
/// </summary>
public enum LevelLockState
{
    /// <summary>Reachable and not yet finished.</summary>
    Unlocked,
    /// <summary>Finished at least once. Still reachable — completion does not re-lock a level.</summary>
    Completed,
    /// <summary>Not yet reachable. <see cref="LevelLockStatus.RequiredLevelId"/> names the one requirement.</summary>
    Locked,
    /// <summary>
    /// The level could not be classified (unknown level id, empty campaign, or an
    /// unusable save). Callers must stay silent rather than explain a prerequisite
    /// that is not the real cause.
    /// </summary>
    Unknown,
}

/// <summary>
/// Immutable result of <see cref="LevelLockResolver.Resolve"/>. The requirement fields
/// are populated only for <see cref="LevelLockState.Locked"/>, and only when a
/// preceding level actually exists — the first configured level has no prerequisite.
/// </summary>
public sealed class LevelLockStatus
{
    public LevelLockState State { get; }

    /// <summary>The level this status describes. Echoed back for caller convenience.</summary>
    public string LevelId { get; }

    /// <summary>
    /// The single immediately preceding level that must be completed, or <c>null</c>
    /// when there is none. SALIN-137 AC2 asks for exactly one requirement, and the
    /// authored unlock rule (<see cref="CampaignOutcomeCoordinator.ApplyLevelProgression"/>)
    /// only ever has one.
    /// </summary>
    public string RequiredLevelId { get; }

    /// <summary>
    /// 1-based position of <see cref="RequiredLevelId"/> in the campaign's configured
    /// level order, or 0 when there is no requirement. Lets UI name the requirement
    /// without re-deriving campaign order.
    /// </summary>
    public int RequiredLevelOrder { get; }

    /// <summary>
    /// True when the requirement lives in a different era than this level — i.e. this
    /// level is the first of its era and the player must finish the previous era.
    /// </summary>
    public bool RequirementCrossesEra { get; }

    /// <summary>Era id owning <see cref="RequiredLevelId"/>, or <c>null</c> when unknown.</summary>
    public string RequiredEraId { get; }

    public bool HasRequirement => RequiredLevelId != null;

    private LevelLockStatus(
        LevelLockState state,
        string levelId,
        string requiredLevelId,
        int requiredLevelOrder,
        bool requirementCrossesEra,
        string requiredEraId)
    {
        State = state;
        LevelId = levelId;
        RequiredLevelId = requiredLevelId;
        RequiredLevelOrder = requiredLevelOrder;
        RequirementCrossesEra = requirementCrossesEra;
        RequiredEraId = requiredEraId;
    }

    public static LevelLockStatus Unlocked(string levelId) =>
        new LevelLockStatus(LevelLockState.Unlocked, levelId, null, 0, false, null);

    public static LevelLockStatus Completed(string levelId) =>
        new LevelLockStatus(LevelLockState.Completed, levelId, null, 0, false, null);

    /// <summary>Locked with an identifiable single prerequisite.</summary>
    public static LevelLockStatus LockedBehind(
        string levelId, string requiredLevelId, int requiredLevelOrder, bool crossesEra, string requiredEraId) =>
        new LevelLockStatus(
            LevelLockState.Locked, levelId, requiredLevelId, requiredLevelOrder, crossesEra, requiredEraId);

    /// <summary>
    /// Locked but with nothing to explain — the first configured level reaching this
    /// state means the save is inconsistent, not that the player missed a step.
    /// </summary>
    public static LevelLockStatus LockedWithoutRequirement(string levelId) =>
        new LevelLockStatus(LevelLockState.Locked, levelId, null, 0, false, null);

    public static LevelLockStatus Unknown(string levelId) =>
        new LevelLockStatus(LevelLockState.Unknown, levelId, null, 0, false, null);
}

/// <summary>
/// SALIN-137: pure, read-only classification of one level's locked / unlocked /
/// completed state over a committed <see cref="CampaignSaveDocument"/> snapshot,
/// plus the single prerequisite that would unlock it.
///
/// This class adds no unlock rule. It restates, for display only, the rule already
/// authored in <see cref="CampaignOutcomeCoordinator.ApplyLevelProgression"/> and
/// re-checked in <see cref="CampaignProgressRepository.TryUnlockLevel"/>: completing
/// the level at index <c>i</c> unlocks index <c>i + 1</c> of
/// <see cref="CampaignSaveValidator.GetConfiguredLevelIds"/>, crossing era boundaries
/// implicitly because that list is a single flattened order.
///
/// Never mutates the document — the same contract as
/// <see cref="JourneyEntryResolver"/> (SALIN-136).
/// </summary>
public static class LevelLockResolver
{
    /// <summary>
    /// Classifies <paramref name="levelId"/> against the committed document and the
    /// campaign's configured level-id order. Defensive by design: Level Select calls
    /// this once per visible button on every era change, so unknown ids, a null or
    /// empty order, and missing <see cref="LevelProgressRecord"/> entries must degrade
    /// to a safe answer instead of throwing.
    /// </summary>
    public static LevelLockStatus Resolve(
        CampaignSaveDocument document, IReadOnlyList<string> configuredLevelIds, string levelId)
    {
        if (string.IsNullOrEmpty(levelId) || configuredLevelIds == null || configuredLevelIds.Count == 0)
            return LevelLockStatus.Unknown(levelId);

        int index = IndexOf(configuredLevelIds, levelId);
        if (index < 0)
            return LevelLockStatus.Unknown(levelId);

        LevelProgressRecord record = FindRecord(document?.progress?.levelProgress, levelId);

        // Completion is checked before unlock: ApplyLevelProgression sets both flags on
        // the finished level, so a completed level is always also unlocked, and the
        // player-facing "completed" state must win over the plainer "unlocked" one.
        if (record != null && record.completed)
            return LevelLockStatus.Completed(levelId);

        if (record != null && record.unlocked)
            return LevelLockStatus.Unlocked(levelId);

        // Locked from here down. A missing record is treated as locked rather than
        // unlocked so an incomplete save never advertises an unplayable level.
        if (index == 0)
        {
            // The first configured level has no predecessor. Reaching this branch means
            // the save is inconsistent (CampaignProgressFactory always unlocks index 0),
            // so report the lock with nothing to explain.
            return LevelLockStatus.LockedWithoutRequirement(levelId);
        }

        string requiredLevelId = configuredLevelIds[index - 1];
        string levelEraId = ContentIdentity.GetEraIdForLevel(levelId);
        string requiredEraId = ContentIdentity.GetEraIdForLevel(requiredLevelId);
        bool crossesEra = levelEraId != null && requiredEraId != null &&
            !string.Equals(levelEraId, requiredEraId, StringComparison.Ordinal);

        // RequiredLevelOrder is 1-based, so the predecessor at array index (index - 1)
        // has order `index`.
        return LevelLockStatus.LockedBehind(levelId, requiredLevelId, index, crossesEra, requiredEraId);
    }

    private static int IndexOf(IReadOnlyList<string> configuredLevelIds, string levelId)
    {
        for (int i = 0; i < configuredLevelIds.Count; i++)
        {
            if (string.Equals(configuredLevelIds[i], levelId, StringComparison.Ordinal))
                return i;
        }
        return -1;
    }

    private static LevelProgressRecord FindRecord(List<LevelProgressRecord> records, string levelId)
    {
        if (records == null)
            return null;
        for (int i = 0; i < records.Count; i++)
        {
            LevelProgressRecord record = records[i];
            if (record != null && string.Equals(record.levelId, levelId, StringComparison.Ordinal))
                return record;
        }
        return null;
    }
}
