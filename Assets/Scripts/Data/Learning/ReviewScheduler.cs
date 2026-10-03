using System;
using System.Collections.Generic;

public enum ReviewCheckpoint { NextLevel, ThreeLevelsLater, EraEnding, LaterEra }

public sealed class ScheduledCheckpoint
{
    public ReviewCheckpoint Checkpoint { get; }
    public int DueLevelIndex { get; }

    public ScheduledCheckpoint(ReviewCheckpoint checkpoint, int dueLevelIndex)
    {
        Checkpoint = checkpoint;
        DueLevelIndex = dueLevelIndex;
    }
}

/// <summary>
/// Pure review scheduling. Era boundaries come from the configured era sizes.
/// </summary>
public static class ReviewScheduler
{
    public static IReadOnlyList<ScheduledCheckpoint> BuildSchedule(
        int sourceIndex, IReadOnlyList<int> eraSizes, LearningTuningSO tuning)
    {
        var schedule = new List<ScheduledCheckpoint>();
        if (eraSizes == null || tuning == null || sourceIndex < 0)
            return schedule;

        int totalLevels = 0;
        for (int i = 0; i < eraSizes.Count; i++)
        {
            if (eraSizes[i] <= 0)
                return schedule;
            totalLevels += eraSizes[i];
        }
        if (sourceIndex >= totalLevels)
            return schedule;

        if (!TryResolveEra(sourceIndex, eraSizes, out int eraLast, out int eraIndex))
            return schedule;

        Add(schedule, ReviewCheckpoint.NextLevel,
            sourceIndex + tuning.nextLevelOffset, sourceIndex, totalLevels);
        Add(schedule, ReviewCheckpoint.ThreeLevelsLater,
            sourceIndex + tuning.laterLevelOffset, sourceIndex, totalLevels);
        Add(schedule, ReviewCheckpoint.EraEnding, eraLast, sourceIndex, totalLevels);

        if (eraIndex + 1 < eraSizes.Count)
            Add(schedule, ReviewCheckpoint.LaterEra, eraLast + 1, sourceIndex, totalLevels);

        return schedule;
    }

    public static IReadOnlyList<ScheduledCheckpoint> GetDue(
        int sourceIndex,
        int currentIndex,
        IReadOnlyList<int> eraSizes,
        IReadOnlyList<string> satisfiedCheckpoints,
        LearningTuningSO tuning)
    {
        var due = new List<ScheduledCheckpoint>();
        IReadOnlyList<ScheduledCheckpoint> schedule = BuildSchedule(sourceIndex, eraSizes, tuning);

        for (int i = 0; i < schedule.Count; i++)
        {
            ScheduledCheckpoint entry = schedule[i];
            if (entry.DueLevelIndex > currentIndex)
                continue;
            if (IsSatisfied(satisfiedCheckpoints, entry.Checkpoint))
                continue;
            due.Add(entry);
        }

        return due;
    }

    public static bool IsSatisfied(
        IReadOnlyList<string> satisfiedCheckpoints, ReviewCheckpoint checkpoint)
    {
        if (satisfiedCheckpoints == null)
            return false;

        string name = checkpoint.ToString();
        for (int i = 0; i < satisfiedCheckpoints.Count; i++)
            if (string.Equals(satisfiedCheckpoints[i], name, StringComparison.Ordinal))
                return true;

        return false;
    }

    private static void Add(
        List<ScheduledCheckpoint> schedule,
        ReviewCheckpoint checkpoint,
        int dueIndex,
        int sourceIndex,
        int totalLevels)
    {
        if (dueIndex <= sourceIndex || dueIndex >= totalLevels)
            return;
        schedule.Add(new ScheduledCheckpoint(checkpoint, dueIndex));
    }

    private static bool TryResolveEra(
        int sourceIndex, IReadOnlyList<int> eraSizes, out int eraLast, out int eraIndex)
    {
        int eraFirst = 0;
        for (eraIndex = 0; eraIndex < eraSizes.Count; eraIndex++)
        {
            eraLast = eraFirst + eraSizes[eraIndex] - 1;
            if (sourceIndex <= eraLast)
                return true;
            eraFirst = eraLast + 1;
        }

        eraLast = -1;
        return false;
    }
}
