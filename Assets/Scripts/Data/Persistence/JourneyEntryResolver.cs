using System;
using System.Collections.Generic;

/// <summary>
/// SALIN-136: classifies where the journey should route when the player
/// presses Play on the main menu.
/// </summary>
public enum JourneyEntryKind
{
    /// <summary>Fresh journey — no level completed; start at the first level.</summary>
    NewJourney,
    /// <summary>Journey in progress — continue at the resolved level.</summary>
    ContinueLevel,
    /// <summary>Every configured level is completed — show review/replay, never a next-level prompt.</summary>
    CompletedJourney,
    /// <summary>Routing is not possible (blocked save or unresolvable campaign); do not enter gameplay.</summary>
    Blocked,
}

/// <summary>
/// Immutable result of <see cref="JourneyEntryResolver.Resolve"/>. The level id is
/// populated only for the routable kinds (<see cref="JourneyEntryKind.NewJourney"/> and
/// <see cref="JourneyEntryKind.ContinueLevel"/>).
/// </summary>
public sealed class JourneyEntryPoint
{
    public JourneyEntryKind Kind { get; }
    public string LevelId { get; }

    private JourneyEntryPoint(JourneyEntryKind kind, string levelId)
    {
        Kind = kind;
        LevelId = levelId;
    }

    public static JourneyEntryPoint NewJourney(string levelId) =>
        new JourneyEntryPoint(JourneyEntryKind.NewJourney, levelId);

    public static JourneyEntryPoint ContinueLevel(string levelId) =>
        new JourneyEntryPoint(JourneyEntryKind.ContinueLevel, levelId);

    public static JourneyEntryPoint CompletedJourney() =>
        new JourneyEntryPoint(JourneyEntryKind.CompletedJourney, null);

    public static JourneyEntryPoint Blocked() =>
        new JourneyEntryPoint(JourneyEntryKind.Blocked, null);
}

/// <summary>
/// SALIN-136: pure, read-only classification of the next meaningful journey entry
/// point over a committed <see cref="CampaignSaveDocument"/> snapshot. Never mutates
/// the document — committing a routed selection stays with
/// <see cref="CampaignProgressRepository.TrySetActiveLevel"/>.
/// Continue semantics deliberately prefer the first unlocked-and-incomplete level in
/// campaign order over <c>activeLevelId</c>, which records the last selected level
/// (including replays).
/// </summary>
public static class JourneyEntryResolver
{
    /// <summary>
    /// Resolves the journey entry point for the given committed document against the
    /// campaign's configured level-id order
    /// (<see cref="CampaignSaveValidator.GetConfiguredLevelIds"/>).
    /// Defensive: inconsistent data falls back to the first non-completed level rather
    /// than ever producing an invalid next-level prompt.
    /// </summary>
    public static JourneyEntryPoint Resolve(
        CampaignSaveDocument document, IReadOnlyList<string> configuredLevelIds)
    {
        if (configuredLevelIds == null || configuredLevelIds.Count == 0)
            return JourneyEntryPoint.Blocked();

        List<LevelProgressRecord> records = document?.progress?.levelProgress;
        bool anyCompleted = false;
        bool allCompleted = true;
        string firstUnlockedIncomplete = null;
        string firstIncomplete = null;

        for (int i = 0; i < configuredLevelIds.Count; i++)
        {
            string levelId = configuredLevelIds[i];
            LevelProgressRecord record = FindRecord(records, levelId);
            if (record != null && record.completed)
            {
                anyCompleted = true;
                continue;
            }

            allCompleted = false;
            if (firstIncomplete == null)
                firstIncomplete = levelId;
            if (firstUnlockedIncomplete == null && record != null && record.unlocked)
                firstUnlockedIncomplete = levelId;
        }

        if (allCompleted)
            return JourneyEntryPoint.CompletedJourney();

        if (!anyCompleted)
        {
            // Nothing has been completed: this is a new journey. Any unlock pattern
            // other than "first level only" is inconsistent data; route to the first
            // unlocked-and-incomplete level so the target is always playable.
            string firstConfigured = configuredLevelIds[0];
            if (firstUnlockedIncomplete == null ||
                string.Equals(firstUnlockedIncomplete, firstConfigured, StringComparison.Ordinal))
                return JourneyEntryPoint.NewJourney(firstConfigured);
            return JourneyEntryPoint.ContinueLevel(firstUnlockedIncomplete);
        }

        if (firstUnlockedIncomplete != null)
            return JourneyEntryPoint.ContinueLevel(firstUnlockedIncomplete);

        // Inconsistent data: progress exists but the next level was never unlocked.
        // Fall back to the first non-completed level — never an invalid prompt.
        return JourneyEntryPoint.ContinueLevel(firstIncomplete);
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
